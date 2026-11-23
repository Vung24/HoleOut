using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace HoleOut.Runtime
{
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] string gameplayScene;
        [Tooltip("Màu màn mờ khi chuyển sang màn chơi (nên trùng màu nền màn chơi).")]
        [SerializeField] Color fadeColor = new Color(0.133f, 0.173f, 0.416f, 1f);
        [SerializeField] Button playButton;
        [Tooltip("Chữ trên nút Chơi; hiện số màn sẽ vào.")]
        [SerializeField] TMP_Text playLabel;
        [SerializeField] Button instructionsButton;
        [SerializeField] Button closeInstructionsButton;
        [SerializeField] CanvasGroup homeControls;
        [SerializeField] EventSystem eventSystem;
        [SerializeField] GameObject instructions;
        [SerializeField] GameObject settings;
        [SerializeField] Button settingsButton;
        [SerializeField] Button entranceAnimationButton;
        [SerializeField] TMP_Text entranceAnimationValue;
        [SerializeField] Color optionOnColor;
        [SerializeField] Color optionOffColor;

        [Header("Bộ sưu tập")]
        [Tooltip("Chiến dịch dùng chung với màn chơi; tranh đã thắng hiện đủ màu, còn lại là bóng tối.")]
        [SerializeField] CampaignSO campaign;
        [SerializeField] GameObject album;
        [SerializeField] RectTransform albumGrid;
        [SerializeField] Button albumButton;
        [SerializeField] Button closeAlbumButton;
        [Tooltip("Nền ô tranh (bo góc, cùng kiểu dòng trong Cài đặt).")]
        [SerializeField] Sprite albumCellSprite;
        [SerializeField] Color albumCellColor = Color.white;
        [Tooltip("Màu bóng của tranh chưa mở.")]
        [SerializeField] Color albumLockedTint = new Color(0.78f, 0.77f, 0.88f, 1f);
        [Min(0)] [SerializeField] float albumCellPadding = 14f;

        [Header("Quà hằng ngày")]
        [SerializeField] EconomySO economy;
        [SerializeField] GameObject daily;
        [SerializeField] RectTransform dailyGrid;
        [SerializeField] Button dailyClaimButton;
        [SerializeField] TMP_FontAsset dailyFont;
        [SerializeField] Sprite dailyTodaySprite;
        [SerializeField] Sprite dailyCoinIcon;
        [SerializeField] CoinCounter coinCounter;
        [SerializeField] CoinFly dailyCoinFly;
        [SerializeField] Color dailyClaimedColor = new Color(1f, 1f, 1f, 0.45f);

        readonly System.Collections.Generic.List<Texture2D> _albumTextures = new System.Collections.Generic.List<Texture2D>();

        void Start()
        {
            int index = campaign ? PlayerProgress.ResolveIndex(campaign.levels.ConvertAll(l => l ? l.name : "")) : PlayerProgress.LevelIndex;
            if (playLabel) playLabel.text = $"Chơi màn {index + 1}";
            if (DailyAvailable) ShowDaily();
        }

        DailyReward DailyRules => new DailyReward(economy ? economy.dailyCoins.Length : 7);
        static int Today => DailyReward.DayNumber(System.DateTime.Now);
        bool DailyAvailable => economy && daily && economy.dailyCoins.Length > 0 && DailyRules.CanClaim(DailyRewardSave.Load(), Today);

        public void ShowDaily()
        {
            BuildDaily();
            SetDialog(daily, true, dailyClaimButton);
        }

        /// <summary>Nhận quà hôm nay, đóng bảng và phát hiệu ứng coin về bộ đếm.</summary>
        public void ClaimDaily()
        {
            var state = DailyRewardSave.Load();
            if (DailyRules.Claim(ref state, Today, out int index))
            {
                DailyRewardSave.Save(state);
                CoinWallet.Shared.Add(economy.dailyCoins[index]);
                if (dailyCoinFly && coinCounter && dailyClaimButton)
                    dailyCoinFly.Play((RectTransform)dailyClaimButton.transform, coinCounter.Icon);
            }
            SetDialog(daily, false, playButton);
        }

        /// <summary>7 ô theo chu kỳ: đã nhận (mờ, "Đã nhận"), hôm nay (nền nổi bật), sắp tới.</summary>
        void BuildDaily()
        {
            if (!dailyGrid || !economy) return;
            for (int i = dailyGrid.childCount - 1; i >= 0; i--) Destroy(dailyGrid.GetChild(i).gameObject);
            var state = DailyRewardSave.Load();
            var rules = DailyRules;
            int today = rules.TodayIndex(state, Today);
            bool claimedToday = !rules.CanClaim(state, Today);
            for (int i = 0; i < economy.dailyCoins.Length; i++)
            {
                bool claimed = i < today || (i == today && claimedToday);
                bool isToday = i == today && !claimedToday;
                var cell = new GameObject("Day " + (i + 1), typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(dailyGrid, false);
                var bg = cell.GetComponent<Image>();
                bg.sprite = isToday && dailyTodaySprite ? dailyTodaySprite : albumCellSprite;
                bg.type = Image.Type.Sliced;
                var cream = albumCellColor;
                bg.color = isToday ? Color.white : claimed ? new Color(cream.r, cream.g, cream.b, dailyClaimedColor.a) : cream;
                bg.raycastTarget = false;
                AddLabel(cell.transform, "Ngày " + (i + 1), 26, new Vector2(0, 0.72f), new Vector2(1, 0.95f), isToday);
                if (claimed)
                {
                    AddLabel(cell.transform, "Đã nhận", 24, new Vector2(0, 0.18f), new Vector2(1, 0.65f), false);
                    continue;
                }

                if (dailyCoinIcon) AddDailyCoinIcon(cell.transform);
                AddLabel(cell.transform, "+" + economy.dailyCoins[i], 30, new Vector2(0, 0.06f), new Vector2(1, 0.31f), isToday);
            }
        }

        void AddDailyCoinIcon(Transform parent)
        {
            var icon = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)icon.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(56, 56);
            var image = icon.GetComponent<Image>();
            image.sprite = dailyCoinIcon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        void AddLabel(Transform parent, string text, float size, Vector2 min, Vector2 max, bool light)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (dailyFont) t.font = dailyFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = light ? Color.white : new Color(0.18f, 0.165f, 0.44f);
            t.raycastTarget = false;
        }

        public void Play()
        {
            if (!playButton.interactable)
                return;

            playButton.interactable = false;
            if (!SceneLoader.Load(gameplayScene, fadeColor, playLabel ? playLabel.font : null))
                playButton.interactable = true;
        }

        public void ShowInstructions()
        {
            SetDialog(instructions, true, closeInstructionsButton);
        }

        public void HideInstructions()
        {
            SetDialog(instructions, false, instructionsButton);
        }

        public void ShowSettings()
        {
            UpdateEntranceAnimation();
            SetDialog(settings, true, entranceAnimationButton);
        }

        public void HideSettings() => SetDialog(settings, false, settingsButton);

        public void ToggleEntranceAnimation()
        {
            Preferences.EntranceAnimation = !Preferences.EntranceAnimation;
            UpdateEntranceAnimation();
        }

        void UpdateEntranceAnimation()
        {
            bool enabled = Preferences.EntranceAnimation;
            entranceAnimationValue.text = enabled ? "Bật" : "Tắt";
            entranceAnimationButton.image.color = enabled ? optionOnColor : optionOffColor;
        }

        public void ShowAlbum()
        {
            BuildAlbum();
            SetDialog(album, true, closeAlbumButton);
        }

        public void HideAlbum()
        {
            SetDialog(album, false, albumButton);
            ClearAlbum();
        }

        /// <summary>Dựng lưới tranh theo thứ tự chiến dịch; màn có chỉ số nhỏ hơn tiến trình là đã thắng.</summary>
        void BuildAlbum()
        {
            ClearAlbum();
            if (!campaign || !albumGrid) return;
            int won = PlayerProgress.ResolveIndex(campaign.levels.ConvertAll(l => l ? l.name : ""));
            for (int i = 0; i < campaign.levels.Count; i++)
            {
                var level = campaign.levels[i];
                bool unlocked = i < won;
                var texture = level ? PictureThumbnail.Build(level.data, unlocked ? (Color32?)null : albumLockedTint) : null;
                if (texture) _albumTextures.Add(texture);

                var cell = new GameObject("Picture " + (i + 1), typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(albumGrid, false);
                var bg = cell.GetComponent<Image>();
                bg.sprite = albumCellSprite;
                bg.type = Image.Type.Sliced;
                bg.color = albumCellColor;
                bg.raycastTarget = false;
                if (!texture) continue;

                var pic = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
                var rt = (RectTransform)pic.transform;
                rt.SetParent(cell.transform, false);
                // Vừa khít ô trừ lề, giữ tỉ lệ tranh.
                var grid = albumGrid.GetComponent<GridLayoutGroup>();
                float inner = (grid ? Mathf.Min(grid.cellSize.x, grid.cellSize.y) : 150f) - albumCellPadding * 2;
                float aspect = (float)texture.width / texture.height;
                rt.sizeDelta = aspect >= 1 ? new Vector2(inner, inner / aspect) : new Vector2(inner * aspect, inner);
                var raw = pic.GetComponent<RawImage>();
                raw.texture = texture;
                raw.raycastTarget = false;
            }
        }

        void ClearAlbum()
        {
            if (albumGrid)
                for (int i = albumGrid.childCount - 1; i >= 0; i--) Destroy(albumGrid.GetChild(i).gameObject);
            foreach (var t in _albumTextures) if (t) Destroy(t);
            _albumTextures.Clear();
        }

        void OnDestroy() => ClearAlbum();

        void SetDialog(GameObject dialog, bool open, Button focus)
        {
            dialog.SetActive(open);
            homeControls.interactable = !open;
            eventSystem.SetSelectedGameObject(focus.gameObject);
        }
    }
}
