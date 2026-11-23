using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HoleOut.Tests
{
    public class Artwork4017Tests
    {
        const string Path = "Assets/07data/HoleOutTestLevels/holeout-artwork-4017.asset";

        [Test]
        public void Character_KeepsPictureMaterialAcrossPosesAndPoolReuse()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<LevelSO>(Path).visuals;
            var agent = Object.Instantiate(visuals.agentPrefab);
            try
            {
                foreach (var look in visuals.colors)
                {
                    agent.Spawn(visuals.GetCharacterMaterial(look.code));
                    Assert.AreSame(look.seated, agent.body.sharedMaterials[0], look.code + " standing");
                    agent.Run();
                    Assert.AreSame(look.seated, agent.body.sharedMaterials[0], look.code + " running");
                    agent.Jump(1);
                    Assert.AreSame(look.seated, agent.body.sharedMaterials[0], look.code + " jumping");
                    agent.Despawn();
                }
            }
            finally { Object.DestroyImmediate(agent.gameObject); }
        }

        [Test]
        public void RunnerLanding_LeavesClearanceForAnimatedBody()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<VisualsSO>("Assets/07data/HoleOutVisuals.asset");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/05animation/AnimationClip/Armature_Jump.anim");
            var agent = Object.Instantiate(visuals.agentPrefab);
            var mesh = new Mesh();
            try
            {
                agent.Spawn(null);
                agent.animator.enabled = false;
                var seated = visuals.agentPrefab.seated;
                float footprint = seated.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * seated.transform.localScale.x;
                float agentScale = (float)DefaultBoardLayout.FixedCell * 0.92f / footprint;
                float bodyRadius = 0;
                for (int frame = 0; frame <= 60; frame++)
                {
                    clip.SampleAnimation(agent.animator.gameObject, clip.length * frame / 60f);
                    agent.body.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        var p = agent.transform.InverseTransformPoint(agent.body.transform.TransformPoint(vertex));
                        bodyRadius = Mathf.Max(bodyRadius, new Vector2(p.x, p.z).magnitude * agentScale);
                    }
                }

                var hole = visuals.holePrefab;
                var layout = new DefaultBoardLayout(30, 30);
                float spacing = (float)Vec2.Distance(layout.DockSlot(0, Dock.MaxSlotsLimit), layout.DockSlot(1, Dock.MaxSlotsLimit));
                float holeScale = spacing * visuals.dockHoleFill / hole.baseRenderer.bounds.size.x;
                float openingRadius = Mathf.Min(hole.stencil.bounds.extents.x, hole.stencil.bounds.extents.z) * holeScale;
                // Giới hạn bảo thủ cho toàn bộ phần hạ xuống: chưa tính thân thu nhỏ ở cuối cú nhảy.
                double remainingTravel = (MotionConfig.LedgeOffset - MotionConfig.RouteOvershoot) * 0.25;
                Assert.Less(MotionConfig.EntryRadius + remainingTravel + bodyRadius, openingRadius,
                    "Làn đáp, đoạn tiến ngang cuối và thân đang nhảy phải nằm lọt trong miệng hố.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(agent.gameObject);
            }
        }

        [Test]
        public void Picture_ContainsAll1600CellsAndSixExactColors()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelSO>(Path);
            Assert.IsNotNull(level);
            Assert.AreEqual(40, level.data.Columns);
            Assert.AreEqual(40, level.data.Rows);
            Assert.AreEqual(PictureDisplayMode.FullPicture, level.data.displayMode);
            Assert.IsTrue(level.visuals.useSeatedMaterial);
            var cells = string.Concat(level.data.grid);
            foreach (var (code, count, hex) in new[]
            {
                ("D", 137, "403F40"), ("G", 71, "74C927"), ("O", 940, "FF8000"),
                ("Y", 215, "F7D205"), ("K", 188, "067F13"), ("C", 49, "E4C891")
            })
            {
                Assert.AreEqual(count, cells.Count(c => c.ToString() == code), code);
                var visual = level.visuals.Get(code);
                Assert.AreEqual(hex, ColorUtility.ToHtmlStringRGB(visual.color), code);
                Assert.AreEqual(hex, ColorUtility.ToHtmlStringRGB(visual.seated.GetColor("_BaseColor")), code);
                Assert.AreEqual("HoleOut/Picture Cell", visual.seated.shader.name);
                Assert.AreNotEqual(AssetDatabase.LoadAssetAtPath<VisualsSO>("Assets/07data/HoleOutVisuals.asset").Get(code)?.agent, visual.agent);
            }
        }

        [Test]
        public void Picture_FitsExistingTrayAtBothCorners()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelSO>(Path);
            var sim = new GameSimulation(level.data);
            var layout = (DefaultBoardLayout)sim.Layout;
            var (min, max) = DefaultBoardLayout.FrameBounds();
            double half = layout.Cell / 2;
            foreach (int col in new[] { 0, 39 })
                foreach (int row in new[] { 0, 39 })
                {
                    var p = layout.CellToWorld(col, row);
                    Assert.GreaterOrEqual(p.X - half, min.X - 1e-6);
                    Assert.LessOrEqual(p.X + half, max.X + 1e-6);
                    Assert.GreaterOrEqual(p.Z - half, min.Z - 1e-6);
                    Assert.LessOrEqual(p.Z + half, max.Z + 1e-6);
                }
        }
    }
}
