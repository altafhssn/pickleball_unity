using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    /// <summary>
    /// The 3D character standing in the middle of the home screen (Docs/Figma/Frame-2.svg).
    ///
    /// The meta canvas is ScreenSpaceOverlay and the lobby paints a full-screen gradient behind
    /// itself, so nothing in the 3D scene can ever show through it — a camera pointed at the court
    /// would be drawn over, whatever its depth. The character is therefore rendered by its own
    /// camera into a RenderTexture and composited as a RawImage inside the UI, which also means the
    /// lobby can put it exactly where the board does instead of wherever the scene happens to allow.
    ///
    /// The stage builds itself a private world <see cref="StageOrigin"/> units from the origin:
    /// beyond every scene camera's far plane, so the court cameras cannot see the character, and
    /// empty, so the stage camera cannot see the court. Its lights are point lights with a finite
    /// range for the same reason — a directional light would have lit the whole match as well.
    ///
    /// Attached to the RawImage it feeds, and tears the stage down with it.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class HomeCharacterStage : MonoBehaviour
    {
        public const string PrefabResourcePath = "Characters/HomeCharacter";

        private const float StageOrigin = 5000f;
        /// <summary>The rig's mesh faces -Z at yaw 0 — the gameplay Player sits at rotation 0 facing
        /// the net, with the match camera behind him. Turning him to meet a camera that stands on
        /// +Z therefore starts from a half turn.</summary>
        private const float RigFacingOffset = 180f;
        /// <summary>Turn off straight-on, in degrees. Zero is square to the camera.</summary>
        private const float Yaw = 0f;
        /// <summary>How far the head may be turned from where the animator put it, in degrees.</summary>
        private const float MaxHeadTurn = 42f;
        private const float FieldOfView = 22f;
        /// <summary>
        /// How much of the frame's height the figure fills.
        ///
        /// The board's character is a realistically proportioned adult, so it fills its column
        /// almost completely. This rig is chibi — head and hair are 48% of his total height — and
        /// filling the column with him puts a blank head a third of a phone screen tall in the
        /// middle of the home screen. Standing him smaller inside the same column is the difference
        /// between a character on a home screen and a face pressed against the glass.
        /// </summary>
        private const float FigureHeight = 0.65f;
        /// <summary>Where his feet sit, as a fraction up from the bottom of the frame. He stands on
        /// the bottom of his column rather than floating in the middle of it, so the empty space
        /// the smaller framing buys ends up above his head where the board has it.</summary>
        private const float FigureBaseline = 0.05f;
        /// <summary>Widest the figure may get before the framing pulls back, as a fraction of the
        /// frame's width. Over 1 by a little: the frame is a narrow column, and letting the last few
        /// pixels of an elbow run past the edge is cheaper than shrinking him for it.</summary>
        private const float FigureWidth = 1.06f;
        /// <summary>Texture height cap. The rect is ~1700px tall at the 1080 reference width, but
        /// this is a soft-shaded figure with no fine detail to lose, and a full-resolution portrait
        /// render texture is a real cost on the one screen players sit on longest.</summary>
        private const int MaxTextureHeight = 1024;

        private GameObject stage;
        private RenderTexture texture;
        private Transform character;
        private Transform headBone;
        private Transform stageCamera;
        /// <summary>The head bone's own axis that points out of his face, captured from the bind
        /// pose. Bone axes in this pack are arbitrary, so it is measured rather than assumed.</summary>
        private Vector3 headFaceAxis = Vector3.forward;

        /// <summary>
        /// Builds the stage and points <paramref name="host"/>'s RawImage at it. Returns false and
        /// changes nothing if the prefab is missing — the home screen then simply has no character
        /// in it, which is the state it shipped in, rather than a black rectangle where he stood.
        /// Run Pickleball/Setup Home Character to create the prefab.
        /// </summary>
        public static bool Attach(GameObject host, Vector2 rectSize)
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null) return false;

            HomeCharacterStage stageComp = host.AddComponent<HomeCharacterStage>();
            if (!stageComp.Build(prefab, rectSize))
            {
                Destroy(stageComp);
                return false;
            }
            return true;
        }

        private bool Build(GameObject prefab, Vector2 rectSize)
        {
            if (rectSize.x < 1f || rectSize.y < 1f) return false;

            int height = Mathf.Min(MaxTextureHeight, Mathf.RoundToInt(rectSize.y));
            int width = Mathf.Max(1, Mathf.RoundToInt(height * rectSize.x / rectSize.y));

            stage = new GameObject("HomeCharacterStage");
            stage.transform.position = new Vector3(StageOrigin, StageOrigin, StageOrigin);

            GameObject go = Instantiate(prefab, stage.transform);
            character = go.transform;
            character.localPosition = Vector3.zero;
            character.localRotation = Quaternion.Euler(0f, RigFacingOffset + Yaw, 0f);

            foreach (Transform t in character.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head") { headBone = t; break; }
            }
            // Read before the animator has touched anything: in the bind pose he faces the way the
            // mesh was authored, which after RigFacingOffset is straight at the camera.
            if (headBone != null) headFaceAxis = headBone.InverseTransformDirection(-character.forward);

            Bounds bounds;
            if (!TryMeasure(go, out bounds))
            {
                Destroy(stage);
                return false;
            }

            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            texture.name = "HomeCharacterRT";
            texture.antiAliasing = 2;
            texture.Create();

            BuildCamera(bounds, (float)width / height);
            BuildLights(bounds);

            RawImage image = GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;
            return true;
        }

        /// <summary>
        /// Measures the figure by baking the posed skinned meshes and taking the box around the
        /// vertices — the only measurement of this rig that is actually true.
        ///
        /// Renderer.bounds reports 2.04u of height on a figure that stands 1.80u, and off-centre
        /// with it: the pack's meshes carry authored bounds from a centimetre-scale rig hanging
        /// under a scale-100 compensation node, and updateWhenOffscreen cannot fix that because the
        /// local bounds themselves are what is wrong. Bone positions are honest but measure the
        /// wrong thing — the head bone sits at the base of the skull, and on a character whose head
        /// and hair are nearly half his total height, everything above that bone is invisible to a
        /// skeleton-based measurement. Framing off the bones cropped him at the chin.
        ///
        /// Runs once, on ~3,900 vertices, while the lobby is being built.
        /// </summary>
        private static bool TryMeasure(GameObject character, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;

            foreach (SkinnedMeshRenderer smr in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                Mesh baked = new Mesh();
                smr.BakeMesh(baked, true);
                Encapsulate(baked.vertices, smr.transform.localToWorldMatrix, ref bounds, ref any);
                Destroy(baked);
            }

            // The paddle is a plain mesh on a hand bone, and it is part of his silhouette.
            foreach (MeshFilter mf in character.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Encapsulate(mf.sharedMesh.vertices, mf.transform.localToWorldMatrix, ref bounds, ref any);
            }

            return any && bounds.size.y > 0.001f;
        }

        private static void Encapsulate(Vector3[] vertices, Matrix4x4 toWorld, ref Bounds bounds, ref bool any)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]);
                if (!any) { bounds = new Bounds(world, Vector3.zero); any = true; }
                else bounds.Encapsulate(world);
            }
        }

        private void BuildCamera(Bounds bounds, float aspect)
        {
            float halfFov = FieldOfView * 0.5f * Mathf.Deg2Rad;
            float frameHeight = bounds.size.y / FigureHeight;

            // The frame is a tall, narrow column, so a wide pose — an arm out with a paddle on the
            // end of it — runs out of room sideways long before it runs out of room vertically.
            float widthLimited = bounds.size.x / (FigureWidth * aspect);
            frameHeight = Mathf.Max(frameHeight, widthLimited);

            float distance = (frameHeight * 0.5f) / Mathf.Tan(halfFov);
            // Aimed above his middle, which drops him to the bottom of the frame.
            float aimY = bounds.min.y + frameHeight * (0.5f - FigureBaseline);
            Vector3 target = new Vector3(bounds.center.x, aimY, bounds.center.z);

            // The camera stands on a fixed axis and the character turns to meet it, rather than the
            // camera orbiting to wherever he happens to be pointing — otherwise Yaw would move the
            // viewpoint and leave the pose unchanged, which is the opposite of what it reads as.
            Vector3 position = target + Vector3.forward * distance;

            GameObject go = new GameObject("StageCamera");
            stageCamera = go.transform;
            go.transform.SetParent(stage.transform, true);
            go.transform.position = position;
            // At the aim point, not at him: looking at the figure's centre would tilt the camera
            // down and put him straight back in the middle of the frame.
            go.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);

            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = Mathf.Max(0.05f, distance - bounds.size.magnitude);
            cam.farClipPlane = distance + bounds.size.magnitude * 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Fully transparent, so the lobby's gradient shows through around him rather than a
            // black plate. The RawImage does the compositing.
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.useOcclusionCulling = false;
            cam.targetTexture = texture;

            UnityEngine.Rendering.Universal.UniversalAdditionalCameraData data =
                go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data == null) data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            // Post-processing here would apply the match's colour grading to a UI element and, worse,
            // composite the transparent background against it.
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            data.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
        }

        /// <summary>
        /// Three point lights rather than one directional: a directional light has no position, so
        /// it would light the court and both match characters from wherever this stage wanted its
        /// key. Range is set from the character's own size so the falloff sits on him.
        /// </summary>
        private void BuildLights(Bounds bounds)
        {
            float reach = Mathf.Max(2f, bounds.size.magnitude * 3f);

            // Key: high and camera-left, the direction the board's render is lit from.
            AddLight("Key", bounds.center + new Vector3(-1.1f, 1.4f, 1.9f) * bounds.size.y * 0.6f,
                new Color(1f, 0.97f, 0.90f), 6.5f, reach);

            // Fill: opposite side, low and weak — enough to keep the shadow side from going flat
            // black against a violet backdrop.
            AddLight("Fill", bounds.center + new Vector3(1.5f, 0.2f, 1.2f) * bounds.size.y * 0.6f,
                new Color(0.78f, 0.84f, 1f), 2.2f, reach);

            // Rim: behind and above, in the kit's volt. It is the one light doing a design job
            // rather than a lighting one — it separates the silhouette from the backdrop and ties
            // him to the accent the rest of the screen is built on.
            AddLight("Rim", bounds.center + new Vector3(0.6f, 1.5f, -2f) * bounds.size.y * 0.6f,
                UITheme.Volt, 4.5f, reach);
        }

        private void AddLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(stage.transform, true);
            go.transform.position = position;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>
        /// Turns his head to the camera, after the animator has posed him.
        ///
        /// The only standing clip in the pack is the in-play ready stance: chin down, eyes on the
        /// court in front of him. That is the right pose for a rally and the wrong one for a
        /// character whose whole job on this screen is to look back at the player.
        ///
        /// A fixed pitch correction does not work, because the idle is a loop and the head moves
        /// through it — the same number of degrees reads as looking up, straight ahead, or at the
        /// floor depending on which frame you catch. Aiming his face at the camera every frame is
        /// self-correcting: it holds through the whole cycle, and through any clip that replaces it.
        /// The stance below the neck — feet, knees, the paddle hand — is left exactly as animated.
        ///
        /// Has to be LateUpdate: the Animator writes the whole skeleton every frame, so a bone set
        /// anywhere earlier is simply overwritten before anything renders.
        /// </summary>
        private void LateUpdate()
        {
            if (headBone == null || stageCamera == null) return;

            Vector3 wanted = stageCamera.position - headBone.position;
            if (wanted.sqrMagnitude < 1e-6f) return;

            Quaternion correction = Quaternion.FromToRotation(headBone.TransformDirection(headFaceAxis), wanted.normalized);

            // Clamped, so a clip that buries his chin in his chest turns his head as far as a neck
            // goes and then stops, rather than spinning it round to find the camera.
            float angle;
            Vector3 axis;
            correction.ToAngleAxis(out angle, out axis);
            if (float.IsNaN(axis.x)) return;
            headBone.rotation = Quaternion.AngleAxis(Mathf.Min(angle, MaxHeadTurn), axis) * headBone.rotation;
        }

        private void OnDestroy()
        {
            if (stage != null) Destroy(stage);
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
