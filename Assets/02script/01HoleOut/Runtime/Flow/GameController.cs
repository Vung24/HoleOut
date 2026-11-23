using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Điểm vào của scene chơi: nạp màn, chạy vòng lặp mô phỏng → view → HUD mỗi frame và nối các phần:
    /// <see cref="BoardInput"/> (kéo khối), <see cref="BoosterController"/> (booster),
    /// <see cref="CameraFitter"/> (căn camera). Luật chơi nằm ở <see cref="GameSimulation"/>.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Tooltip("Thứ tự màn của chiến dịch (dùng chung với Home và Level Editor).")]
        [SerializeField] CampaignSO campaign;
        // Bản sao chạy của danh sách màn; chơi thử từ Level Editor có thể chèn thêm mà không sửa asset.
        List<LevelSO> levels = new List<LevelSO>();
        [SerializeField] int startLevel;
        [Tooltip("Tên scene Home để quay về từ Cài đặt.")]
        [SerializeField] string homeScene = "Home";
        [SerializeField] VisualsSO visuals;
        [SerializeField] Camera viewCamera;
        [SerializeField] int seed = 1;
        [Tooltip("Giới hạn bước thời gian mỗi frame (giây).")]
        [SerializeField] float maxStep = 0.05f;
        [Tooltip("Góc nghiêng camera (độ).")]
        [SerializeField] float cameraPitch = 55f;
        [SerializeField, Range(0, 1), Tooltip("Mức dựng bàn khối về phía camera: 0 nằm phẳng như tranh, 1 thẳng mặt camera.")]
        float boardUpright = 1f;
        [Tooltip("Tốc độ tối đa khối đuổi theo tay khi kéo (ô/giây). Khi hết vướng, khối trượt tới tay thay vì nhảy ngay.")]
        [SerializeField, Min(1)] float dragFollowSpeed = 40f;
        [Tooltip("Chờ sau khi thắng rồi mới hiện bảng, để hiệu ứng hố cuối diễn xong (giây).")]
        [SerializeField, Min(0)] float winPanelDelay = 0.8f;
        [Tooltip("Cấu hình coin thưởng và giá booster. Trống thì bảng thắng chỉ có Màn tiếp/Chơi lại.")]
        [SerializeField] EconomySO economy;
        [Tooltip("Màn mở khóa và số lượt tặng ban đầu của từng booster.")]
        [SerializeField] List<BoosterSettings> boosters = new List<BoosterSettings>
        {
            new BoosterSettings(BoosterType.Lift, 4, 3),
            new BoosterSettings(BoosterType.ExtraSlot, 5, 3),
            new BoosterSettings(BoosterType.Magnet, 7, 3),
        };
        [SerializeField] GameHud _hud;
        [Tooltip("Âm thanh và rung theo sự kiện (tùy chọn).")]
        [SerializeField] GameFeedback _feedback;

        /// <summary>Khóa SessionState: GUID màn Level Editor yêu cầu chơi thử ở lần Play kế tiếp.</summary>
        public const string EditorTestLevelKey = "HoleOut.EditorTestLevel";

        GameSimulation _sim;
        GameView _view;
        VisualsSO _activeVisuals;
        BoardInput _input;
        BoosterController _boosters;
        CameraFitter _cameraFitter;
        int _levelIndex;
        int _runId;

        public GameSimulation Simulation => _sim;
        public LevelSO CurrentLevel => levels.Count > 0 ? levels[_levelIndex] : null;
        public int RunId => _runId;

        void Awake()
        {
            if (visuals == null) { Debug.LogError("[HoleOut] Chưa gán HoleOutVisualsSO (Tools/HoleOut/Build Visuals)."); enabled = false; return; }
            if (!viewCamera || !_hud)
            {
                Debug.LogError("[HoleOut] Chưa gán camera hoặc HUD cho controller.", this);
                enabled = false;
                return;
            }
            _input = new BoardInput(viewCamera, dragFollowSpeed);
            _boosters = new BoosterController(boosters, _hud, _input, viewCamera);
            _cameraFitter = new CameraFitter(viewCamera, _hud);

            _hud.Init();
            _hud.OnPrev = () => LoadLevel(_levelIndex - 1);
            _hud.OnNext = () => LoadLevel(_levelIndex + 1);
            _hud.OnClaim = Claim;
            _boosters.Price = t => economy ? economy.Price(t) : 0;
            _hud.OnRetry = () => LoadLevel(_levelIndex);
            _hud.OnHome = () => SceneLoader.Load(homeScene, visuals ? visuals.backgroundColor : Color.black, _hud.LevelFont);
            _hud.OnContinue = () => { if (_sim.TryContinueWithExtraSlot()) _hud.HidePanel(); };
            _hud.OnBooster = _boosters.Press;
            if (_feedback) _hud.OnButtonClick = _feedback.PlayButtonClick;
            viewCamera.backgroundColor = visuals.backgroundColor;
        }

        public CampaignSO Campaign => campaign;

        /// <summary>Nhận thưởng thắng (x2 qua quảng cáo thưởng) rồi sang màn tiếp.</summary>
        void Claim(bool doubled)
        {
            if (_claiming || !_reward.CanClaim) return;
            _claiming = true;
            int runId = _runId;
            if (!doubled) { Finish(false); return; }
            RewardedAds.ShowRewarded("win_double", watched => { if (runId == _runId) Finish(watched); });

            void Finish(bool asDouble)
            {
                _reward.Claim(asDouble);
                LoadLevel(_levelIndex + 1);
            }
        }

        void Start()
        {
            levels = campaign ? new List<LevelSO>(campaign.levels) : new List<LevelSO>();
#if UNITY_EDITOR
            // Chơi thử từ Level Editor: chỉ áp dụng một lần, danh sách màn trong Play Mode không được lưu lại.
            string testGuid = UnityEditor.SessionState.GetString(EditorTestLevelKey, "");
            if (testGuid.Length > 0)
            {
                UnityEditor.SessionState.EraseString(EditorTestLevelKey);
                var test = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(testGuid));
                if (test != null)
                {
                    int index = levels.IndexOf(test);
                    if (index < 0) { levels.Insert(0, test); index = 0; }
                    LoadLevel(index);
                    return;
                }
            }
