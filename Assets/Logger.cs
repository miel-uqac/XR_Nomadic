using System;
using System.IO;
using UnityEngine;

public static class Logger
{
    // ✅ Works on both Android/Quest and PC
    private static readonly string basePath = Application.persistentDataPath;

    public static void LogLine(string filename, string header, string line, bool showInConsole = false)
    {
        string path = Path.Combine(basePath, filename);

        if (!File.Exists(path))
            File.WriteAllText(path, header + "\n");

        File.AppendAllText(path, line + "\n");

        if (showInConsole)
            Debug.Log($"[📄 Logged to {path}]\n{line}");
    }

    public static void LogBlock(string label, string content)
    {
        Debug.Log($"[📋 {label} Log]\n{content}");
    }
}
