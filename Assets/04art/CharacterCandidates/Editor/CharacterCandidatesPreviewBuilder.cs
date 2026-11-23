using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HoleSortJam.CharacterCandidates.Editor
{
    public static class CharacterCandidatesPreviewBuilder
    {
        private const string MenuPath = "Tools/Character Candidates/Build Preview Scene";

        private const string RootFolder = "Assets/04art/CharacterCandidates";
        private const string MeshFolder = RootFolder + "/Mesh";
        private const string AnimMapFolder = RootFolder + "/AnimMap";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string ShaderPath = RootFolder + "/Shaders/AnimMapPreview.shader";
        private const string ScenePath = RootFolder + "/CharacterPreview.unity";

        private const string AnimMapShaderName = "CharacterCandidates/AnimMapPreview";
        private const string StaticMaterialPath = MaterialsFolder + "/Mat_StaticCandidate.mat";
        private const string FloorMaterialPath = MaterialsFolder + "/Mat_PreviewFloor.mat";
        private const string PrimaryReferenceMeshName = "Cube";

        private const float AnimatedRowZ = 0f;
        private const float StaticRowZ = 5.5f;
        private const float SlotSpacing = 3.2f;
        private const float LabelOffsetZ = -1.95f;
        private const float LabelHeightY = 0.08f;
        private const float OversizedThresholdRatio = 5f;
        private const float DefaultAnimSpeed = 1f;

        private static readonly string[] PlatformOverrideNames = { "Standalone", "Android", "iPhone", "WebGL" };
        private static readonly Color DefaultAnimColor = new Color(0.86f, 0.88f, 0.92f, 1f);
        private static readonly Color DefaultStaticColor = new Color(0.76f, 0.80f, 0.85f, 1f);
        private static readonly Color FloorColor = new Color(0.20f, 0.22f, 0.26f, 1f);
        private static readonly Color CameraBackgroundColor = new Color(0.13f, 0.15f, 0.19f, 1f);

        private sealed class MeshCandidate
        {
            public Mesh Mesh;
            public string AssetPath;
            public string DisplayName;
        }

        private sealed class AnimatedCandidate
        {
            public string DisplayName;
            public Texture2D Texture;
            public MeshCandidate MeshCandidate;
            public Material Material;
            public int UsedColumnCount;
        }

        [MenuItem(MenuPath)]
        public static void BuildPreviewScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            List<string> animMapPaths = FindSortedAssetPaths("t:Texture2D", AnimMapFolder);
            int updatedImporters = ConfigureAnimMapImporters(animMapPaths);

            Shader animShader = LoadAnimMapShader();
            if (animShader == null)
            {
                Debug.LogError($"[CharacterCandidates] Khong tim thay shader '{AnimMapShaderName}' tai '{ShaderPath}'.");
                return;
            }

            EnsureFolderExists(MaterialsFolder);

            List<MeshCandidate> allMeshes = LoadAllMeshes();
            if (allMeshes.Count == 0)
            {
                Debug.LogWarning($"[CharacterCandidates] Khong tim thay mesh nao trong '{MeshFolder}'.");
            }

            Dictionary<Mesh, Vector3[]> vertexCache = new Dictionary<Mesh, Vector3[]>();
            List<AnimatedCandidate> animatedCandidates = new List<AnimatedCandidate>(animMapPaths.Count);
            int skippedAnimCount = 0;

            for (int i = 0; i < animMapPaths.Count; i++)
            {
                string animPath = animMapPaths[i];
                Texture2D animTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(animPath);
                if (animTexture == null)
                {
                    Debug.LogWarning($"[CharacterCandidates] Khong tai duoc Texture2D tai '{animPath}'. Bo qua.");
                    skippedAnimCount++;
                    continue;
                }

                string displayName = GetCleanAnimName(animTexture.name);
                if (!TryReadPngPixelsLinear(animPath, out Color32[] pixels, out int pngWidth, out int pngHeight))
                {
                    Debug.LogWarning($"[CharacterCandidates] Khong doc duoc PNG tuyen tinh cho '{displayName}' tai '{animPath}'. Bo qua.");
                    skippedAnimCount++;
                    continue;
                }

                if (animTexture.width != pngWidth || animTexture.height != pngHeight)
                {
                    Debug.LogWarning(
                        $"[CharacterCandidates] Kich thuoc Texture2D da import ({animTexture.width}x{animTexture.height}) " +
                        $"khac kich thuoc PNG goc ({pngWidth}x{pngHeight}) cho '{displayName}'.");
                }

                int usedColumns = DetectUsedColumnCount(pixels, pngWidth, pngHeight);
                if (usedColumns <= 0)
                {
                    Debug.LogWarning($"[CharacterCandidates] AnimMap '{displayName}' ({pngWidth}x{pngHeight}) khong co pixel RGB khac 0. Bo qua.");
                    skippedAnimCount++;
                    continue;
                }

                MeshCandidate matchedMesh = FindMatchingMesh(allMeshes, usedColumns, displayName, pngWidth, pngHeight);
                if (matchedMesh == null)
                {
                    skippedAnimCount++;
                    continue;
                }

                Vector3[] restVertices = GetCachedMeshVertices(matchedMesh.Mesh, vertexCache);
                if (restVertices.Length != usedColumns)
                {
                    Debug.LogWarning(
                        $"[CharacterCandidates] Khong doc duoc du {usedColumns} vertex tu mesh '{matchedMesh.DisplayName}' " +
                        $"(doc duoc {restVertices.Length}) cho clip '{displayName}'. Bo qua.");
                    skippedAnimCount++;
                    continue;
                }

                ComputeClipRegressionBounds(
                    pixels,
                    restVertices,
                    matchedMesh.Mesh.bounds,
                    displayName,
                    out Vector3 boundsMin,
                    out Vector3 boundsMax,
                    out Vector3 correlation);

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[CharacterCandidates] Clip '{0}' ({1}x{2}, usedCols={3}) -> mesh '{4}' ({5} verts) | " +
                    "BoundsMin=({6:F4}, {7:F4}, {8:F4}), BoundsMax=({9:F4}, {10:F4}, {11:F4}), r=({12:F3}, {13:F3}, {14:F3})",
                    displayName,
                    pngWidth,
                    pngHeight,
                    usedColumns,
                    matchedMesh.DisplayName,
                    matchedMesh.Mesh.vertexCount,
                    boundsMin.x, boundsMin.y, boundsMin.z,
                    boundsMax.x, boundsMax.y, boundsMax.z,
                    correlation.x, correlation.y, correlation.z));

                Material animMaterial = CreateOrUpdateAnimMaterial(animTexture, animShader, boundsMin, boundsMax);
                animatedCandidates.Add(new AnimatedCandidate
                {
                    DisplayName = displayName,
                    Texture = animTexture,
                    MeshCandidate = matchedMesh,
                    Material = animMaterial,
                    UsedColumnCount = usedColumns
                });
            }

            float referenceSize = GetReferenceMaxDimension(allMeshes, animatedCandidates);

            Material staticMaterial = CreateOrUpdateLitMaterial(StaticMaterialPath, DefaultStaticColor, 0.2f);
            Material floorMaterial = CreateOrUpdateLitMaterial(FloorMaterialPath, FloorColor, 0.1f);
            AssetDatabase.SaveAssets();

            Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();

            float animRowWidth = BuildAnimatedRow(animatedCandidates);
            float staticRowWidth = BuildStaticRow(allMeshes, staticMaterial, referenceSize);
            float maxRowWidth = Mathf.Max(animRowWidth, staticRowWidth);

            BuildPreviewFloor(maxRowWidth, floorMaterial);
            BuildPreviewCamera(maxRowWidth);

            bool saved = EditorSceneManager.SaveScene(previewScene, ScenePath);
            AssetDatabase.SaveAssets();

            if (saved)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                if (sceneAsset != null)
                {
                    EditorGUIUtility.PingObject(sceneAsset);
                }

                Debug.Log(
                    $"[CharacterCandidates] Da tao scene preview tai '{ScenePath}': " +
                    $"{animatedCandidates.Count} animMap (bo qua: {skippedAnimCount}, cap nhat importer: {updatedImporters}), " +
                    $"{allMeshes.Count} mesh tinh.");
            }
            else
            {
                Debug.LogError($"[CharacterCandidates] Luu scene '{ScenePath}' that bai.");
            }
        }

        private static List<string> FindSortedAssetPaths(string filter, string folder)
        {
            List<string> paths = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return paths;
            }

            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static int ConfigureAnimMapImporters(List<string> animMapPaths)
        {
            int updatedCount = 0;
            for (int i = 0; i < animMapPaths.Count; i++)
            {
                string path = animMapPaths[i];
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool changed = false;

                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    changed = true;
                }

                if (importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    changed = true;
                }

                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    changed = true;
                }

                if (importer.anisoLevel != 0)
                {
                    importer.anisoLevel = 0;
                    changed = true;
                }

                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    changed = true;
                }

                if (importer.npotScale != TextureImporterNPOTScale.None)
                {
                    importer.npotScale = TextureImporterNPOTScale.None;
                    changed = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }

                if (importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = false;
                    changed = true;
                }

                int desiredMaxSize = Mathf.Max(importer.maxTextureSize, 2048);
                if (importer.maxTextureSize != desiredMaxSize)
                {
                    importer.maxTextureSize = desiredMaxSize;
                    changed = true;
                }

                TextureImporterPlatformSettings defaultPlatform = importer.GetDefaultPlatformTextureSettings();
                if (defaultPlatform.textureCompression != TextureImporterCompression.Uncompressed ||
                    defaultPlatform.format != TextureImporterFormat.RGBA32 ||
                    defaultPlatform.maxTextureSize != desiredMaxSize)
                {
                    defaultPlatform.textureCompression = TextureImporterCompression.Uncompressed;
                    defaultPlatform.format = TextureImporterFormat.RGBA32;
                    defaultPlatform.maxTextureSize = desiredMaxSize;
                    importer.SetPlatformTextureSettings(defaultPlatform);
                    changed = true;
                }

                for (int p = 0; p < PlatformOverrideNames.Length; p++)
                {
                    TextureImporterPlatformSettings platformSettings =
                        importer.GetPlatformTextureSettings(PlatformOverrideNames[p]);
                    if (platformSettings != null && platformSettings.overridden)
                    {
                        platformSettings.overridden = false;
                        importer.SetPlatformTextureSettings(platformSettings);
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    updatedCount++;
                }
            }

            return updatedCount;
        }

        private static Shader LoadAnimMapShader()
        {
            Shader shader = Shader.Find(AnimMapShaderName);
            if (shader != null)
            {
                return shader;
            }

            return AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        }

        private static List<MeshCandidate> LoadAllMeshes()
        {
            List<string> meshPaths = FindSortedAssetPaths("t:Mesh", MeshFolder);
            List<MeshCandidate> result = new List<MeshCandidate>(meshPaths.Count);
            for (int i = 0; i < meshPaths.Count; i++)
            {
                string path = meshPaths[i];
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    continue;
                }

                result.Add(new MeshCandidate
                {
                    Mesh = mesh,
                    AssetPath = path,
                    DisplayName = GetMeshDisplayName(mesh, path)
                });
            }

            return result;
        }

        private static string GetMeshDisplayName(Mesh mesh, string assetPath)
        {
            if (mesh == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsMainAsset(mesh))
            {
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    return fileName;
                }
            }

            return mesh.name;
        }

        private static bool TryReadPngPixelsLinear(
            string assetPath,
            out Color32[] pixels,
            out int width,
            out int height)
        {
            pixels = null;
            width = 0;
            height = 0;

            string fullPath = AssetPathToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            byte[] pngBytes = File.ReadAllBytes(fullPath);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return false;
            }

            Texture2D tempTex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!tempTex.LoadImage(pngBytes, false))
                {
                    return false;
                }

                width = tempTex.width;
                height = tempTex.height;
                pixels = tempTex.GetPixels32();
                return pixels != null && pixels.Length == width * height && width > 0 && height > 0;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempTex);
            }
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, assetPath));
        }

        private static int DetectUsedColumnCount(Color32[] pixels, int width, int height)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                for (int y = 0; y < height; y++)
                {
                    Color32 p = pixels[y * width + x];
                    if (p.r != 0 || p.g != 0 || p.b != 0)
                    {
                        return x + 1;
                    }
                }
            }

            return 0;
        }

        private static MeshCandidate FindMatchingMesh(
            List<MeshCandidate> allMeshes,
            int usedColumnCount,
            string clipName,
            int texWidth,
            int texHeight)
        {
            List<MeshCandidate> matches = new List<MeshCandidate>();
            for (int i = 0; i < allMeshes.Count; i++)
            {
                MeshCandidate candidate = allMeshes[i];
                if (candidate.Mesh != null && candidate.Mesh.vertexCount == usedColumnCount)
                {
                    matches.Add(candidate);
                }
            }

            if (matches.Count == 0)
            {
                Debug.LogWarning(
                    $"[CharacterCandidates] Bo qua animMap '{clipName}' ({texWidth}x{texHeight}): " +
                    $"so cot su dung = {usedColumnCount}, khong co mesh nao trong '{MeshFolder}' co dung {usedColumnCount} vertex.");
                return null;
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            MeshCandidate skinnedCandidate = null;
            int skinnedCount = 0;
            List<string> matchNames = new List<string>(matches.Count);
            for (int i = 0; i < matches.Count; i++)
            {
                matchNames.Add(matches[i].DisplayName);
                Matrix4x4[] bindposes = matches[i].Mesh.bindposes;
                if (bindposes != null && bindposes.Length > 0)
                {
                    skinnedCandidate = matches[i];
                    skinnedCount++;
                }
            }

            MeshCandidate chosen = skinnedCount == 1 ? skinnedCandidate : matches[0];
            Debug.LogWarning(
                $"[CharacterCandidates] Nhieu mesh cung co {usedColumnCount} vertex ({string.Join(", ", matchNames)}) " +
                $"cho clip '{clipName}'. Chon '{chosen.DisplayName}'.");
            return chosen;
        }

        private static Vector3[] GetCachedMeshVertices(Mesh mesh, Dictionary<Mesh, Vector3[]> cache)
        {
            if (mesh == null)
            {
                return Array.Empty<Vector3>();
            }

            if (cache.TryGetValue(mesh, out Vector3[] cached))
            {
                return cached;
            }

            Vector3[] vertices = ReadMeshVertices(mesh);
            cache[mesh] = vertices;
            return vertices;
        }

        private static Vector3[] ReadMeshVertices(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount <= 0)
            {
                return Array.Empty<Vector3>();
            }

            if (mesh.isReadable)
            {
                return mesh.vertices;
            }

            using (Mesh.MeshDataArray dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                Mesh.MeshData data = dataArray[0];
                int vertexCount = data.vertexCount;
                Vector3[] vertices = new Vector3[vertexCount];
                if (vertexCount == 0)
                {
                    return vertices;
                }

                NativeArray<Vector3> nativeVertices = new NativeArray<Vector3>(vertexCount, Allocator.Temp);
                try
                {
                    data.GetVertices(nativeVertices);
                    nativeVertices.CopyTo(vertices);
                }
                finally
                {
                    nativeVertices.Dispose();
                }

                return vertices;
            }
        }

        private static void ComputeClipRegressionBounds(
            Color32[] pixels,
            Vector3[] meshVertices,
            Bounds fallbackMeshBounds,
            string clipName,
            out Vector3 boundsMin,
            out Vector3 boundsMax,
            out Vector3 correlation)
        {
            bool okX = FitAxisLinearRegression(
                pixels, meshVertices, 0, fallbackMeshBounds.min.x, fallbackMeshBounds.max.x,
                out float minX, out float maxX, out float corrX);
            bool okY = FitAxisLinearRegression(
                pixels, meshVertices, 1, fallbackMeshBounds.min.y, fallbackMeshBounds.max.y,
                out float minY, out float maxY, out float corrY);
            bool okZ = FitAxisLinearRegression(
                pixels, meshVertices, 2, fallbackMeshBounds.min.z, fallbackMeshBounds.max.z,
                out float minZ, out float maxZ, out float corrZ);

            if (!okX || !okY || !okZ)
            {
                Debug.LogWarning(
                    $"[CharacterCandidates] Clip '{clipName}' co truc bien thien bang 0 tren frame 0 " +
                    $"(okX={okX}, okY={okY}, okZ={okZ}); dung mesh.bounds cho truc do.");
            }

            boundsMin = new Vector3(minX, minY, minZ);
            boundsMax = new Vector3(maxX, maxY, maxZ);
            correlation = new Vector3(corrX, corrY, corrZ);
        }

        private static bool FitAxisLinearRegression(
            Color32[] pixels,
            Vector3[] vertices,
            int axis,
            float fallbackMin,
            float fallbackMax,
            out float boundsMin,
            out float boundsMax,
            out float correlation)
        {
            int count = vertices.Length;
            if (count < 2 || pixels == null || pixels.Length < count)
            {
                boundsMin = fallbackMin;
                boundsMax = fallbackMax;
                correlation = 0f;
                return false;
            }

            double sumC = 0.0;
            double sumP = 0.0;
            for (int i = 0; i < count; i++)
            {
                sumC += GetPixelChannelNormalized(pixels[i], axis);
                sumP += GetVectorAxis(vertices[i], axis);
            }

            double meanC = sumC / count;
            double meanP = sumP / count;

            double sCC = 0.0;
            double sPP = 0.0;
            double sCP = 0.0;
            for (int i = 0; i < count; i++)
            {
                double dc = GetPixelChannelNormalized(pixels[i], axis) - meanC;
                double dp = GetVectorAxis(vertices[i], axis) - meanP;
                sCC += dc * dc;
                sPP += dp * dp;
                sCP += dc * dp;
            }

            const double VarianceEpsilon = 1e-12;
            if (sCC <= VarianceEpsilon)
            {
                boundsMin = fallbackMin;
                boundsMax = fallbackMax;
                correlation = 0f;
                return false;
            }

            double k = sCP / sCC;
            double b = meanP - k * meanC;

            boundsMin = (float)b;
            boundsMax = (float)(k + b);
            correlation = sPP > VarianceEpsilon
                ? (float)(sCP / Math.Sqrt(sCC * sPP))
                : 0f;
            return true;
        }

        private static double GetPixelChannelNormalized(Color32 pixel, int axis)
        {
            switch (axis)
            {
                case 0: return pixel.r / 255.0;
                case 1: return pixel.g / 255.0;
                default: return pixel.b / 255.0;
            }
        }

        private static double GetVectorAxis(Vector3 v, int axis)
        {
            switch (axis)
            {
                case 0: return v.x;
                case 1: return v.y;
                default: return v.z;
            }
        }

        private static float GetReferenceMaxDimension(
            List<MeshCandidate> allMeshes,
            List<AnimatedCandidate> animatedCandidates)
        {
            Mesh referenceMesh = null;

            for (int i = 0; i < allMeshes.Count; i++)
            {
                if (string.Equals(allMeshes[i].DisplayName, PrimaryReferenceMeshName, StringComparison.OrdinalIgnoreCase))
                {
                    referenceMesh = allMeshes[i].Mesh;
                    break;
                }
            }

            if (referenceMesh == null && animatedCandidates.Count > 0)
            {
                referenceMesh = animatedCandidates[0].MeshCandidate.Mesh;
            }

            if (referenceMesh == null)
            {
                return 1.5f;
            }

            Vector3 size = referenceMesh.bounds.size;
            float maxDim = Mathf.Max(size.x, size.y, size.z);
            return maxDim > 0.0001f ? maxDim : 1.5f;
        }

        private static Material CreateOrUpdateAnimMaterial(
            Texture2D animTexture,
            Shader animShader,
            Vector3 boundsMin,
            Vector3 boundsMax)
        {
            string cleanName = GetCleanAnimName(animTexture.name);
            string matPath = $"{MaterialsFolder}/Mat_AnimMap_{SanitizeAssetName(cleanName)}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = mat == null;

            if (isNew)
            {
                mat = new Material(animShader);
                mat.SetFloat("_Speed", DefaultAnimSpeed);
                mat.SetColor("_Color", DefaultAnimColor);
                mat.SetFloat("_EnableAnim", 1f);
                mat.SetFloat("_InterpolateFrames", 1f);
                mat.SetFloat("_FlipV", 0f);
                mat.SetFloat("_Cull", (float)CullMode.Back);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != animShader)
            {
                mat.shader = animShader;
            }

            mat.SetTexture("_AnimMap", animTexture);
            mat.SetFloat("_FrameCount", Mathf.Max(animTexture.height, 1));
            mat.SetVector("_BoundsMin", new Vector4(boundsMin.x, boundsMin.y, boundsMin.z, 0f));
            mat.SetVector("_BoundsMax", new Vector4(boundsMax.x, boundsMax.y, boundsMax.z, 0f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateLitMaterial(string matPath, Color baseColor, float smoothness)
        {
            Shader litShader = FindFallbackLitShader();
            if (litShader == null)
            {
                Debug.LogWarning("[CharacterCandidates] Khong tim thay shader URP Lit/Unlit cho material tinh.");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != litShader)
            {
                mat.shader = litShader;
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", baseColor);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", baseColor);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Shader FindFallbackLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Sprites/Default");
        }

        private static float BuildAnimatedRow(List<AnimatedCandidate> animatedCandidates)
        {
            GameObject rowRoot = new GameObject("AnimatedCandidates");
            int count = animatedCandidates.Count;
            if (count == 0)
            {
                return 0f;
            }

            float rowWidth = (count - 1) * SlotSpacing;
            float startX = -rowWidth * 0.5f;

            CreateRowHeader(
                rowRoot.transform,
                "ANIMATED\n(AnimMap)",
                new Vector3(startX - SlotSpacing * 0.75f, 0.35f, AnimatedRowZ));

            for (int i = 0; i < count; i++)
            {
                AnimatedCandidate entry = animatedCandidates[i];
                Texture2D tex = entry.Texture;
                Mesh mesh = entry.MeshCandidate.Mesh;
                string meshName = entry.MeshCandidate.DisplayName;
                float slotX = startX + i * SlotSpacing;

                GameObject slot = new GameObject($"Anim_{entry.DisplayName}");
                slot.transform.SetParent(rowRoot.transform, false);
                slot.transform.localPosition = new Vector3(slotX, 0f, AnimatedRowZ);

                GameObject meshObj = new GameObject(meshName);
                meshObj.transform.SetParent(slot.transform, false);

                MeshFilter mf = meshObj.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
                AssignSharedMaterialToAllSubmeshes(mr, mesh, entry.Material);

                string labelText = $"{entry.DisplayName}\n<size=70%>{meshName} ({entry.UsedColumnCount}v) | {tex.width}x{tex.height}</size>";
                CreateItemLabel(slot.transform, labelText);
            }

            return rowWidth;
        }

        private static float BuildStaticRow(List<MeshCandidate> staticMeshes, Material staticMaterial, float referenceSize)
        {
            GameObject rowRoot = new GameObject("StaticCandidates");
            int count = staticMeshes.Count;
            if (count == 0)
            {
                return 0f;
            }

            float rowWidth = (count - 1) * SlotSpacing;
            float startX = -rowWidth * 0.5f;

            CreateRowHeader(
                rowRoot.transform,
                "STATIC MESHES",
                new Vector3(startX - SlotSpacing * 0.75f, 0.35f, StaticRowZ));

            for (int i = 0; i < count; i++)
            {
                MeshCandidate entry = staticMeshes[i];
                Mesh mesh = entry.Mesh;
                string displayName = entry.DisplayName;
                float slotX = startX + i * SlotSpacing;

                GameObject slot = new GameObject($"Static_{displayName}");
                slot.transform.SetParent(rowRoot.transform, false);
                slot.transform.localPosition = new Vector3(slotX, 0f, StaticRowZ);

                Bounds b = mesh.bounds;
                float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                bool shouldNormalizeScale =
                    string.Equals(displayName, "Char_Ambulance_2", StringComparison.OrdinalIgnoreCase) ||
                    maxDim > referenceSize * OversizedThresholdRatio;

                float uniformScale = 1f;
                if (shouldNormalizeScale && maxDim > 0.0001f)
                {
                    uniformScale = referenceSize / maxDim;
                }

                GameObject meshObj = new GameObject(displayName);
                meshObj.transform.SetParent(slot.transform, false);
                meshObj.transform.localScale = Vector3.one * uniformScale;
                meshObj.transform.localPosition = new Vector3(
                    -b.center.x * uniformScale,
                    -b.min.y * uniformScale,
                    -b.center.z * uniformScale);

                MeshFilter mf = meshObj.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
                AssignSharedMaterialToAllSubmeshes(mr, mesh, staticMaterial);

                string scaleSuffix = shouldNormalizeScale
                    ? string.Format(CultureInfo.InvariantCulture, ", x{0:0.000}", uniformScale)
                    : string.Empty;
                string labelText = $"{displayName}\n<size=75%>{mesh.vertexCount} verts{scaleSuffix}</size>";
                CreateItemLabel(slot.transform, labelText);
            }

            return rowWidth;
        }

        private static void AssignSharedMaterialToAllSubmeshes(MeshRenderer renderer, Mesh mesh, Material material)
        {
            if (material == null)
            {
                return;
            }

            int subMeshCount = mesh != null ? Mathf.Max(mesh.subMeshCount, 1) : 1;
            Material[] materials = new Material[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }

        private static void CreateItemLabel(Transform parent, string text)
        {
            GameObject labelGo = new GameObject("Label");
            TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 3.0f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.overflowMode = TextOverflowModes.Overflow;

            RectTransform rect = tmp.rectTransform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(SlotSpacing * 0.95f, 1.2f);
            rect.localPosition = new Vector3(0f, LabelHeightY, LabelOffsetZ);
            rect.localRotation = Quaternion.Euler(40f, 0f, 0f);
        }

        private static void CreateRowHeader(Transform parent, string text, Vector3 localPosition)
        {
            GameObject headerGo = new GameObject("RowHeader");
            TextMeshPro tmp = headerGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 4.2f;
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.color = new Color(0.95f, 0.85f, 0.45f, 1f);
            tmp.overflowMode = TextOverflowModes.Overflow;

            RectTransform rect = tmp.rectTransform;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(4.5f, 1.8f);
            rect.localPosition = localPosition;
            rect.localRotation = Quaternion.Euler(35f, 0f, 0f);
        }

        private static void SetupLighting()
        {
            GameObject lightGo = new GameObject("Directional Light");
            lightGo.transform.position = new Vector3(0f, 10f, -6f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f, 1f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.35f, 0.40f, 1f);
        }

        private static void BuildPreviewFloor(float maxRowWidth, Material floorMaterial)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "PreviewFloor";

            Collider collider = floor.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            float centerZ = (AnimatedRowZ + StaticRowZ) * 0.5f;
            float width = Mathf.Max(maxRowWidth + 14f, 18f);
            float depth = Mathf.Abs(StaticRowZ - AnimatedRowZ) + 12f;

            floor.transform.position = new Vector3(0f, -0.01f, centerZ);
            floor.transform.localScale = new Vector3(width * 0.1f, 1f, depth * 0.1f);

            MeshRenderer mr = floor.GetComponent<MeshRenderer>();
            if (mr != null && floorMaterial != null)
            {
                mr.sharedMaterial = floorMaterial;
            }
        }

        private static void BuildPreviewCamera(float maxRowWidth)
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";

            Camera cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CameraBackgroundColor;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            float centerZ = (AnimatedRowZ + StaticRowZ) * 0.5f;
            float halfWidth = Mathf.Max(maxRowWidth * 0.5f + 3f, 8f);
            float tanHalfHFov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * (16f / 9f);
            float distance = (halfWidth / tanHalfHFov) + 2f;
            float height = Mathf.Clamp(distance * 0.36f, 5f, 14f);

            cameraGo.transform.position = new Vector3(0f, height, centerZ - distance);
            cameraGo.transform.LookAt(new Vector3(0f, 0.9f, centerZ));
        }

        private static string GetCleanAnimName(string textureName)
        {
            string name = textureName;
            const string animMapSuffix = ".animMap";
            if (name.EndsWith(animMapSuffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - animMapSuffix.Length);
            }

            const string prefix = "character_";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(prefix.Length);
            }

            return name;
        }

        private static string SanitizeAssetName(string rawName)
        {
            char[] chars = rawName.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == ' ' || chars[i] == '/' || chars[i] == '\\' || chars[i] == ':')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetFolderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
