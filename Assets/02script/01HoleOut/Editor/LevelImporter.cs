using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HoleOut.Core;
using HoleOut.Runtime;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>
    /// Nhập bộ mẫu test từ Reference/HoleSortGarden theo manifest, xuất LevelSO vào
    /// Assets/07data/TestLevels/. Nhập lại cập nhật asset tại chỗ (giữ GUID).
    /// </summary>
    public static class LevelImporter
    {
        public const string OutputFolder = "Assets/07data/HoleOutTestLevels";
        public const string ManifestPath = OutputFolder + "/import-manifest.json";
        const string ReferenceFolder = "Reference/HoleSortGarden";

        public struct ImportReport
        {
            public int Imported, Failed;
            public List<string> Lines;
        }

        [MenuItem("Tools/HoleOut/Import Test Levels")]
        public static void ImportMenu()
        {
            var report = ImportAll(replay: true);
            Debug.Log($"[HoleOut] Nhập {report.Imported} màn, lỗi {report.Failed}.\n" + string.Join("\n", report.Lines));
        }

        [MenuItem("Tools/HoleOut/Replay Test Levels")]
        public static void ReplayMenu()
        {
            var lines = new List<string>();
            foreach (var so in LoadAll())
            {
                var r = SolutionReplayer.Run(so.data, so.solution);
                so.lastReplay = r.ToString();
                EditorUtility.SetDirty(so);
                lines.Add($"{so.name}: {r}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[HoleOut] Replay\n" + string.Join("\n", lines));
        }

        public static IEnumerable<LevelSO> LoadAll() =>
            AssetDatabase.FindAssets("t:LevelSO", new[] { OutputFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .OrderBy(s => s.name, StringComparer.Ordinal);

        public static ImportReport ImportAll(bool replay)
        {
            var report = new ImportReport { Lines = new List<string>() };
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string refRoot = Path.Combine(projectRoot, ReferenceFolder);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(projectRoot, ManifestPath)));

            JArray campaign = null;
            var seen = new HashSet<string>();
            foreach (JObject entry in manifest["entries"])
            {
                string id = (string)entry["id"];
                try
                {
                    string source = (string)entry["source"];
                    string key = source == "campaign" ? "campaign:" + (int)entry["sourceId"] : "file:" + (string)entry["file"];
                    if (!seen.Add(key)) throw new Exception($"Nguồn {key} đã được nhập bởi mục khác.");
                    if (!seen.Add("id:" + id)) throw new Exception($"ID {id} bị trùng trong manifest.");

                    LevelImport li;
                    if (source == "campaign")
                    {
                        campaign ??= ReadCampaign(Path.Combine(refRoot, "campaign.js"));
                        int sid = (int)entry["sourceId"];
                        var raw = campaign.Cast<JObject>().FirstOrDefault(o => (int)o["id"] == sid)
                                  ?? throw new Exception($"campaign không có màn id {sid}.");
                        li = FromCampaign(raw);
                    }
                    else if (source == "file")
                    {
                        li = FromTestJson(JObject.Parse(File.ReadAllText(Path.Combine(refRoot, (string)entry["file"]))));
                    }
                    else throw new Exception($"Nguồn không hỗ trợ: {source}");

                    li.Source = key;
                    li.Level.levelId = id;
                    if (entry["lBlocks"] is JArray lBlocks)
                    {
                        foreach (int blockId in lBlocks.Values<int>())
                        {
                            var block = li.Level.waves.SelectMany(w => w.blocks).SingleOrDefault(b => b.id == blockId)
                                ?? throw new Exception($"Không thấy khối chữ L {blockId}.");
                            block.shape = BlockShape.L;
                        }
                    }
                    ApplyMode(li, (string)entry["mode"]);
                    li.Level.pictureScale = (float?)entry["pictureScale"] ?? 1f;

                    var errors = LevelValidator.Validate(li.Level);
                    if (errors.Count > 0) throw new Exception("Validation:\n  - " + string.Join("\n  - ", errors));

                    var so = WriteAsset(id, li);
                    string line = $"{id} ← {key}: {li.Level.Rows}×{li.Level.Columns}, {li.Level.displayMode}, hiện {li.Level.EffectiveVisibleRows} hàng, {li.Level.waves.Count} đợt, {li.Solution.steps.Count} bước lời giải";
                    if (replay)
                    {
                        var r = SolutionReplayer.Run(so.data, so.solution);
                        so.lastReplay = r.ToString();
                        EditorUtility.SetDirty(so);
                        line += " — replay " + r;
                    }
                    report.Lines.Add(line);
                    report.Imported++;
                }
                catch (Exception e)
                {
                    report.Failed++;
                    report.Lines.Add($"LỖI {id}: {e.Message}");
                }
            }
            AssetDatabase.SaveAssets();
            return report;
        }

        class LevelImport
        {
            public LevelData Level;
            public LevelSolution Solution;
            public bool NoScroll;
            public string Name;
            public string Source;
            public float CharScale;
            public bool TutorialDrag, TutorialScroll;
        }

        static JArray ReadCampaign(string path)
        {
            string text = File.ReadAllText(path);
            int start = text.IndexOf('['), end = text.LastIndexOf(']');
            if (start < 0 || end < start) throw new Exception("Không đọc được mảng màn trong campaign.js.");
            return JArray.Parse(text.Substring(start, end - start + 1));
        }

        static LevelImport FromCampaign(JObject o)
        {
            var board = (JObject)o["board"];
            var level = new LevelData
            {
                grid = o["grid"].Select(t => (string)t).ToArray(),
                visibleRows = (int?)o["visibleRows"] ?? 10,
                dockSlots = (int?)o["dockSlots"] ?? 5,
                gates = ReadGates((JArray)board["doors"]),
            };
            // Campaign: board.blocks là đợt đầu, board.waves là các đợt sau.
            level.waves.Add(ReadWave((JArray)board["blocks"]));
            if (board["waves"] is JArray later) foreach (JArray w in later) level.waves.Add(ReadWave(w));

            return new LevelImport
            {
                Level = level,
                Solution = ReadSolution(o["solution"] as JArray),
                NoScroll = (bool?)o["noScroll"] ?? false,
                Name = (string)o["name"],
                CharScale = (float?)o["charScale"] ?? 0,
                TutorialDrag = IsSet(o["tutorial"]),
                TutorialScroll = IsSet(o["scrollTutorial"]),
            };
        }

        static LevelImport FromTestJson(JObject o)
        {
            // JSON test: front (hàng gần mép thoát trước) đảo thứ tự thành grid, vis → visibleRows, slots → dockSlots; waves đã gồm đợt đầu; doors → gates; steps → lời giải.
            var level = new LevelData
            {
                grid = o["front"].Select(t => (string)t).Reverse().ToArray(),
                visibleRows = (int?)o["vis"] ?? 10,
                dockSlots = (int?)o["slots"] ?? 5,
                gates = ReadGates((JArray)o["doors"]),
            };
            foreach (JArray w in (JArray)o["waves"]) level.waves.Add(ReadWave(w));
            return new LevelImport
            {
                Level = level,
                Solution = ReadSolution(o["steps"] as JArray),
                NoScroll = (bool?)o["noScroll"] ?? false,
                Name = (string)o["name"],
            };
        }

        /// <summary>
        /// Tranh đứng yên và hiện đủ mọi hàng → FullPicture; còn lại → WindowedScroll.
        /// Báo lỗi khi dữ liệu yêu cầu đứng yên nhưng giấu một phần tranh.
        /// </summary>
        static void ApplyMode(LevelImport li, string overrideMode)
        {
            var level = li.Level;
            if (!string.IsNullOrEmpty(overrideMode))
            {
                level.displayMode = (PictureDisplayMode)Enum.Parse(typeof(PictureDisplayMode), overrideMode);
            }
            else if (li.NoScroll)
            {
                if (level.visibleRows < level.Rows)
                    throw new Exception($"Dữ liệu yêu cầu tranh đứng yên nhưng chỉ hiện {level.visibleRows}/{level.Rows} hàng.");
                level.displayMode = PictureDisplayMode.FullPicture;
            }
            else level.displayMode = PictureDisplayMode.WindowedScroll;

            if (level.displayMode == PictureDisplayMode.FullPicture)
            {
                level.visibleRows = level.Rows;
                li.TutorialScroll = false;
            }

            li.Solution.levelId = level.levelId;
            li.Solution.verifiedMode = level.displayMode;
            li.Solution.dataVersion = DataVersion(level);
        }

        static List<GateData> ReadGates(JArray arr) => arr.Select(t => new GateData
        {
            color = (string)t["color"],
            side = ParseSide((string)t["side"]),
            start = (int)t["start"],
            len = (int?)t["len"] ?? 1,
        }).ToList();

        static WaveData ReadWave(JArray arr) => new WaveData
        {
            blocks = arr.Select(t => new BlockData
            {
                id = (int)t["id"], gx = (int)t["gx"], gy = (int)t["gy"],
                w = (int?)t["w"] ?? 1, h = (int?)t["h"] ?? 1,
                color = (string)t["color"], capacity = (int)t["capacity"],
                mystery = (bool?)t["mystery"] ?? false,
                ice = (int?)t["ice"] ?? 0,
            }).ToList()
        };

        static LevelSolution ReadSolution(JArray arr)
        {
            var s = new LevelSolution();
            if (arr == null) return s;
            foreach (JObject t in arr)
            {
                string kind = (string)t["kind"];
                var step = new SolutionStep
                {
                    kind = kind == "move" ? SolutionStepKind.Move : kind == "exit" ? SolutionStepKind.Exit : throw new Exception($"Bước lời giải không hỗ trợ: {kind}"),
                    block = (int)t["block"],
                };
                if (t["path"] is JArray path) foreach (JArray p in path) step.path.Add(new GridPoint((int)p[0], (int)p[1]));
                if (step.kind == SolutionStepKind.Exit) step.door = ParseSide((string)t["door"]);
                s.steps.Add(step);
            }
            return s;
        }

        static bool IsSet(JToken t) => t != null && t.Type != JTokenType.Null && !(t.Type == JTokenType.Boolean && !(bool)t);

        static GateSide ParseSide(string side) => side switch
        {
            "top" => GateSide.Top,
            "bottom" => GateSide.Bottom,
            "left" => GateSide.Left,
            "right" => GateSide.Right,
            _ => throw new Exception($"Cạnh cổng không hợp lệ: {side}"),
        };

        /// <summary>Phiên bản dữ liệu: băm ổn định từ tranh, khối, cổng, cửa sổ và khay.</summary>
        public static int DataVersion(LevelData l)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join("/", l.grid)).Append('|').Append(l.displayMode).Append('|').Append(l.EffectiveVisibleRows).Append('|').Append(l.dockSlots);
            foreach (var w in l.waves)
            {
                sb.Append('#');
                foreach (var b in w.blocks)
                {
                    sb.Append($"{b.id},{b.gx},{b.gy},{b.w},{b.h},{b.color},{b.capacity},{b.mystery}");
                    if (b.shape != BlockShape.Rectangle) sb.Append(',').Append(b.shape);
                    // Chỉ thêm khi có băng để dấu vân của màn cũ giữ nguyên.
                    if (b.ice > 0) sb.Append(",ice").Append(b.ice);
                    sb.Append(';');
                }
            }
            foreach (var g in l.gates) sb.Append($"@{g.color},{g.side},{g.start},{g.len}");
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in sb.ToString()) h = (h ^ c) * 16777619;
                return h & 0x7fffffff;
            }
        }

        static LevelSO WriteAsset(string id, LevelImport li)
        {
            string path = $"{OutputFolder}/{id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<LevelSO>(path);
            bool created = so == null;
            if (created) so = ScriptableObject.CreateInstance<LevelSO>();
            so.displayName = li.Name;
            so.source = li.Source;
            so.data = li.Level;
            so.solution = li.Solution;
            so.charScale = li.CharScale;
            so.tutorialFirstDrag = li.TutorialDrag;
            so.lastReplay = "";
            if (created) AssetDatabase.CreateAsset(so, path);
            else EditorUtility.SetDirty(so);
            return so;
        }
    }
}
