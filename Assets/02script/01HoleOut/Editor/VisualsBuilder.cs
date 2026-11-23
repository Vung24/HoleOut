using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoleOut.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>
    /// Tạo prefab hình ảnh cho Hole Out từ art có sẵn của dự án (nhân vật, hố, khay, hiệu ứng)
    /// rồi bỏ các script gameplay cũ khỏi bản sao. Đọc material theo màu từ ColorSO.
    /// Kết quả: Assets/03prefab/HoleOut/*.prefab và Assets/07data/HoleOutVisuals.asset.
    /// </summary>
    public static class VisualsBuilder
    {
        const string PrefabFolder = "Assets/03prefab/HoleOut";
        const string MaterialFolder = "Assets/04art/Material/HoleOut";
        public const string VisualsPath = "Assets/07data/HoleOutVisuals.asset";
        const string ColorSoPath = "Assets/07data/MonoBehaviour/ColorSO.asset";

        /// <summary>Mã màu tranh → giá trị ColorType của dự án. Mã không có ở đây tô lại từ material xám.</summary>
        static readonly (string code, int colorType)[] ColorMap =
        {
            ("M", 1), ("K", 2), ("O", 3), ("Y", 4), ("G", 5), ("T", 6), ("W", 7), ("R", 8),
            ("L", 9), ("B", 10), ("A", 11), ("P", 12), ("E", 14), ("D", 11),
        };

        static readonly string[] AgentDropChildren = { "Hidden", "HiddenBlockOpen", "FrozenBreak_Effect_Block", "DisabledBlockIce", "FeedBackJump" };
        static readonly string[] HoleDropChildren = { "HoleConnect", "Collect_Feedback", "HiddenHoleUnlocked", "HoleLocked", "Hole_Outline" };

        [MenuItem("Tools/HoleOut/Build Visuals")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            var visuals = AssetDatabase.LoadAssetAtPath<VisualsSO>(VisualsPath);
            if (visuals == null)
            {
                visuals = ScriptableObject.CreateInstance<VisualsSO>();
                AssetDatabase.CreateAsset(visuals, VisualsPath);
            }

            visuals.agentPrefab = BuildAgent();
            visuals.font = BuildFont();
            visuals.holePrefab = BuildHole(visuals.font);
            visuals.dockSlotPrefab = BuildClean("Assets/03prefab/Slot.prefab", "HoleOutDockSlot", new string[0], null);
            visuals.holeCompleteEffect = BuildClean("Assets/03prefab/HoleComplete.prefab", "HoleOutHoleComplete", new string[0], null);
            visuals.boardMaterial = LitMaterial("HoleOutBoard", new Color(0.42f, 0.5f, 0.62f));
            visuals.pictureMaterial = LitMaterial("HoleOutPicture", new Color(0.96f, 0.93f, 0.86f));
            visuals.groundMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/04art/Material/Ground.mat");
            visuals.trayMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/04art/Material/GridFill.mat");
            visuals.rimMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/04art/Material/GridBorder.mat");
            visuals.roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/04art/Sprite/NVRoundedCorners.asset");
            visuals.colors = ReadColors(visuals.colors);
            ApplyPictureCells(visuals);
            AssignBoardArt(visuals);
            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();

            AssignToOpenScene(visuals);
            Debug.Log($"[HoleOut] Đã tạo visuals: {visuals.colors.Count} màu, prefab trong {PrefabFolder}.");
        }

        const string BoardArtFolder = "Assets/04art/BoardArtKit/Textures";

        /// <summary>Mã màu tranh → tên màu trong BoardArtKit. Màu không có ở đây giữ cổng hộp 3D.</summary>
        static readonly (string code, string kit)[] BoardArtColors =
        {
            ("K", "DarkGreen"), ("O", "Orange"), ("Y", "Yellow"), ("G", "Green"), ("W", "White"), ("R", "Red"),
            ("L", "Blue"), ("B", "Pink"), ("A", "DarkBlue"), ("P", "Purple"), ("D", "Black"),
        };

        [MenuItem("Tools/HoleOut/Assign Board Art")]
        static void AssignBoardArtMenu()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<VisualsSO>(VisualsPath);
            if (visuals == null) { Debug.LogError("[HoleOut] Chưa có " + VisualsPath + ", chạy Build Visuals trước."); return; }
            AssignBoardArt(visuals);
            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Gán sprite nền ô, thanh cổng và mũi tên từ BoardArtKit. Thiếu file thì để trống (view dùng hộp 3D).</summary>
        static void AssignBoardArt(VisualsSO visuals)
        {
            visuals.boardCellSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoardArtFolder + "/Floor/TiledCell.png");
            int n = 0;
            foreach (var v in visuals.colors)
            {
                string kit = null;
                foreach (var (code, name) in BoardArtColors) if (code == v.code) kit = name;
                // Khối trên bàn: màu chưa có bộ sprite riêng (bí ẩn, nâu...) mượn bộ trắng rồi nhân màu.
                AssignBlockParts(v, kit ?? "White", kit == null ? v.color : Color.white);
                if (kit == null) continue;
                string low = kit.ToLowerInvariant();
                // Tên _f_v là thanh dài theo chiều ngang, _f_h là thanh dài theo chiều dọc.
                v.gateBarX = AssetDatabase.LoadAssetAtPath<Sprite>($"{BoardArtFolder}/Gates/{low}_exit_f_v.png");
                v.gateBarZ = AssetDatabase.LoadAssetAtPath<Sprite>($"{BoardArtFolder}/Gates/{low}_exit_f_h.png");
                v.arrowUp = Arrow(kit, "Up");
                v.arrowDown = Arrow(kit, "Down");
                v.arrowLeft = Arrow(kit, "Left");
                v.arrowRight = Arrow(kit, "Right");
                if (v.gateBarX) n++;
            }
            Debug.Log($"[HoleOut] Board art: nền ô {(visuals.boardCellSprite ? "có" : "thiếu")}, {n} màu có sprite cổng.");
        }

        static void AssignBlockParts(VisualsSO.ColorVisual v, string kit, Color tint)
        {
            v.blockParts = new Sprite[BoardBlockPrefabBuilder.PartCount];
            for (int i = 0; i < v.blockParts.Length; i++)
            {
                string path = $"{BoardArtFolder}/Blocks/{kit}/{BoardBlockPrefabBuilder.PartFile((BoardBlockPart)i)}.png";
                v.blockParts[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!v.blockParts[i]) Debug.LogWarning("[HoleOut] Thiếu sprite khối: " + path);
            }
            v.blockTint = tint;
        }

        static Sprite Arrow(string kit, string dir) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{BoardArtFolder}/GateArrows/{kit}{dir}.png")
            ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{BoardArtFolder}/GateArrows/{kit}e{dir}.png");

        /// <summary>Font SDF động từ Nunito Black (đủ dấu tiếng Việt, sinh ký tự khi cần).</summary>
        static TMP_FontAsset BuildFont()
        {
            string path = MaterialFolder + "/HoleOut Nunito Black SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/04art/Font/Nunito-Black.ttf");
            if (ttf == null) return null;
            var fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = "HoleOut Nunito Black SDF";
            AssetDatabase.CreateAsset(fa, path);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            foreach (var tex in fa.atlasTextures) { tex.name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            AssetDatabase.SaveAssets();
            return fa;
        }

        static AgentRig BuildAgent()
        {
            var go = BuildCleanInstance("Assets/03prefab/AiAgent.prefab", AgentDropChildren);
            var rig = go.AddComponent<AgentRig>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "DisabledBlock") rig.seated = t.GetComponent<Renderer>();
                if (t.name == "WalkDusts") rig.dust = t.GetComponent<ParticleSystem>();
            }
            rig.body = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            rig.animator = go.GetComponentInChildren<Animator>(true);
            if (rig.animator) rig.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            AddAgentShadow(rig);
            return Save(go, "HoleOutAgent").GetComponent<AgentRig>();
        }

        /// <summary>
        /// Bóng tròn mềm dưới chân nhân vật (mesh + texture bóng giả có sẵn). Không dùng bóng đèn thật:
        /// hướng bóng giữ cố định xuống-phải cho khớp mặt trước tối của ô tranh và bóng khối/khay chờ.
        /// </summary>
        public static void AddAgentShadow(AgentRig rig)
        {
            // Dưới đất: nhân vật/ô phía trước che bóng như bình thường.
            rig.shadow = ShadowQuad(rig, "BlobShadow", new Vector3(0.3f, 0.3f, 1),
                ShadowMaterial("HoleOutAgentShadow", new Color(0.13f, 0.17f, 0.38f, 0.38f), UnityEngine.Rendering.CompareFunction.LessEqual));
        }

        static Renderer ShadowQuad(AgentRig rig, string name, Vector3 scale, Material mat)
        {
            var old = rig.transform.Find(name);
            if (old) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(rig.transform, false);
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/04art/Mesh/ShadowCircle.asset");
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static Material ShadowMaterial(string name, Color color, UnityEngine.Rendering.CompareFunction zTest)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";
            var shader = Shader.Find("HoleOut/Blob Shadow");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/04art/Texture2D/FakeShadow.png"));
            m.SetColor("_Color", color);
            m.SetFloat("_ZTest", (float)zTest);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        static HoleRig BuildHole(TMP_FontAsset font)
        {
            var go = BuildCleanInstance("Assets/03prefab/BoxSpawner.prefab", HoleDropChildren);
            var rig = go.AddComponent<HoleRig>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>();
                switch (t.name.Trim())
                {
                    case "HoleBaseV2": rig.baseRenderer = r; break;
                    case "HoleCover1": rig.door1 = r; break;
                    case "HoleCover1.003": rig.door2 = r; break;
                    case "HoleBaseV2Cylinder": rig.cylinder = r; break;
                    case "HoleBaseV2Bottom": rig.bottom = r; break;
                    case "Cylinder": if (rig.stencil == null) rig.stencil = r; break;
                }
            }
            rig.animator = go.GetComponentInChildren<Animator>(true);
            rig.count = go.GetComponentInChildren<TMP_Text>(true);
            if (rig.stencil && rig.baseRenderer)
            {
                var opening = rig.stencil.transform;
                // Khớp đường kính lớp stencil với miệng hố và giữ nó cố định khi nắp mở.
                opening.SetParent(rig.baseRenderer.transform.parent, false);
                opening.localPosition = Vector3.up * 0.055f;
                opening.localRotation = Quaternion.Euler(90, 0, 0);
                opening.localScale = Vector3.one * 3.87f;
            }
            if (rig.count)
            {
                if (font)
                {
                    rig.count.font = font;
                    rig.count.fontSharedMaterial = font.material;
                }
                rig.count.fontStyle = FontStyles.Bold;
                rig.count.rectTransform.sizeDelta = new Vector2(2.8f, 2.3f);
                rig.count.enableAutoSizing = true;
                rig.count.fontSizeMin = 8;
                rig.count.fontSizeMax = 17;
                rig.count.fontSize = 17;
            }
            return Save(go, "HoleOutHole").GetComponent<HoleRig>();
        }

        static GameObject BuildClean(string source, string name, string[] drop, System.Action<GameObject> configure)
        {
            var go = BuildCleanInstance(source, drop);
            configure?.Invoke(go);
            return Save(go, name);
        }

        /// <summary>Bản sao prefab đã bỏ script gameplay cũ, collider và các nhánh không dùng.</summary>
        static GameObject BuildCleanInstance(string source, string[] drop)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            var go = (GameObject)Object.Instantiate(prefab);
            go.name = prefab.name;
            foreach (var t in go.GetComponentsInChildren<Transform>(true).ToArray())
                if (t != null && t != go.transform && drop.Contains(t.name.Trim())) Object.DestroyImmediate(t.gameObject);

            // Prefab nguồn còn giữ chỗ cho script gameplay đã gỡ khỏi dự án.
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            for (int pass = 0; pass < 4; pass++)
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || KeepBehaviour(mb)) continue;
                    Object.DestroyImmediate(mb);
                }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        /// <summary>Giữ component Unity/TMP và HoleOut (gồm bộ nhận animation event); bỏ script khác.</summary>
        static bool KeepBehaviour(MonoBehaviour mb)
        {
            string asm = mb.GetType().Assembly.GetName().Name;
            return asm.StartsWith("Unity") || asm.StartsWith("HoleOut");
        }

        static GameObject Save(GameObject go, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        /// <summary>
        /// Đọc material theo màu từ bảng màu nguồn. Không còn bảng nguồn thì giữ nguyên
        /// danh sách màu đang có trong HoleOutVisuals.
        /// </summary>
        static List<VisualsSO.ColorVisual> ReadColors(List<VisualsSO.ColorVisual> current)
        {
            var result = new List<VisualsSO.ColorVisual>();
            var asset = AssetDatabase.LoadMainAssetAtPath(ColorSoPath);
            if (asset == null)
            {
                Debug.Log("[HoleOut] Không có " + ColorSoPath + ", giữ màu hiện có trong HoleOutVisuals.");
                return current ?? result;
            }
            var so = new SerializedObject(asset);
            var colors = so.FindProperty("colors");
            var byType = new Dictionary<int, SerializedProperty>();
            for (int i = 0; i < colors.arraySize; i++)
            {
                var e = colors.GetArrayElementAtIndex(i);
                byType[e.FindPropertyRelative("_colorType").intValue] = e;
            }

            foreach (var swatch in PaletteSO.Standard)
            {
                string code = swatch.code;
                int type = -1;
                foreach (var (c, t) in ColorMap) if (c == code) type = t;
                bool missing = !byType.TryGetValue(type, out var e);
                // Màu chưa có trong ColorSO: dựng từ màu xám rồi tô lại.
                if (missing && !byType.TryGetValue(1, out e)) { Debug.LogWarning($"[HoleOut] ColorSO thiếu màu cho '{code}'."); continue; }
                var v = new VisualsSO.ColorVisual
                {
                    code = code,
                    color = PaletteSO.FromHex(swatch.body),
                    agent = e.FindPropertyRelative("_material").objectReferenceValue as Material,
                    seated = e.FindPropertyRelative("_disableMaterial").objectReferenceValue as Material,
                    hole = e.FindPropertyRelative("_holeMaterial").objectReferenceValue as Material,
                    holeStencil = e.FindPropertyRelative("_stencileMaterial").objectReferenceValue as Material,
                    holeDoors = Materials(e.FindPropertyRelative("_holeMaterials")),
                    holeCylinder = Materials(e.FindPropertyRelative("_holeCylinderdMaterials")),
                };
                // Màu không có trong bảng của dự án: tạo biến thể tô lại bằng màu model 3D của bộ màu chuẩn.
                if (code == "D" || missing)
                {
                    var model = PaletteSO.FromHex(swatch.model);
                    v.agent = Tinted(v.agent, $"HoleOut_{code}_Body", model);
                    v.seated = Tinted(v.seated, $"HoleOut_{code}_Seated", model * 0.8f);
                    v.hole = Tinted(v.hole, $"HoleOut_{code}_Hole", model);
                }
                result.Add(v);
            }
            return result;
        }

        [MenuItem("Tools/HoleOut/Build Picture Cell Materials")]
        static void ApplyPictureCellsMenu()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<VisualsSO>(VisualsPath);
            if (visuals == null) { Debug.LogError("[HoleOut] Chưa có " + VisualsPath + ", chạy Build Visuals trước."); return; }
            ApplyPictureCells(visuals);
            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HoleOut] Ô tranh dạng khối cho {visuals.colors.Count} màu.");
        }

        /// <summary>
        /// Ô tranh, nhân vật và khối trên bàn của mọi màn dùng shader khối "HoleOut/Picture Cell":
        /// mỗi màu có material riêng, sắc độ lấy từ bộ màu chuẩn (mặt trước dùng màu viền tối).
        /// </summary>
        public static void ApplyPictureCells(VisualsSO visuals)
        {
            var shader = Shader.Find("HoleOut/Picture Cell");
            if (shader == null) { Debug.LogError("[HoleOut] Thiếu shader HoleOut/Picture Cell."); return; }
            EnsureFolder(MaterialFolder);
            visuals.useSeatedMaterial = true;
            visuals.pictureCellFill = PictureCellFill;
            visuals.pictureCellHeight = PictureCellHeight;
            visuals.pictureCellTilt = PictureCellTilt;
            foreach (var look in visuals.colors)
            {
                if (!PaletteSO.TryGetSwatch(look.code, out var swatch))
                { Debug.LogWarning($"[HoleOut] Mã màu '{look.code}' không có trong bộ màu chuẩn."); continue; }
                string path = $"{MaterialFolder}/HoleOut_{look.code}_Picture.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                ApplySwatch(m, swatch);
                EditorUtility.SetDirty(m);
                look.seated = m;
            }
        }

        /// <summary>Kích thước ô tranh dạng khối, dùng chung mọi màn.</summary>
        public const float PictureCellFill = 1.08f, PictureCellHeight = 0.95f, PictureCellTilt = 0f;

        /// <summary>Độ sáng mặt trước ô tranh so với màu viền; tối hơn để các hàng tách rõ.</summary>
        public const float PictureFrontShade = 0.72f;

        /// <summary>Gán màu mặt trên, vạch sáng, viền và mặt trước của shader ô tranh từ một màu chuẩn.</summary>
        public static void ApplySwatch(Material m, PaletteSO.Swatch swatch)
        {
            var edge = PaletteSO.FromHex(swatch.edge);
            m.SetColor("_BaseColor", PaletteSO.FromHex(swatch.body));
            m.SetColor("_HighlightColor", PaletteSO.FromHex(swatch.highlight));
            m.SetColor("_EdgeColor", edge);
            var front = edge * PictureFrontShade;
            front.a = 1;
            m.SetColor("_FrontColor", front);
        }

        static List<Material> Materials(SerializedProperty list)
        {
            var r = new List<Material>();
            if (list == null) return r;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Material m) r.Add(m);
            return r;
        }

        static Material Tinted(Material source, string name, Color color)
        {
            if (source == null) return null;
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(source); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(source);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LitMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.15f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void AssignToOpenScene(VisualsSO visuals)
        {
            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null) return;
            var so = new SerializedObject(controller);
            so.FindProperty("visuals").objectReferenceValue = visuals;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            EditorSceneManager.SaveScene(controller.gameObject.scene);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
