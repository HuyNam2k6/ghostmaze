using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.IO;

[InitializeOnLoad]
public static class HorrorCemeteryBuilder
{
    private const string BUILD_VERSION_KEY = "GhostMaze_HorrorMap_v10";

    private static class Object
    {
        public static GameObject Instantiate(GameObject original, Transform parent) => UnityEngine.Object.Instantiate(original, parent);
        public static void DestroyImmediate(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (obj is Collider col) { col.enabled = false; return; }
            try { UnityEngine.Object.DestroyImmediate(obj); } catch { }
        }
    }

    static HorrorCemeteryBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetString("LastBuiltVersion", "") != BUILD_VERSION_KEY)
            {
                SessionState.SetString("LastBuiltVersion", BUILD_VERSION_KEY);
                BuildHorrorCemeteryMap();
            }
        };
    }

    [MenuItem("Tools/Rebuild Horror Cemetery Map")]
    public static void ScheduleRebuild()
    {
        EditorApplication.delayCall += () => BuildHorrorCemeteryMap();
    }

    public static void BuildHorrorCemeteryMap(CemeteryMapConfig config = null)
    {
        try
        {
            if (config == null)
            {
                string configPath = "Assets/Settings/CemeteryMapConfig.asset";
                config = AssetDatabase.LoadAssetAtPath<CemeteryMapConfig>(configPath);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<CemeteryMapConfig>();
                    if (!Directory.Exists("Assets/Settings")) Directory.CreateDirectory("Assets/Settings");
                    AssetDatabase.CreateAsset(config, configPath);
                    AssetDatabase.SaveAssets();
                }
            }

            var colSettings = config.colliders;
            var atmSettings = config.atmosphere;
            var layoutSettings = config.layout;
            var densitySettings = config.density;

            Random.InitState(2026);

            // 1. Clean all existing or leftover world roots in the active scene (including "Maps" & "--- CEMETERY WORLD ---")
            string rootName = "Maps";
            foreach (var rootGo in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootGo != null && (rootGo.name == "Maps" || rootGo.name == "--- CEMETERY WORLD ---" || rootGo.name.StartsWith("_OLD_CEMETERY_WORLD")))
                {
                    rootGo.SetActive(false);
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(rootGo);
                    }
                    catch
                    {
                        GameObject toDestroy = rootGo;
                        EditorApplication.delayCall += () => { if (toDestroy != null) UnityEngine.Object.DestroyImmediate(toDestroy); };
                    }
                }
            }

            GameObject worldRoot = new GameObject(rootName);
            var mapManager = worldRoot.AddComponent<CemeteryMapManager>();
            mapManager.config = config;

            GameObject envGroup = new GameObject("Environment & Horror Sky");
            envGroup.transform.SetParent(worldRoot.transform);

            GameObject cloudsGroup = new GameObject("Volumetric Cloud Sea & Mist");
            cloudsGroup.transform.SetParent(worldRoot.transform);

            GameObject islandsGroup = new GameObject("Floating Islands & Cliffs");
            islandsGroup.transform.SetParent(worldRoot.transform);

            GameObject bridgesGroup = new GameObject("Arched Bridges & Staircases");
            bridgesGroup.transform.SetParent(worldRoot.transform);

            GameObject plazaGroup = new GameObject("Central Plaza & Ritual Altar");
            plazaGroup.transform.SetParent(worldRoot.transform);

            GameObject cathedralGroup = new GameObject("Gothic Cathedral Complex");
            cathedralGroup.transform.SetParent(worldRoot.transform);

            GameObject fencesGroup = new GameObject("Iron Fences & Stone Pillars");
            fencesGroup.transform.SetParent(worldRoot.transform);

            GameObject graveyardsGroup = new GameObject("Graveyards, Tombs & Monuments");
            graveyardsGroup.transform.SetParent(worldRoot.transform);

            GameObject foliageGroup = new GameObject("Gnarled Dead Trees");
            foliageGroup.transform.SetParent(worldRoot.transform);

            GameObject lanternsGroup = new GameObject("Lanterns, Candles & Lights");
            lanternsGroup.transform.SetParent(worldRoot.transform);

            // 2. Generate Procedural Horror Textures & Materials
            EnsureHorrorMaterials(
                out Material matSkybox,
                out Material matNightBackdrop,
                out Material matMoonSurface,
                out Material matMoonHalo,
                out Material matMoonRay,
                out Material matCloudAbyss,
                out Material matCloudDeep,
                out Material matCloudMid,
                out Material matCloudHigh,
                out Material matWarmPool,
                out Material matFireGlow,
                out Material matGhostGlow,
                out Material matCliffDark,
                out Material matIronFence,
                out Material matCathedralStone
            );

            // 3. Load 3D Asset Packs & Existing Materials
            var fbx1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Asset/CemeteryPack/Model1_FantasyHalloween/source/Demo_Scene.fbx");
            var fbx3 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Asset/CemeteryPack/Model3_HauntedCemetery/source/rts_props.blend");
            var fbx4 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Asset/CemeteryPack/Model4_GravestonesPack/source/GravestonesAssets.blend");
            var fbx5 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Asset/CemeteryPack/Model5_HalloweenGraveyard/source/halloween_tombstones_low.fbx");

            var matPavers = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_HallowPavers.mat");
            var matDirt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_HallowDirt.mat");
            var matTomb = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_HallowTombstones.mat");
            var matTree = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_HallowTrees.mat");
            var matLantern = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_ModularLantern.mat");
            var matStone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_AncientStone.mat");

            // Tune material colors for dark gothic moonlit contrast
            if (matPavers != null)
            {
                matPavers.color = new Color(0.45f, 0.48f, 0.60f, 1f);
                matPavers.SetFloat("_Smoothness", 0.28f);
            }
            if (matDirt != null)
            {
                matDirt.color = new Color(0.26f, 0.30f, 0.38f, 1f);
                matDirt.SetFloat("_Smoothness", 0.12f);
            }
            if (matStone != null)
            {
                matStone.color = new Color(0.50f, 0.54f, 0.66f, 1f);
                matStone.SetFloat("_Smoothness", 0.22f);
            }
            if (matTomb != null)
            {
                matTomb.color = new Color(0.62f, 0.67f, 0.78f, 1f);
            }
            if (matTree != null)
            {
                matTree.color = new Color(0.14f, 0.13f, 0.18f, 1f);
                matTree.SetFloat("_Smoothness", 0.08f);
            }
            if (matLantern != null)
            {
                matLantern.color = new Color(1.0f, 0.76f, 0.28f, 1f);
                matLantern.EnableKeyword("_EMISSION");
                matLantern.SetColor("_EmissionColor", new Color(1.0f, 0.62f, 0.16f) * 5.0f);
            }

            // Locate Mesh Parts
            Transform cryptPart = FindChildDeep(fbx1.transform, "Crypt_3");
            Transform tree1Part = FindChildDeep(fbx1.transform, "Dead_Tree_1");
            Transform coffin1Part = FindChildDeep(fbx1.transform, "Coffin_1");

            Transform obelisk1Part = FindChildDeep(fbx3.transform, "Obelisk1");
            Transform obelisk2Part = FindChildDeep(fbx3.transform, "Obelisk2");
            Transform obelisk3Part = FindChildDeep(fbx3.transform, "Obelisk3");
            Transform pillar1Part = FindChildDeep(fbx3.transform, "BrokenPillar1");
            Transform pillar2Part = FindChildDeep(fbx3.transform, "BrokenPillar2");
            Transform cross1Part = FindChildDeep(fbx3.transform, "Cross1");
            Transform cross2Part = FindChildDeep(fbx3.transform, "Cross2");
            Transform cross3Part = FindChildDeep(fbx3.transform, "Cross3");
            Transform cross4Part = FindChildDeep(fbx3.transform, "Cross4");
            Transform tomb1Part = FindChildDeep(fbx3.transform, "Tombstone1");
            Transform tomb3Part = FindChildDeep(fbx3.transform, "Tombstone3");

            Transform obeliscMonPart = FindChildDeep(fbx4.transform, "ObeliscMonument");
            Transform grave01Part = FindChildDeep(fbx4.transform, "Gravestone01");
            Transform grave02Part = FindChildDeep(fbx4.transform, "Gravestone02");
            Transform grave03Part = FindChildDeep(fbx4.transform, "Gravestone03");
            Transform grave04Part = FindChildDeep(fbx4.transform, "Gravestone04");
            Transform grave05Part = FindChildDeep(fbx4.transform, "Gravestone05");
            Transform grave06Part = FindChildDeep(fbx4.transform, "Gravestone06");
            Transform grave08Part = FindChildDeep(fbx4.transform, "Gravestone08");

            Transform fencePart = FindChildDeep(fbx5.transform, "fence.015");
            Transform postPart = FindChildDeep(fbx5.transform, "fence_post.015");
            Transform treeLargePart = FindChildDeep(fbx5.transform, "tree_large.006");
            Transform treeSmallPart = FindChildDeep(fbx5.transform, "tree_small.006");
            Transform tombLargePart = FindChildDeep(fbx5.transform, "tonbstone_large.005");
            Transform tombSmallPart = FindChildDeep(fbx5.transform, "tombstone_small.011");
            Transform tombCrossPart = FindChildDeep(fbx5.transform, "tombstone_cross.005");
            Transform candle1Part = FindChildDeep(fbx5.transform, "candle1.009");
            Transform ghostPart = FindChildDeep(fbx5.transform, "ghost.006");
            Transform crowPart = FindChildDeep(fbx5.transform, "crow.009");

            // Helper: Upright rotation for Blender Z-up meshes
            Quaternion UprightZ(float yawDeg, float tiltX = 0f, float tiltZ = 0f)
            {
                return Quaternion.Euler(tiltX, yawDeg, tiltZ) * Quaternion.Euler(-90f, 0f, 0f);
            }

            // Helper: Instantiate mesh and plant its exact bottom-center onto groundPos
            GameObject SpawnOnGround(Transform source, Transform parent, Vector3 groundPos, Quaternion rot, Vector3 scale, Material overrideMat = null)
            {
                if (source == null) return null;
                GameObject inst = Object.Instantiate(source.gameObject, parent);
                inst.name = source.name;
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = rot;
                inst.transform.localScale = scale;

                if (overrideMat != null)
                {
                    foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                        r.sharedMaterial = overrideMat;
                }

                Bounds b = new Bounds(Vector3.zero, Vector3.zero);
                bool hasB = false;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hasB) { b = r.bounds; hasB = true; }
                    else b.Encapsulate(r.bounds);
                }

                if (hasB)
                {
                    Vector3 bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
                    inst.transform.position = groundPos - bottomCenter;
                }
                else
                {
                    inst.transform.position = groundPos;
                }
                return inst;
            }

            // Helper: Add solid BoxCollider to obstacle for physics collision & raycast hit detection
            void AddBoxCollider(Transform parent, Vector3 center, Vector3 size, Quaternion rot, string name = "ObstacleCollider")
            {
                GameObject colGo = new GameObject(name);
                colGo.transform.SetParent(parent);
                colGo.transform.position = center;
                colGo.transform.rotation = rot;
                var col = colGo.AddComponent<BoxCollider>();
                col.size = size;
            }

            // Helper: Add solid CapsuleCollider for tree trunks & cylindrical obstacles
            void AddCapsuleCollider(Transform parent, Vector3 center, float radius, float height, string name = "ObstacleCollider")
            {
                GameObject colGo = new GameObject(name);
                colGo.transform.SetParent(parent);
                colGo.transform.position = center;
                var col = colGo.AddComponent<CapsuleCollider>();
                col.radius = radius;
                col.height = height;
                col.direction = 1; // Y-axis
            }

            // Helper: Spawn glowing lantern + guaranteed warm floor light pool decal + optional point light
            void SpawnLantern(Vector3 pos, float groundY, Transform parent, float poolRadius = 3.6f, bool withPointLight = false, float lightIntensity = -1f, float lightRange = -1f)
            {
                if (lightIntensity < 0f) lightIntensity = atmSettings.lanternIntensity;
                if (lightRange < 0f) lightRange = atmSettings.lanternRange;

                GameObject housing = new GameObject("GothicLantern");
                housing.transform.SetParent(parent);
                housing.transform.position = pos;

                // Base cap
                GameObject baseCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseCap.transform.SetParent(housing.transform);
                baseCap.transform.localPosition = Vector3.zero;
                baseCap.transform.localScale = new Vector3(0.36f, 0.08f, 0.36f);
                Object.DestroyImmediate(baseCap.GetComponent<Collider>());
                if (matIronFence != null) baseCap.GetComponent<MeshRenderer>().sharedMaterial = matIronFence;

                // Glowing glass core
                GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                core.transform.SetParent(housing.transform);
                core.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                core.transform.localScale = new Vector3(0.28f, 0.24f, 0.28f);
                Object.DestroyImmediate(core.GetComponent<Collider>());
                if (matLantern != null) core.GetComponent<MeshRenderer>().sharedMaterial = matLantern;

                // Top roof cap
                GameObject roofCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roofCap.transform.SetParent(housing.transform);
                roofCap.transform.localPosition = new Vector3(0f, 0.50f, 0f);
                roofCap.transform.localScale = new Vector3(0.44f, 0.08f, 0.44f);
                Object.DestroyImmediate(roofCap.GetComponent<Collider>());
                if (matIronFence != null) roofCap.GetComponent<MeshRenderer>().sharedMaterial = matIronFence;

                // Warm Golden Floor Light Pool Quad (Guarantees warm illumination on ground regardless of URP per-object light limit!)
                if (matWarmPool != null && poolRadius > 0.1f)
                {
                    GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    pool.name = "WarmLightPool";
                    pool.transform.SetParent(housing.transform);
                    pool.transform.position = new Vector3(pos.x, groundY + 0.04f, pos.z);
                    pool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    pool.transform.localScale = new Vector3(poolRadius, poolRadius, 1f);
                    Object.DestroyImmediate(pool.GetComponent<Collider>());
                    pool.GetComponent<MeshRenderer>().sharedMaterial = matWarmPool;
                }

                if (withPointLight)
                {
                    GameObject lGo = new GameObject("LanternPointLight");
                    lGo.transform.SetParent(housing.transform);
                    lGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                    Light l = lGo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1.0f, 0.56f, 0.14f);
                    l.intensity = lightIntensity;
                    l.range = lightRange;
                    l.shadows = LightShadows.None;
                }
            }

            // Track Bridge/Staircase Gate Openings & Cobblestone Roads so Fences and Graves NEVER block walkways!
            List<(Vector3 pos, float radius)> bridgeGates = new List<(Vector3 pos, float radius)>();
            List<(Vector3 a, Vector3 b, float halfWidth)> roadSegments = new List<(Vector3 a, Vector3 b, float halfWidth)>();

            void RegisterBridgeGates(Vector3 start, Vector3 end, float width)
            {
                bridgeGates.Add((start, width * 0.68f + 0.6f));
                bridgeGates.Add((end, width * 0.68f + 0.6f));
                roadSegments.Add((start, end, width * 0.55f + 0.8f));
            }

            bool IsInsideBridgeGate(Vector3 pt)
            {
                foreach (var g in bridgeGates)
                {
                    float dx = pt.x - g.pos.x;
                    float dz = pt.z - g.pos.z;
                    if ((dx * dx + dz * dz) < (g.radius * g.radius) && Mathf.Abs(pt.y - g.pos.y) < 3.5f)
                        return true;
                }
                return false;
            }

            bool IsOnWalkwayOrGate(Vector3 pt)
            {
                if (IsInsideBridgeGate(pt)) return true;
                Vector2 p2 = new Vector2(pt.x, pt.z);
                foreach (var seg in roadSegments)
                {
                    Vector2 a2 = new Vector2(seg.a.x, seg.a.z);
                    Vector2 b2 = new Vector2(seg.b.x, seg.b.z);
                    Vector2 ab = b2 - a2;
                    float lenSq = ab.sqrMagnitude;
                    float t = (lenSq > 0.001f) ? Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / lenSq) : 0f;
                    Vector2 closest = a2 + ab * t;
                    if (Vector2.Distance(p2, closest) < seg.halfWidth)
                        return true;
                }
                return false;
            }

            // Pre-register ALL 11 Inter-Island Bridges & Staircases so Island Fences automatically leave open Gateways!
            // 1. Central Plaza (0,0,0, R=16) <-> Upper-Right Cathedral Citadel (Center: (26, 8.5, 42), Size: 44x32 -> X:4..48, Z:26..58)
            Vector3 stairCathStart = new Vector3(8.5f, 0.2f, 13.5f);
            Vector3 stairCathEnd   = new Vector3(18.0f, 8.5f, 26.0f);
            RegisterBridgeGates(stairCathStart, stairCathEnd, 6.8f);

            // 2. Central Plaza <-> Upper-Left High Cliff Island (Center: (-20, 4.5, 38), Size: 26x22 -> X:-33..-7, Z:27..49)
            Vector3 brPlazaToNW_A = new Vector3(-7.5f, 0.2f, 14.1f);
            Vector3 brPlazaToNW_B = new Vector3(-13.5f, 4.5f, 27.0f);
            RegisterBridgeGates(brPlazaToNW_A, brPlazaToNW_B, 4.8f);

            // 3. Central Plaza <-> Mid-Left Giant Tree Island (Center: (-38, 1.0, 5), Size: 28x26 -> X:-52..-24, Z:-8..18)
            Vector3 brPlazaToWest_A = new Vector3(-15.7f, 0.2f, 3.0f);
            Vector3 brPlazaToWest_B = new Vector3(-24.0f, 1.0f, 3.0f);
            RegisterBridgeGates(brPlazaToWest_A, brPlazaToWest_B, 5.0f);

            // 4. Central Plaza <-> Mid-Right Graveyard Island (Center: (38, 1.5, 0), Size: 28x26 -> X:24..52, Z:-13..13)
            Vector3 brPlazaToEast_A = new Vector3(15.9f, 0.2f, -1.5f);
            Vector3 brPlazaToEast_B = new Vector3(24.0f, 1.5f, -1.5f);
            RegisterBridgeGates(brPlazaToEast_A, brPlazaToEast_B, 5.0f);

            // 5. Central Plaza <-> South-East Foreground Avenue Island (Center: (12, -2.0, -34), Size: 28x24 -> X:-2..26, Z:-46..-22)
            Vector3 brPlazaToSE_A = new Vector3(3.5f, 0.2f, -15.6f);
            Vector3 brPlazaToSE_B = new Vector3(6.5f, -2.0f, -22.0f);
            RegisterBridgeGates(brPlazaToSE_A, brPlazaToSE_B, 6.4f);

            // 6. Central Plaza <-> South-West Foreground Island (Center: (-28, -2.0, -28), Size: 28x24 -> X:-42..-14, Z:-40..-16)
            Vector3 brPlazaToSW_A = new Vector3(-10.5f, 0.2f, -12.0f);
            Vector3 brPlazaToSW_B = new Vector3(-16.5f, -2.0f, -16.0f);
            RegisterBridgeGates(brPlazaToSW_A, brPlazaToSW_B, 5.0f);

            // 7. Mid-Left Giant Tree Island <-> Upper-Left High Cliff Island
            Vector3 brWestToNW_A = new Vector3(-31.0f, 1.0f, 18.0f);
            Vector3 brWestToNW_B = new Vector3(-25.0f, 4.5f, 27.0f);
            RegisterBridgeGates(brWestToNW_A, brWestToNW_B, 4.6f);

            // 8. Upper-Left High Cliff Island <-> Upper-Right Cathedral Citadel
            Vector3 brNWToCath_A = new Vector3(-7.0f, 4.5f, 38.0f);
            Vector3 brNWToCath_B = new Vector3(4.0f, 8.5f, 38.0f);
            RegisterBridgeGates(brNWToCath_A, brNWToCath_B, 4.6f);

            // 9. Mid-Right Graveyard Island <-> Upper-Right Cathedral Citadel
            Vector3 brEastToCath_A = new Vector3(36.0f, 1.5f, 13.0f);
            Vector3 brEastToCath_B = new Vector3(36.0f, 8.5f, 26.0f);
            RegisterBridgeGates(brEastToCath_A, brEastToCath_B, 4.8f);

            // 10. Mid-Right Graveyard Island <-> South-East Foreground Avenue Island
            Vector3 brEastToSE_A = new Vector3(28.0f, 1.5f, -13.0f);
            Vector3 brEastToSE_B = new Vector3(21.0f, -2.0f, -22.0f);
            RegisterBridgeGates(brEastToSE_A, brEastToSE_B, 4.6f);

            // 11. South-West Foreground Island <-> South-East Foreground Avenue Island
            Vector3 brSWToSE_A = new Vector3(-14.0f, -2.0f, -28.0f);
            Vector3 brSWToSE_B = new Vector3(-2.0f, -2.0f, -28.0f);
            RegisterBridgeGates(brSWToSE_A, brSWToSE_B, 5.0f);

            // 12. Mid-Left Giant Tree Island <-> South-West Foreground Island
            Vector3 brWestToSW_A = new Vector3(-33.0f, 1.0f, -8.0f);
            Vector3 brWestToSW_B = new Vector3(-30.0f, -2.0f, -16.0f);
            RegisterBridgeGates(brWestToSW_A, brWestToSW_B, 4.6f);

            // 13. South-West Foreground Island <-> Lower-Left Shrine Crag (Center: (-34, -3.5, -49), Size: 11x11 -> Z:-54.5..-43.5)
            Vector3 brSWToCrag_A = new Vector3(-32.5f, -2.0f, -40.0f);
            Vector3 brSWToCrag_B = new Vector3(-33.5f, -3.5f, -43.5f);
            RegisterBridgeGates(brSWToCrag_A, brSWToCrag_B, 3.8f);

            // 14. South-East Foreground Avenue Island <-> Bottom Cloud Abyss Staircase
            Vector3 fgStairStart = new Vector3(4.0f, -2.0f, -46.0f);
            Vector3 fgStairEnd   = new Vector3(-1.0f, -6.5f, -56.0f);
            RegisterBridgeGates(fgStairStart, fgStairEnd, 5.4f);

            // Helper: Build stone post + wrought iron fence line between two points (with automatic Gate Openings!)
            void BuildFencedLine(Vector3 start, Vector3 end, float postInterval, bool lanternEveryOther = true, bool isBridgeRailing = false)
            {
                Vector3 diff = end - start;
                float dist = diff.magnitude;
                if (dist < 0.8f) return;
                int segments = Mathf.Max(1, Mathf.RoundToInt(dist / postInterval));
                Vector3 flatDir = new Vector3(diff.x, 0f, diff.z).normalized;
                if (flatDir.sqrMagnitude < 0.001f) flatDir = Vector3.forward;
                float yaw = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;

                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    Vector3 pPos = Vector3.Lerp(start, end, t);

                    // Skip island perimeter posts/fences if they fall inside a Bridge/Staircase Gate opening!
                    if (!isBridgeRailing && IsInsideBridgeGate(pPos))
                        continue;

                    // Stone Pillar (Always 100% upright!)
                    if (postPart != null)
                    {
                        SpawnOnGround(postPart, fencesGroup.transform, pPos, UprightZ(yaw), new Vector3(46f, 46f, 44f), matStone);
                        if (colSettings.enableAllColliders)
                            AddBoxCollider(fencesGroup.transform, pPos + new Vector3(0f, colSettings.pillarColliderSize.y * 0.5f, 0f), colSettings.pillarColliderSize, UprightZ(yaw), "FencePostCollider");
                    }

                    // Lantern on pillar
                    if (!lanternEveryOther || (i % 2 == 0) || i == 0 || i == segments)
                    {
                        bool addLight = (i == 0 || i == segments || i % 4 == 0);
                        SpawnLantern(pPos + new Vector3(0f, 1.52f, 0f), pPos.y, lanternsGroup.transform, 3.8f, addLight, atmSettings.lanternIntensity, atmSettings.lanternRange);
                    }

                    // Wrought Iron Fence Segment between post i and i+1
                    if (i < segments)
                    {
                        Vector3 nextPos = Vector3.Lerp(start, end, (float)(i + 1) / segments);
                        Vector3 midPos = (pPos + nextPos) * 0.5f;

                        if (!isBridgeRailing && (IsInsideBridgeGate(midPos) || IsInsideBridgeGate(nextPos)))
                            continue;

                        float segLen = Vector3.Distance(pPos, nextPos);
                        Vector3 segDir = (nextPos - pPos).normalized;

                        // Low stone curb wall under iron fence with tall invisible barrier collider to block falling off cliffs
                        GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        curb.name = "FenceCurb";
                        curb.transform.SetParent(fencesGroup.transform);
                        curb.transform.position = midPos + new Vector3(0f, 0.22f, 0f);
                        curb.transform.rotation = Quaternion.LookRotation(segDir);
                        curb.transform.localScale = new Vector3(0.38f, 0.44f, segLen);
                        var curbCol = curb.GetComponent<BoxCollider>();
                        if (curbCol != null)
                        {
                            curbCol.enabled = colSettings.enableAllColliders && colSettings.enableFenceBarriers;
                            float halfH = colSettings.fenceBarrierHeight * 0.5f;
                            curbCol.center = new Vector3(0f, halfH * 1.6f, 0f);
                            curbCol.size = new Vector3(colSettings.fenceBarrierThickness, colSettings.fenceBarrierHeight * 2.2f, 1.0f);
                        }
                        if (matStone != null) curb.GetComponent<MeshRenderer>().sharedMaterial = matStone;

                        // Iron railing
                        if (fencePart != null)
                        {
                            float flatSegLen = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(nextPos.x, nextPos.z));
                            float scaleX = (flatSegLen / 2.2f) * 90f;
                            SpawnOnGround(fencePart, fencesGroup.transform, midPos + new Vector3(0f, 0.35f, 0f), UprightZ(yaw + 90f), new Vector3(scaleX, 85f, 46f), matIronFence);
                        }
                    }
                }
            }

            // Helper: Build Continuous Raised Cobblestone Road with Seamless Stone Borders across an Island
            int roadIndexCounter = 0;
            void BuildCobblestoneRoad(Vector3 start, Vector3 end, float width, bool lanternSidePosts = false)
            {
                float flatY = Mathf.Min(start.y, end.y);
                start = new Vector3(start.x, flatY, start.z);
                end   = new Vector3(end.x, flatY, end.z);

                roadSegments.Add((start, end, width * 0.55f + 0.8f));
                roadIndexCounter++;
                float microLift = (roadIndexCounter % 7) * 0.0012f;

                Vector3 diff = end - start;
                float len = diff.magnitude;
                if (len < 0.5f) return;
                Vector3 mid = (start + end) * 0.5f;
                Vector3 dir = diff.normalized;
                Quaternion rot = Quaternion.LookRotation(dir);

                // Layer 1: Wide Stone Border Foundation (Top at flatY + 0.18f; 100% hidden under intersecting roads!)
                GameObject stoneBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stoneBase.name = "RoadStoneBorderBase";
                stoneBase.transform.SetParent(bridgesGroup.transform);
                stoneBase.transform.position = mid + new Vector3(0f, 0.09f + microLift * 0.5f, 0f);
                stoneBase.transform.rotation = rot;
                stoneBase.transform.localScale = new Vector3(width + 0.68f, 0.18f, len + 0.50f);
                if (matStone != null) stoneBase.GetComponent<MeshRenderer>().sharedMaterial = matStone;

                // Layer 2: Upper Paved Cobblestone Road Surface (Top at flatY + 0.24f; merges seamlessly at all intersections!)
                GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                road.name = "CobblestoneRoad";
                road.transform.SetParent(bridgesGroup.transform);
                road.transform.position = mid + new Vector3(0f, 0.13f + microLift, 0f);
                road.transform.rotation = rot;
                road.transform.localScale = new Vector3(width, 0.22f, len + 0.55f);
                if (matPavers != null) road.GetComponent<MeshRenderer>().sharedMaterial = matPavers;

                // Circular Cobblestone Hub Node at both ends (Using flat octagon of 2 rotated cubes so normals & UVs match 100%!)
                foreach (var nodePos in new Vector3[] { start, end })
                {
                    for (int k = 0; k < 2; k++)
                    {
                        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        hub.name = "RoadIntersectionPad";
                        hub.transform.SetParent(bridgesGroup.transform);
                        hub.transform.position = nodePos + new Vector3(0f, 0.13f + microLift, 0f);
                        hub.transform.rotation = rot * Quaternion.Euler(0f, k * 45f, 0f);
                        hub.transform.localScale = new Vector3(width * 0.92f, 0.22f, width * 0.92f);
                        if (matPavers != null) hub.GetComponent<MeshRenderer>().sharedMaterial = matPavers;
                    }
                }

                if (lanternSidePosts && postPart != null)
                {
                    int postCount = Mathf.Max(1, Mathf.RoundToInt(len / 7.5f));
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    for (int p = 1; p < postCount; p++)
                    {
                        Vector3 basePt = Vector3.Lerp(start, end, (float)p / postCount);
                        foreach (float side in new float[] { -1f, 1f })
                        {
                            Vector3 lp = basePt + rot * new Vector3(side * (width * 0.5f + 0.45f), 0.15f, 0f);
                            SpawnOnGround(postPart, fencesGroup.transform, lp, UprightZ(yaw), new Vector3(42f, 42f, 40f), matStone);
                            SpawnLantern(lp + new Vector3(0f, 1.42f, 0f), basePt.y, lanternsGroup.transform, 4.0f, false);
                        }
                    }
                }
            }

            // Helper: Spawn Gnarled Spooky Dead Tree Cluster (Using high-detail Dead_Tree_1 & tree_large.006)
            void SpawnGnarledTree(Vector3 pos, float scaleMul = 1.0f, bool colossal = false)
            {
                float yaw = Random.Range(0f, 360f);
                if (colossal)
                {
                    // Multi-trunk colossal gnarled horror tree (like the giant left tree in reference!)
                    if (treeLargePart != null)
                    {
                        SpawnOnGround(treeLargePart, foliageGroup.transform, pos, UprightZ(yaw, 4f, -5f), new Vector3(175f, 175f, 195f) * scaleMul, matTree);
                        SpawnOnGround(treeLargePart, foliageGroup.transform, pos + new Vector3(1.2f, 0f, -0.8f), UprightZ(yaw + 115f, -6f, 6f), new Vector3(145f, 145f, 165f) * scaleMul, matTree);
                    }
                    if (tree1Part != null)
                    {
                        SpawnOnGround(tree1Part, foliageGroup.transform, pos + new Vector3(-0.8f, 0f, 0.6f), Quaternion.Euler(6f, yaw + 55f, -8f), new Vector3(3.8f, 4.2f, 3.8f) * scaleMul, matTree);
                    }
                    if (colSettings.enableAllColliders)
                        AddCapsuleCollider(foliageGroup.transform, pos + new Vector3(0.2f, colSettings.treeTrunkHeight * 0.5f, 0f), colSettings.giantTreeTrunkRadius * scaleMul, colSettings.treeTrunkHeight, "TreeTrunkCollider");
                }
                else
                {
                    if (treeLargePart != null && Random.value > 0.35f)
                    {
                        SpawnOnGround(treeLargePart, foliageGroup.transform, pos, UprightZ(yaw, Random.Range(-5f, 5f), Random.Range(-5f, 5f)), new Vector3(120f, 120f, 130f) * scaleMul, matTree);
                    }
                    else if (tree1Part != null)
                    {
                        SpawnOnGround(tree1Part, foliageGroup.transform, pos, Quaternion.Euler(Random.Range(-5f, 5f), yaw, Random.Range(-5f, 5f)), new Vector3(2.5f, 2.8f, 2.5f) * scaleMul, matTree);
                    }
                    if (colSettings.enableAllColliders)
                        AddCapsuleCollider(foliageGroup.transform, pos + new Vector3(0f, colSettings.treeTrunkHeight * 0.5f, 0f), colSettings.normalTreeTrunkRadius * scaleMul, colSettings.treeTrunkHeight, "TreeTrunkCollider");
                }
            }

            // Helper: Create Detailed Layered Gothic Floating Island
            GameObject CreateFloatingIsland(string name, Vector3 pos, float width, float length, float cliffDepth, Material topMat, bool circular = false, bool fencedRim = false)
            {
                GameObject island = new GameObject(name);
                island.transform.SetParent(islandsGroup.transform);
                island.transform.position = pos;

                // 1. Walkable Top Surface
                GameObject top = GameObject.CreatePrimitive(circular ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                top.name = "TopSurface";
                top.transform.SetParent(island.transform);
                top.transform.position = pos;
                top.transform.localScale = circular ? new Vector3(width, 0.22f, length) : new Vector3(width, 0.42f, length);
                if (circular)
                {
                    // Replace bulging CapsuleCollider with an exact flat circular MeshCollider
                    Object.DestroyImmediate(top.GetComponent<Collider>());
                    top.AddComponent<MeshCollider>();
                }
                if (topMat != null) top.GetComponent<MeshRenderer>().sharedMaterial = topMat;

                // Stone border trim around the island top
                GameObject rim = GameObject.CreatePrimitive(circular ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                rim.name = "StoneRim";
                rim.transform.SetParent(island.transform);
                rim.transform.position = pos + new Vector3(0f, -0.25f, 0f);
                rim.transform.localScale = circular ? new Vector3(width * 1.03f, 0.25f, length * 1.03f) : new Vector3(width * 1.02f, 0.45f, length * 1.02f);
                Object.DestroyImmediate(rim.GetComponent<Collider>());
                if (matStone != null) rim.GetComponent<MeshRenderer>().sharedMaterial = matStone;

                // 2. Main Cliff Core
                GameObject cliff = GameObject.CreatePrimitive(circular ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                cliff.name = "CliffCore";
                cliff.transform.SetParent(island.transform);
                float cliffH = cliffDepth * 0.65f;
                cliff.transform.position = new Vector3(pos.x, pos.y - cliffH * 0.5f - 0.3f, pos.z);
                cliff.transform.localScale = new Vector3(width * 0.90f, circular ? cliffH * 0.5f : cliffH, length * 0.90f);
                Object.DestroyImmediate(cliff.GetComponent<Collider>());
                if (matCliffDark != null) cliff.GetComponent<MeshRenderer>().sharedMaterial = matCliffDark;

                // 3. Tapered Lower Underbelly
                GameObject keel = GameObject.CreatePrimitive(circular ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                keel.name = "CliffKeel";
                keel.transform.SetParent(island.transform);
                float keelH = cliffDepth * 0.55f;
                keel.transform.position = new Vector3(pos.x, pos.y - cliffDepth * 0.72f, pos.z);
                keel.transform.rotation = Quaternion.Euler(0f, 28f, 0f);
                keel.transform.localScale = new Vector3(width * 0.68f, circular ? keelH * 0.5f : keelH, length * 0.68f);
                Object.DestroyImmediate(keel.GetComponent<Collider>());
                if (matCliffDark != null) keel.GetComponent<MeshRenderer>().sharedMaterial = matCliffDark;

                // 4. Rugged Layered Rock Strata & Hanging Crags around the cliff perimeter
                int rockCount = Mathf.Clamp(Mathf.RoundToInt((width + length) * 0.48f), 14, 30);
                for (int r = 0; r < rockCount; r++)
                {
                    float ang = (r * 360f / rockCount) + Random.Range(-6f, 6f);
                    float rad = ang * Mathf.Deg2Rad;
                    float rx, rz;
                    if (circular)
                    {
                        rx = Mathf.Sin(rad) * (width * 0.46f);
                        rz = Mathf.Cos(rad) * (length * 0.46f);
                    }
                    else
                    {
                        float cos = Mathf.Cos(rad);
                        float sin = Mathf.Sin(rad);
                        float scaleToBox = 1f / Mathf.Max(Mathf.Abs(sin), Mathf.Abs(cos));
                        rx = sin * scaleToBox * (width * 0.45f);
                        rz = cos * scaleToBox * (length * 0.45f);
                    }

                    float rockH = Random.Range(cliffDepth * 0.45f, cliffDepth * 0.95f);
                    float rockY = pos.y - (rockH * 0.5f) - Random.Range(0.25f, 1.2f);

                    GameObject crag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    crag.name = "CliffCrag_" + r;
                    crag.transform.SetParent(island.transform);
                    crag.transform.position = new Vector3(pos.x + rx, rockY, pos.z + rz);
                    crag.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), ang + Random.Range(-20f, 20f), Random.Range(-8f, 8f));
                    crag.transform.localScale = new Vector3(Random.Range(3.2f, 6.2f), rockH, Random.Range(2.8f, 5.5f));
                    Object.DestroyImmediate(crag.GetComponent<Collider>());
                    if (matCliffDark != null) crag.GetComponent<MeshRenderer>().sharedMaterial = matCliffDark;
                }

                // 5. Perimeter fencing for rectangular islands (Automatically skips Bridge/Staircase Gateways!)
                if (fencedRim && !circular)
                {
                    float hw = width * 0.48f;
                    float hl = length * 0.48f;
                    Vector3 c1 = pos + new Vector3(-hw, 0.2f, -hl);
                    Vector3 c2 = pos + new Vector3(-hw, 0.2f, hl);
                    Vector3 c3 = pos + new Vector3(hw, 0.2f, hl);
                    Vector3 c4 = pos + new Vector3(hw, 0.2f, -hl);
                    BuildFencedLine(c1, c2, 3.8f, true, false);
                    BuildFencedLine(c2, c3, 3.8f, true, false);
                    BuildFencedLine(c3, c4, 3.8f, true, false);
                    BuildFencedLine(c4, c1, 3.8f, true, false);
                }

                return island;
            }

            // Helper: Build Gothic Stone Viaduct Bridge or Stepped Bridge with TRUE Hollow Archways & Seamless Landing Pads
            void BuildArchedBridge(Vector3 start, Vector3 end, float width, int numArches = 1)
            {
                Vector3 diff = end - start;
                float length3D = diff.magnitude;
                Vector3 mid = (start + end) * 0.5f;
                Vector3 dir3D = diff.normalized;
                Quaternion rot3D = Quaternion.LookRotation(dir3D);

                Vector3 flatDiff = new Vector3(diff.x, 0f, diff.z);
                float flatLen = flatDiff.magnitude;
                Vector3 flatDir = (flatLen > 0.001f) ? flatDiff.normalized : Vector3.forward;
                Quaternion flatRot = Quaternion.LookRotation(flatDir);
                float heightDiff = Mathf.Abs(end.y - start.y);

                // 1. Seamless Stone Transition Landing Pads extending onto both islands
                foreach (var endpoint in new (Vector3 pt, float dirSign)[] { (start, -1f), (end, 1f) })
                {
                    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pad.name = "BridgeLandingPad";
                    pad.transform.SetParent(bridgesGroup.transform);
                    pad.transform.position = endpoint.pt + flatDir * (endpoint.dirSign * 0.8f) + new Vector3(0f, 0.08f, 0f);
                    pad.transform.rotation = flatRot;
                    pad.transform.localScale = new Vector3(width + 0.5f, 0.28f, 2.4f);
                    if (matPavers != null) pad.GetComponent<MeshRenderer>().sharedMaterial = matPavers;
                }

                // 2. Bridge Deck or Level Stone Steps (when connecting islands of different heights)
                if (heightDiff <= 0.6f)
                {
                    GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    deck.name = "BridgeDeck";
                    deck.transform.SetParent(bridgesGroup.transform);
                    deck.transform.position = mid;
                    deck.transform.rotation = rot3D;
                    deck.transform.localScale = new Vector3(width, 0.45f, length3D + 0.6f);
                    if (matPavers != null) deck.GetComponent<MeshRenderer>().sharedMaterial = matPavers;
                }
                else
                {
                    // Underlying Sloped Stone Spine
                    GameObject spine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    spine.name = "SteppedBridgeSpine";
                    spine.transform.SetParent(bridgesGroup.transform);
                    spine.transform.position = mid + new Vector3(0f, -0.35f, 0f);
                    spine.transform.rotation = rot3D;
                    spine.transform.localScale = new Vector3(width, 0.65f, length3D + 0.4f);
                    if (matStone != null) spine.GetComponent<MeshRenderer>().sharedMaterial = matStone;

                    // Horizontal Level Cobblestone Steps along the entire bridge span
                    int stepCount = Mathf.Clamp(Mathf.RoundToInt(flatLen / 0.82f), 8, 32);
                    float stepDepth = (flatLen / stepCount) * 1.28f;
                    for (int s = 0; s <= stepCount; s++)
                    {
                        float t = (float)s / stepCount;
                        Vector3 sPos = Vector3.Lerp(start, end, t);
                        GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        step.name = "BridgeStep_" + s;
                        step.transform.SetParent(bridgesGroup.transform);
                        step.transform.position = sPos + new Vector3(0f, 0.12f, 0f);
                        step.transform.rotation = flatRot;
                        step.transform.localScale = new Vector3(width - 0.2f, 0.38f, stepDepth);
                        if (matPavers != null) step.GetComponent<MeshRenderer>().sharedMaterial = matPavers;
                    }
                }

                // Stone Side Parapet Trim
                GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trim.name = "BridgeTrim";
                trim.transform.SetParent(bridgesGroup.transform);
                trim.transform.position = mid + new Vector3(0f, -0.38f, 0f);
                trim.transform.rotation = rot3D;
                trim.transform.localScale = new Vector3(width + 0.48f, 0.55f, length3D + 0.4f);
                if (matStone != null) trim.GetComponent<MeshRenderer>().sharedMaterial = matStone;

                // 3. Hollow Stone Arch Viaduct Supports Underneath (Using 100% vertical upright piers!)
                float archSpanLen = flatLen / numArches;
                for (int a = 0; a < numArches; a++)
                {
                    float t0 = (float)a / numArches;
                    float t1 = (float)(a + 1) / numArches;
                    float tm = (t0 + t1) * 0.5f;
                    Vector3 archCenter = Vector3.Lerp(start, end, tm);

                    Vector3 pierPos1 = Vector3.Lerp(start, end, t0 + 0.08f / numArches);
                    Vector3 pierPos2 = Vector3.Lerp(start, end, t1 - 0.08f / numArches);

                    foreach (var pPos in new Vector3[] { pierPos1, pierPos2 })
                    {
                        GameObject pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        pier.name = "ViaductPier";
                        pier.transform.SetParent(bridgesGroup.transform);
                        pier.transform.position = pPos + new Vector3(0f, -7.5f, 0f);
                        pier.transform.rotation = flatRot;
                        pier.transform.localScale = new Vector3(width * 0.88f, 14.5f, 2.2f);
                        Object.DestroyImmediate(pier.GetComponent<Collider>());
                        if (matCliffDark != null) pier.GetComponent<MeshRenderer>().sharedMaterial = matCliffDark;
                    }

                    int archSegments = 7;
                    float clearSpan = archSpanLen * 0.72f;
                    float archRise = 3.2f;
                    for (int s = 0; s < archSegments; s++)
                    {
                        float u = ((float)s / (archSegments - 1)) * 2f - 1f;
                        float zOff = u * (clearSpan * 0.5f);
                        float yOff = -archRise * (u * u) - 0.9f;
                        float pitch = u * 48f;

                        Vector3 vPos = Vector3.Lerp(start, end, Mathf.Lerp(t0 + 0.1f / numArches, t1 - 0.1f / numArches, (float)s / (archSegments - 1)))
                                       + new Vector3(0f, yOff, 0f);

                        GameObject voussoir = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        voussoir.name = "ArchVoussoir";
                        voussoir.transform.SetParent(bridgesGroup.transform);
                        voussoir.transform.position = vPos;
                        voussoir.transform.rotation = flatRot * Quaternion.Euler(pitch, 0f, 0f);
                        voussoir.transform.localScale = new Vector3(width * 0.85f, 1.15f, (clearSpan / archSegments) * 1.38f);
                        Object.DestroyImmediate(voussoir.GetComponent<Collider>());
                        if (matStone != null) voussoir.GetComponent<MeshRenderer>().sharedMaterial = matStone;
                    }
                }

                // 4. Fenced Railings & Lanterns along both sides of the bridge (isBridgeRailing = true)
                Vector3 leftOffset = flatRot * new Vector3(-width * 0.46f, 0.22f, 0f);
                Vector3 rightOffset = flatRot * new Vector3(width * 0.46f, 0.22f, 0f);
                BuildFencedLine(start + leftOffset, end + leftOffset, 3.6f, false, true);
                BuildFencedLine(start + rightOffset, end + rightOffset, 3.6f, false, true);
            }

            // =========================================================================
            // 4. BUILD THE GRAND MULTI-ISLAND CEMETERY LAYOUT & CONNECTED WALKWAYS
            // =========================================================================

            // --- 4.1 CENTRAL CIRCULAR PLAZA (Diameter = 32m, Y = 0) ---
            CreateFloatingIsland("Island_CentralPlaza", Vector3.zero, 32f, 32f, 16f, matPavers, true, false);

            // Concentric Cobblestone & Stone Rings in Plaza
            float[] ringSizes = new float[] { 25f, 18.5f, 11.5f, 7.2f };
            float[] ringHeights = new float[] { 0.14f, 0.22f, 0.30f, 0.42f };
            for (int r = 0; r < ringSizes.Length; r++)
            {
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "PlazaRing_" + r;
                ring.transform.SetParent(plazaGroup.transform);
                ring.transform.position = new Vector3(0f, ringHeights[r], 0f);
                ring.transform.localScale = new Vector3(ringSizes[r], 0.12f, ringSizes[r]);
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.GetComponent<MeshRenderer>().sharedMaterial = (r % 2 == 0) ? matPavers : matStone;
            }

            // 6 Radial Cobblestone Avenues on Central Plaza connecting the Altar Ring out to all 6 Bridge/Staircase Gates!
            BuildCobblestoneRoad(new Vector3(3.2f, 0.08f, 5.2f), stairCathStart, 5.8f, false);
            BuildCobblestoneRoad(new Vector3(-3.0f, 0.08f, 5.5f), brPlazaToNW_A, 4.4f, false);
            BuildCobblestoneRoad(new Vector3(-6.0f, 0.08f, 1.2f), brPlazaToWest_A, 4.6f, false);
            BuildCobblestoneRoad(new Vector3(6.0f, 0.08f, -0.6f), brPlazaToEast_A, 4.6f, false);
            BuildCobblestoneRoad(new Vector3(1.5f, 0.08f, -6.0f), brPlazaToSE_A, 5.6f, false);
            BuildCobblestoneRoad(new Vector3(-4.2f, 0.08f, -4.8f), brPlazaToSW_A, 4.6f, false);

            // Central Ritual Altar & Glowing Fire Shrine
            GameObject altarPedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altarPedestal.name = "AltarPedestal";
            altarPedestal.transform.SetParent(plazaGroup.transform);
            altarPedestal.transform.position = new Vector3(0f, 0.65f, 0f);
            altarPedestal.transform.localScale = new Vector3(4.2f, 0.32f, 4.2f);
            Object.DestroyImmediate(altarPedestal.GetComponent<Collider>());
            if (matStone != null) altarPedestal.GetComponent<MeshRenderer>().sharedMaterial = matStone;

            if (obelisk1Part != null)
            {
                SpawnOnGround(obelisk1Part, plazaGroup.transform, new Vector3(0f, 0.95f, 0f), UprightZ(0f), new Vector3(1.1f, 1.1f, 1.4f), matStone);
                AddBoxCollider(plazaGroup.transform, new Vector3(0f, 2.3f, 0f), new Vector3(1.1f, 3.2f, 1.1f), Quaternion.identity, "CentralObeliskCollider");
            }

            // Central Ritual Fire Core + Large Golden Floor Glow Pool
            GameObject fireCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireCore.name = "RitualFireCore";
            fireCore.transform.SetParent(plazaGroup.transform);
            fireCore.transform.position = new Vector3(0f, 1.55f, 0f);
            fireCore.transform.localScale = new Vector3(1.15f, 1.85f, 1.15f);
            Object.DestroyImmediate(fireCore.GetComponent<Collider>());
            if (matFireGlow != null) fireCore.GetComponent<MeshRenderer>().sharedMaterial = matFireGlow;

            if (matWarmPool != null)
            {
                GameObject centralPool = GameObject.CreatePrimitive(PrimitiveType.Quad);
                centralPool.name = "CentralAltarLightPool";
                centralPool.transform.SetParent(plazaGroup.transform);
                centralPool.transform.position = new Vector3(0f, 0.56f, 0f);
                centralPool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                centralPool.transform.localScale = new Vector3(13.5f, 13.5f, 1f);
                Object.DestroyImmediate(centralPool.GetComponent<Collider>());
                centralPool.GetComponent<MeshRenderer>().sharedMaterial = matWarmPool;
            }

            GameObject altarLightGo = new GameObject("AltarRitualLight");
            altarLightGo.transform.SetParent(plazaGroup.transform);
            altarLightGo.transform.position = new Vector3(0f, 2.6f, 0f);
            Light altarLight = altarLightGo.AddComponent<Light>();
            altarLight.type = LightType.Point;
            altarLight.color = new Color(1.0f, 0.52f, 0.12f);
            altarLight.intensity = atmSettings.ritualFireIntensity;
            altarLight.range = 20f;
            altarLight.shadows = LightShadows.Soft;

            // Ring of 12 Ritual Candles around the Central Altar
            for (int c = 0; c < 12; c++)
            {
                float cAng = c * 30f * Mathf.Deg2Rad;
                Vector3 cPos = new Vector3(Mathf.Sin(cAng) * 2.5f, 0.55f, Mathf.Cos(cAng) * 2.5f);
                if (candle1Part != null)
                    SpawnOnGround(candle1Part, plazaGroup.transform, cPos, UprightZ(c * 30f), new Vector3(90f, 90f, 90f), matLantern);
                SpawnLantern(cPos + new Vector3(0f, 0.1f, 0f), 0.45f, lanternsGroup.transform, 2.4f, false);
            }

            // 6 Towering Gothic Obelisks standing upright around the Central Plaza (Placed cleanly between radial roads)
            float[] obeliskAngles = new float[] { 56f, 118f, 156f, 204f, 250f, 310f };
            for (int i = 0; i < obeliskAngles.Length; i++)
            {
                float ang = obeliskAngles[i];
                float rad = ang * Mathf.Deg2Rad;
                Vector3 obPos = new Vector3(Mathf.Sin(rad) * 12.2f, 0.25f, Mathf.Cos(rad) * 12.2f);

                GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ped.name = "ObeliskPedestal_" + i;
                ped.transform.SetParent(plazaGroup.transform);
                ped.transform.position = obPos + new Vector3(0f, 0.45f, 0f);
                ped.transform.rotation = Quaternion.Euler(0f, ang, 0f);
                ped.transform.localScale = new Vector3(2.2f, 0.9f, 2.2f);
                if (matCathedralStone != null) ped.GetComponent<MeshRenderer>().sharedMaterial = matCathedralStone;

                Transform chosenOb = (i % 2 == 0 && obelisk3Part != null) ? obelisk3Part : obeliscMonPart;
                if (chosenOb != null)
                {
                    Vector3 sc = (chosenOb == obelisk3Part) ? new Vector3(0.85f, 0.85f, 0.92f) : new Vector3(1.25f, 1.25f, 1.55f);
                    SpawnOnGround(chosenOb, plazaGroup.transform, obPos + new Vector3(0f, 0.9f, 0f), UprightZ(ang), sc, matCathedralStone);
                    AddBoxCollider(plazaGroup.transform, obPos + new Vector3(0f, 4.4f, 0f), new Vector3(1.4f, 8.0f, 1.4f), Quaternion.Euler(0f, ang, 0f), "PlazaObeliskCollider");
                }

                SpawnLantern(obPos + new Vector3(0.95f, 0.92f, 0f), 0.25f, lanternsGroup.transform, 4.8f, true, 6.5f, 10f);
                SpawnLantern(obPos + new Vector3(-0.95f, 0.92f, 0f), 0.25f, lanternsGroup.transform, 4.0f, false);
            }

            // Curved Fenced Railing Arcs around Central Plaza Rim (Automatically leaves open gateways at all 6 radial bridges!)
            int rimSteps = 36;
            for (int s = 0; s < rimSteps; s++)
            {
                float r1 = (s * 360f / rimSteps) * Mathf.Deg2Rad;
                float r2 = ((s + 1) * 360f / rimSteps) * Mathf.Deg2Rad;
                Vector3 p1 = new Vector3(Mathf.Sin(r1) * 15.3f, 0.22f, Mathf.Cos(r1) * 15.3f);
                Vector3 p2 = new Vector3(Mathf.Sin(r2) * 15.3f, 0.22f, Mathf.Cos(r2) * 15.3f);
                BuildFencedLine(p1, p2, 3.5f, true, false);
            }

            // --- 4.2 UPPER-RIGHT CATHEDRAL CITADEL ISLAND (Pos: (26f, 8.5f, 42f), Size: 44x32 -> X:4..48, Z:26..58) ---
            Vector3 cathIslandPos = new Vector3(26f, 8.5f, 42f);
            CreateFloatingIsland("Island_CathedralCitadel", cathIslandPos, 44f, 32f, 20f, matDirt, false, true);

            // Cathedral Stone Terrace Podium
            GameObject cathTerrace = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cathTerrace.name = "Cathedral_StoneTerrace";
            cathTerrace.transform.SetParent(cathedralGroup.transform);
            cathTerrace.transform.position = new Vector3(26f, 8.85f, 43f);
            cathTerrace.transform.rotation = Quaternion.Euler(0f, -22f, 0f);
            cathTerrace.transform.localScale = new Vector3(28f, 0.7f, 19f);
            if (matPavers != null) cathTerrace.GetComponent<MeshRenderer>().sharedMaterial = matPavers;

            // Connected Cobblestone Courtyard & Roads on Cathedral Citadel Island
            Vector3 cathCourtyardHub = new Vector3(21.5f, 8.55f, 33.0f);
            BuildCobblestoneRoad(stairCathEnd, cathCourtyardHub, 6.8f, true);           // Main Staircase -> Cathedral Courtyard
            BuildCobblestoneRoad(brNWToCath_B, new Vector3(13.0f, 8.55f, 36.0f), 4.6f, true); // West Bridge -> Courtyard
            BuildCobblestoneRoad(new Vector3(13.0f, 8.55f, 36.0f), cathCourtyardHub, 4.6f, false);
            BuildCobblestoneRoad(brEastToCath_B, new Vector3(34.0f, 8.55f, 33.0f), 4.6f, true); // East Stepped Bridge -> Courtyard
            BuildCobblestoneRoad(new Vector3(34.0f, 8.55f, 33.0f), cathCourtyardHub, 4.6f, false);

            // Grand Gothic Cathedral Complex
            Quaternion cathRot = Quaternion.Euler(0f, 212f, 0f);
            Vector3 cathCenter = new Vector3(26.5f, 9.2f, 44.0f);
            if (cryptPart != null)
            {
                SpawnOnGround(cryptPart, cathedralGroup.transform, cathCenter, cathRot, new Vector3(7.5f, 8.2f, 7.2f), matCathedralStone);
                SpawnOnGround(cryptPart, cathedralGroup.transform, cathCenter + cathRot * new Vector3(0f, 0f, 4.8f), cathRot, new Vector3(4.8f, 5.2f, 3.8f), matCathedralStone);
                SpawnOnGround(cryptPart, cathedralGroup.transform, cathCenter + cathRot * new Vector3(-9.5f, 0f, -0.5f), cathRot, new Vector3(4.8f, 6.0f, 4.8f), matCathedralStone);
                SpawnOnGround(cryptPart, cathedralGroup.transform, cathCenter + cathRot * new Vector3(9.5f, 0f, -0.5f), cathRot, new Vector3(4.8f, 6.0f, 4.8f), matCathedralStone);

                // Solid colliders for Cathedral buildings
                AddBoxCollider(cathedralGroup.transform, cathCenter + cathRot * new Vector3(0f, 5.5f, 0.5f), new Vector3(15.0f, 11.0f, 15.0f), cathRot, "CathedralNaveCollider");
                AddBoxCollider(cathedralGroup.transform, cathCenter + cathRot * new Vector3(-9.5f, 4.5f, -0.5f), new Vector3(7.0f, 9.0f, 8.0f), cathRot, "CathedralLeftWingCollider");
                AddBoxCollider(cathedralGroup.transform, cathCenter + cathRot * new Vector3(9.5f, 4.5f, -0.5f), new Vector3(7.0f, 9.0f, 8.0f), cathRot, "CathedralRightWingCollider");
            }
            if (obelisk3Part != null)
            {
                SpawnOnGround(obelisk3Part, cathedralGroup.transform, cathCenter + cathRot * new Vector3(-6.2f, 4.5f, 2.5f), UprightZ(212f), new Vector3(1.1f, 1.1f, 1.85f), matCathedralStone);
                SpawnOnGround(obelisk3Part, cathedralGroup.transform, cathCenter + cathRot * new Vector3(6.2f, 4.5f, 2.5f), UprightZ(212f), new Vector3(1.1f, 1.1f, 1.85f), matCathedralStone);
                SpawnOnGround(obelisk3Part, cathedralGroup.transform, cathCenter + cathRot * new Vector3(-12.5f, 2.5f, -1.0f), UprightZ(212f), new Vector3(0.95f, 0.95f, 1.65f), matCathedralStone);
                SpawnOnGround(obelisk3Part, cathedralGroup.transform, cathCenter + cathRot * new Vector3(12.5f, 2.5f, -1.0f), UprightZ(212f), new Vector3(0.95f, 0.95f, 1.65f), matCathedralStone);
            }

            // Glowing Fiery Cathedral Portal Archway + Facade Uplights
            Vector3 portalPos = cathCenter + cathRot * new Vector3(0f, 2.2f, 8.2f);
            GameObject portalArch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portalArch.name = "CathedralPortalGlow";
            portalArch.transform.SetParent(cathedralGroup.transform);
            portalArch.transform.position = portalPos;
            portalArch.transform.rotation = cathRot;
            portalArch.transform.localScale = new Vector3(2.4f, 4.2f, 0.35f);
            Object.DestroyImmediate(portalArch.GetComponent<Collider>());
            if (matFireGlow != null) portalArch.GetComponent<MeshRenderer>().sharedMaterial = matFireGlow;

            if (matWarmPool != null)
            {
                GameObject portalPool = GameObject.CreatePrimitive(PrimitiveType.Quad);
                portalPool.name = "PortalLightPool";
                portalPool.transform.SetParent(cathedralGroup.transform);
                portalPool.transform.position = new Vector3(portalPos.x, 9.26f, portalPos.z) + cathRot * new Vector3(0f, 0f, 2.5f);
                portalPool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                portalPool.transform.localScale = new Vector3(14f, 14f, 1f);
                Object.DestroyImmediate(portalPool.GetComponent<Collider>());
                portalPool.GetComponent<MeshRenderer>().sharedMaterial = matWarmPool;
            }

            Vector3[] cathLightOffsets = new Vector3[] {
                new Vector3(0f, 3.0f, 11.5f),
                new Vector3(-8.5f, 4.0f, 7.5f),
                new Vector3(8.5f, 4.0f, 7.5f)
            };
            foreach (var clo in cathLightOffsets)
            {
                GameObject clGo = new GameObject("CathedralFacadeLight");
                clGo.transform.SetParent(cathedralGroup.transform);
                clGo.transform.position = cathCenter + cathRot * clo;
                Light cl = clGo.AddComponent<Light>();
                cl.type = LightType.Point;
                cl.color = new Color(1.0f, 0.56f, 0.16f);
                cl.intensity = 12.0f;
                cl.range = 20f;
                cl.shadows = LightShadows.None;
            }

            // --- 4.3 MID-LEFT GIANT TREE ISLAND (Pos: (-38f, 1.0f, 5f), Size: 28x26 -> X:-52..-24, Z:-8..18) ---
            CreateFloatingIsland("Island_WestGiantTree", new Vector3(-38f, 1.0f, 5f), 28f, 26f, 16f, matDirt, false, true);
            Vector3 westHub = new Vector3(-33.5f, 1.05f, 4.0f);
            BuildCobblestoneRoad(brPlazaToWest_B, westHub, 4.8f, true);
            BuildCobblestoneRoad(westHub, brWestToNW_A, 4.4f, true);
            BuildCobblestoneRoad(westHub, brWestToSW_A, 4.4f, true);
            BuildCobblestoneRoad(westHub, new Vector3(-44.0f, 1.05f, 7.5f), 4.2f, false);

            // Colossal Gnarled Dead Tree on the Left Cliff
            SpawnGnarledTree(new Vector3(-47f, 1.2f, 9f), 1.25f, true);

            // --- 4.4 UPPER-LEFT HIGH CLIFF ISLAND (Pos: (-20f, 4.5f, 38f), Size: 26x22 -> X:-33..-7, Z:27..49) ---
            CreateFloatingIsland("Island_NorthWestHigh", new Vector3(-20f, 4.5f, 38f), 26f, 22f, 18f, matDirt, false, true);
            Vector3 nwHub = new Vector3(-19.0f, 4.55f, 36.5f);
            BuildCobblestoneRoad(brPlazaToNW_B, nwHub, 4.6f, true);
            BuildCobblestoneRoad(brWestToNW_B, nwHub, 4.4f, true);
            BuildCobblestoneRoad(nwHub, brNWToCath_A, 4.6f, true);

            // --- 4.5 MID-RIGHT GRAVEYARD ISLAND (Pos: (38f, 1.5f, 0f), Size: 28x26 -> X:24..52, Z:-13..13) ---
            CreateFloatingIsland("Island_EastGraveyard", new Vector3(38f, 1.5f, 0f), 28f, 26f, 16f, matDirt, false, true);
            Vector3 eastHub = new Vector3(34.0f, 1.55f, -1.0f);
            BuildCobblestoneRoad(brPlazaToEast_B, eastHub, 4.8f, true);
            BuildCobblestoneRoad(eastHub, brEastToCath_A, 4.6f, true);
            BuildCobblestoneRoad(eastHub, brEastToSE_A, 4.4f, true);
            BuildCobblestoneRoad(eastHub, new Vector3(45.0f, 1.55f, -1.0f), 4.2f, false);

            // --- 4.6 LOWER-LEFT FOREGROUND ISLAND (Pos: (-28f, -2.0f, -28f), Size: 28x24 -> X:-42..-14, Z:-40..-16) ---
            CreateFloatingIsland("Island_SouthWestForeground", new Vector3(-28f, -2.0f, -28f), 28f, 24f, 16f, matDirt, false, true);
            Vector3 swHub = new Vector3(-26.5f, -1.95f, -26.5f);
            BuildCobblestoneRoad(brPlazaToSW_B, swHub, 4.8f, true);
            BuildCobblestoneRoad(brWestToSW_B, swHub, 4.4f, true);
            BuildCobblestoneRoad(swHub, brSWToSE_A, 4.6f, true);
            BuildCobblestoneRoad(swHub, brSWToCrag_A, 4.0f, false);

            // --- 4.7 FOREGROUND AVENUE & BOTTOM-RIGHT MONUMENT TERRACE (Pos: (12f, -2.0f, -34f), Size: 28x24 -> X:-2..26, Z:-46..-22) ---
            CreateFloatingIsland("Island_SouthEastForeground", new Vector3(12f, -2.0f, -34f), 28f, 24f, 16f, matDirt, false, true);
            Vector3 seHub = new Vector3(7.5f, -1.95f, -32.0f);
            BuildCobblestoneRoad(fgStairStart, seHub, 6.0f, true);
            BuildCobblestoneRoad(seHub, brPlazaToSE_B, 6.2f, true);
            BuildCobblestoneRoad(seHub, brSWToSE_B, 4.6f, true);
            BuildCobblestoneRoad(seHub, brEastToSE_B, 4.4f, true);
            BuildCobblestoneRoad(seHub, new Vector3(20.0f, -1.95f, -35.0f), 4.4f, false);

            // Towering Obelisk Monument at Bottom-Right Foreground Corner
            Vector3 brObPos = new Vector3(22.5f, -1.8f, -36.5f);
            if (obelisk3Part != null)
            {
                SpawnOnGround(obelisk3Part, graveyardsGroup.transform, brObPos, UprightZ(-35f), new Vector3(1.05f, 1.05f, 1.15f), matCathedralStone);
                AddBoxCollider(graveyardsGroup.transform, brObPos + new Vector3(0f, 4.0f, 0f), new Vector3(1.35f, 8.0f, 1.35f), Quaternion.Euler(0f, -35f, 0f), "CornerObeliskCollider");
                SpawnLantern(brObPos + new Vector3(-1.1f, 0.4f, 0.8f), -1.8f, lanternsGroup.transform, 5.5f, true, 7.0f, 10f);
            }

            // --- 4.8 CONNECTED SHRINE CRAG & BACKGROUND ROCK CRAGS ---
            CreateFloatingIsland("Crag_LowerLeft", new Vector3(-34f, -3.5f, -49f), 11f, 11f, 14f, matPavers, false, false);
            if (obeliscMonPart != null)
            {
                SpawnOnGround(obeliscMonPart, graveyardsGroup.transform, new Vector3(-34f, -3.3f, -49f), UprightZ(25f), new Vector3(1.2f, 1.2f, 1.3f), matCathedralStone);
                AddBoxCollider(graveyardsGroup.transform, new Vector3(-34f, -3.3f + 4.0f, -49f), new Vector3(1.35f, 8.0f, 1.35f), Quaternion.Euler(0f, 25f, 0f), "CragObeliskCollider");
            }
            SpawnLantern(new Vector3(-33f, -3.2f, -47.5f), -3.3f, lanternsGroup.transform, 4.5f, true, 6.0f, 9f);

            CreateFloatingIsland("Crag_FarRight", new Vector3(56f, -2.5f, -16f), 12f, 12f, 14f, matDirt, false, false);
            CreateFloatingIsland("Crag_FarLeft", new Vector3(-58f, -1.5f, 2f), 12f, 12f, 14f, matDirt, false, false);

            // =========================================================================
            // 5. BUILD ALL 14 ARCHED VIADUCT BRIDGES & GRAND STAIRCASES CONNECTING EVERY ISLAND
            // =========================================================================

            // 5.1 Grand Cathedral Staircase Viaduct (Central Plaza <-> Cathedral Citadel)
            BuildArchedBridge(stairCathStart, stairCathEnd, 6.8f, 2);

            // 5.2 Central Plaza <-> Upper-Left High Cliff Island
            BuildArchedBridge(brPlazaToNW_A, brPlazaToNW_B, 4.8f, 2);

            // 5.3 Central Plaza <-> Mid-Left Giant Tree Island
            BuildArchedBridge(brPlazaToWest_A, brPlazaToWest_B, 5.0f, 1);

            // 5.4 Central Plaza <-> Mid-Right Graveyard Island
            BuildArchedBridge(brPlazaToEast_A, brPlazaToEast_B, 5.0f, 1);

            // 5.5 Central Plaza <-> South-East Foreground Avenue Island
            BuildArchedBridge(brPlazaToSE_A, brPlazaToSE_B, 6.4f, 1);

            // 5.6 Central Plaza <-> South-West Foreground Island
            BuildArchedBridge(brPlazaToSW_A, brPlazaToSW_B, 5.0f, 1);

            // 5.7 Mid-Left Giant Tree Island <-> Upper-Left High Cliff Island
            BuildArchedBridge(brWestToNW_A, brWestToNW_B, 4.6f, 1);

            // 5.8 Upper-Left High Cliff Island <-> Upper-Right Cathedral Citadel
            BuildArchedBridge(brNWToCath_A, brNWToCath_B, 4.6f, 1);

            // 5.9 Mid-Right Graveyard Island <-> Upper-Right Cathedral Citadel
            BuildArchedBridge(brEastToCath_A, brEastToCath_B, 4.8f, 2);

            // 5.10 Mid-Right Graveyard Island <-> South-East Foreground Avenue Island
            BuildArchedBridge(brEastToSE_A, brEastToSE_B, 4.6f, 1);

            // 5.11 South-West Foreground Island <-> South-East Foreground Avenue Island
            BuildArchedBridge(brSWToSE_A, brSWToSE_B, 5.0f, 1);

            // 5.12 Mid-Left Giant Tree Island <-> South-West Foreground Island
            BuildArchedBridge(brWestToSW_A, brWestToSW_B, 4.6f, 1);

            // 5.13 South-West Foreground Island <-> Lower-Left Shrine Crag
            BuildArchedBridge(brSWToCrag_A, brSWToCrag_B, 3.8f, 1);

            // 5.14 Foreground Stone Staircase descending into the Bottom Cloud Abyss
            BuildArchedBridge(fgStairStart, fgStairEnd, 5.4f, 1);

            // =========================================================================
            // 6. POPULATE DENSE GRAVEYARDS, TOMBSTONES, CROSSES, CROWS & CANDLES (Keeping Roads Clear!)
            // =========================================================================

            Transform[] zUpGraves = new Transform[] {
                grave01Part, grave02Part, grave03Part, grave04Part, grave05Part, grave06Part, grave08Part,
                cross1Part, cross2Part, cross3Part, cross4Part, tomb1Part, tomb3Part,
                tombLargePart, tombSmallPart, tombCrossPart, pillar1Part, pillar2Part
            };

            void PopulateGraveyard(Vector3 center, float rx, float rz, int count, bool addCandles = true)
            {
                int targetCount = Mathf.Max(2, Mathf.RoundToInt(count * densitySettings.tombstoneDensityMultiplier));
                int placed = 0;
                int attempts = 0;
                while (placed < targetCount && attempts < targetCount * 4)
                {
                    attempts++;
                    Vector3 pos = center + new Vector3(Random.Range(-rx, rx), 0.22f, Random.Range(-rz, rz));

                    // Never place gravestones on top of cobblestone roads or bridge gateways!
                    if (IsOnWalkwayOrGate(pos))
                        continue;

                    placed++;
                    float yaw = Random.Range(0f, 360f);
                    float tiltX = Random.Range(-4f, 4f);
                    float tiltZ = Random.Range(-4f, 4f);

                    Transform g = zUpGraves[Random.Range(0, zUpGraves.Length)];
                    if (g != null)
                    {
                        bool isModel5 = (g == tombLargePart || g == tombSmallPart || g == tombCrossPart);
                        bool isPillar = (g == pillar1Part || g == pillar2Part);
                        Vector3 sc = isModel5 ? Vector3.one * Random.Range(75f, 95f)
                                   : isPillar ? Vector3.one * Random.Range(0.45f, 0.65f)
                                   : Vector3.one * Random.Range(1.05f, 1.35f);

                        SpawnOnGround(g, graveyardsGroup.transform, pos, UprightZ(yaw, tiltX, tiltZ), sc, matTomb);
                        if (colSettings.enableAllColliders)
                        {
                            Vector3 colSize = (isPillar ? new Vector3(0.65f, 1.6f, 0.65f) : new Vector3(0.85f, 1.4f, 0.45f)) * colSettings.tombstoneColliderMultiplier;
                            AddBoxCollider(graveyardsGroup.transform, pos + new Vector3(0f, colSize.y * 0.5f, 0f), colSize, Quaternion.Euler(0f, yaw, 0f), "TombstoneCollider");
                        }
                    }

                    if (addCandles && (placed % 4 == 0))
                    {
                        SpawnLantern(pos + new Vector3(0.4f, 0.08f, 0.3f), center.y, lanternsGroup.transform, 3.6f, (placed % 12 == 0), atmSettings.lanternIntensity, atmSettings.lanternRange);
                    }

                    if (densitySettings.spawnCrows && crowPart != null && (placed % 9 == 0))
                    {
                        SpawnOnGround(crowPart, graveyardsGroup.transform, pos + new Vector3(0f, 1.2f, 0f), UprightZ(Random.Range(0f, 360f)), Vector3.one * 85f, matIronFence);
                    }
                }
            }

            PopulateGraveyard(new Vector3(-8.5f, 0.2f, -5.5f), 3.5f, 3.5f, 12);
            PopulateGraveyard(new Vector3(8.5f, 0.2f, -7.5f), 3.5f, 3.5f, 12);
            PopulateGraveyard(new Vector3(0.0f, 0.2f, 9.5f), 3.2f, 2.8f, 10);
            PopulateGraveyard(new Vector3(-38f, 1.2f, 5f), 11.5f, 10.5f, 44);
            PopulateGraveyard(new Vector3(-20f, 4.7f, 38f), 10.5f, 8.5f, 32);
            PopulateGraveyard(new Vector3(38f, 1.7f, 0f), 11.5f, 10.5f, 44);
            PopulateGraveyard(new Vector3(-28f, -1.8f, -28f), 11.5f, 9.5f, 38);
            PopulateGraveyard(new Vector3(12f, -1.8f, -34f), 11.5f, 9.5f, 38);
            PopulateGraveyard(new Vector3(12f, 8.7f, 42f), 6.0f, 8.5f, 24);
            PopulateGraveyard(new Vector3(41f, 8.7f, 42f), 5.5f, 8.5f, 24);

            // Stone Sarcophagi / Coffins (Placed beside roads)
            if (densitySettings.spawnCoffins && coffin1Part != null)
            {
                Vector3[] coffinCoords = new Vector3[] {
                    new Vector3(1.5f, -1.8f, -36f),
                    new Vector3(17f, -1.8f, -28f),
                    new Vector3(-36f, -1.8f, -24f),
                    new Vector3(43f, 1.7f, -6f),
                    new Vector3(-43f, 1.2f, -2f)
                };
                foreach (var cPos in coffinCoords)
                {
                    float cYaw = Random.Range(0f, 360f);
                    SpawnOnGround(coffin1Part, graveyardsGroup.transform, cPos, Quaternion.Euler(0f, cYaw, 0f), Vector3.one * 1.45f, matCathedralStone);
                    if (colSettings.enableAllColliders)
                        AddBoxCollider(graveyardsGroup.transform, cPos + new Vector3(0f, colSettings.coffinColliderSize.y * 0.5f, 0f), colSettings.coffinColliderSize, Quaternion.Euler(0f, cYaw, 0f), "CoffinCollider");
                }
            }

            // Subtle Haunted Spirits (Ghosts hovering over graveyard plots)
            if (densitySettings.spawnGhosts && ghostPart != null && matGhostGlow != null)
            {
                Vector3[] ghostPositions = new Vector3[] {
                    new Vector3(-36f, 2.0f, 9f),
                    new Vector3(40f, 2.5f, -4f),
                    new Vector3(-22f, -1.0f, -32f),
                    new Vector3(13f, 9.5f, 40f)
                };
                foreach (var gp in ghostPositions)
                {
                    SpawnOnGround(ghostPart, graveyardsGroup.transform, gp, UprightZ(Random.Range(0f, 360f)), Vector3.one * 75f, matGhostGlow);
                }
            }

            // =========================================================================
            // 7. GNARLED SPOOKY DEAD TREES FRAMING ALL CLIFF EDGES
            // =========================================================================

            SpawnGnarledTree(new Vector3(49f, 1.6f, -4f), 1.15f, true);    // Colossal right cliff tree
            SpawnGnarledTree(new Vector3(45f, 8.6f, 38f), 1.10f, true);    // Cathedral right cliff tree
            SpawnGnarledTree(new Vector3(-40f, -1.9f, -35f), 1.05f, true); // Lower-left cliff tree
            SpawnGnarledTree(new Vector3(-18f, -5.8f, -48f), 1.0f, true);  // Bottom foreground silhouette tree

            Vector3[] mediumTreeCoords = new Vector3[] {
                new Vector3(-27f, 1.1f, 15f),
                new Vector3(9f, 8.6f, 52f),
                new Vector3(-30f, 4.6f, 45f),
                new Vector3(24f, -1.9f, -42f),
                new Vector3(-48f, 1.1f, -3f),
                new Vector3(48f, 1.6f, 8f),
                new Vector3(-38f, -1.9f, -20f),
                new Vector3(28f, 1.6f, 9f),
                new Vector3(44f, 1.6f, -10f),
                new Vector3(-10f, 4.6f, 46f),
                new Vector3(18f, 8.6f, 54f),
                new Vector3(36f, 8.6f, 54f)
            };
            foreach (var mtPos in mediumTreeCoords)
            {
                if (!IsOnWalkwayOrGate(mtPos))
                    SpawnGnarledTree(mtPos, Random.Range(0.85f, 1.15f), false);
            }

            // =========================================================================
            // 8. ENDLESS VOLUMETRIC SEA OF CLOUDS & MIST IN THE ABYSS BELOW
            // =========================================================================

            // 8.1 Huge Dark Abyssal Floor (-24m)
            GameObject abyssFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            abyssFloor.name = "AbyssDarkFloor";
            abyssFloor.transform.SetParent(cloudsGroup.transform);
            abyssFloor.transform.position = new Vector3(0f, -24f, 10f);
            abyssFloor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            abyssFloor.transform.localScale = new Vector3(900f, 900f, 1f);
            Object.DestroyImmediate(abyssFloor.GetComponent<Collider>());
            if (matCloudAbyss != null) abyssFloor.GetComponent<MeshRenderer>().sharedMaterial = matCloudAbyss;

            // 8.2 Layer 1: Wide Abyssal Indigo Cloud Blanket covering entire horizon (-18m to -11m)
            for (int c = 0; c < 130; c++)
            {
                float x = Random.Range(-155f, 155f);
                float z = Random.Range(-95f, 125f);
                float y = Random.Range(-17.5f, -10.5f);

                GameObject cq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cq.name = "CloudDeep_" + c;
                cq.transform.SetParent(cloudsGroup.transform);
                cq.transform.position = new Vector3(x, y, z);
                cq.transform.rotation = Quaternion.Euler(66f + Random.Range(-8f, 8f), Random.Range(-20f, 20f), Random.Range(0f, 360f));
                float sc = Random.Range(48f, 88f);
                cq.transform.localScale = new Vector3(sc, sc * Random.Range(0.75f, 1.0f), 1f);
                Object.DestroyImmediate(cq.GetComponent<Collider>());
                if (matCloudDeep != null) cq.GetComponent<MeshRenderer>().sharedMaterial = matCloudDeep;
            }

            // 8.3 Layer 2: Billowing Moonlit Violet-Blue Clouds (-10.5m to -4.5m)
            for (int c = 0; c < 120; c++)
            {
                float x = Random.Range(-115f, 115f);
                float z = Random.Range(-75f, 95f);
                float y = Random.Range(-10.0f, -4.5f);

                GameObject cq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cq.name = "CloudMid_" + c;
                cq.transform.SetParent(cloudsGroup.transform);
                cq.transform.position = new Vector3(x, y, z);
                cq.transform.rotation = Quaternion.Euler(60f + Random.Range(-10f, 10f), Random.Range(-18f, 18f), Random.Range(0f, 360f));
                float sc = Random.Range(32f, 58f);
                cq.transform.localScale = new Vector3(sc, sc * Random.Range(0.7f, 1.0f), 1f);
                Object.DestroyImmediate(cq.GetComponent<Collider>());
                if (matCloudMid != null) cq.GetComponent<MeshRenderer>().sharedMaterial = matCloudMid;
            }

            // 8.4 Layer 3: Spectral Mist Tendrils wrapping around the cliffs, arches & foreground stairs (-4.8m to -1.5m)
            Vector3[] mistFocusCenters = new Vector3[] {
                new Vector3(-18f, -3.2f, 5f),
                new Vector3(18f, -3.0f, -3f),
                new Vector3(-10f, -3.5f, -19f),
                new Vector3(-2f, -4.8f, -42f),
                new Vector3(-22f, -5.2f, -44f),
                new Vector3(24f, -4.8f, -42f),
                new Vector3(-38f, -4.2f, -34f),
                new Vector3(42f, -4.0f, -24f),
                new Vector3(12f, 2.5f, 22f),
                new Vector3(-22f, 1.5f, 24f),
                new Vector3(-52f, -2.2f, -6f),
                new Vector3(52f, -2.2f, 12f),
                new Vector3(-56f, 2.0f, 26f),
                new Vector3(56f, 4.0f, 32f)
            };

            foreach (var focus in mistFocusCenters)
            {
                for (int m = 0; m < 8; m++)
                {
                    Vector3 mPos = focus + new Vector3(Random.Range(-13f, 13f), Random.Range(-1.8f, 1.6f), Random.Range(-13f, 13f));
                    GameObject mq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    mq.name = "MistTendril";
                    mq.transform.SetParent(cloudsGroup.transform);
                    mq.transform.position = mPos;
                    mq.transform.rotation = Quaternion.Euler(54f + Random.Range(-10f, 10f), Random.Range(-15f, 15f), Random.Range(0f, 360f));
                    float sc = Random.Range(22f, 38f);
                    mq.transform.localScale = new Vector3(sc, sc * 0.75f, 1f);
                    Object.DestroyImmediate(mq.GetComponent<Collider>());
                    if (matCloudHigh != null) mq.GetComponent<MeshRenderer>().sharedMaterial = matCloudHigh;
                }
            }

            // =========================================================================
            // 9. PITCH-DARK NIGHT SKY BACKDROP, CRATERED FULL MOON & GOD RAYS
            // =========================================================================

            Vector3 camPos = new Vector3(-9f, 35.5f, -68f);
            Vector3 lookTarget = new Vector3(1.5f, 3.0f, 1.5f);
            Quaternion camRot = Quaternion.LookRotation(lookTarget - camPos);

            // Giant Curved Night Sky Backdrop Dome behind the entire horizon (Guarantees deep midnight blue sky in SceneView & GameView!)
            GameObject skyWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyWall.name = "HorrorNightSkyBackdrop";
            skyWall.transform.SetParent(envGroup.transform);
            skyWall.transform.position = camPos + (lookTarget - camPos).normalized * 210f + new Vector3(0f, 45f, 0f);
            skyWall.transform.rotation = camRot;
            skyWall.transform.localScale = new Vector3(480f, 260f, 1f);
            Object.DestroyImmediate(skyWall.GetComponent<Collider>());
            if (matNightBackdrop != null) skyWall.GetComponent<MeshRenderer>().sharedMaterial = matNightBackdrop;

            // Place Cratered Full Moon comfortably inside Upper-Left of Camera Frustum
            Vector3 moonPos = new Vector3(-22f, 32.5f, 52f);
            GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "CrateredFullMoon";
            moon.transform.SetParent(envGroup.transform);
            moon.transform.position = moonPos;
            moon.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            moon.transform.localScale = Vector3.one * 12.5f;
            Object.DestroyImmediate(moon.GetComponent<Collider>());
            if (matMoonSurface != null) moon.GetComponent<MeshRenderer>().sharedMaterial = matMoonSurface;

            // Moon Outer Glow Halo
            GameObject moonHalo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            moonHalo.name = "MoonGlowHalo";
            moonHalo.transform.SetParent(envGroup.transform);
            moonHalo.transform.position = moonPos + (camPos - moonPos).normalized * -2.0f;
            moonHalo.transform.rotation = Quaternion.LookRotation(moonPos - camPos);
            moonHalo.transform.localScale = Vector3.one * 38f;
            Object.DestroyImmediate(moonHalo.GetComponent<Collider>());
            if (matMoonHalo != null) moonHalo.GetComponent<MeshRenderer>().sharedMaterial = matMoonHalo;

            // Diagonal Crepuscular Moon Rays (God Rays emanating from Moon center down-right!)
            Quaternion moonFaceRot = Quaternion.LookRotation(moonPos - camPos);
            for (int r = 0; r < 6; r++)
            {
                GameObject ray = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ray.name = "MoonGodRay_" + r;
                ray.transform.SetParent(envGroup.transform);
                float angleZ = 22f + r * 4.2f;
                float rayLen = 26f + r * 2.5f;
                Quaternion rayRot = moonFaceRot * Quaternion.Euler(0f, 0f, angleZ);
                ray.transform.rotation = rayRot;
                ray.transform.position = moonPos + (rayRot * Vector3.down) * (rayLen * 0.46f) + (camPos - moonPos).normalized * -1.0f;
                ray.transform.localScale = new Vector3(4.8f + r * 1.2f, rayLen, 1f);
                Object.DestroyImmediate(ray.GetComponent<Collider>());
                if (matMoonRay != null) ray.GetComponent<MeshRenderer>().sharedMaterial = matMoonRay;
            }

            // Dramatic Night Sky Clouds Framing the Moon & Upper Horizon
            Vector3[] skyCloudOffsets = new Vector3[] {
                moonPos + new Vector3(-15f, -3.5f, -2f),
                moonPos + new Vector3(12f, -2.5f, -2f),
                moonPos + new Vector3(-28f, -6.5f, 3f),
                moonPos + new Vector3(24f, 1.5f, 4f),
                new Vector3(38f, 34f, 58f),
                new Vector3(55f, 28f, 52f),
                new Vector3(-48f, 26f, 46f)
            };
            for (int sc = 0; sc < skyCloudOffsets.Length; sc++)
            {
                GameObject sq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sq.name = "NightSkyCloud_" + sc;
                sq.transform.SetParent(envGroup.transform);
                sq.transform.position = skyCloudOffsets[sc];
                sq.transform.rotation = Quaternion.LookRotation(skyCloudOffsets[sc] - camPos);
                sq.transform.localScale = new Vector3(30f, 11.5f, 1f);
                Object.DestroyImmediate(sq.GetComponent<Collider>());
                if (matCloudMid != null) sq.GetComponent<MeshRenderer>().sharedMaterial = matCloudMid;
            }

            // =========================================================================
            // 10. TRUE HORROR ATMOSPHERIC LIGHTING, SKYBOX & POST-PROCESSING
            // =========================================================================

            // Increase URP maxAdditionalLightsCount to 8 on active RenderPipelineAsset so point lights don't get culled!
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
            {
                urpAsset.maxAdditionalLightsCount = 8;
                EditorUtility.SetDirty(urpAsset);
            }

            // Directional Light (Cold Spectral Moonlight with crisp soft shadows)
            GameObject dirLightGo = GameObject.Find("Directional Light");
            if (dirLightGo == null) dirLightGo = new GameObject("Directional Light");
            dirLightGo.transform.SetParent(envGroup.transform);
            dirLightGo.transform.position = moonPos;
            dirLightGo.transform.rotation = Quaternion.Euler(36f, 132f, 0f);

            Light dirLight = dirLightGo.GetComponent<Light>();
            if (dirLight == null) dirLight = dirLightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = atmSettings.moonlightColor;
            dirLight.intensity = atmSettings.moonlightIntensity;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.88f;

            if (matSkybox != null)
                RenderSettings.skybox = matSkybox;

            // Deep Haunted Midnight Fog
            RenderSettings.fog = atmSettings.enableFog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = atmSettings.fogColor;
            RenderSettings.fogDensity = atmSettings.fogDensity;

            // Dark Gothic Ambient Lighting
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = atmSettings.ambientSkyColor;
            RenderSettings.ambientEquatorColor = atmSettings.ambientEquatorColor;
            RenderSettings.ambientGroundColor = atmSettings.ambientGroundColor;

            // Global Volume
            GameObject volGo = GameObject.Find("Global Volume");
            if (volGo == null) volGo = new GameObject("Global Volume");
            volGo.transform.SetParent(envGroup.transform);
            Volume vol = volGo.GetComponent<Volume>();
            if (vol == null) vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;

            VolumeProfile profile = vol.sharedProfile;
            if (profile != null)
            {
                if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
                bloom.active = true;
                bloom.threshold.Override(0.70f);
                bloom.intensity.Override(1.9f);
                bloom.scatter.Override(0.70f);

                if (!profile.TryGet<Vignette>(out var vignette)) vignette = profile.Add<Vignette>(true);
                vignette.active = true;
                vignette.color.Override(new Color(0.008f, 0.010f, 0.035f));
                vignette.intensity.Override(0.44f);
                vignette.smoothness.Override(0.45f);

                if (!profile.TryGet<ColorAdjustments>(out var colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
                colorAdj.active = true;
                colorAdj.postExposure.Override(0.18f);
                colorAdj.contrast.Override(28f);
                colorAdj.colorFilter.Override(new Color(0.86f, 0.90f, 1.0f));
                colorAdj.saturation.Override(15f);

                EditorUtility.SetDirty(profile);
            }

            // =========================================================================
            // 11. CAMERA & SCENEVIEW ALIGNMENT
            // =========================================================================

            GameObject camGo = GameObject.FindWithTag("MainCamera");
            if (camGo == null) camGo = GameObject.Find("Main Camera");
            if (camGo != null)
            {
                if (GameObject.Find("Player_ThirdPerson") == null)
                {
                    camGo.transform.position = camPos;
                    camGo.transform.rotation = camRot;
                }
                Camera cam = camGo.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.backgroundColor = new Color(0.008f, 0.012f, 0.032f);
                    cam.fieldOfView = 48f;
                    cam.nearClipPlane = 0.5f;
                    cam.farClipPlane = 600f;

                    var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    camData.renderPostProcessing = true;
                    camData.renderShadows = true;

                    if (SceneView.lastActiveSceneView != null)
                    {
                        var sv = SceneView.lastActiveSceneView;
                        sv.sceneViewState.showFog = true;
                        sv.sceneViewState.showSkybox = true;
                        sv.sceneViewState.showImageEffects = true;
                        float dist = Vector3.Distance(camPos, lookTarget);
                        sv.LookAtDirect(lookTarget, camRot, dist * 0.52f);
                        sv.Repaint();
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            File.WriteAllText("Logs/horror_build_result.txt", "SUCCESS: Horror Cemetery Map v8 built!");
            Debug.Log(">>> HORROR CEMETERY MAP v8 BUILT & SAVED SUCCESSFULLY! <<<");
        }
        catch (System.Exception ex)
        {
            File.WriteAllText("Logs/horror_build_result.txt", "ERROR: " + ex.ToString());
            Debug.LogError("HorrorCemeteryBuilder failed: " + ex);
        }
    }

    private static Transform FindChildDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var f = FindChildDeep(t.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    private static void EnsureHorrorMaterials(
        out Material matSkybox,
        out Material matNightBackdrop,
        out Material matMoonSurface,
        out Material matMoonHalo,
        out Material matMoonRay,
        out Material matCloudAbyss,
        out Material matCloudDeep,
        out Material matCloudMid,
        out Material matCloudHigh,
        out Material matWarmPool,
        out Material matFireGlow,
        out Material matGhostGlow,
        out Material matCliffDark,
        out Material matIronFence,
        out Material matCathedralStone)
    {
        string dir = "Assets/Asset/CemeteryPack/Materials";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        Texture2D cloudTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Tex_HorrorCloud.png");
        Texture2D moonTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Tex_CraterMoon.png");
        Texture2D rayTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Tex_MoonRay.png");
        Texture2D softDiscTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Tex_SoftCloud.png");

        // Procedural Night Sky Gradient & Starfield Texture (256x256)
        string skyTexPath = dir + "/Tex_NightSkyBackdrop.png";
        {
            int res = 256;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            for (int y = 0; y < res; y++)
            {
                float v = (float)y / res; // 0 = bottom horizon, 1 = top zenith
                Color topCol = new Color(0.015f, 0.022f, 0.055f, 1f);
                Color midCol = new Color(0.035f, 0.050f, 0.115f, 1f);
                Color botCol = new Color(0.065f, 0.090f, 0.195f, 1f);
                Color rowCol = (v > 0.45f)
                    ? Color.Lerp(midCol, topCol, (v - 0.45f) / 0.55f)
                    : Color.Lerp(botCol, midCol, v / 0.45f);

                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / res;
                    // Subtle nebula cloudiness
                    float neb = Mathf.PerlinNoise(u * 5f + 2.1f, v * 4f + 1.3f) * 0.025f * (1f - v * 0.5f);
                    Color c = rowCol + new Color(neb * 0.6f, neb * 0.8f, neb * 1.4f, 0f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(skyTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(skyTexPath);
        }
        Texture2D skyBackdropTex = AssetDatabase.LoadAssetAtPath<Texture2D>(skyTexPath);

        Shader spriteShader = Shader.Find("Sprites/Default");
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        Shader skyShader = Shader.Find("Skybox/Procedural");

        // Skybox Material
        matSkybox = CreateOrUpdateMat(dir + "/Mat_HorrorSkybox.mat", skyShader);
        if (matSkybox != null)
        {
            matSkybox.SetInt("_SunDisk", 0);
            matSkybox.SetFloat("_AtmosphereThickness", 0.15f);
            matSkybox.SetColor("_SkyTint", new Color(0.015f, 0.025f, 0.065f));
            matSkybox.SetColor("_GroundColor", new Color(0.005f, 0.008f, 0.020f));
            matSkybox.SetFloat("_Exposure", 0.25f);
            EditorUtility.SetDirty(matSkybox);
        }

        // Night Backdrop Material
        matNightBackdrop = CreateOrUpdateMat(dir + "/Mat_NightBackdrop.mat", unlitShader);
        if (matNightBackdrop != null)
        {
            matNightBackdrop.mainTexture = skyBackdropTex;
            matNightBackdrop.SetTexture("_BaseMap", skyBackdropTex);
            matNightBackdrop.color = Color.white;
            matNightBackdrop.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(matNightBackdrop);
        }

        // Moon Surface Material
        matMoonSurface = CreateOrUpdateMat(dir + "/Mat_CraterMoon.mat", unlitShader);
        if (matMoonSurface != null)
        {
            matMoonSurface.mainTexture = moonTex;
            matMoonSurface.SetTexture("_BaseMap", moonTex);
            matMoonSurface.color = new Color(0.90f, 0.95f, 1.0f) * 2.5f;
            matMoonSurface.SetColor("_BaseColor", new Color(0.90f, 0.95f, 1.0f) * 2.5f);
            EditorUtility.SetDirty(matMoonSurface);
        }

        // Moon Halo Material
        matMoonHalo = CreateOrUpdateMat(dir + "/Mat_MoonHalo.mat", spriteShader);
        if (matMoonHalo != null)
        {
            matMoonHalo.mainTexture = cloudTex;
            matMoonHalo.color = new Color(0.50f, 0.68f, 1.0f, 0.48f);
            EditorUtility.SetDirty(matMoonHalo);
        }

        // Moon Ray Material
        matMoonRay = CreateOrUpdateMat(dir + "/Mat_MoonRay.mat", spriteShader);
        if (matMoonRay != null)
        {
            matMoonRay.mainTexture = rayTex;
            matMoonRay.color = new Color(0.65f, 0.80f, 1.0f, 0.52f);
            EditorUtility.SetDirty(matMoonRay);
        }

        // Solid Abyss Floor Material (Unlit deep midnight indigo)
        matCloudAbyss = CreateOrUpdateMat(dir + "/Mat_CloudAbyss.mat", unlitShader);
        if (matCloudAbyss != null)
        {
            Color abCol = new Color(0.025f, 0.036f, 0.082f, 1f);
            matCloudAbyss.color = abCol;
            matCloudAbyss.SetColor("_BaseColor", abCol);
            EditorUtility.SetDirty(matCloudAbyss);
        }

        // 3 Cloud Layer Materials
        matCloudDeep = CreateOrUpdateMat(dir + "/Mat_CloudDeep.mat", spriteShader);
        if (matCloudDeep != null)
        {
            matCloudDeep.mainTexture = cloudTex;
            matCloudDeep.color = new Color(0.11f, 0.16f, 0.34f, 0.92f);
            EditorUtility.SetDirty(matCloudDeep);
        }

        matCloudMid = CreateOrUpdateMat(dir + "/Mat_CloudMid.mat", spriteShader);
        if (matCloudMid != null)
        {
            matCloudMid.mainTexture = cloudTex;
            matCloudMid.color = new Color(0.24f, 0.33f, 0.60f, 0.72f);
            EditorUtility.SetDirty(matCloudMid);
        }

        matCloudHigh = CreateOrUpdateMat(dir + "/Mat_CloudHigh.mat", spriteShader);
        if (matCloudHigh != null)
        {
            matCloudHigh.mainTexture = cloudTex;
            matCloudHigh.color = new Color(0.44f, 0.56f, 0.88f, 0.52f);
            EditorUtility.SetDirty(matCloudHigh);
        }

        // Warm Golden Lantern Light Pool Material
        matWarmPool = CreateOrUpdateMat(dir + "/Mat_WarmLightPool.mat", spriteShader);
        if (matWarmPool != null)
        {
            matWarmPool.mainTexture = (softDiscTex != null) ? softDiscTex : cloudTex;
            matWarmPool.color = new Color(1.0f, 0.62f, 0.16f, 0.52f);
            EditorUtility.SetDirty(matWarmPool);
        }

        // Ritual Fire & Portal Glow Material
        matFireGlow = CreateOrUpdateMat(dir + "/Mat_RitualFireGlow.mat", unlitShader);
        if (matFireGlow != null)
        {
            matFireGlow.color = new Color(1.0f, 0.55f, 0.14f) * 4.2f;
            matFireGlow.SetColor("_BaseColor", new Color(1.0f, 0.55f, 0.14f) * 4.2f);
            EditorUtility.SetDirty(matFireGlow);
        }

        // Ghost Glow Material
        matGhostGlow = CreateOrUpdateMat(dir + "/Mat_GhostGlow.mat", spriteShader);
        if (matGhostGlow != null)
        {
            matGhostGlow.color = new Color(0.45f, 0.78f, 1.0f, 0.45f);
            EditorUtility.SetDirty(matGhostGlow);
        }

        // Dark Basalt Cliff Material
        var dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/CemeteryPack/Model5_HalloweenGraveyard/textures/tile_dirt_BaseColor.png");
        var dirtNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/CemeteryPack/Model5_HalloweenGraveyard/textures/tile_dirt_Normal.png");
        matCliffDark = CreateOrUpdateMat(dir + "/Mat_CliffRockDark.mat", litShader);
        if (matCliffDark != null)
        {
            if (dirtTex != null) matCliffDark.SetTexture("_BaseMap", dirtTex);
            if (dirtNorm != null) matCliffDark.SetTexture("_BumpMap", dirtNorm);
            matCliffDark.SetColor("_BaseColor", new Color(0.14f, 0.16f, 0.23f, 1f));
            matCliffDark.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(matCliffDark);
        }

        // Dark Wrought-Iron Fence Material
        matIronFence = CreateOrUpdateMat(dir + "/Mat_GothicIronFence.mat", litShader);
        if (matIronFence != null)
        {
            matIronFence.SetColor("_BaseColor", new Color(0.08f, 0.09f, 0.13f, 1f));
            matIronFence.SetFloat("_Metallic", 0.80f);
            matIronFence.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(matIronFence);
        }

        // Weathered Gothic Cathedral Limestone Material
        var paverTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/CemeteryPack/Model5_HalloweenGraveyard/textures/tile_pavers_BaseColor.png");
        var paverNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Asset/CemeteryPack/Model5_HalloweenGraveyard/textures/tile_pavers_Normal.png");
        matCathedralStone = CreateOrUpdateMat(dir + "/Mat_CathedralStone.mat", litShader);
        if (matCathedralStone != null)
        {
            if (paverTex != null) matCathedralStone.SetTexture("_BaseMap", paverTex);
            if (paverNorm != null) matCathedralStone.SetTexture("_BumpMap", paverNorm);
            matCathedralStone.SetColor("_BaseColor", new Color(0.64f, 0.68f, 0.80f, 1f));
            matCathedralStone.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(matCathedralStone);
        }
    }

    private static Material CreateOrUpdateMat(string path, Shader shader)
    {
        if (shader == null) return null;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }
        return mat;
    }
}
