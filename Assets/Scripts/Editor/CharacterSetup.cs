#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Pickleball.Gameplay;
using Pickleball.VFX;

namespace Pickleball.EditorTools
{
    /// <summary>
    /// Turns the PB_Animations_FBX_P01 pack into a usable in-game character.
    ///
    /// The pack ships in the common Mixamo shape: nine FBX files that each redundantly contain the
    /// same 52-bone rig plus exactly one take, and every one of them imports with avatarSetup =
    /// NoAvatar. A Generic rig with no avatar cannot be played by an Animator at all, so none of these
    /// clips would run without the import fix-up this does first.
    ///
    /// Re-runnable: every step overwrites rather than appends, so it can be run again after the pack
    /// is re-imported or the scene is rebuilt.
    /// </summary>
    public static class CharacterSetup
    {
        private const string PackRoot = "Assets/PB_Animations_FBX_P01 1";
        private const string FbxRoot = PackRoot + "/FBX";
        private const string GeneratedRoot = PackRoot + "/Generated";
        private const string ClipsRoot = GeneratedRoot + "/Clips";
        private const string ControllerPath = GeneratedRoot + "/PB_Character.controller";

        /// <summary>
        /// The home screen's character is loaded by name at runtime rather than referenced from the
        /// scene, because the lobby builds itself procedurally and has nowhere to serialise a
        /// reference. Resources is the same door UIBuilder already uses for the display font.
        /// </summary>
        private const string HomePrefabDir = "Assets/Resources/Characters";
        private const string HomePrefabPath = HomePrefabDir + "/HomeCharacter.prefab";

        private static readonly Color PlayerTeamColor = new Color(0.20f, 0.55f, 0.95f);
        private static readonly Color OpponentTeamColor = new Color(0.92f, 0.28f, 0.22f);

        private const string RigChildName = "CharacterRig";
        private const string PaddleChildName = "PaddleAttachment";

        // Paddle_bat1.fbx's mesh, in its own space: the long axis is +Z (butt of the handle at z = 0,
        // blade tip at z = max), the face normal is Y (it is 0.0006 thick), the width is X. The
        // handle is the narrow first ~20% of the length. The old attachment assumed a +Y long axis
        // centred on the origin, which put the butt of the handle beyond the fingertips with the
        // blade sticking straight out of the back of the fist.

        /// <summary>How far along the handle (0 = butt, 1 = where it widens into the blade) the
        /// middle of the fist sits. Just under half leaves a little butt showing below the pinky.</summary>
        private const float HandleGripFraction = 0.45f;
        private const float HandleLengthFraction = 0.2f;

        /// <summary>Shake-hands grip: the handle runs diagonally across the palm, so the paddle leaves
        /// the fist between the fingers' line and the thumb side. 0 would continue the fingers, 90
        /// would be a hammer grip straight across the knuckles.</summary>
        private const float GripAngleFromFingers = 45f;

        /// <summary>Where the fist closes around the handle, in knuckle lengths (wrist to the middle
        /// finger's base): partway up the palm, in front of it by the handle's thickness.</summary>
        private const float GripAlongFingers = 0.8f;
        private const float GripInFrontOfPalm = 0.33f;

        /// <summary>
        /// The rig stands 1.562u in its posed silhouette, against the 2u capsule it replaces. Left at
        /// 1:1 the 0.9u net reaches 58% of the character's height, where it was 45% of the capsule
        /// and is about 49% in the real sport -- so the players read as children at the net. Scaling
        /// to ~1.87u puts the net back at 48% and keeps them legible at the camera's distance. The
        /// rig's origin is at the feet, so scaling does not lift it off the court.
        /// </summary>
        private const float CharacterScale = 1.2f;

        /// <summary>
        /// Paddle_bat1.fbx (1,660 verts) replaces the original Paddle_bat.fbx, which at 161,892
        /// verts -- more than 40x the entire character -- was far past a sane mobile budget and is
        /// no longer used. Kept as a switch: turn off to fall back to no paddle if that ever changes.
        /// </summary>
        private const bool AttachSuppliedPaddle = true;
        private const string PaddleFile = "Paddle_bat1.fbx";

        // Source of the mesh + the avatar every other clip is retargeted onto.
        private const string RigFile = "PB_IDLE.fbx";

