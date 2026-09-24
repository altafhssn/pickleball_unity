#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Pickleball.Gameplay;
using Pickleball.Systems;
using Pickleball.Utils;
using Pickleball.VFX;
using Pickleball.UI;
using Pickleball.Backend;

namespace Pickleball.EditorTools
{
    public static class PrototypeSceneSetup
    {
        [MenuItem("Pickleball/Setup Prototype 1 Scene", false, 0)]
        public static void BuildScene()
        {
            // ============================================================
            // 1. MANAGERS GAMEOBJECT
            // ============================================================
            GameObject managersObj = FindOrCreate("Managers");
            EnsureComponent<ProfileService>(managersObj);
            EnsureComponent<MatchClock>(managersObj);
            EnsureComponent<ShotSystem>(managersObj);
            EnsureComponent<InputManager>(managersObj);
            RallyManager rallyManager = EnsureComponent<RallyManager>(managersObj);
            EnsureComponent<GameFeedbackManager>(managersObj);
            EnsureComponent<SoundManager>(managersObj);
            EnsureComponent<ImpactVFX>(managersObj);
            EnsureComponent<GameplayHUD>(managersObj);
            EnsureComponent<ScreenManager>(managersObj);
            EnsureComponent<StrikeTimingIndicator>(managersObj);

            // ============================================================
            // 2. COURT ENVIRONMENT
            // ============================================================
            GameObject courtParent = FindOrCreate("Court Environment");
            CourtLineRenderer oldLinesComp = courtParent.GetComponent<CourtLineRenderer>();
            if (oldLinesComp != null) Object.DestroyImmediate(oldLinesComp);

            // Court Surround / apron. The camera frames the whole court and necessarily sees a little
            // past the far baseline and the sidelines; without this, that margin rendered as raw
            // skybox, which read as a dead grey band across the top of the screen. Sits fractionally
            // below the court floor so the two never z-fight.
            GameObject apron = FindOrCreatePrimitive("CourtSurround", PrimitiveType.Plane, courtParent.transform);
            apron.transform.position = new Vector3(0f, -0.02f, 0f);
            apron.transform.localScale = new Vector3(3.0f, 1.0f, 4.0f);
            SetMaterial(apron, new Color(0.07f, 0.19f, 0.26f));
            Collider apronCol = apron.GetComponent<Collider>();
            if (apronCol != null) Object.DestroyImmediate(apronCol);

            // Court Floor
            GameObject floor = FindOrCreatePrimitive("CourtFloor", PrimitiveType.Plane, courtParent.transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1.2f, 1.0f, 2.0f);
            SetMaterial(floor, new Color(0.12f, 0.35f, 0.45f));

            // Court Net
            GameObject net = FindOrCreatePrimitive("Net", PrimitiveType.Cube, courtParent.transform);
            net.transform.position = new Vector3(0f, 0.45f, 0f);
            net.transform.localScale = new Vector3(10f, 0.9f, 0.15f);
            SetMaterial(net, Color.white);

            // Net Posts
            GameObject leftPost = FindOrCreatePrimitive("NetPostLeft", PrimitiveType.Cylinder, courtParent.transform);
            leftPost.transform.position = new Vector3(-5.1f, 0.5f, 0f);
            leftPost.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            SetMaterial(leftPost, new Color(0.3f, 0.3f, 0.3f));

            GameObject rightPost = FindOrCreatePrimitive("NetPostRight", PrimitiveType.Cylinder, courtParent.transform);
            rightPost.transform.position = new Vector3(5.1f, 0.5f, 0f);
            rightPost.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            SetMaterial(rightPost, new Color(0.3f, 0.3f, 0.3f));

            // Court Lines (dedicated child object so DrawAllLines doesn't clear Floor/Net)
            GameObject linesObj = FindOrCreate("CourtLines", courtParent.transform);
            CourtLineRenderer courtLines = EnsureComponent<CourtLineRenderer>(linesObj);
            courtLines.DrawAllLines();

            // ============================================================
            // 3. BALL (with Shadow & Trail)
            // ============================================================
            GameObject ballObj = FindOrCreatePrimitive("Ball", PrimitiveType.Sphere, null);
            ballObj.transform.position = new Vector3(0f, 0.2f, 0f);
            ballObj.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            SetMaterial(ballObj, new Color(1.0f, 0.85f, 0.0f));
            BallController ballController = EnsureComponent<BallController>(ballObj);
            EnsureComponent<BallTrailEffect>(ballObj);

            // Ball Shadow (child quad)
            Transform existingShadow = ballObj.transform.Find("BallShadowQuad");
            if (existingShadow == null)
            {
                GameObject shadowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shadowQuad.name = "BallShadowQuad";
                shadowQuad.transform.SetParent(ballObj.transform);
                shadowQuad.transform.localPosition = Vector3.zero;
                shadowQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                shadowQuad.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

                Collider shadowCol = shadowQuad.GetComponent<Collider>();
                if (shadowCol != null) Object.DestroyImmediate(shadowCol);

                Renderer shadowRend = shadowQuad.GetComponent<Renderer>();
                if (shadowRend != null)
                {
                    Shader shadowShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    Material shadowMat = new Material(shadowShader);
                    shadowMat.color = new Color(0f, 0f, 0f, 0.35f);
                    shadowRend.sharedMaterial = shadowMat;
                }

                EnsureComponent<BallShadow>(shadowQuad);
            }

            // ============================================================
            // 4. PLAYER (Blue Capsule)
            // ============================================================
            GameObject playerObj = FindOrCreatePrimitive("Player", PrimitiveType.Capsule, null);
            playerObj.transform.position = new Vector3(0f, 1.0f, -6f);
            SetMaterial(playerObj, new Color(0.2f, 0.6f, 1.0f));
            PlayerController playerController = EnsureComponent<PlayerController>(playerObj);

            // ============================================================
            // 5. OPPONENT (Red Capsule)
            // ============================================================
            GameObject opponentObj = FindOrCreatePrimitive("Opponent", PrimitiveType.Capsule, null);
            opponentObj.transform.position = new Vector3(0f, 1.0f, 6f);
            SetMaterial(opponentObj, new Color(0.9f, 0.25f, 0.2f));
            OpponentAI opponentAI = EnsureComponent<OpponentAI>(opponentObj);

            // ============================================================
            // 6. TRAJECTORY VISUALIZER
            // ============================================================
            GameObject targetIndicator = FindOrCreatePrimitive("LandingTargetIndicator", PrimitiveType.Cylinder, null);
            targetIndicator.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
            targetIndicator.transform.position = new Vector3(0f, 0.02f, 3f);
            Collider indCol = targetIndicator.GetComponent<Collider>();
            if (indCol != null) Object.DestroyImmediate(indCol);
            SetMaterial(targetIndicator, new Color(1.0f, 0.3f, 0.1f, 0.7f));
            targetIndicator.SetActive(false);

            GameObject trajectoryObj = FindOrCreate("TrajectoryVisualizer");
            LineRenderer lineRenderer = EnsureComponent<LineRenderer>(trajectoryObj);
            lineRenderer.startWidth = 0.15f;
            lineRenderer.endWidth = 0.08f;
            Shader projShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            lineRenderer.material = new Material(projShader);
            lineRenderer.startColor = new Color(0.2f, 0.9f, 1.0f, 0.8f);
            lineRenderer.endColor = new Color(1.0f, 0.9f, 0.2f, 0.8f);
            TrajectoryVisualizer visualizer = EnsureComponent<TrajectoryVisualizer>(trajectoryObj);

            // ============================================================
            // 7. CAMERA SETUP
            // ============================================================
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                CameraController camCtrl = EnsureComponent<CameraController>(mainCam.gameObject);
                camCtrl.SetInitialCameraPosition();
            }