#endif
            if (levels.Count == 0) { Debug.LogWarning("[HoleOut] Chưa gán level nào."); return; }
            int first = PlayerProgress.HasSave ? PlayerProgress.ResolveIndex(levels.ConvertAll(l => l ? l.name : "")) : startLevel;
            LoadLevel(Mathf.Clamp(first, 0, levels.Count - 1));
        }

        float _wonAt = -1;
        readonly WinReward _reward = new WinReward(CoinWallet.Shared);
        bool _claiming;
        string _winBody;

        void OnDestroy() => _view?.Dispose();

        public void LoadLevel(int index)
        {
            _levelIndex = ((index % levels.Count) + levels.Count) % levels.Count;
            _runId++;
            _input.Reset();
            var level = levels[_levelIndex];
            var levelVisuals = level.visuals ? level.visuals : visuals;
            if (_activeVisuals != levelVisuals)
            {
                _view?.Dispose();
                _activeVisuals = levelVisuals;
                _view = new GameView(transform, _activeVisuals);
                viewCamera.backgroundColor = _activeVisuals.backgroundColor;
            }
            _sim = new GameSimulation(level.data, seed);
            _reward.Reset();
            _winBody = null;
            _claiming = false;
            _wonAt = -1;
            BoardSpace.SetLean(boardUpright * (90 - cameraPitch), cameraPitch);
            _view.Bind(_sim);
            _boosters.Bind(_sim, _view, _levelIndex + 1);
            // Hướng dẫn màn đầu: gợi ý thao tác tới khi khối đầu tiên ra cổng.
            _boosters.IdleHint = level.tutorialFirstDrag ? "Kéo khối tới cổng cùng màu" : null;
            _hud.HidePanel();
            _hud.SetLevel(_levelIndex + 1);
            _boosters.AnnounceUnlock();
            _hud.SetWaves(_sim.Board.WaveTotal, true);
            UpdateHud();
            _cameraFitter.Fit(_view, cameraPitch);
        }

        void Update()
        {
            if (_sim == null) return;
            HandleInput();
            _sim.Step(Mathf.Min(Time.deltaTime, maxStep));
            _view.ProcessEvents(_sim.Events);
            if (_boosters.IdleHint != null)
                foreach (var e in _sim.Events)
                    if (e.Type == GameEventType.BlockExited) { _boosters.IdleHint = null; break; }
            if (_feedback) _feedback.Process(_sim.Events);
            _sim.ClearEvents();
            _view.Render(Time.time);
            UpdateHud();
        }

        void LateUpdate()
        {
            if (_sim != null && _cameraFitter.ScreenChanged) _cameraFitter.Fit(_view, cameraPitch);
        }

        void UpdateHud()
        {
            _boosters.Refresh();
            if (_sim.Status == GameStatus.Won && !_hud.PanelVisible)
            {
                if (_wonAt < 0)
                {
                    _wonAt = Time.time;
                    // Thắng thì lần mở sau vào thẳng màn kế tiếp.
                    int next = (_levelIndex + 1) % levels.Count;
                    PlayerProgress.SaveNext(next, levels[next] ? levels[next].name : null);
                    int streak = PlayerProgress.WinStreak + 1;
                    PlayerProgress.WinStreak = streak;                    if (economy)
                    {
                        int streakBonus = economy.StreakBonus(streak), themeBonus = economy.ThemeBonus(_levelIndex + 1);
                        _reward.Prepare(economy.WinCoins(CurrentLevel.targetTier) + streakBonus + themeBonus, economy.doubleMultiplier);
                        _winBody = themeBonus > 0 ? $"Hoàn thành album! +{themeBonus} coin"
                            : streakBonus > 0 ? $"Chuỗi thắng {streak}: +{streakBonus} coin" : null;
                    }
                }
                if (Time.time - _wonAt >= winPanelDelay) _hud.ShowWon(_reward.Coins, _reward.DoubledCoins, _winBody);
            }
            else if (_sim.Status == GameStatus.Jammed && !_hud.PanelVisible)
            {
                PlayerProgress.WinStreak = 0;
                _hud.ShowJammed(_sim.Dock.CanAddSlot);
            }
        }

        void HandleInput()
        {
            // Khoá thao tác khi nhân vật còn đang chạy vào tranh hoặc có bảng che bàn chơi.
            if (!_boosters.BoardInputOpen)
            {
                if (_input.IsPressing) { _input.Cancel(_sim); _boosters.CancelPending(); }
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null) return;
            var screen = pointer.position.ReadValue();
            if (_boosters.HasPending)
            {
                if (pointer.press.wasPressedThisFrame && !OverUi()) _boosters.HandleTarget(screen);
                return;
            }
            _input.Tick(_sim, _view, pointer, screen, pointer.press.wasPressedThisFrame && OverUi());
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && _input != null && _input.IsPressing) { _input.Cancel(_sim); _boosters.CancelPending(); }
        }

        static bool OverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
