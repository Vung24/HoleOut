using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Coin bay từ một điểm UI về icon bộ đếm: mỗi đồng phồng 1,2→1 rồi bay (ease vào) và nhỏ dần.
    /// </summary>
    public class CoinFly : MonoBehaviour
    {
        [SerializeField] RectTransform layer;
        [SerializeField] Sprite coinSprite;
        [SerializeField] Color coinColor = new Color32(255, 194, 61, 255);
        [Min(1)] [SerializeField] int count = 6;
        [Min(1)] [SerializeField] float size = 64f;
        [Min(0)] [SerializeField] float spread = 70f;
        [Min(0)] [SerializeField] float stagger = 0.05f;
        [Min(0.05f)] [SerializeField] float popTime = 0.26f;
        [Min(0.05f)] [SerializeField] float flightTime = 0.45f;

        public void Play(RectTransform from, RectTransform to)
        {
            if (!layer || !from || !to) return;
            Vector2 start = layer.InverseTransformPoint(from.TransformPoint(from.rect.center));
            Vector2 end = layer.InverseTransformPoint(to.TransformPoint(to.rect.center));
            StartCoroutine(Run(start, end));
        }

        IEnumerator Run(Vector2 start, Vector2 end)
        {
            var coins = new RectTransform[count];
            var offsets = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Coin", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(layer, false);
                rt.anchorMin = rt.anchorMax = layer.pivot;
                rt.sizeDelta = Vector2.one * size;
                var img = go.GetComponent<Image>();
                img.sprite = coinSprite;
                img.color = coinColor;
                img.raycastTarget = false;
                coins[i] = rt;
                offsets[i] = UnityEngine.Random.insideUnitCircle * spread;
            }
            float total = popTime + flightTime + stagger * (count - 1), t0 = Time.unscaledTime;
            while (true)
            {
                float t = Time.unscaledTime - t0;
                for (int i = 0; i < count; i++)
                {
                    float local = t - stagger * i;
                    Vector2 burst = start + offsets[i];
                    if (local < popTime)
                    {
                        float k = Mathf.Clamp01(local / popTime);
                        coins[i].anchoredPosition = Vector2.Lerp(start, burst, 1 - (1 - k) * (1 - k));
                        coins[i].localScale = Vector3.one * (k < 0.6f ? Mathf.Lerp(0, 1.2f, k / 0.6f) : Mathf.Lerp(1.2f, 1f, (k - 0.6f) / 0.4f));
                    }
                    else
                    {
                        float k = Mathf.Clamp01((local - popTime) / flightTime);
                        float e = k * k * k;
                        coins[i].anchoredPosition = Vector2.Lerp(burst, end, e);
                        coins[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, e);
                        if (k >= 1)
                            coins[i].gameObject.SetActive(false);
                    }
                }
                if (t >= total) break;
                yield return null;
            }
            foreach (var c in coins) if (c) Destroy(c.gameObject);
        }
    }
}
