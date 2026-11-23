using System.Linq;
using HoleOut.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoleOut.EditorTools
{
    /// <summary>Tạo scene HoleOut.unity: camera nhìn từ trên, đèn, controller gán bộ level test.</summary>
    public static class SceneSetup
    {
        public const string ScenePath = "Assets/01scene/HoleOut.unity";

        const string HudPrefabPath = "Assets/03prefab/HoleOut/HoleOutHUD.prefab";

        [MenuItem("Tools/HoleOut/Create HoleOut Scene")]
        public static void CreateScene()
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (hudPrefab == null || !hudPrefab.TryGetComponent<GameHud>(out _))
            {
                Debug.LogError($"[HoleOut] Missing HUD prefab at {HudPrefabPath}.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.88f, 0.8f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100;
            camGo.transform.SetPositionAndRotation(new Vector3(0, 30, 0), Quaternion.Euler(90, 0, 0));
            var urpCam = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCam != null) camGo.AddComponent(urpCam);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(60, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.68f);

            var root = new GameObject("HoleOut");
            var controller = root.AddComponent<GameController>();
            var so = new SerializedObject(controller);
            var assets = LevelImporter.LoadAll().ToList();
            var campaign = CampaignAsset.LoadOrCreate();
            if (campaign.levels.Count == 0) { campaign.levels.AddRange(assets); EditorUtility.SetDirty(campaign); AssetDatabase.SaveAssets(); }
            so.FindProperty("campaign").objectReferenceValue = campaign;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("visuals").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualsSO>(VisualsBuilder.VisualsPath);
            var hudObject = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            var hud = hudObject.GetComponent<GameHud>();
            so.FindProperty("_hud").objectReferenceValue = hud;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[HoleOut] Đã tạo {ScenePath} với {assets.Count} level test.");
        }
    }
}
