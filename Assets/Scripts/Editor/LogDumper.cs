using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Reflection;

[InitializeOnLoad]
public static class LogDumper
{
    static LogDumper()
    {
        EditorApplication.delayCall += CleanAndDumpLogs;
    }

    [MenuItem("Tools/Dump Logs")]
    public static void DumpLogs()
    {
        CleanAndDumpLogs();
    }

    public static void CleanAndDumpLogs()
    {
        try
        {
            // Unity 6: LogEntries/LogEntry nằm trong UnityEditor.CoreModule, thử tên cũ để tương thích ngược
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.CoreModule")
                ?? Type.GetType("UnityEditor.LogEntries, UnityEditor");
            if (logEntriesType != null)
            {
                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
                var clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);

                if (getCountMethod == null || getEntryInternalMethod == null)
                {
                    File.WriteAllText("Logs/console_dump_err.txt",
                        $"LogDumper: không tìm thấy method trên {logEntriesType.FullName}");
                    return;
                }

                int count = (int)getCountMethod.Invoke(null, null);
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor.CoreModule")
                    ?? Type.GetType("UnityEditor.LogEntry, UnityEditor");
                if (logEntryType == null)
                {
                    File.WriteAllText("Logs/console_dump_err.txt", "LogDumper: không tìm thấy UnityEditor.LogEntry");
                    return;
                }
                var logEntry = Activator.CreateInstance(logEntryType);

                var fields = logEntryType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo messageField = null;
                FieldInfo modeField = null;
                foreach (var f in fields)
                {
                    if (f.Name == "message" || f.Name == "condition") messageField = f;
                    if (f.Name == "mode") modeField = f;
                }

                if (messageField == null)
                {
                    File.WriteAllText("Logs/console_dump_err.txt",
                        $"LogDumper: LogEntry không có field message/condition ({fields.Length} field)");
                    return;
                }

                if (!Directory.Exists("Logs")) Directory.CreateDirectory("Logs");
                using (var sw = new StreamWriter("Logs/console_dump.txt", false))
                {
                    sw.WriteLine($"Total console logs: {count}");
                    for (int i = 0; i < count; i++)
                    {
                        getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });
                        string message = messageField?.GetValue(logEntry) as string ?? "";
                        int mode = modeField != null ? (int)modeField.GetValue(logEntry) : 0;
                        sw.WriteLine($"[{mode}] {message.Replace("\n", " | ")}");
                    }
                }

                bool hasPackageManagerSingletonNoise = false;
                bool hasRealWarningsOrErrors = false;

                for (int i = 0; i < count; i++)
                {
                    getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });
                    string message = messageField?.GetValue(logEntry) as string ?? "";
                    int mode = modeField != null ? (int)modeField.GetValue(logEntry) : 0;
                    if (message.Contains("ScriptableSingleton already exists"))
                    {
                        hasPackageManagerSingletonNoise = true;
                    }
                    else if ((mode & (1 | 2 | 4)) != 0 || message.Contains("error CS") || message.Contains("warning CS"))
                    {
                        hasRealWarningsOrErrors = true;
                    }
                }

                // Chỉ tự động dọn rác ScriptableSingleton nếu không có cảnh báo hay lỗi thật
                if (hasPackageManagerSingletonNoise && !hasRealWarningsOrErrors && clearMethod != null)
                {
                    clearMethod.Invoke(null, null);
                }
            }
        }
        catch (Exception ex)
        {
            if (!Directory.Exists("Logs")) Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/console_dump_err.txt", ex.ToString());
        }
    }
}
