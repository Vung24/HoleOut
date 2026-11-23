using System;
using System.Collections.Generic;
using System.IO;
using HoleOut.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoleOut.EditorTools
{
    public static class BoardBlockPrefabBuilder
    {
        public const string PrefabPath = "Assets/03prefab/HoleOut/BoardBlock.prefab";
        const string Art = "Assets/04art/BoardArtKit/Textures/Blocks/Blue/";
        const string ResourcesPath = "Assets/04art/BoardBlock";

        // Mảnh cạnh của art kit: 32px ứng với EdgeTile trên khối 1 ô = 1 đơn vị.
        const float EdgeTile = 0.24f;
        // Sprite bóng là viền đáy thân khối dịch lên 7px; hạ thêm 16px (nửa mảnh) để lộ mặt bên dày ~9px.
        const float ShadowDrop = EdgeTile * 0.5f;

        static readonly string[] PartFiles =
        {
            "ConvexUpLeft1", "LinearXUp2", "ConvexUpRight3",
            "LinearYLeft4", "Close5", "LinearYRight6",
            "ConvexDownLeft7", "LinearXDown8", "ConvexDownRight9",
            "ConcaveUpLeft13", "ConcaveUpRight14", "ConcaveDownLeft17", "ConcaveDownRight18",
            "ConvexDownLeftShadow22", "LinearXShadow23", "ConvexDownRightShadow24",
        };

        /// <summary>Tên file sprite trong BoardArtKit/Textures/Blocks/&lt;Màu&gt;/ ứng với mảnh.</summary>
        public static string PartFile(BoardBlockPart part) => PartFiles[(int)part];
        public static int PartCount => PartFiles.Length;

        [MenuItem("Tools/HoleOut/Build Board Blocks")]
        public static void BuildShapes()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<VisualsSO>(VisualsBuilder.VisualsPath);
            if (!visuals)
            {
                Debug.LogError("[BoardBlock] Thiếu HoleOutVisuals.");
                return;
            }

            var rectangles = new List<BoardBlockRig>();
            foreach (var shape in new[] { "1x1", "1x2", "2x1", "2x2", "1x3" })
            {
                rectangles.Add(LoadShape(shape));
            }
            visuals.boardBlocks = rectangles;
            visuals.lBoardBlock = LoadShape("L");
            visuals.tBoardBlock = LoadOrBuild("T", TCells);
            visuals.plusBoardBlock = LoadOrBuild("Plus", PlusCells);
            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();
        }

        // Ô theo trục z hướng lên (hàng trên của bàn có y lớn nhất), khớp cách dựng khối L.
        static readonly Vector2Int[] TCells = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) };
        static readonly Vector2Int[] PlusCells = { new Vector2Int(1, 2), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 0) };

        static BoardBlockRig LoadOrBuild(string shapeName, Vector2Int[] cells) =>
            AssetDatabase.LoadAssetAtPath<BoardBlockRig>(ShapePath(shapeName)) ?? BuildShape(shapeName, cells);

        static BoardBlockRig LoadShape(string shapeName)
        {
            string path = ShapePath(shapeName);
            var prefab = AssetDatabase.LoadAssetAtPath<BoardBlockRig>(path);
            if (!prefab) throw new InvalidOperationException($"Thiếu prefab khối tại {path}.");
            return prefab;
        }

        static string ShapePath(string shapeName) => "Assets/03prefab/HoleOut/BoardBlock_" + shapeName + ".prefab";

        /// <summary>
        /// Tạo prefab khối mới theo các ô nối cạnh; mỗi ô một đơn vị.
        /// Hố và số lấy từ BoardBlock.prefab để chỉnh vị trí/kiểu chữ ở một nơi.
        /// </summary>
        public static BoardBlockRig BuildShape(string shapeName, Vector2Int[] cells)
        {
            if (string.IsNullOrWhiteSpace(shapeName) || shapeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Tên hình không hợp lệ.", nameof(shapeName));
            if (cells == null || cells.Length == 0) throw new ArgumentException("Hình phải có ít nhất một ô.", nameof(cells));
            var occupied = new HashSet<Vector2Int>(cells);
            if (occupied.Count != cells.Length) throw new ArgumentException("Hình có ô trùng nhau.", nameof(cells));
            var visited = new HashSet<Vector2Int> { cells[0] };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(cells[0]);
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var direction in directions)
                    if (occupied.Contains(cell + direction) && visited.Add(cell + direction)) pending.Enqueue(cell + direction);
            }
            if (visited.Count != cells.Length) throw new ArgumentException("Các ô phải nối với nhau qua cạnh.", nameof(cells));
            string path = ShapePath(shapeName);
            if (AssetDatabase.LoadMainAssetAtPath(path))
                throw new InvalidOperationException($"Prefab đã tồn tại tại {path}.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!source) throw new InvalidOperationException("Hãy tạo BoardBlock.prefab trước.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ResourcesPath + "/BoardBlockSprites.mat");
            if (!material) throw new InvalidOperationException("Thiếu material BoardBlockSprites.");
            var root = UnityEngine.Object.Instantiate(source);
            root.name = "BoardBlock_" + shapeName;
            try
            {
                foreach (string group in new[] { "Body", "BottomEdge", "Studs" })
                {
                    var old = root.transform.Find(group);
                    if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                foreach (var collider in root.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var old in root.GetComponents<BoardBlockRig>()) UnityEngine.Object.DestroyImmediate(old);
                var visual = Child(root.transform, "Visual");
                var body = Child(visual, "Body");
                var shadow = Child(visual, "BottomEdge");
                var parts = new List<SpriteRenderer>();
                var kinds = new List<BoardBlockPart>();
                void Part(Transform parent, BoardBlockPart kind, Vector3 position, float width, float height, int order, string partName)
                {
                    parts.Add(SpritePart(parent, PartFile(kind), position, width, height, order, material, partName));
                    kinds.Add(kind);
                }
                var min = cells[0];
                var max = cells[0];
                foreach (var cell in cells) { min = Vector2Int.Min(min, cell); max = Vector2Int.Max(max, cell); }
                var center = (Vector2)(min + max) * 0.5f;
                // Hố và chữ cùng nằm trong một ô thật, kể cả hình có tâm nằm ngoài thân.
                var anchor = cells[0];
                foreach (var cell in cells)
                    if (cell.y > anchor.y || (cell.y == anchor.y && cell.x < anchor.x)) anchor = cell;
                var anchorPosition = new Vector3(anchor.x - center.x, 0, anchor.y - center.y);
                var hole = root.transform.Find("HoleRoot");
                var holeMaterial = GetHoleMaterial();
                foreach (var renderer in hole.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterial = holeMaterial;
                hole.SetParent(visual, false);
                hole.localPosition = anchorPosition + new Vector3(0, 0.05f, 0.14f);
                hole.localScale = Vector3.one * 0.72f;
                var count = root.transform.Find("Count").GetComponent<TMP_Text>();
                count.transform.SetParent(visual, false);
                count.transform.localPosition = anchorPosition + new Vector3(0, 0.20f, -0.30f);
                count.fontSize = 3;
                count.rectTransform.sizeDelta = new Vector2(0.72f, 0.28f);
                foreach (var cell in cells)
                {
                    bool left = occupied.Contains(cell + Vector2Int.left), right = occupied.Contains(cell + Vector2Int.right);
                    bool down = occupied.Contains(cell + Vector2Int.down), up = occupied.Contains(cell + Vector2Int.up);
                    float x0 = left ? -0.5f : -0.46f, x1 = right ? 0.5f : 0.46f;
                    float z0 = down ? -0.5f : -0.46f, z1 = up ? 0.5f : 0.46f;
                    float[] xs = { x0, x0 + EdgeTile, x1 - EdgeTile, x1 };
                    float[] zs = { z0, z0 + EdgeTile, z1 - EdgeTile, z1 };
                    var origin = new Vector3(cell.x - center.x, 0, cell.y - center.y);
                    var cellBody = Child(body, "Cell_" + cell.x + "_" + cell.y);
                    for (int z = 0; z < 3; z++)
                        for (int x = 0; x < 3; x++)
                        {
                            var part = BoardBlockPart.Close;
                            bool edgeX = x == 0 ? !left : x == 2 && !right;
                            bool edgeZ = z == 0 ? !down : z == 2 && !up;
                            if (edgeX && edgeZ) part = z == 0 ? (x == 0 ? BoardBlockPart.ConvexDownLeft : BoardBlockPart.ConvexDownRight) : (x == 0 ? BoardBlockPart.ConvexUpLeft : BoardBlockPart.ConvexUpRight);
                            else if (edgeX) part = x == 0 ? BoardBlockPart.LinearYLeft : BoardBlockPart.LinearYRight;
                            else if (edgeZ) part = z == 0 ? BoardBlockPart.LinearXDown : BoardBlockPart.LinearXUp;
                            else if (x != 1 && z != 1 && !occupied.Contains(cell + new Vector2Int(x == 0 ? -1 : 1, z == 0 ? -1 : 1)))
                                part = z == 0 ? (x == 0 ? BoardBlockPart.ConcaveDownLeft : BoardBlockPart.ConcaveDownRight) : (x == 0 ? BoardBlockPart.ConcaveUpLeft : BoardBlockPart.ConcaveUpRight);
                            Part(cellBody, part, origin + new Vector3((xs[x] + xs[x + 1]) * 0.5f, 0.03f, (zs[z] + zs[z + 1]) * 0.5f), xs[x + 1] - xs[x], zs[z + 1] - zs[z], 0, "Part_" + x + "_" + z);
                        }
                    if (!down)
                    {
                        // Mảnh bóng cùng tỉ lệ pixel với mảnh cạnh đáy để góc bo trùng khớp.
                        BoardBlockPart[] edges = { left ? BoardBlockPart.ShadowX : BoardBlockPart.ShadowDownLeft, BoardBlockPart.ShadowX, right ? BoardBlockPart.ShadowX : BoardBlockPart.ShadowDownRight };
                        float shadowZ = z0 + EdgeTile * 0.5f - ShadowDrop;
                        for (int x = 0; x < 3; x++)
                            Part(shadow, edges[x], origin + new Vector3((xs[x] + xs[x + 1]) * 0.5f, 0.01f, shadowZ), xs[x + 1] - xs[x], EdgeTile, -1, "Edge_" + cell.x + "_" + cell.y + "_" + x);
                    }
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = origin + new Vector3(0, -0.07f, 0);
                    collider.size = new Vector3(x1 - x0, 0.28f, z1 - z0);
                    collider.center += new Vector3((x0 + x1) * 0.5f, 0, (z0 + z1) * 0.5f);
                }
                var rig = root.AddComponent<BoardBlockRig>();
                rig.size = max - min + Vector2Int.one;
                rig.visual = visual;
                rig.parts = parts.ToArray();
                rig.kinds = kinds.ToArray();
                rig.rimRenderers = new Renderer[] { hole.Find("OuterWall").GetComponent<Renderer>(), hole.Find("Rim").GetComponent<Renderer>() };
                rig.interiorRenderers = new Renderer[] { hole.Find("InnerWall").GetComponent<Renderer>(), hole.Find("Basin").GetComponent<Renderer>() };
                rig.label = count;
                var solidMaterial = AssetDatabase.LoadAssetAtPath<Material>(ResourcesPath + "/BoardBlockSolid.mat");
                if (!solidMaterial) throw new InvalidOperationException("Thiếu material BoardBlockSolid.");
                rig.BuildSolidGeometry(hole, solidMaterial);
                var mesh = rig.solid.sharedMesh;
                mesh.hideFlags = HideFlags.None;
                string meshPath = ResourcesPath + "/BoardBlock_" + shapeName + "Body.asset";
                var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (savedMesh)
                {
                    // Ghi lại dữ liệu vào asset cũ để giữ GUID; CopySerialized không làm mới buffer GPU khi đổi số tam giác.
                    savedMesh.Clear();
                    savedMesh.name = Path.GetFileNameWithoutExtension(meshPath);
                    savedMesh.SetVertices(mesh.vertices);
                    savedMesh.SetColors(mesh.colors);
                    savedMesh.SetTriangles(mesh.triangles, 0);
                    savedMesh.normals = mesh.normals;
                    savedMesh.RecalculateBounds();
                    EditorUtility.SetDirty(savedMesh);
                    rig.solid.sharedMesh = savedMesh;
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BoardBlockRig>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Tools/HoleOut/Create Board Block Prefab")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
                throw new InvalidOperationException("BoardBlock.prefab đã tồn tại. Đổi tên hoặc di chuyển prefab trước khi tạo mẫu mới.");
            Directory.CreateDirectory(ResourcesPath);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            var shader = Shader.Find("Sprites/Default");
            if (!shader) throw new InvalidOperationException("Không tìm thấy shader Sprites/Default.");
            var material = new Material(shader) { name = "BoardBlockSprites" };
            AssetDatabase.CreateAsset(material, ResourcesPath + "/BoardBlockSprites.mat");
            var root = new GameObject("BoardBlock");
            try
            {
                var body = Child(root.transform, "Body");
                string[] parts = { "ConvexDownLeft7", "LinearXDown8", "ConvexDownRight9",
                    "LinearYLeft4", "Close5", "LinearYRight6",
                    "ConvexUpLeft1", "LinearXUp2", "ConvexUpRight3" };
                float[] pos = { -0.82f, 0, 0.82f };
                float[] size = { 0.28f, 1.36f, 0.28f };
                for (int z = 0; z < 3; z++)
                    for (int x = 0; x < 3; x++)
                        SpritePart(body, parts[z * 3 + x], new Vector3(pos[x], 0.03f, pos[z]), size[x], size[z], 0, material);

                var shadow = Child(root.transform, "BottomEdge");
                string[] shadows = { "ConvexDownLeftShadow22", "LinearXShadow23", "ConvexDownRightShadow24" };
                for (int x = 0; x < 3; x++)
                    SpritePart(shadow, shadows[x], new Vector3(pos[x], 0.01f, pos[0] - size[0] * 0.5f), size[x], size[0], -1, material);

                var hole = Child(root.transform, "HoleRoot");
                hole.localPosition = new Vector3(-0.43f, 0.05f, 0.43f);
                const float rimRadius = BoardBlockRig.HoleRimRadius;
                const float openingRadius = BoardBlockRig.HoleOpeningRadius;
                const float rimHeight = BoardBlockRig.HoleRimHeight;
                var holeMaterial = GetHoleMaterial();
                // Thành ngoài và vành dùng sắc xám; rig tô theo màu khối.
                Ring(hole, "OuterWall", rimRadius, rimRadius, -0.15f, rimHeight,
                    new Color(0.62f, 0.62f, 0.62f), new Color(0.86f, 0.86f, 0.86f), holeMaterial, 16);
                Ring(hole, "Rim", rimRadius, openingRadius, rimHeight, rimHeight,
                    new Color(0.92f, 0.92f, 0.92f), Color.white, holeMaterial, 20);
                Ring(hole, "InnerWall", openingRadius, openingRadius, rimHeight, -0.10f,
                    new Color(0.03f, 0.18f, 0.28f), new Color(0.05f, 0.35f, 0.47f), holeMaterial, 19);
                Ring(hole, "Basin", openingRadius, 0, -0.10f, -0.10f,
                    new Color(0.015f, 0.07f, 0.12f), new Color(0.02f, 0.13f, 0.19f), holeMaterial, 18);

                var countRoot = Child(root.transform, "Count");
                countRoot.localPosition = new Vector3(0.43f, 0.20f, 0.43f);
                countRoot.localRotation = Quaternion.Euler(90, 0, 0);
                var count = countRoot.gameObject.AddComponent<TextMeshPro>();
                var visuals = AssetDatabase.LoadAssetAtPath<VisualsSO>("Assets/07data/HoleOutVisuals.asset");
                if (visuals && visuals.font) count.font = visuals.font;
                count.text = "23";
                count.fontSize = 5;
                count.fontStyle = FontStyles.Bold;
                count.color = Color.white;
                count.alignment = TextAlignmentOptions.Center;
                count.rectTransform.sizeDelta = new Vector2(0.74f, 0.50f);
                count.GetComponent<MeshRenderer>().sortingOrder = 30;

                var collider = root.AddComponent<BoxCollider>();
                collider.size = new Vector3(1.92f, 0.28f, 1.92f);
                collider.center = new Vector3(0, -0.07f, 0);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);
                Debug.Log("[BoardBlock] Đã tạo prefab block 2×2 có hole riêng và số đếm.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static Material GetHoleMaterial()
        {
            const string path = ResourcesPath + "/BoardBlockHole.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var shader = Resources.Load<Shader>("HoleOutBlockHole");
            if (!shader) throw new InvalidOperationException("Thiếu shader Resources/HoleOutBlockHole.");
            // Lòng hố và vành cùng ghi độ sâu để lớp bên trong không xuyên qua thành ngoài.
            material = new Material(shader) { name = "BoardBlockHole" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static SpriteRenderer SpritePart(Transform parent, string asset, Vector3 position, float width, float height, int order, Material material, string name = null)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + asset + ".png");
            if (!sprite) throw new InvalidOperationException("Thiếu sprite: " + Art + asset + ".png");
            var t = Child(parent, name ?? asset);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(90, 0, 0);
            t.localScale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
            var r = t.gameObject.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sharedMaterial = material;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static void Ring(Transform parent, string name, float radiusA, float radiusB, float yA, float yB, Color colorA, Color colorB, Material material, int order)
        {
            const int segments = 64;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices.Add(direction * radiusA + Vector3.up * yA);
                vertices.Add(direction * radiusB + Vector3.up * yB);
                colors.Add(colorA); colors.Add(colorB);
                if (i == segments) continue;
                int v = i * 2;
                triangles.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = "BoardBlock" + name };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.uv = new Vector2[vertices.Count];
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, ResourcesPath + "/" + mesh.name + ".asset");
            var t = Child(parent, name);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public static string RenderPreview()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D image = null;
            var previous = RenderTexture.active;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (!prefab) throw new InvalidOperationException("Chưa có prefab block.");
                var go = UnityEngine.Object.Instantiate(prefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                foreach (var text in go.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                var cameraGo = new GameObject("PreviewCamera", typeof(Camera));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.GetComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 6, -1.3f);
                camera.transform.LookAt(new Vector3(0, 0, 0), Vector3.forward);
                camera.orthographic = true;
                camera.orthographicSize = 1.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.66f, 0.71f, 0.86f);
                camera.cullingMask = 1 << 31;
                camera.nearClipPlane = 0.01f;
                var lightGo = new GameObject("PreviewLight", typeof(Light));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
                lightGo.GetComponent<Light>().type = LightType.Directional;
                target = RenderTexture.GetTemporary(900, 900, 24);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(900, 900, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Captures");
                string path = Path.GetFullPath("Captures/board-block-prefab.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                return path;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target) RenderTexture.ReleaseTemporary(target);
                if (image) UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
