using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MultiplayerTestLauncher
{
    private const string BUILD_DIR = "Builds/GhostMaze_Test";
    private const string BUILD_EXE = "Builds/GhostMaze_Test/GhostMaze.exe";

    [MenuItem("GhostMaze/🚀 Mở 3 Cửa Sổ Test", false, 10)]
    public static void Launch3Clients()
    {
        if (!EnsureBuildExists()) return;
        LaunchClients(3);
    }

    private static bool EnsureBuildExists()
    {
        if (!File.Exists(BUILD_EXE))
        {
            bool ok = EditorUtility.DisplayDialog(
                "Chưa Có Bản Build Test",
                "Chưa tìm thấy file GhostMaze.exe để test.\nBạn có muốn tự động Build ngay bây giờ không?\n(Mất khoảng 30 giây - 1 phút)",
                "Build Ngay",
                "Hủy"
            );
            if (!ok) return false;
            return BuildStandaloneTestPlayer();
        }
        else
        {
            int opt = EditorUtility.DisplayDialogComplex(
                "Khởi Chạy Test Multiplayer",
                "Bạn muốn cập nhật code mới hay mở ngay bản đang có?\n(Vừa sửa code thu nhỏ người chơi thì nên chọn 'Build Lại & Mở' để 3 cửa sổ cập nhật code mới)",
                "Build Lại & Mở (~30s)",
                "Hủy",
                "Mở Luôn Bản Cũ"
            );
            if (opt == 0) // Build Lại & Mở
            {
                return BuildStandaloneTestPlayer();
            }
            else if (opt == 2) // Mở Luôn Bản Cũ
            {
                return true;
            }
            return false; // Hủy
        }
    }

    private static bool BuildStandaloneTestPlayer()
    {
        if (!Directory.Exists(BUILD_DIR))
        {
            Directory.CreateDirectory(BUILD_DIR);
        }

        string[] scenes = new string[]
        {
            "Assets/Scenes/LobbyScene.unity",
            "Assets/Scenes/SampleScene.unity"
        };

        BuildPlayerOptions buildOpts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = BUILD_EXE,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        UnityEngine.Debug.Log("<color=#00FFAA>[GhostMaze Test] Đang tiến hành Build bản test Windows Standalone 64-bit...</color>");
        BuildReport report = BuildPipeline.BuildPlayer(buildOpts);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log($"<color=#00FFAA>[GhostMaze Test] Build hoàn tất thành công ({summary.totalTime.TotalSeconds:F1}s)!</color>");
            return true;
        }
        else if (summary.result == BuildResult.Cancelled)
        {
            UnityEngine.Debug.LogWarning("[GhostMaze Test] Bạn đã bấm Hủy (Cancel) tiến trình Build.");
            return false;
        }
        else
        {
            UnityEngine.Debug.LogError($"<color=#FF4444>[GhostMaze Test] Build gặp lỗi! Kết quả: {summary.result}, Số lỗi: {summary.totalErrors}</color>");
            EditorUtility.DisplayDialog("Build Gặp Lỗi", $"Quá trình build gặp lỗi: {summary.result}. Vui lòng kiểm tra Console.", "Đóng");
            return false;
        }
    }

    private static void LaunchClients(int count)
    {
        string fullExePath = Path.GetFullPath(BUILD_EXE);
        if (!File.Exists(fullExePath))
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy file: " + fullExePath, "OK");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = fullExePath,
                Arguments = "-screen-fullscreen 0 -screen-width 854 -screen-height 480 -popupwindow",
                WorkingDirectory = Path.GetDirectoryName(fullExePath)
            };

            try
            {
                Process.Start(psi);
                System.Threading.Thread.Sleep(300);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[GhostMaze Test] Không thể khởi chạy Client #{i + 1}: {ex.Message}");
            }
        }

        UnityEngine.Debug.Log($"<color=#00FFAA>[GhostMaze Test] Đã khởi chạy thành công {count} cửa sổ game test song song!</color>");
    }
}
