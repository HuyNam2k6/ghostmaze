#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ngăn Unity biên dịch lại script NGAY TRONG LÚC đang Play.
/// Nếu recompile giữa lúc Play, Unity sẽ domain-reload: mọi biến không serialize (Dictionary, NetworkRunner, ...) bị mất,
/// gây ra lỗi "Collection was modified", "The referenced script is missing", NullReference trong OnGUI...
/// Script này đặt Preferences > General > Script Changes While Playing = "Recompile After Finished Playing".
/// </summary>
[InitializeOnLoad]
public static class PlayModeRecompileGuard
{
    // Giá trị của Unity: 0 = Recompile And Continue Playing, 1 = Recompile After Finished Playing, 2 = Stop Playing And Recompile
    private const string PrefKey = "ScriptCompilationDuringPlay";
    private const int RecompileAfterFinishedPlaying = 1;

    static PlayModeRecompileGuard()
    {
        if (EditorPrefs.GetInt(PrefKey, 0) != RecompileAfterFinishedPlaying)
        {
            EditorPrefs.SetInt(PrefKey, RecompileAfterFinishedPlaying);
            Debug.Log("<color=#2ECC71>[GhostMaze] Đã đặt 'Script Changes While Playing' = Recompile After Finished Playing để tránh lỗi khi sửa code lúc đang Play.</color>");
        }
    }
}
#endif

