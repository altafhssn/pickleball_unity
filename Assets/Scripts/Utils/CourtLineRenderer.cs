using UnityEngine;
using Pickleball.Gameplay;
using Sim = Pickleball.Sim;

namespace Pickleball.Utils
{
    public class CourtLineRenderer : MonoBehaviour
    {
        [Header("Line Settings")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color lineColor = Color.white;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float lineY = 0.02f;

        private LineRenderer serviceCourtIndicator;
        private Material serviceIndicatorMaterial;

        [ContextMenu("Draw All Lines")]
        public void DrawAllLines()
        {
            // Clear existing lines
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }

            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                lineMaterial = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(shader));
            }

            float halfWidth = MatchConfig.CourtHalfWidth;
            float halfLength = MatchConfig.CourtHalfLength;
            float kitchen = MatchConfig.KitchenDepth;

            // Baselines
            CreateLine("Baseline_South", new Vector3(-halfWidth, lineY, -halfLength), new Vector3(halfWidth, lineY, -halfLength));
            CreateLine("Baseline_North", new Vector3(-halfWidth, lineY, halfLength), new Vector3(halfWidth, lineY, halfLength));

            // Sidelines
            CreateLine("Sideline_West", new Vector3(-halfWidth, lineY, -halfLength), new Vector3(-halfWidth, lineY, halfLength));
            CreateLine("Sideline_East", new Vector3(halfWidth, lineY, -halfLength), new Vector3(halfWidth, lineY, halfLength));

            // Net Line
            CreateLine("Net_Line", new Vector3(-halfWidth, lineY, 0f), new Vector3(halfWidth, lineY, 0f));

            // Kitchen / NVZ Lines
            CreateLine("NVZ_South", new Vector3(-halfWidth, lineY, -kitchen), new Vector3(halfWidth, lineY, -kitchen));
            CreateLine("NVZ_North", new Vector3(-halfWidth, lineY, kitchen), new Vector3(halfWidth, lineY, kitchen));

            // Center Service Lines
            CreateLine("CenterLine_South", new Vector3(0f, lineY, -halfLength), new Vector3(0f, lineY, -kitchen));
            CreateLine("CenterLine_North", new Vector3(0f, lineY, halfLength), new Vector3(0f, lineY, kitchen));
        }

        /// <summary>Outlines the only legal landing box while a serve is being prepared/in flight.</summary>
        public void ShowServiceCourt(int serverSideId, bool serveFromRight, Color color)
        {
            EnsureLineMaterial();
            if (serviceCourtIndicator == null)
            {
                GameObject indicator = new GameObject("ActiveServiceCourt");
                indicator.transform.SetParent(transform);
                serviceCourtIndicator = indicator.AddComponent<LineRenderer>();
                Shader indicatorShader = Shader.Find("Sprites/Default") ?? lineMaterial.shader;
                serviceIndicatorMaterial = new Material(indicatorShader) { name = "ServiceCourtIndicator_Runtime" };
                serviceCourtIndicator.material = serviceIndicatorMaterial;
                serviceCourtIndicator.useWorldSpace = true;
                serviceCourtIndicator.loop = false;
                serviceCourtIndicator.positionCount = 5;
                serviceCourtIndicator.startWidth = lineWidth * 2.5f;
                serviceCourtIndicator.endWidth = lineWidth * 2.5f;
            }

            bool positiveX = Sim.ServeRules.TargetCourtIsPositiveX(serverSideId, serveFromRight);
            float inset = lineWidth * 2.5f;
            float x0 = positiveX ? inset : -MatchConfig.CourtHalfWidth + inset;
            float x1 = positiveX ? MatchConfig.CourtHalfWidth - inset : -inset;
            float nearZ = serverSideId == 0 ? MatchConfig.KitchenDepth + inset : -MatchConfig.KitchenDepth - inset;
            float farZ = serverSideId == 0 ? MatchConfig.CourtHalfLength - inset : -MatchConfig.CourtHalfLength + inset;
            float y = lineY + 0.015f;

            if (serviceIndicatorMaterial != null)
            {
                serviceIndicatorMaterial.color = color;
                if (serviceIndicatorMaterial.HasProperty("_BaseColor"))
                    serviceIndicatorMaterial.SetColor("_BaseColor", color);
            }
            serviceCourtIndicator.startColor = color;
            serviceCourtIndicator.endColor = color;
            serviceCourtIndicator.SetPosition(0, new Vector3(x0, y, nearZ));
            serviceCourtIndicator.SetPosition(1, new Vector3(x1, y, nearZ));
            serviceCourtIndicator.SetPosition(2, new Vector3(x1, y, farZ));
            serviceCourtIndicator.SetPosition(3, new Vector3(x0, y, farZ));
            serviceCourtIndicator.SetPosition(4, new Vector3(x0, y, nearZ));
            serviceCourtIndicator.enabled = true;
        }

        public void HideServiceCourt()
        {
            if (serviceCourtIndicator != null) serviceCourtIndicator.enabled = false;
        }

        private void OnDestroy()
        {
            if (serviceIndicatorMaterial != null) Destroy(serviceIndicatorMaterial);
        }

        private void EnsureLineMaterial()
        {
            if (lineMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            lineMaterial = Pickleball.Systems.OwnedRuntimeAssets.Track(gameObject, new Material(shader));
        }

        private void CreateLine(string name, Vector3 start, Vector3 end)
        {
            GameObject lineObj = new GameObject(name);
            lineObj.transform.SetParent(transform);
            
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = lineMaterial;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.useWorldSpace = true;
            
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }
    }
}
