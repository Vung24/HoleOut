using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    /// <summary>Chuyển scene có màn mờ dần ngắn bằng màu nền; dùng chung cho Home và màn chơi.</summary>
    public static class SceneLoader
    {
        const float FadeTime = 0.3f;
        const float MinimumLoadingTime = 0.3f;
        static bool _busy;

        public static bool Load(string scene, Color color, TMP_FontAsset font)
        {
            if (_busy || string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            _busy = true;
            var go = new GameObject("SceneFade");
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2160);
            go.AddComponent<GraphicRaycaster>();
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = true;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)fill.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            color.a = 1;
            fill.GetComponent<Image>().color = color;

            var loading = new GameObject("Loading Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)loading.transform;
            labelRect.SetParent(rt, false);
            labelRect.anchorMin = new Vector2(0.1f, 0.45f);
            labelRect.anchorMax = new Vector2(0.9f, 0.55f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = loading.GetComponent<TextMeshProUGUI>();
            if (font) label.font = font;
            label.fontSize = 40;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            loading.SetActive(false);

            go.AddComponent<Runner>().StartCoroutine(Run(scene, group, label));
            return true;
        }

        static IEnumerator Run(string scene, CanvasGroup group, TMP_Text label)
        {
            yield return Fade(group, 0, 1);
            label.gameObject.SetActive(true);
            label.text = "Đang tải…";
            float start = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(scene);
            while ((op != null && !op.isDone) || Time.unscaledTime - start < MinimumLoadingTime)
                yield return null;
            yield return null;
            yield return Fade(group, 1, 0);
            _busy = false;
            Object.Destroy(group.gameObject);
        }

        static IEnumerator Fade(CanvasGroup group, float from, float to)
        {
            for (float t = 0; t < FadeTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / FadeTime);
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>Chỉ để chạy coroutine trên object màn mờ.</summary>
        sealed class Runner : MonoBehaviour { }
    }
}
