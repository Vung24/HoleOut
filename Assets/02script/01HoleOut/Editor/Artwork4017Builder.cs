using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HoleOut.EditorTools
{
    public static class Artwork4017Builder
    {
        const string Folder = "Assets/07data/HoleOutTestLevels";
        public const string LevelPath = Folder + "/holeout-artwork-4017.asset";
        const string VisualsPath = Folder + "/Artwork4017Visuals.asset";
        const string MaterialsPath = "Assets/04art/Material/HoleOut/Artwork4017";

        [Serializable] class Artwork { public string[] grid; public Swatch[] palette; }
        [Serializable] class Swatch
        {
            public int id, count;
            public string code, template;
        }

        [MenuItem("Tools/HoleOut/Build Artwork 4017 Level")]
        public static void Build()
        {
            var artwork = JsonUtility.FromJson<Artwork>(File.ReadAllText(Folder + "/Artwork4017.json"));
            if (artwork.grid.Length != 40 || artwork.grid.Any(row => row.Length != 40))
                throw new InvalidOperationException("Artwork 4017 cần lưới 40 × 40.");
            var defaults = AssetDatabase.LoadAssetAtPath<VisualsSO>(VisualsBuilder.VisualsPath);
            if (!defaults) throw new InvalidOperationException("Chưa có bộ hình ảnh HoleOut.");
            foreach (var swatch in artwork.palette)
                if (!PaletteSO.TryGetSwatch(swatch.id, out _))
                    throw new InvalidOperationException("Bộ màu chuẩn không có màu id " + swatch.id);
            foreach (var swatch in artwork.palette)
                if (artwork.grid.Sum(row => row.Count(c => c.ToString() == swatch.code)) != swatch.count)
                    throw new InvalidOperationException("Số ô không khớp bảng màu " + swatch.code);
            // Tranh gốc 40 × 40 được lược còn đúng cỡ khung tranh để nhân vật cùng cỡ với các màn khác.
            var grid = TrimToSize(artwork.grid, DefaultBoardLayout.FrameRows, DefaultBoardLayout.FrameColumns);
            var shader = Shader.Find("HoleOut/Picture Cell");
            if (!shader) throw new InvalidOperationException("Thiếu shader ô tranh.");
            if (!AssetDatabase.IsValidFolder(MaterialsPath))
                AssetDatabase.CreateFolder("Assets/04art/Material/HoleOut", "Artwork4017");
            var visuals = LoadOrCreate<VisualsSO>(VisualsPath);
            EditorUtility.CopySerialized(defaults, visuals);
            visuals.name = "Artwork4017Visuals";
            visuals.useSeatedMaterial = true;
            visuals.pictureCellHeight = VisualsBuilder.PictureCellHeight;
            visuals.pictureCellFill = VisualsBuilder.PictureCellFill;
            visuals.colors.Clear();
            foreach (var swatch in artwork.palette)
            {
                var template = defaults.Get(swatch.template);
                PaletteSO.TryGetSwatch(swatch.id, out var standard);
                var model = PaletteSO.FromHex(standard.model);
                var look = JsonUtility.FromJson<VisualsSO.ColorVisual>(JsonUtility.ToJson(template));
                look.code = swatch.code;
                look.color = PaletteSO.FromHex(standard.body);
                look.blockTint = look.color;
                look.agent = Tint(template.agent, swatch.code + "_Body", model);
                look.hole = Tint(template.hole, swatch.code + "_Hole", model);
                look.holeStencil = Tint(template.holeStencil, swatch.code + "_Stencil", model);
                look.holeDoors = template.holeDoors.Select((m, i) => Tint(m, swatch.code + "_Door" + i, model)).ToList();
                look.holeCylinder = template.holeCylinder.Select((m, i) => Tint(m, swatch.code + "_Cylinder" + i, model)).ToList();
                string path = MaterialsPath + "/" + swatch.code + "_Picture.mat";
                look.seated = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!look.seated) { look.seated = new Material(shader); AssetDatabase.CreateAsset(look.seated, path); }
                look.seated.shader = shader;
                VisualsBuilder.ApplySwatch(look.seated, standard);
                EditorUtility.SetDirty(look.seated);
                visuals.colors.Add(look);
            }
            var level = LoadOrCreate<LevelSO>(LevelPath);
            level.displayName = "Tranh 4017";
            level.source = "Artwork4017.json";
            level.visuals = visuals;
            level.data = new LevelData
            {
                levelId = "artwork-4017", grid = grid,
                displayMode = PictureDisplayMode.FullPicture, visibleRows = grid.Length, dockSlots = 6
            };
            level.solution = new LevelSolution
            {
                levelId = level.data.levelId, dataVersion = 1,
                verifiedMode = PictureDisplayMode.FullPicture, seed = 1
            };
            var wave = new WaveData();
            var used = artwork.palette.Where(swatch => CountCode(grid, swatch.code) > 0).ToArray();
            for (int i = 0; i < used.Length; i++)
            {
                var swatch = used[i];
                bool top = i < 3;
                int x = (i % 3) * 2, y = top ? 1 : 2;
                var side = top ? GateSide.Top : GateSide.Bottom;
                wave.blocks.Add(new BlockData { id = i + 1, gx = x, gy = y, w = 2, h = 1, color = swatch.code, capacity = CountCode(grid, swatch.code) });
                level.data.gates.Add(new GateData { color = swatch.code, side = side, start = x, len = 2 });
                level.solution.steps.Add(new SolutionStep
                {
                    kind = SolutionStepKind.Exit, block = i + 1, door = side,
                    path = new List<GridPoint> { new GridPoint(x, top ? 0 : 3) }
                });
            }
            level.data.waves.Add(wave);
            var errors = LevelValidator.Validate(level.data);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            var result = SolutionReplayer.Run(level.data, level.solution);
            level.lastReplay = result.ToString();
            if (!result.Success) throw new InvalidOperationException(level.lastReplay);
            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();

            // Chỉ duyệt scene đang mở trong công cụ Editor để đăng ký màn mới một lần.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<GameController>(true))
                {
                    var so = new SerializedObject(controller);
                    var campaign = controller.Campaign ? controller.Campaign : CampaignAsset.LoadOrCreate();
                    so.FindProperty("campaign").objectReferenceValue = campaign;
                    int index = campaign.levels.IndexOf(level);
                    if (index < 0) { index = campaign.levels.Count; campaign.levels.Add(level); EditorUtility.SetDirty(campaign); AssetDatabase.SaveAssets(); }
                    so.FindProperty("startLevel").intValue = index;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[HoleOut] Tranh 4017: lược từ 40 × 40 còn {grid[0].Length} × {grid.Length}, {used.Length} màu. " + result);
        }

        static int CountCode(string[] grid, string code) => grid.Sum(row => row.Count(c => c.ToString() == code));

        /// <summary>
        /// Lược bớt hàng rồi cột cho tới cỡ yêu cầu: mỗi lần bỏ hàng/cột khác ít ô nhất so với hàng/cột kề bên
        /// (nền trống và đoạn lặp đi trước), hoà thì bỏ cái xa tâm hơn để giữ phần chính của tranh.
        /// </summary>
        public static string[] TrimToSize(string[] grid, int rows, int columns)
        {
            var trimmed = TrimRows(grid.ToList(), rows);
            return Transpose(TrimRows(Transpose(trimmed), columns)).ToArray();
        }

        static List<string> TrimRows(List<string> lines, int target)
        {
            while (lines.Count > target)
            {
                int n = lines.Count, best = 0, bestCost = int.MaxValue;
                double bestDistance = -1, center = (n - 1) / 2.0;
                for (int i = 0; i < n; i++)
                {
                    int cost = int.MaxValue;
                    if (i > 0) cost = Math.Min(cost, Difference(lines[i], lines[i - 1]));
                    if (i < n - 1) cost = Math.Min(cost, Difference(lines[i], lines[i + 1]));
                    double distance = Math.Abs(i - center);
                    if (cost < bestCost || (cost == bestCost && distance > bestDistance))
                    { best = i; bestCost = cost; bestDistance = distance; }
                }
                lines.RemoveAt(best);
            }
            return lines;
        }

        static int Difference(string a, string b)
        {
            int count = 0;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) count++;
            return count;
        }

        static List<string> Transpose(List<string> lines) =>
            Enumerable.Range(0, lines[0].Length).Select(c => new string(lines.Select(line => line[c]).ToArray())).ToList();

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }

        static Material Tint(Material template, string name, Color color)
        {
            if (!template) return null;
            string path = MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(template); AssetDatabase.CreateAsset(material, path); }
            else material.CopyPropertiesFromMaterial(template);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_SColor")) material.SetColor("_SColor", color * 0.72f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
