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
        EditorApplication.delayCall += DumpLogs;
    }

    [MenuItem("Tools/Dump Logs")]
    public static void DumpLogs()
    {
        try
        {
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            if (logEntriesType != null)
            {
                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
                
                int count = (int)getCountMethod.Invoke(null, null);
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");
                var logEntry = Activator.CreateInstance(logEntryType);
                
                var fields = logEntryType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo messageField = null;
                FieldInfo modeField = null;
                foreach (var f in fields)
                {
                    if (f.Name == "message" || f.Name == "condition") messageField = f;
                    if (f.Name == "mode") modeField = f;
                }

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
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText("Logs/console_dump_err.txt", ex.ToString());
        }
    }
}
