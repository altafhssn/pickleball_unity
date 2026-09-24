using System.Collections;
using UnityEngine;
using Pickleball.Gameplay;

namespace Pickleball.VFX
{
    public class PaddleVisual : MonoBehaviour
    {
        [Header("Paddle Visual Settings")]
        [SerializeField] private Color paddleRimColor = new Color(0.1f, 0.1f, 0.15f);
        [SerializeField] private Color paddleFaceColor = new Color(0.15f, 0.55f, 0.95f);
        [SerializeField] private bool isOpponent = false;

        private Transform paddleRoot;
        private Transform paddleMeshHolder;
        private Coroutine swingCoroutine;
        private Vector3 defaultLocalPos;
        private Quaternion defaultLocalRot;

        public void Initialize(bool opponent)
        {
            isOpponent = opponent;
            if (isOpponent)
            {
                paddleFaceColor = new Color(0.95f, 0.25f, 0.2f);
            }

            BuildPaddleMesh();
        }

        private void Start()
        {
            if (paddleRoot == null)
            {
                BuildPaddleMesh();
            }
        }

        private void BuildPaddleMesh()
        {
            if (paddleRoot != null) return;

            GameObject rootObj = new GameObject("PaddleRoot");
            rootObj.transform.SetParent(transform, false);

            // Default position: held to the right side of player
            defaultLocalPos = isOpponent ? new Vector3(-0.45f, 0.1f, -0.2f) : new Vector3(0.45f, 0.1f, 0.2f);
            defaultLocalRot = Quaternion.Euler(15f, isOpponent ? 160f : 20f, -10f);

            rootObj.transform.localPosition = defaultLocalPos;
            rootObj.transform.localRotation = defaultLocalRot;
            paddleRoot = rootObj.transform;

            GameObject meshHolder = new GameObject("MeshHolder");
            meshHolder.transform.SetParent(paddleRoot, false);
            paddleMeshHolder = meshHolder.transform;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Unlit");

            // Handle (Grip)
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "PaddleHandle";
            handle.transform.SetParent(paddleMeshHolder, false);
            handle.transform.localPosition = new Vector3(0f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.06f, 0.12f, 0.06f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Collider hCol = handle.GetComponent<Collider>();
            if (hCol != null) Destroy(hCol);
            SetMaterial(handle, shader, new Color(0.2f, 0.2f, 0.22f));

            // Paddle Face (Oval Head)
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "PaddleFace";
            face.transform.SetParent(paddleMeshHolder, false);
            face.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            face.transform.localScale = new Vector3(0.28f, 0.02f, 0.32f);
            Collider fCol = face.GetComponent<Collider>();
            if (fCol != null) Destroy(fCol);
            SetMaterial(face, shader, paddleFaceColor);

            // Paddle Rim
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "PaddleRim";
            rim.transform.SetParent(paddleMeshHolder, false);
            rim.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            rim.transform.localScale = new Vector3(0.30f, 0.025f, 0.34f);
            Collider rCol = rim.GetComponent<Collider>();
            if (rCol != null) Destroy(rCol);
            SetMaterial(rim, shader, paddleRimColor);
        }

        private void SetMaterial(GameObject obj, Shader shader, Color color)
        {
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(shader));
                m.color = color;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                r.sharedMaterial = m;
            }
        }

        public void PlaySwing(bool isForehand, ShotType shotType, float power)
        {
            if (paddleRoot == null) BuildPaddleMesh();
            if (swingCoroutine != null) StopCoroutine(swingCoroutine);
            swingCoroutine = StartCoroutine(AnimateSwing(isForehand, shotType, power));
        }

        private IEnumerator AnimateSwing(bool isForehand, ShotType shotType, float power)
        {
            float duration = Mathf.Lerp(0.22f, 0.14f, Mathf.Clamp01(power));
            float elapsed = 0f;

            float swingSign = isForehand ? 1f : -1f;
            if (isOpponent) swingSign *= -1f;

            // Tailored wind-up and follow-through angles per shot type
            float startAngleY = -50f * swingSign;
            float endAngleY = 75f * swingSign;
            startAngleY *= Mathf.Lerp(0.4f, 1f, power);
            endAngleY *= Mathf.Lerp(0.4f, 1f, power);
            float pitchAngle = 0f;
            float scalePop = 1.0f + (power * 0.15f);

            switch (shotType)
            {
                case ShotType.Topspin:
                    pitchAngle = 35f; // Low to high whip
                    break;
                case ShotType.Smash:
                    pitchAngle = -45f; // Downward chop
                    break;
                case ShotType.Lob:
                    pitchAngle = 55f; // Skyward scoop
                    break;
                case ShotType.Dink:
                    pitchAngle = 15f;
                    startAngleY *= 0.5f;
                    endAngleY *= 0.6f;
                    break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Smooth bell-curve swing trajectory
                float curveT = Mathf.SmoothStep(0f, 1f, t);
                float curY = Mathf.Lerp(startAngleY, endAngleY, curveT);
                float curPitch = Mathf.Sin(t * Mathf.PI) * pitchAngle;

                Quaternion swingRot = defaultLocalRot * Quaternion.Euler(curPitch, curY, curY * 0.3f);
                paddleRoot.localRotation = swingRot;

                // Punch scale effect
                float s = Mathf.Sin(t * Mathf.PI);
                paddleMeshHolder.localScale = Vector3.Lerp(Vector3.one, new Vector3(scalePop, scalePop, 1.1f), s);

                yield return null;
            }

            // Return to ready stance
            float returnTime = 0.12f;
            float returnElapsed = 0f;
            Quaternion currentRot = paddleRoot.localRotation;

            while (returnElapsed < returnTime)
            {
                returnElapsed += Time.deltaTime;
                float t = returnElapsed / returnTime;
                paddleRoot.localRotation = Quaternion.Slerp(currentRot, defaultLocalRot, t);
                paddleMeshHolder.localScale = Vector3.Lerp(paddleMeshHolder.localScale, Vector3.one, t);
                yield return null;
            }

            paddleRoot.localRotation = defaultLocalRot;
            paddleMeshHolder.localScale = Vector3.one;
        }

        private void Update()
        {
            // Subtle idle breathing when not swinging
            if (swingCoroutine == null && paddleRoot != null)
            {
                float bob = Mathf.Sin(Time.time * 3.5f) * 0.02f;
                paddleRoot.localPosition = defaultLocalPos + new Vector3(0f, bob, 0f);
            }
        }
    }
}