            // ============================================================
            // 8. WIRE SERIALIZED REFERENCES
            // ============================================================
            WireField(playerController, "ballController", ballController);
            WireField(opponentAI, "ballController", ballController);
            WireField(rallyManager, "player", playerController);
            WireField(rallyManager, "opponent", opponentAI);
            WireField(rallyManager, "ball", ballController);
            WireField(visualizer, "player", playerController);
            WireField(visualizer, "landingTargetIndicator", targetIndicator.transform);

            if (mainCam != null)
            {
                CameraController camCtrl = mainCam.GetComponent<CameraController>();
                if (camCtrl != null)
                {
                    WireField(camCtrl, "playerTransform", playerObj.transform);
                    WireField(camCtrl, "ballTransform", ballObj.transform);
                }
            }

            EditorUtility.SetDirty(managersObj);
            EditorUtility.SetDirty(ballObj);
            EditorUtility.SetDirty(playerObj);
            EditorUtility.SetDirty(opponentObj);

            // Swaps the capsule stand-ins for the rigged character. Runs last so it can find the
            // Player/Opponent objects this method just created.
            CharacterSetup.SetupCharacters();

            Debug.Log("<color=green>[Pickleball] Full Prototype Scene Setup Complete! (All 8 systems wired)</color>");
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private static GameObject FindOrCreate(string name, Transform parent = null)
        {
            GameObject obj = FindSceneObject(name);
            if (obj == null)
            {
                obj = new GameObject(name);
            }
            if (parent != null) obj.transform.SetParent(parent);
            return obj;
        }

        private static GameObject FindOrCreatePrimitive(string name, PrimitiveType type, Transform parent)
        {
            GameObject obj = FindSceneObject(name);
            if (obj == null)
            {
                obj = GameObject.CreatePrimitive(type);
                obj.name = name;
                if (parent != null) obj.transform.SetParent(parent);
            }
            return obj;
        }

        private static GameObject FindSceneObject(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == name) return candidate.gameObject;
            return null;
        }

        private static T EnsureComponent<T>(GameObject obj) where T : Component
        {
            T comp = obj.GetComponent<T>();
            if (comp == null) comp = obj.AddComponent<T>();
            return comp;
        }

        private static void SetMaterial(GameObject obj, Color color)
        {
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                Material mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                rend.sharedMaterial = mat;
            }
        }

        private static void WireField(Component target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedProperties();
            }
        }
    }
}
#endif
