using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GhostMaze.Editor
{
    /// <summary>
    /// Tự động kiểm tra và điền App ID Photon Fusion vào file cấu hình PhotonAppSettings ngay khi gói SDK được nhập vào Unity.
    /// </summary>
    [InitializeOnLoad]
    public static class PhotonFusionAutoSetup
    {
        private const string FUSION_APP_ID = "438b0f59-e83e-495b-9660-9bc8f0510c60";

        static PhotonFusionAutoSetup()
        {
            EditorApplication.delayCall += TryConfigurePhotonAppId;
        }

        [MenuItem("Tools/GhostMaze/Cấu hình Photon Fusion App ID")]
        public static void ForceConfigure()
        {
            TryConfigurePhotonAppId(true);
        }

        public static void TryConfigurePhotonAppId()
        {
            TryConfigurePhotonAppId(false);
        }

        private static void TryConfigurePhotonAppId(bool logIfNotFound)
        {
            try
            {
                // 1. Cấu hình PhotonAppSettings.asset (Photon Fusion / Realtime)
                string[] guids = AssetDatabase.FindAssets("PhotonAppSettings");
                if (guids != null && guids.Length > 0)
                {
                    foreach (string guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyAppIdToYamlFile(path);
                        }
                    }
                }

                // 2. Cấu hình PhotonServerSettings.asset (PUN 2 / Photon Voice 2)
                string[] punGuids = AssetDatabase.FindAssets("PhotonServerSettings");
                if (punGuids != null && punGuids.Length > 0)
                {
                    foreach (string guid in punGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyAppIdToPunServerSettings(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhotonFusionAutoSetup] Lỗi khi cấu hình Photon App ID: {ex.Message}");
            }
        }

        private static void ApplyAppIdToPunServerSettings(string assetPath)
        {
            if (!File.Exists(assetPath)) return;

            string[] lines = File.ReadAllLines(assetPath);
            bool updated = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains("AppIdRealtime:") || line.Contains("AppIdVoice:") || line.Contains("AppIdChat:"))
                {
                    int colonIndex = line.IndexOf(":", StringComparison.Ordinal);
                    string key = line.Substring(0, colonIndex).Trim();
                    string currentVal = line.Substring(colonIndex + 1).Trim();
                    if (currentVal != FUSION_APP_ID)
                    {
                        string indent = line.Substring(0, line.IndexOf(key, StringComparison.Ordinal));
                        lines[i] = $"{indent}{key}: {FUSION_APP_ID}";
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                File.WriteAllLines(assetPath, lines);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"<color=#00FF88>[PhotonAutoSetup] ĐÃ TỰ ĐỘNG CẤU HÌNH THÀNH CÔNG App ID: {FUSION_APP_ID} vào {assetPath}</color>");
            }
        }

        private static void ApplyAppIdToYamlFile(string assetPath)
        {
            if (!File.Exists(assetPath)) return;

            string[] lines = File.ReadAllLines(assetPath);
            bool updated = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains("AppIdFusion:") || line.Contains("AppIdChat:") || line.Contains("AppIdVoice:"))
                {
                    int colonIndex = line.IndexOf(":", StringComparison.Ordinal);
                    string key = line.Substring(0, colonIndex).Trim();
                    string currentVal = line.Substring(colonIndex + 1).Trim();
                    if (currentVal != FUSION_APP_ID)
                    {
                        string indent = line.Substring(0, line.IndexOf(key, StringComparison.Ordinal));
                        lines[i] = $"{indent}{key}: {FUSION_APP_ID}";
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                File.WriteAllLines(assetPath, lines);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"<color=#00FF88>[PhotonFusionAutoSetup] ĐÃ TỰ ĐỘNG CẤU HÌNH THÀNH CÔNG AppIdFusion: {FUSION_APP_ID} vào {assetPath}</color>");
            }
        }
    }

    /// <summary>
    /// Lắng nghe sự kiện thêm tệp mới vào dự án để bắt ngay file PhotonAppSettings khi gói SDK hoàn tất giải nén.
    /// </summary>
    public class PhotonImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null) return;

            foreach (string asset in importedAssets)
            {
                if (asset.Contains("PhotonAppSettings") && asset.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    PhotonFusionAutoSetup.TryConfigurePhotonAppId();
                    break;
                }
            }
        }
    }
}

