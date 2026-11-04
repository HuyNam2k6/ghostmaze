using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

[InitializeOnLoad]
public static class AssetInspector
{
    static AssetInspector()
    {
        EditorApplication.delayCall += InspectAssets;
    }

    [MenuItem("Tools/Inspect Assets")]
    public static void InspectAssets()
    {
        var sb = new StringBuilder();
        string[] assetPaths = new string[]
        {
            "Assets/Asset/CemeteryPack/Model1_FantasyHalloween/source/Demo_Scene.fbx",
            "Assets/Asset/CemeteryPack/Model2_ModularCemetery/source/Modular Cemetary (Sketchfab).fbx",
            "Assets/Asset/CemeteryPack/Model3_HauntedCemetery/source/rts_props.blend",
            "Assets/Asset/CemeteryPack/Model4_GravestonesPack/source/GravestonesAssets.blend",
            "Assets/Asset/CemeteryPack/Model5_HalloweenGraveyard/source/halloween_tombstones_low.fbx"
        };

        foreach (var path in assetPaths)
        {
            sb.AppendLine("==================================================");
            sb.AppendLine($"ASSET: {path}");
            var mainObj = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            if (mainObj == null)
            {
                sb.AppendLine("  -> FAILED TO LOAD MAIN ASSET!");
                continue;
            }

            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            sb.AppendLine($"  Sub-assets count: {allSubAssets.Length}");
            foreach (var sub in allSubAssets)
            {
                if (sub is Mesh mesh)
                {
                    sb.AppendLine($"  - Mesh: '{mesh.name}' (vertices: {mesh.vertexCount}, bounds: {mesh.bounds.size})");
                }
                else if (sub is Material mat)
                {
                    sb.AppendLine($"  - Material: '{mat.name}' (shader: {mat.shader.name})");
                }
            }

            sb.AppendLine("  Hierarchy:");
            PrintHierarchy(mainObj.transform, "    ", sb);
        }

        File.WriteAllText("Logs/cemetery_assets_report.txt", sb.ToString());
        Debug.Log("Finished inspecting assets -> Logs/cemetery_assets_report.txt");
    }

    private static void PrintHierarchy(Transform t, string indent, StringBuilder sb)
    {
        sb.AppendLine($"{indent}- {t.name} (pos: {t.localPosition}, rot: {t.localEulerAngles}, scale: {t.localScale})");
        for (int i = 0; i < t.childCount; i++)
        {
            PrintHierarchy(t.GetChild(i), indent + "  ", sb);
        }
    }
}

