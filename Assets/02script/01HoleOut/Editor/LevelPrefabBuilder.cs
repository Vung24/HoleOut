using System;
using System.Collections.Generic;
using HoleOut.Core;
using HoleOut.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoleOut.EditorTools
{
    /// <summary>
    /// Dựng prefab bàn khối, khối kéo và khay bo góc cho màn chơi từ mesh art có sẵn, gán vào HoleOutVisuals.
    /// Sau khi tạo, chỉnh trực tiếp trên prefab; chạy lại sẽ ghi đè prefab.
    /// </summary>
    public static class LevelPrefabBuilder
    {
        const string PrefabFolder = "Assets/03prefab/HoleOut";
        const string MaterialFolder = "Assets/04art/Material/HoleOut";
        const string MeshFolder = "Assets/04art/Mesh/";

        [MenuItem("Tools/HoleOut/Build Level Prefabs")]
        public static void Build()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<VisualsSO>(VisualsBuilder.VisualsPath);
            if (!visuals) throw new InvalidOperationException("Chưa có " + VisualsBuilder.VisualsPath + ", chạy Build Visuals trước.");
            if (!visuals.agentPrefab || !visuals.agentPrefab.seated)
                throw new InvalidOperationException("HoleOutVisuals thiếu prefab nhân vật ngồi.");

            var selectionMat = SelectionMaterial();

            visuals.boardPrefab = Save(BuildBoard(visuals), "HoleOutBoard").GetComponent<BoardRig>();
            visuals.blockPrefab = Save(BuildBlock(visuals, selectionMat), "HoleOutBlock").GetComponent<BlockRig>();
            visuals.pictureTrayPrefab = Save(BuildTray(), "HoleOutPictureTray").GetComponent<TrayRig>();
            SaveDockTray(visuals);
            if (!visuals.dockSlotPrefab) Debug.LogWarning("[HoleOut] HoleOutVisuals chưa có dockSlotPrefab, chạy Build Visuals.");
            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();
            Debug.Log("[HoleOut] Đã tạo prefab bàn, khối và khay tranh trong " + PrefabFolder);
        }

        static GameObject BuildBoard(VisualsSO visuals)
        {
            var root = new GameObject("HoleOutBoard");
            var rig = root.AddComponent<BoardRig>();
            int width = DragSolver.BoardWidth, height = DragSolver.BoardHeight;
            var center = BoardSpace.BoardToWorld(0, 0, width, height);
            float bc = BoardSpace.BoardCell, m = BoardRig.Margin;
            float frameW = width * bc + m * 2, frameD = height * bc + m * 2;
            float radius = BoardRig.CornerRadius;

            var tray = Group("Tray", root.transform);
            tray.localPosition = center;
            var wallMat = SurfaceMaterial("HoleOutBoardWall", new Color(0.24f, 0.24f, 0.45f), 0.30f);
            var floorMat = SurfaceMaterial("HoleOutBoardCellAlt", new Color(0.11f, 0.13f, 0.26f));
            var baseMat = SurfaceMaterial("HoleOutBoardBase", new Color(0.12f, 0.13f, 0.29f), 0.23f);
            var baseMesh = SavedPanel("HoleOutBoardBase", frameW, frameD, radius, -0.118f, -0.001f, 0.035f);
            MeshPart("Base", tray, baseMesh, baseMat);
            var surface = SavedPanel("HoleOutBoardSurface", frameW - 0.08f, frameD - 0.08f, radius - 0.04f,
                -0.008f, 0.001f, 0.004f);
            MeshPart("Surface", tray, surface, floorMat);

            const float groove = 0.013f;
            var cellMesh = SavedPanel("HoleOutBoardCell", bc - groove, bc - groove, 0.012f, -0.01f, 0.004f, 0.003f);
            // Đường lưới do shader ô vẽ dọc mép (rộng tối thiểu theo pixel), không dựa vào khe hình học.
            var cellMaterial = CellMaterial((bc - groove) * 0.5f);
            var cells = Group("Cells", root.transform);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var cell = MeshPart($"Cell_{x}_{y}", cells, cellMesh, cellMaterial);
                    cell.localPosition = BoardSpace.BoardToWorld(x, y, 1, 1);
                }

            float off = BoardRig.RimOffset;
            var wallMesh = BoardRimMesh("HoleOutBoardWall", bc, RimShape.Wall);
            var gateMaterials = new[] { wallMat, GateMaterial() };
            int maxLen = Mathf.Max(width, height);
            var gateMeshes = new Mesh[maxLen];
            for (int len = 1; len <= maxLen; len++)
                gateMeshes[len - 1] = BoardRimMesh("HoleOutBoardGate" + len, len * bc, RimShape.Gate);

            var wallRoot = Group("Walls", root.transform);
            var gateRoot = Group("Gates", root.transform);
            var rims = new List<MeshFilter>();
            var gateRenderers = new List<MeshRenderer>();
            var walls = new List<GameObject>();
            for (int side = 0; side < 4; side++)
            {
                var gs = (GateSide)side;
                bool horizontal = BoardRig.IsHorizontal(gs);
                float yaw = gs switch { GateSide.Top => 0, GateSide.Bottom => 180, GateSide.Left => -90, _ => 90 };
                var rotation = Quaternion.Euler(0, yaw, 0);
                for (int i = 0; i < BoardRig.SideLength(gs); i++)
                {
                    var pos = horizontal ? BoardSpace.BoardToWorld(i, gs == GateSide.Top ? 0 : height - 1, 1, 1)
                        : BoardSpace.BoardToWorld(gs == GateSide.Left ? 0 : width - 1, i, 1, 1);
                    if (horizontal) pos.z += gs == GateSide.Top ? off : -off; else pos.x += gs == GateSide.Left ? -off : off;

                    var wall = Group($"Wall_{gs}_{i}", wallRoot);
                    wall.localPosition = pos;
                    wall.localRotation = rotation;
                    MeshPart("Body", wall, wallMesh, wallMat);
                    walls.Add(wall.gameObject);

                    var gate = MeshPart($"Gate_{gs}_{i}", gateRoot, gateMeshes[0], wallMat);
                    gate.localPosition = pos;
                    gate.localRotation = rotation;
                    gate.gameObject.SetActive(false);
                    rims.Add(gate.GetComponent<MeshFilter>());
                    var renderer = gate.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = gateMaterials;
                    gateRenderers.Add(renderer);
                }
            }

            float cx = frameW / 2 - radius, cz = frameD / 2 - radius;
            var postMesh = BoardRimMesh("HoleOutBoardPost", bc, RimShape.Corner);
            // Mesh trụ bo góc (+x,+z); xoay cho từng góc: trên-trái, trên-phải, dưới-trái, dưới-phải.
            float[] cornerYaw = { -90, 0, 180, 90 };
            for (int c = 0; c < 4; c++)
            {
                var post = Group($"Corner_{c}", wallRoot);
                post.localPosition = new Vector3(center.x + (c % 2 == 0 ? -cx : cx), 0, center.z + (c < 2 ? cz : -cz));
                post.localRotation = Quaternion.Euler(0, cornerYaw[c], 0);
                MeshPart("Body", post, postMesh, wallMat);
            }

            rig.rim = rims.ToArray();
            rig.gateRenderers = gateRenderers.ToArray();
            rig.gateMeshes = gateMeshes;
            rig.walls = walls.ToArray();
            rig.blocksRoot = Group("Blocks", root.transform);
            rig.ApplyGates(Array.Empty<GateData>(), new ColorPalette(visuals));
            return root;
        }

        static GameObject BuildBlock(VisualsSO visuals, Material selectionMat)
        {
            var src = visuals.agentPrefab.seated;
            var root = new GameObject("HoleOutBlock");
            var rig = root.AddComponent<BlockRig>();

            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0, -0.3f, 0);
            body.transform.localRotation = src.transform.localRotation;
            rig.body = body.GetComponent<MeshFilter>();
            rig.body.sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            rig.bodyRenderer = body.GetComponent<MeshRenderer>();
            rig.bodyRenderer.sharedMaterial = visuals.colors.Count > 0 ? visuals.colors[0].agent : null;

            var line = new GameObject("SelectionOutline").AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false);
            line.sharedMaterial = selectionMat;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.045f;
            line.startColor = line.endColor = Color.white;
            line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            rig.outline = line;

            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0, 0.36f, 0);
            label.transform.localRotation = Quaternion.Euler(90, 0, 0);
            if (visuals.font) label.font = visuals.font;
            label.text = "0";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 6;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            rig.label = label;
            return root;
        }

        [MenuItem("Tools/HoleOut/Refresh Board And Tray")]
        public static void RefreshBoardAndTray()
        {
            var visuals = Load<VisualsSO>(VisualsBuilder.VisualsPath);
            visuals.boardPrefab = Save(BuildBoard(visuals), "HoleOutBoard").GetComponent<BoardRig>();
            visuals.pictureTrayPrefab = Save(BuildTray(), "HoleOutPictureTray").GetComponent<TrayRig>();
            SaveDockTray(visuals);
            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();
        }

        static void SaveDockTray(VisualsSO visuals)
        {
            var root = new GameObject("HoleOutDockTray");
            var rig = root.AddComponent<TrayRig>();
            var rim = SurfaceMaterial("HoleOutDockRim", new Color(0.26f, 0.28f, 0.54f), 0.24f);
            var fill = SurfaceMaterial("HoleOutDockSurface", new Color(0.11f, 0.12f, 0.26f));
            var picture = visuals.pictureTrayPrefab;
            rig.baseLayer = MeshPart("Base", root.transform, picture.baseLayer.sharedMesh, fill).GetComponent<MeshFilter>();
            rig.rimLayer = MeshPart("Rim", root.transform, picture.rimLayer.sharedMesh, rim).GetComponent<MeshFilter>();
            rig.surfaceLayer = MeshPart("Surface", root.transform, picture.surfaceLayer.sharedMesh, fill).GetComponent<MeshFilter>();
            rig.cornerRadius = 0.24f;
            rig.borderWidth = 0.10f;
            var prefab = Save(root, "HoleOutDockTray").GetComponent<TrayRig>();
            visuals.dockTrayPrefab = prefab;
            foreach (string guid in AssetDatabase.FindAssets("t:VisualsSO"))
            {
                var style = AssetDatabase.LoadAssetAtPath<VisualsSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (!style || style == visuals || style.boardPrefab != visuals.boardPrefab || style.dockTrayPrefab) continue;
                style.dockTrayPrefab = prefab;
                EditorUtility.SetDirty(style);
            }
        }

        static GameObject BuildTray()
        {
            var root = new GameObject("HoleOutTray");
            var rig = Shell(root.transform);
            rig.SetBounds(-0.5f, 0.5f, -0.5f, 0.5f);
            PersistShell(rig, "Picture");
            return root;
        }

        static TrayRig Shell(Transform parent)
        {
            var rig = parent.gameObject.AddComponent<TrayRig>();
            rig.baseLayer = MeshPart("Base", parent, null,
                SurfaceMaterial("HoleOutTrayBase", new Color(0.06f, 0.07f, 0.17f))).GetComponent<MeshFilter>();
            rig.rimLayer = MeshPart("Rim", parent, null,
                SurfaceMaterial("HoleOutBoardRim", new Color(0.26f, 0.28f, 0.54f))).GetComponent<MeshFilter>();
            rig.surfaceLayer = MeshPart("Surface", parent, null,
                SurfaceMaterial("HoleOutBoard", new Color(0.09f, 0.10f, 0.22f))).GetComponent<MeshFilter>();
            return rig;
        }

        /// <summary>Tạo material bề mặt nếu chưa có; material đã có giữ nguyên chỉnh sửa trên Inspector.</summary>
        static Material SurfaceMaterial(string name, Color color, float smoothness = 0.12f)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            // HoleOut/Surface thay URP Lit: URP Lit sinh pixel trắng nhấp nháy ngẫu nhiên trên các
            // mặt phẳng này (dữ liệu ánh sáng theo vật thể bị sai); shader riêng chỉ dùng đèn chính + ambient.
            mat = new Material(Shader.Find("HoleOut/Surface"));
            AssetDatabase.CreateAsset(mat, path);
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void PersistShell(TrayRig rig, string prefix)
        {
            foreach (var filter in new[] { rig.baseLayer, rig.rimLayer, rig.surfaceLayer })
                filter.sharedMesh = PersistMesh(filter.sharedMesh, "HoleOut" + prefix + filter.name);
        }

        static Mesh SavedPanel(string name, float width, float depth, float radius, float bottom, float top, float bevel)
        {
            var mesh = TrayRig.Panel(width, depth, radius, bottom, top, bevel, 0);
            var saved = PersistMesh(mesh, name);
            UnityEngine.Object.DestroyImmediate(mesh);
            return saved;
        }

        enum RimShape { Wall, Corner, Gate }

        // Tường, góc và hai đầu cổng có cùng tiết diện để mép nối vừa khít.
        static Mesh BoardRimMesh(string name, float length, RimShape shape)
        {
            const float bevel = 0.065f;
            const float bottom = -0.025f;
            const int bevelSteps = 4;
            float half = BoardRig.WallThickness * 0.5f;
            float top = BoardRig.WallTop;
            float shoulder = top - bevel;
            var profile = new List<Vector2> { new Vector2(-half, bottom) };
            var profileNormals = new List<Vector2> { Vector2.left };
            for (int i = 0; i <= bevelSteps; i++)
            {
                float angle = i * Mathf.PI / (2 * bevelSteps);
                profile.Add(new Vector2(-half + bevel * (1 - Mathf.Cos(angle)), shoulder + bevel * Mathf.Sin(angle)));
                profileNormals.Add(new Vector2(-Mathf.Cos(angle), Mathf.Sin(angle)));
            }
            for (int i = 0; i <= bevelSteps; i++)
            {
                float angle = i * Mathf.PI / (2 * bevelSteps);
                profile.Add(new Vector2(half - bevel * (1 - Mathf.Sin(angle)), shoulder + bevel * Mathf.Cos(angle)));
                profileNormals.Add(new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
            }
            profile.Add(new Vector2(half, bottom));
            profileNormals.Add(Vector2.right);

            bool corner = shape == RimShape.Corner;
            bool gate = shape == RimShape.Gate;
            const float rampStart = 0.032f;
            const float rampEnd = 0.258f;
            const float colorStart = 0.122f;
            float[] rampSamples = { 0, rampStart, 0.072f, colorStart, 0.170f, 0.214f, rampEnd };
            int segments = corner ? 20 : gate ? rampSamples.Length * 2 - 1 : 1;
            int stride = profile.Count;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var plastic = new List<int>();
            var insert = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float fraction = (float)i / segments;
                float angle = (1 - fraction) * Mathf.PI * 0.5f;
                var outward = corner ? new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) : Vector3.forward;
                float x = length * (fraction - 0.5f);
                float heightScale = 1, slope = 0;
                var strip = plastic;
                if (gate)
                {
                    bool firstHalf = i < rampSamples.Length;
                    int index = firstHalf ? i : segments - i;
                    float inset = rampSamples[index];
                    x = (length * 0.5f - inset) * (firstHalf ? -1 : 1);
                    float t = Mathf.InverseLerp(rampStart, rampEnd, inset);
                    float drop = top - BoardRig.GateTop;
                    heightScale -= drop * t * t * (3 - 2 * t) / (top - bottom);
                    slope = drop * (6 * t * (1 - t)) / (rampEnd - rampStart) * (firstHalf ? -1 : 1);
                    // Phần sát tiếp tuyến và má trên là nhựa viền, lòng dốc mang màu cổng.
                    if ((firstHalf && inset > colorStart) || (!firstHalf && inset >= colorStart))
                        strip = insert;
                }
                var center = corner ? outward * (BoardRig.CornerRadius - half)
                    : Vector3.right * x;
                for (int j = 0; j < stride; j++)
                {
                    Vector2 p = profile[j];
                    float h = p.y - bottom;
                    vertices.Add(center + outward * p.x + Vector3.up * (bottom + h * heightScale));
                    Vector2 n = profileNormals[j];
                    var normal = outward * (n.x * heightScale) + Vector3.up * n.y
                        - Vector3.right * (n.y * slope * h / (top - bottom));
                    normals.Add(normal.normalized);
                    uvs.Add(new Vector2(x, p.x));
                    if (i == 0 || j == 0) continue;
                    int a = (i - 1) * stride + j - 1, b = a + stride;
                    strip.Add(a); strip.Add(a + 1); strip.Add(b);
                    strip.Add(a + 1); strip.Add(b + 1); strip.Add(b);
                }
            }

            var mesh = new Mesh { subMeshCount = gate ? 2 : 1 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(plastic, 0);
            if (gate) mesh.SetTriangles(insert, 1);
            var saved = PersistMesh(mesh, name);
            UnityEngine.Object.DestroyImmediate(mesh);
            return saved;
        }

        /// <summary>Material ô bàn; giữ màu đã chỉnh trên Inspector, chỉ cập nhật nửa cạnh ô theo mesh.</summary>
        static Material CellMaterial(float halfSize)
        {
            string path = MaterialFolder + "/HoleOutBoardCell.mat";
            var shader = Shader.Find("HoleOut/Board Cell");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { enableInstancing = true };
                material.SetColor("_BaseColor", new Color(0.15f, 0.16f, 0.30f));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            material.SetFloat("_HalfSize", halfSize);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material GateMaterial()
        {
            string path = MaterialFolder + "/HoleOutBoardGate.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("HoleOut/Board Gate"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Mesh PersistMesh(Mesh mesh, string name)
        {
            string path = MeshFolder + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!saved)
            {
                saved = UnityEngine.Object.Instantiate(mesh);
                AssetDatabase.CreateAsset(saved, path);
            }
            else
            {
                // Cập nhật trực tiếp để Unity nạp lại dữ liệu render mà vẫn giữ liên kết asset.
                saved.Clear(false);
                saved.vertices = mesh.vertices;
                saved.normals = mesh.normals;
                saved.uv = mesh.uv;
                saved.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    saved.SetTriangles(mesh.GetTriangles(i), i, false);
                saved.RecalculateBounds();
            }
            saved.name = name;
            EditorUtility.SetDirty(saved);
            return saved;
        }

        // ───────────── Tiện ích ─────────────

        static GameObject Save(GameObject root, string name)
        {
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Không tìm thấy " + path);
            return asset;
        }

        static Transform MeshPart(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        static Material SelectionMaterial()
        {
            string path = MaterialFolder + "/HoleOutBlockSelection.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = new Material(Shader.Find("Sprites/Default")) { name = "HoleOutBlockSelection" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