        private struct ClipDef
        {
            public string file;
            public string clipName;
            public bool loop;
            public ClipDef(string f, string n, bool l) { file = f; clipName = n; loop = l; }
        }

        private static readonly ClipDef[] Clips = new ClipDef[]
        {
            new ClipDef("PB_IDLE.fbx",          "Idle",         true),
            new ClipDef("PB_Run_Forward.fbx",   "RunForward",   true),
            new ClipDef("PB_Run_Backward.fbx",  "RunBackward",  true),
            new ClipDef("PB_Run_Left.fbx",      "RunLeft",      true),
            new ClipDef("PB_Run_Right.fbx",     "RunRight",     true),
            new ClipDef("PB_Smash_Left.fbx",    "SmashLeft",    false),
            new ClipDef("PB_Smash_Right.fbx",   "SmashRight",   false),
            new ClipDef("PB_Victory.fbx",       "Victory",      false),
            new ClipDef("PB_Defeat.fbx",        "Defeat",       false),
        };

        [MenuItem("Pickleball/Setup Characters", false, 1)]
        public static void SetupCharacters()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.CreateFolder(PackRoot, "Generated");
            }
            if (!AssetDatabase.IsValidFolder(ClipsRoot))
            {
                AssetDatabase.CreateFolder(GeneratedRoot, "Clips");
            }

            Avatar avatar = ConfigureImportSettings();
            if (avatar == null)
            {
                Debug.LogError("[CharacterSetup] Could not create the shared avatar; aborting.");
                return;
            }

            BakeClips();

            AnimatorController controller = BuildAnimatorController();
            if (controller == null)
            {
                Debug.LogError("[CharacterSetup] Could not build the animator controller; aborting.");
                return;
            }

