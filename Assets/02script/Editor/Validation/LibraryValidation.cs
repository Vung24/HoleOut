using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectMaintenance
{
    public static class LibraryValidation
    {
        [Serializable]
        public class Result
        {
            public int serializedScriptReferences;
            public int uniqueScriptTypes;
            public string[] missingScriptReferences;
            public string[] unresolvedScriptTypes;
            public int remainingPlaceholders;
        }

        [MenuItem("Tools/Project/Validate Script Libraries")]
        public static void Run()
        {
            var missing = new List<string>();
            var unresolved = new List<string>();
            var seen = new HashSet<string>();
            var pattern = new Regex(@"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}");
            int references = 0;
            foreach (string file in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (ext != ".unity" && ext != ".prefab" && ext != ".asset") continue;
                foreach (Match match in pattern.Matches(File.ReadAllText(file)))
                {
                    references++;
                    string guid = match.Groups[1].Value;
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath) || (assetPath.StartsWith("Assets/", StringComparison.Ordinal) && !File.Exists(assetPath))) missing.Add(file + ": " + guid);
                    else if (seen.Add(guid))
                    {
                        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                        if (script == null || script.GetClass() == null) unresolved.Add(assetPath);
                    }
                }
            }
            int placeholders = 0;
            foreach (string file in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
                if (Regex.IsMatch(File.ReadAllText(file), @"(?m)^\s*// Recovered placeholder:")) placeholders++;
            var result = new Result {
                serializedScriptReferences = references,
                uniqueScriptTypes = seen.Count,
                missingScriptReferences = missing.ToArray(),
                unresolvedScriptTypes = unresolved.ToArray(),
                remainingPlaceholders = placeholders
            };
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/script-library-validation.json", JsonUtility.ToJson(result, true));
            Debug.Log("Library validation: " + references + " references, " + seen.Count + " types, " + missing.Count + " missing, " + unresolved.Count + " unresolved.");
            if (Application.isBatchMode) EditorApplication.Exit(missing.Count == 0 && unresolved.Count == 0 ? 0 : 1);
        }
    }
}

