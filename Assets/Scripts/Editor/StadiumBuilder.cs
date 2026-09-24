#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Pickleball.EditorTools
{
    /// <summary>
    /// Builds a stylized stadium bowl -- perimeter wall, tiered stands, corner floodlights -- around
    /// the court's existing CourtSurround apron. Pure scenery: no colliders and no real-time lights,
    /// since BallController's bounce/fault logic is scripted from trajectory math rather than Unity
    /// physics, so none of this needs to participate in physics, and a directional light + the
    /// existing Global Volume already carry the scene's lighting.
    ///
    /// Re-runnable: deletes and rebuilds the "Stadium" child under "Court Environment" every time,
    /// same pattern as CharacterSetup.
    /// </summary>
    public static class StadiumBuilder
    {
        private const string MatRoot = "Assets/Materials/Stadium";

        // CourtSurround (see Court Environment/CourtSurround) is a 30x40 apron centred on the court;
        // the wall sits just outside its edge.
        private const float SurroundHalfX = 15f;
        private const float SurroundHalfZ = 20f;
        private const float WallThickness = 0.4f;
        private const float WallHeight = 1.0f;

        private const int TierCount = 5;
        private const float TierRise = 0.65f;
        private const float TierDepth = 1.5f;

        private const float WallOuterX = SurroundHalfX + WallThickness;
        private const float WallOuterZ = SurroundHalfZ + WallThickness;

        // Side stands (East/West) run the full length of the bowl, corners included; end stands
        // (North/South) are sized to butt against them rather than overlap at the corners.
        private const float SideStandHalfLength = WallOuterZ + 3f;
        private const float EndStandHalfWidth = WallOuterX;

        private static readonly Color WallColor = HexColor(0x0A, 0x27, 0x49);   // UITheme.PanelDarkDeep
        private static readonly Color TrimColor = HexColor(0xFF, 0xC4, 0x2E);   // UITheme.GoldMid
        private static readonly Color[] TierColors =
        {
            HexColor(0x08, 0x21, 0x3E), // UITheme.OutlineNavy
            HexColor(0x12, 0x3B, 0x6B), // UITheme.PanelDarkTop
            HexColor(0x0A, 0x27, 0x49), // UITheme.PanelDarkDeep
        };
        private static readonly Color PoleColor = new Color(0.16f, 0.16f, 0.18f);
        private static readonly Color LightColor = new Color(1f, 0.95f, 0.85f);

        [MenuItem("Pickleball/Setup Stadium", false, 2)]
        public static void SetupStadium()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MatRoot))
                AssetDatabase.CreateFolder("Assets/Materials", "Stadium");

            GameObject courtEnv = GameObject.Find("Court Environment");
            if (courtEnv == null)
            {
                Debug.LogError("[StadiumBuilder] 'Court Environment' not found in the scene; aborting.");
                return;
            }

            Transform existing = courtEnv.transform.Find("Stadium");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject stadium = new GameObject("Stadium");
            stadium.transform.SetParent(courtEnv.transform, false);

            BuildWall(stadium.transform);
            BuildEndStand(stadium.transform, "NorthStand", +1f);
            BuildEndStand(stadium.transform, "SouthStand", -1f);
            BuildSideStand(stadium.transform, "EastStand", +1f);
            BuildSideStand(stadium.transform, "WestStand", -1f);
            BuildFloodlights(stadium.transform);

            EditorUtility.SetDirty(courtEnv);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[StadiumBuilder] Stadium built.</color>");
        }

        // ============================================================
        private static void BuildWall(Transform parent)
        {
            GameObject wall = new GameObject("PerimeterWall");
            wall.transform.SetParent(parent, false);

            float wallY = WallHeight * 0.5f;
            float spanX = SurroundHalfX * 2f + WallThickness * 2f;
            float spanZ = SurroundHalfZ * 2f + WallThickness * 2f;

            CreateBox(wall.transform, "North", new Vector3(0, wallY, WallOuterZ), new Vector3(spanX, WallHeight, WallThickness), WallColor);
            CreateBox(wall.transform, "South", new Vector3(0, wallY, -WallOuterZ), new Vector3(spanX, WallHeight, WallThickness), WallColor);
            CreateBox(wall.transform, "East", new Vector3(WallOuterX, wallY, 0), new Vector3(WallThickness, WallHeight, spanZ), WallColor);
            CreateBox(wall.transform, "West", new Vector3(-WallOuterX, wallY, 0), new Vector3(WallThickness, WallHeight, spanZ), WallColor);

            // Gold cap trim along the top of each segment -- the wall is otherwise a flat navy slab
            // with no accent to read as a finished structure rather than a placeholder block.
            float trimY = WallHeight + 0.04f;
            float trimH = 0.08f;
            CreateBox(wall.transform, "TrimNorth", new Vector3(0, trimY, WallOuterZ), new Vector3(spanX, trimH, WallThickness + 0.05f), TrimColor);
            CreateBox(wall.transform, "TrimSouth", new Vector3(0, trimY, -WallOuterZ), new Vector3(spanX, trimH, WallThickness + 0.05f), TrimColor);
            CreateBox(wall.transform, "TrimEast", new Vector3(WallOuterX, trimY, 0), new Vector3(WallThickness + 0.05f, trimH, spanZ), TrimColor);
            CreateBox(wall.transform, "TrimWest", new Vector3(-WallOuterX, trimY, 0), new Vector3(WallThickness + 0.05f, trimH, spanZ), TrimColor);
        }

        /// <summary>North/South stand. Each tier is a full-height box set further out and taller than
        /// the last, so from pitch level the shorter, nearer tiers step up in front of the taller,
        /// further ones -- the classic bowl silhouette -- without needing an actual stepped mesh.</summary>
        private static void BuildEndStand(Transform parent, string name, float zSign)
        {
            GameObject stand = new GameObject(name);
            stand.transform.SetParent(parent, false);

            for (int i = 0; i < TierCount; i++)
            {
                float height = TierRise * (i + 1);
                float nearZ = WallOuterZ + i * TierDepth;
                float centerZ = zSign * (nearZ + TierDepth * 0.5f);
                Color color = i == TierCount - 1 ? TrimColor : TierColors[i % TierColors.Length];

                CreateBox(stand.transform, "Tier" + i,
                    new Vector3(0, height * 0.5f, centerZ),
                    new Vector3(EndStandHalfWidth * 2f, height, TierDepth),
                    color);
            }
        }

        /// <summary>East/West stand -- same stepped-tier idea as BuildEndStand, stacked along X instead
        /// of Z, and run the full bowl length so the four corners read as enclosed.</summary>
        private static void BuildSideStand(Transform parent, string name, float xSign)
        {
            GameObject stand = new GameObject(name);
            stand.transform.SetParent(parent, false);

            for (int i = 0; i < TierCount; i++)
            {
                float height = TierRise * (i + 1);
                float nearX = WallOuterX + i * TierDepth;
                float centerX = xSign * (nearX + TierDepth * 0.5f);
                Color color = i == TierCount - 1 ? TrimColor : TierColors[i % TierColors.Length];

                CreateBox(stand.transform, "Tier" + i,
                    new Vector3(centerX, height * 0.5f, 0),
                    new Vector3(TierDepth, height, SideStandHalfLength * 2f),
                    color);
            }
        }

        /// <summary>Four corner floodlight towers. Visual only -- no real Light components, both to
        /// stay within a mobile per-object light budget and because the directional light + Global
        /// Volume already carry scene lighting; the emissive fixture panel is enough to read as lit
        /// at the camera's distance.</summary>
        private static void BuildFloodlights(Transform parent)
        {
            GameObject rig = new GameObject("Floodlights");
            rig.transform.SetParent(parent, false);

            float cornerX = WallOuterX + TierCount * TierDepth + 1.5f;
            float cornerZ = WallOuterZ + TierCount * TierDepth + 1.5f;
            const float poleHeight = 10f;

            Vector3[] corners =
            {
                new Vector3(cornerX, 0, cornerZ),
                new Vector3(-cornerX, 0, cornerZ),
                new Vector3(cornerX, 0, -cornerZ),
                new Vector3(-cornerX, 0, -cornerZ),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 basePos = corners[i];
                GameObject tower = new GameObject("Tower" + i);
                tower.transform.SetParent(rig.transform, false);
                tower.transform.position = basePos;

                GameObject pole = CreateCylinder(tower.transform, "Pole", new Vector3(0, poleHeight * 0.5f, 0), 0.5f, poleHeight, PoleColor);

                GameObject fixturePivot = new GameObject("FixturePivot");
                fixturePivot.transform.SetParent(tower.transform, false);
                fixturePivot.transform.localPosition = new Vector3(0, poleHeight, 0);
                // Face the court centre, then tip the fixture face down toward the pitch.
                Vector3 toCenter = new Vector3(-basePos.x, 0, -basePos.z).normalized;
                fixturePivot.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up) * Quaternion.Euler(35f, 0f, 0f);

                GameObject fixture = CreateBox(fixturePivot.transform, "Fixture", new Vector3(0, 0, 0.2f), new Vector3(3.2f, 1.1f, 0.25f), LightColor);
                MeshRenderer fr = fixture.GetComponent<MeshRenderer>();
                Material fm = fr.sharedMaterial;
                fm.EnableKeyword("_EMISSION");
                fm.SetColor("_EmissionColor", LightColor * 2.2f);
            }
        }

        // ============================================================
        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial("Color_" + ColorKey(color), color);
            // The stadium never moves, so static batching can merge every piece sharing a material
            // into one draw call instead of ~36 separate ones -- without this each wall segment,
            // tier and trim strip is its own draw call, which is real GPU overhead added purely for
            // background scenery on a mobile device.
            go.isStatic = true;
            return go;
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPos, float diameter, float height, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial("Color_" + ColorKey(color), color);
            go.isStatic = true;
            return go;
        }

        private static Material GetMaterial(string key, Color color)
        {
            string path = MatRoot + "/" + key + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static string ColorKey(Color c)
        {
            return ((int)(c.r * 255)) + "_" + ((int)(c.g * 255)) + "_" + ((int)(c.b * 255));
        }

        private static Color HexColor(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f);
        }
    }
}
#endif
