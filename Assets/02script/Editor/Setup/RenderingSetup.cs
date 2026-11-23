using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectMaintenance
{
    /// <summary>
    /// Assigns a URP asset and points materials that use placeholder shaders at the real
    /// package shaders with the same name (URP, TextMeshPro, ...).
    /// </summary>
    public static class RenderingSetup
    {
        private const string ShaderFolder = "Assets/04art/Shader/";
        private const string Marker = "DummyShaderTextExporter";

        [MenuItem("Hole Sort/Fix Rendering")]
        public static void Run()
        {
            var log = new List<string>();
            AssignPipeline(log);

            // Real shaders that live outside the placeholder folder, by name.
            var real = new Dictionary<string, Shader>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(ShaderFolder)) continue;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader && !real.ContainsKey(shader.name)) real[shader.name] = shader;
            }

            var placeholders = new HashSet<string>();
            int remapped = 0;
            var unresolved = new SortedDictionary<string, int>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat || !mat.shader) continue;
                var shaderPath = AssetDatabase.GetAssetPath(mat.shader);
                if (!IsPlaceholder(shaderPath)) continue;
                if (real.TryGetValue(mat.shader.name, out var target))
                {
                    placeholders.Add(shaderPath);
                    int queue = mat.renderQueue;
                    mat.shader = target;
                    mat.renderQueue = queue;
                    EditorUtility.SetDirty(mat);
                    remapped++;
                }
                else
                {
                    unresolved.TryGetValue(mat.shader.name, out int n);
                    unresolved[mat.shader.name] = n + 1;
                }
            }
            AssetDatabase.SaveAssets();
            log.Add("Materials moved to package shaders: " + remapped);

            // Placeholder copies of package shaders are no longer used and clash by name.
            foreach (var path in placeholders)
                if (AssetDatabase.DeleteAsset(path)) log.Add("Removed placeholder " + Path.GetFileName(path));
            foreach (var pair in unresolved) log.Add($"Still placeholder: {pair.Key} ({pair.Value} materials)");
            AssetDatabase.Refresh();
            Debug.Log("Rendering setup:\n- " + string.Join("\n- ", log));
        }

        private static bool IsPlaceholder(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(ShaderFolder) || !path.EndsWith(".shader")) return false;
            return File.ReadAllText(path).Contains(Marker);
        }

        // Imported pipeline assets can be empty shells without a renderer; those render nothing.
        private static bool HasRenderer(UniversalRenderPipelineAsset asset)
        {
            if (!asset) return false;
            var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return false;
            return list.GetArrayElementAtIndex(0).objectReferenceValue != null;
        }

        private static void AssignPipeline(List<string> log)
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (!HasRenderer(asset)) asset = null;
            if (!asset)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var candidate = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                    if (path.StartsWith("Assets/") && HasRenderer(candidate)) { asset = candidate; break; }
                }
            }
            if (!asset)
            {
                const string folder = "Assets/Settings";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Settings");
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, folder + "/URP_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(renderer);
                asset.supportsHDR = false;
                asset.msaaSampleCount = 4;
                asset.shadowDistance = 30f;
                AssetDatabase.CreateAsset(asset, folder + "/URP_Pipeline.asset");
                AssetDatabase.SaveAssets();
                log.Add("Created " + folder + "/URP_Pipeline.asset");
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            log.Add("Render pipeline = " + asset.name);
        }
    }
}