            ApplyToScene(avatar, controller);
            BuildHomePrefab(avatar, controller);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[CharacterSetup] Character rig, animator and scene wiring complete.</color>");
        }

        // ============================================================
        // 1. IMPORT SETTINGS
        // ============================================================
        private static Avatar ConfigureImportSettings()
        {
            // The rig file first: it is the one that generates the avatar the others copy.
            ConfigureModel(RigFile, true, null);
            Avatar avatar = LoadAvatar(FbxRoot + "/" + RigFile);
            if (avatar == null) return null;

            foreach (ClipDef def in Clips)
            {
                if (def.file == RigFile) continue;
                ConfigureModel(def.file, false, avatar);
            }
            return avatar;
        }

        private static void ConfigureModel(string file, bool createAvatar, Avatar sourceAvatar)
        {
            string path = FbxRoot + "/" + file;
            ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null)
            {
                Debug.LogWarning("[CharacterSetup] Missing model: " + path);
                return;
            }

            imp.animationType = ModelImporterAnimationType.Generic;
            imp.importAnimation = true;

            if (createAvatar)
            {
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = sourceAvatar;
            }

            ApplyClipSettings(imp, file);
            imp.SaveAndReimport();
        }

        /// <summary>
        /// Renames the single take to something usable and sets its loop flag. Left alone, every clip
        /// would be called "mixamo.com" or "Take 001", and the run cycles would play once and freeze
        /// instead of looping.
        /// </summary>
        private static void ApplyClipSettings(ModelImporter imp, string file)
        {
            string clipName = null;
            bool loop = false;
            foreach (ClipDef d in Clips)
            {
                if (d.file == file) { clipName = d.clipName; loop = d.loop; break; }
            }
            if (clipName == null) return;

            ModelImporterClipAnimation[] takes = imp.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;

            ModelImporterClipAnimation clip = takes[0];
            clip.name = clipName;
            clip.loopTime = loop;
            imp.clipAnimations = new ModelImporterClipAnimation[] { clip };
        }

        private static Avatar LoadAvatar(string modelPath)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                Avatar a = o as Avatar;
                if (a != null) return a;
            }
            return null;
        }

        private static AnimationClip LoadImportedClip(ClipDef d)
        {
            string path = FbxRoot + "/" + d.file;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip c = o as AnimationClip;
                // Unity keeps a "__preview__" copy of every take alongside the real clip.
                if (c != null && !c.name.StartsWith("__")) return c;
            }
            return null;
        }

        /// <summary>Baked clip, i.e. the one the animator actually plays. See <see cref="BakeClips"/>.</summary>
        private static AnimationClip LoadClip(string clipName)
        {
            AnimationClip c = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipsRoot + "/" + clipName + ".anim");
            if (c == null) Debug.LogWarning("[CharacterSetup] Baked clip not found: " + clipName);
            return c;
        }

        // ============================================================
        // 1b. CLIP BAKING (retarget onto the rig's own skeleton)
        // ============================================================

        /// <summary>
        /// Rewrites every clip so its curves address the skeleton the scene rig actually has.
        ///
        /// The pack is internally inconsistent in two ways that between them leave eight of the nine
        /// clips completely inert -- the character slides around frozen in bind pose because the only
        /// clip that binds to anything is Idle:
        ///
        ///  1. Skeleton root name. PB_IDLE.fbx (the file the mesh and avatar come from) roots its
        ///     skeleton at "transform1"; the other eight root theirs at "Mini Modular Armature". Every
        ///     bone below the root is identically named, but the curve paths are absolute, so
        ///     "Mini Modular Armature/Hips/..." resolves to nothing on a rig whose hips live at
        ///     "transform1/Hips/...". Generic avatar retargeting via CopyFromOther does not bridge a
        ///     differing root, so it silently binds zero curves rather than erroring.
        ///
        ///  2. Units. PB_IDLE.fbx is authored in centimetres and carries a scale-100 compensation node
        ///     ("transform1"), so its bone positions are hundredths -- Hips sits at y 0.0059. The other
        ///     eight are authored in metres at scale 1, with Hips at y 0.5603. Remapping the paths
        ///     alone would therefore apply a 0.56 hip offset underneath a x100 node and fire the
        ///     character ~56 units into the air.
        ///
        /// Baking to standalone .anim assets fixes both, and keeps the FBX importers untouched so a
        /// re-import of the pack cannot quietly undo it.
        /// </summary>
        private static void BakeClips()
        {
            string rigArmature;
            float rigArmatureScale;
            ModelArmature(RigFile, out rigArmature, out rigArmatureScale);
            if (rigArmature == null)
            {
                Debug.LogError("[CharacterSetup] Could not locate the rig's armature node; clips not baked.");
                return;
            }

            foreach (ClipDef def in Clips)
            {
                BakeClip(def, rigArmature, rigArmatureScale);
            }
        }

        private static void BakeClip(ClipDef def, string rigArmature, float rigArmatureScale)
        {
            AnimationClip src = LoadImportedClip(def);
            if (src == null)
            {
                Debug.LogWarning("[CharacterSetup] No imported clip in " + def.file);
                return;
            }

            string srcArmature;
            float srcArmatureScale;
            ModelArmature(def.file, out srcArmature, out srcArmatureScale);
            if (srcArmature == null) srcArmature = rigArmature;

            // Bone-local translations are expressed in the source armature's units; the rig's armature
            // rescales everything under it by a different factor, so they have to be converted.
            float posScale = rigArmatureScale != 0f ? srcArmatureScale / rigArmatureScale : 1f;

            AnimationClip dst = new AnimationClip();
            dst.frameRate = src.frameRate;

            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(src))
            {
                string path = b.path;
                if (path == srcArmature) path = rigArmature;
                else if (path.StartsWith(srcArmature + "/")) path = rigArmature + path.Substring(srcArmature.Length);

                // Never let a clip drive the armature node's scale: that node exists purely to carry
                // the unit compensation, and overwriting it would collapse the whole character.
                if (path == rigArmature && b.propertyName.StartsWith("m_LocalScale")) continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(src, b);
                if (curve == null) continue;

                if (b.propertyName.StartsWith("m_LocalPosition") && !Mathf.Approximately(posScale, 1f))
                {
                    curve = ScaleCurve(curve, posScale);
                }

                EditorCurveBinding nb = b;
                nb.path = path;
                AnimationUtility.SetEditorCurve(dst, nb, curve);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(src);
            settings.loopTime = def.loop;
            AnimationUtility.SetAnimationClipSettings(dst, settings);

            string outPath = ClipsRoot + "/" + def.clipName + ".anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (existing != null)
            {
                // Overwrite in place so the controller's references survive a re-run.
                EditorUtility.CopySerialized(dst, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(dst);
            }
            else
            {
                AssetDatabase.CreateAsset(dst, outPath);
            }
        }

        private static AnimationCurve ScaleCurve(AnimationCurve src, float factor)
        {
            Keyframe[] keys = src.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= factor;
                keys[i].inTangent *= factor;
                keys[i].outTangent *= factor;
            }
            AnimationCurve c = new AnimationCurve(keys);
            c.preWrapMode = src.preWrapMode;
            c.postWrapMode = src.postWrapMode;
            return c;
        }

        /// <summary>
        /// Name and accumulated scale of the node the skeleton hangs from, found via the Hips rather
        /// than by name since the pack uses two different names for it.
        /// </summary>
        private static void ModelArmature(string file, out string nodeName, out float scale)
        {
            nodeName = null;
            scale = 1f;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxRoot + "/" + file);
            if (model == null) return;

            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Hips" || t.parent == null) continue;
                nodeName = t.parent.name;
                scale = t.parent.lossyScale.x;
                if (Mathf.Approximately(scale, 0f)) scale = 1f;
                return;
            }
        }

        // ============================================================
        // 2. ANIMATOR CONTROLLER
        // ============================================================
        private static AnimatorController BuildAnimatorController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ac.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("SmashLeft", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("SmashRight", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Victory", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Defeat", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = ac.layers[0].stateMachine;

            // Locomotion: a 2D directional blend so sidestepping reads as a sidestep rather than the
            // character sliding while facing forward. The net is always ahead, so the character never
            // turns to face its direction of travel.
            BlendTree tree;
            AnimatorState locomotion = ac.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendType = BlendTreeType.SimpleDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            tree.useAutomaticThresholds = false;
            tree.AddChild(LoadClip("Idle"), new Vector2(0f, 0f));
            tree.AddChild(LoadClip("RunForward"), new Vector2(0f, 1f));
            tree.AddChild(LoadClip("RunBackward"), new Vector2(0f, -1f));
            tree.AddChild(LoadClip("RunLeft"), new Vector2(-1f, 0f));
            tree.AddChild(LoadClip("RunRight"), new Vector2(1f, 0f));

            sm.defaultState = locomotion;

            AddOneShot(ac, sm, locomotion, "SmashRight", true);
            AddOneShot(ac, sm, locomotion, "SmashLeft", true);
            // Match-result poses hold: there is no rally left to return to.
            AddOneShot(ac, sm, locomotion, "Victory", false);
            AddOneShot(ac, sm, locomotion, "Defeat", false);

            EditorUtility.SetDirty(ac);
            return ac;
        }

        private static void AddOneShot(AnimatorController ac, AnimatorStateMachine sm,
                                       AnimatorState returnTo, string name, bool returnAfter)
        {
            AnimationClip clip = LoadClip(name);
            if (clip == null) return;

            AnimatorState state = sm.AddState(name);
            state.motion = clip;

            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, name);
            enter.hasExitTime = false;
            enter.duration = 0.06f;
            // Without this a re-trigger mid-swing restarts the state and the swing visibly hitches.
            enter.canTransitionToSelf = false;

            if (returnAfter)
            {
                AnimatorStateTransition exit = state.AddTransition(returnTo);
                exit.hasExitTime = true;
                exit.exitTime = 0.80f;
                exit.duration = 0.15f;
            }
        }

        // ============================================================
        // 3. SCENE WIRING
        // ============================================================
        private static void ApplyToScene(Avatar avatar, AnimatorController controller)
        {
            BuildCharacter("Player", false, avatar, controller, PlayerTeamColor);
            BuildCharacter("Opponent", true, avatar, controller, OpponentTeamColor);
        }

        // ============================================================
        // 4. HOME SCREEN PREFAB
        // ============================================================

        /// <summary>
        /// The same player character, saved standalone for <see cref="Pickleball.UI.HomeCharacterStage"/>
        /// to load on the home screen.
        ///
        /// Deliberately NOT the scene rig: that one is a child of the Player capsule, carries the
        /// gameplay scale that sizes it against the net, and is driven by CharacterVisual off the
        /// match controllers. The home screen wants a bare figure at unit scale, framed by its own
        /// camera and doing nothing but breathing. It shares the animator controller and the
        /// PB_Player_*.mat set with the scene rig, so a colour change lands in both places at once.
        /// </summary>
        [MenuItem("Pickleball/Setup Home Character", false, 2)]
        public static void SetupHomeCharacter()
        {
            Avatar avatar = LoadAvatar(FbxRoot + "/" + RigFile);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (avatar == null || controller == null)
            {
                Debug.LogError("[CharacterSetup] Run Pickleball/Setup Characters first — the avatar or animator controller is missing.");
                return;
            }
            BuildHomePrefab(avatar, controller);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[CharacterSetup] Home character prefab written to " + HomePrefabPath + "</color>");
        }

        private static void BuildHomePrefab(Avatar avatar, AnimatorController controller)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(HomePrefabDir))
                AssetDatabase.CreateFolder("Assets/Resources", "Characters");

            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(FbxRoot + "/" + RigFile);
            if (src == null)
            {
                Debug.LogWarning("[CharacterSetup] Rig model missing; home prefab not built.");
                return;
            }

            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(src);
            rig.name = "HomeCharacter";
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            // The scene rig's scale, even though the stage camera frames whatever height it finds
            // and could work at any of them. AttachPaddle cancels the hand's inherited scale, so the
            // paddle ends up the same absolute size whatever the character does -- which means the
            // character's scale is exactly what sets how large the paddle looks in his hand. At 1.0
            // he holds one 1.2x bigger than the same character holds during a match.
            rig.transform.localScale = Vector3.one * CharacterScale;

            Animator animator = rig.GetComponent<Animator>();
            if (animator == null) animator = rig.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // AlwaysAnimate, not CullUpdateTransforms: the only thing looking at this character is an
            // off-screen camera rendering to a texture, and the home screen's one piece of motion is
            // not worth betting on a visibility test that never had a render-texture case in mind.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            ApplyPartColors(rig, false, PlayerTeamColor);
            AttachPaddle(rig, HomePaddleScale);

            PrefabUtility.SaveAsPrefabAsset(rig, HomePrefabPath);
            Object.DestroyImmediate(rig);
        }

        /// <summary>
        /// Paddle_bat1.fbx is authored at roughly 42% of the character's height; a real pickleball
        /// paddle is nearer 28%. Seen from the match camera it passes, because at that distance it
        /// is thirty pixels of red. Standing at hero size on the home screen it reads as a suitcase
        /// on the end of his arm.
        ///
        /// Scaled on the home prefab only. The match characters keep the size the gameplay has been
        /// tuned and played against, and this does not touch the shared FBX — but the same
        /// proportion is wrong in both places, and worth fixing at the source rather than here.
        /// AttachPaddle scales it about the grip, so the fist stays on the handle.
        /// </summary>
        private const float HomePaddleScale = 0.7f;

        private static void BuildCharacter(string ownerName, bool isOpponent, Avatar avatar,
                                           AnimatorController controller, Color teamColor)
        {
            GameObject owner = GameObject.Find(ownerName);
            if (owner == null)
            {
                Debug.LogWarning("[CharacterSetup] Scene object not found: " + ownerName);
                return;
            }

            // The capsule stays as the logical root (the controllers drive its transform) but stops
            // drawing -- otherwise it renders straight through the character standing inside it.
            MeshRenderer capsule = owner.GetComponent<MeshRenderer>();
            if (capsule != null) capsule.enabled = false;

            Transform existing = owner.transform.Find(RigChildName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(FbxRoot + "/" + RigFile);
            if (src == null)
            {
                Debug.LogWarning("[CharacterSetup] Rig model missing.");
                return;
            }

            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(src);
            rig.name = RigChildName;
            rig.transform.SetParent(owner.transform, false);
            // The owner transform is a capsule centre floating at standing height, but the rig's own
            // origin is at the feet, so it has to be dropped by exactly that height to stand on the
            // court. Read it from the controller rather than hardcoding, so the rig follows if the
            // resting height is ever retuned.
            rig.transform.localPosition = new Vector3(0f, -OwnerStandingHeight(owner), 0f);
            rig.transform.localRotation = Quaternion.Euler(0f, isOpponent ? 180f : 0f, 0f);
            rig.transform.localScale = Vector3.one * CharacterScale;

            Animator animator = rig.GetComponent<Animator>();
            if (animator == null) animator = rig.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            // The gameplay controllers own position; root motion would fight them for it.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            ApplyPartColors(rig, isOpponent, teamColor);
            AttachPaddle(rig);

            CharacterVisual visual = owner.GetComponent<CharacterVisual>();
            if (visual == null) visual = owner.AddComponent<CharacterVisual>();
            visual.Configure(isOpponent, animator, OwnerMoveSpeed(owner));

            // The hand-built primitive paddle would otherwise float alongside the real one.
            PaddleVisual old = owner.GetComponent<PaddleVisual>();
            if (old != null) Object.DestroyImmediate(old, true);

            EditorUtility.SetDirty(owner);
        }

        /// <summary>
        /// The height the owner transform floats at while playing. Both controllers drive themselves
        /// toward intercept points built with y = 1, so that -- not the authored homePosition, which
        /// had drifted out of sync on the player -- is the real standing height.
        /// </summary>
        private static float OwnerStandingHeight(GameObject owner)
        {
            Component c = owner.GetComponent<PlayerController>();
            if (c == null) c = owner.GetComponent<OpponentAI>();
            if (c == null) return 1f;

            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty("homePosition");
            return p != null ? p.vector3Value.y : 1f;
        }

        private static float OwnerMoveSpeed(GameObject owner)
        {
            // moveSpeed is private on both controllers, so read it the same way the inspector would.
            Component c = owner.GetComponent<PlayerController>();
            if (c == null) c = owner.GetComponent<OpponentAI>();
            if (c == null) return 6f;

            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty("moveSpeed");
            return p != null ? p.floatValue : 6f;
        }

        /// <summary>
        /// The pack ships one untextured white material shared by all five sub-meshes, so out of the
        /// box both characters are identical featureless white figures with no team read at all. Each
        /// sub-mesh is its own renderer though, so colouring them individually gives a readable
        /// character without needing textures the pack never included.
        /// </summary>
        private static void ApplyPartColors(GameObject rig, bool isOpponent, Color teamColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            foreach (SkinnedMeshRenderer smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Color c;
                switch (smr.name)
                {
                    case "body": c = new Color(0.98f, 0.80f, 0.66f); break;               // skin
                    case "hair5": c = new Color(0.22f, 0.15f, 0.11f); break;              // hair
                    case "shirt1": c = teamColor; break;                                  // team read
                    case "shorts1": c = isOpponent ? new Color(0.35f, 0.10f, 0.09f)
                                                   : new Color(0.10f, 0.20f, 0.38f); break;
                    case "slip": c = new Color(0.92f, 0.92f, 0.94f); break;
                    default: c = teamColor; break;
                }

                string matPath = GeneratedRoot + "/PB_" + (isOpponent ? "Opp_" : "Player_") + smr.name + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, matPath);
                }
                m.shader = shader;
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
                EditorUtility.SetDirty(m);

                smr.sharedMaterial = m;
            }
        }

        /// <summary>Re-seats the paddle on the scene characters and the home prefab, without
        /// rebuilding the rigs, controller or materials the way Setup Characters does.</summary>
        [MenuItem("Pickleball/Reattach Paddles", false, 3)]
        public static void ReattachPaddles()
        {
            foreach (string ownerName in new[] { "Player", "Opponent" })
            {
                GameObject owner = GameObject.Find(ownerName);
                Transform rig = owner != null ? owner.transform.Find(RigChildName) : null;
                if (rig == null)
                {
                    Debug.LogWarning("[CharacterSetup] No " + RigChildName + " under " + ownerName + "; run Setup Characters.");
                    continue;
                }
                AttachPaddle(rig.gameObject);
                EditorUtility.SetDirty(owner);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            if (AssetDatabase.LoadAssetAtPath<GameObject>(HomePrefabPath) != null)
            {
                GameObject home = PrefabUtility.LoadPrefabContents(HomePrefabPath);
                AttachPaddle(home, HomePaddleScale);
                PrefabUtility.SaveAsPrefabAsset(home, HomePrefabPath);
                PrefabUtility.UnloadPrefabContents(home);
            }
        }

        private static void AttachPaddle(GameObject rig, float sizeMultiplier = 1f)
        {
            if (!AttachSuppliedPaddle) return;

            Transform hand = FindDeep(rig.transform, "RightHand");
            if (hand == null)
            {
                Debug.LogWarning("[CharacterSetup] RightHand bone not found; paddle not attached.");
                return;
            }

            Transform existing = hand.Find(PaddleChildName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject paddleSrc = AssetDatabase.LoadAssetAtPath<GameObject>(FbxRoot + "/" + PaddleFile);
            if (paddleSrc == null)
            {
                Debug.LogWarning("[CharacterSetup] " + PaddleFile + " not found.");
                return;
            }

            GameObject paddle = (GameObject)PrefabUtility.InstantiatePrefab(paddleSrc);
            paddle.name = PaddleChildName;
            // Some paddle exports (Paddle_bat1.fbx included) carry their own root-level scale --
            // 100x here -- needed to inflate a centimetre-scale mesh back to real units; without it
            // the paddle renders as a sub-millimetre point. Captured before reparenting overwrites it.
            Vector3 authoredScale = paddle.transform.localScale;
            paddle.transform.SetParent(hand, false);

            // The rig is authored in centimetres and compensates with a scale-100 node above the
            // skeleton, so every bone carries lossyScale 100. Parenting the paddle to a hand bone at
            // its authored scale therefore renders it 100x oversized. Undo the inherited hand scale
            // while composing with (not discarding) whatever scale the paddle's own export needs.
            Vector3 handScale = hand.lossyScale;
            float inv = Mathf.Approximately(handScale.x, 0f) ? 1f : 1f / handScale.x;
            paddle.transform.localScale = authoredScale * inv * sizeMultiplier;

            // The hand's own frame, in the hand bone's space, from where the finger bases sit on it.
            // Those are fixed offsets, so this is the same in every pose the clips put the arm in.
            Transform middle = FindDeep(rig.transform, "RightHandMiddle1");
            Transform index = FindDeep(rig.transform, "RightHandIndex1");
            Transform pinky = FindDeep(rig.transform, "RightHandPinky1");
            MeshFilter meshFilter = paddle.GetComponentInChildren<MeshFilter>();
            if (middle == null || index == null || pinky == null || meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning("[CharacterSetup] Hand bones or paddle mesh missing; paddle left at the wrist.");
            }
            else
            {
                Vector3 knuckle = hand.InverseTransformPoint(middle.position);
                float knuckleLength = knuckle.magnitude;
                Vector3 fingers = knuckle / knuckleLength;
                Vector3 indexLocal = hand.InverseTransformPoint(index.position);
                Vector3 pinkyLocal = hand.InverseTransformPoint(pinky.position);
                Vector3 thumbSide = Vector3.ProjectOnPlane(indexLocal - pinkyLocal, fingers).normalized;
                // Out of the palm: the side the fingers close towards (+Z on this rig).
                Vector3 palm = Vector3.Cross(fingers, thumbSide).normalized;

                // Handle across the palm, leaving the fist between the fingers and the thumb; blade
                // face flat to the palm, so the palm faces where the paddle face does.
                float angle = GripAngleFromFingers * Mathf.Deg2Rad;
                Vector3 shaft = (fingers * Mathf.Cos(angle) + thumbSide * Mathf.Sin(angle)).normalized;
                Quaternion rotation = Quaternion.LookRotation(shaft, palm);

                Vector3 fist = fingers * (GripAlongFingers * knuckleLength)
                             + palm * (GripInFrontOfPalm * knuckleLength)
                             + thumbSide * Vector3.Dot((indexLocal + pinkyLocal) * 0.5f, thumbSide);

                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                if (meshBounds.size.z < meshBounds.size.x || meshBounds.size.z < meshBounds.size.y)
                    Debug.LogWarning("[CharacterSetup] " + PaddleFile + "'s long axis is no longer +Z; check the grip.");
                Vector3 handleGrip = new Vector3(meshBounds.center.x, meshBounds.center.y,
                    meshBounds.min.z + meshBounds.size.z * HandleLengthFraction * HandleGripFraction);
                Vector3 handleGripInPaddle = paddle.transform.InverseTransformPoint(
                    meshFilter.transform.TransformPoint(handleGrip));

                paddle.transform.localRotation = rotation;
                paddle.transform.localPosition = fist - rotation * Vector3.Scale(paddle.transform.localScale, handleGripInPaddle);
            }

            // The paddle FBX carries its own idle take; left in place it animates on the hand.
            Animator pa = paddle.GetComponent<Animator>();
            if (pa != null) Object.DestroyImmediate(pa, true);
            Animation pl = paddle.GetComponent<Animation>();
            if (pl != null) Object.DestroyImmediate(pl, true);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }
    }
}
#endif
