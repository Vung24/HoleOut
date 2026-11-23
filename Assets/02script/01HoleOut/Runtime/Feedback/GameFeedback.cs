using System;
using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Âm thanh và rung cho một loại sự kiện mô phỏng.</summary>
    [Serializable]
    public class FeedbackCue
    {
        public GameEventType type;
        public AudioClip clip;
        [Range(0, 1)] public float volume = 1f;
        [Tooltip("Lệch cao độ ngẫu nhiên ±, giúp tiếng lặp nhiều lần đỡ đều.")]
        [Range(0, 0.3f)] public float pitchJitter;
        [Tooltip("Khoảng cách tối thiểu giữa hai lần phát tiếng (giây).")]
        public float minInterval;
        public HapticType haptic;
        [Tooltip("Khoảng cách tối thiểu giữa hai lần rung của sự kiện này (giây).")]
        public float hapticInterval;
        [Tooltip("Cao độ tăng thêm mỗi lần phát liên tiếp (trong 0,4 giây). 0 = không tăng.")]
        [Range(0, 0.1f)] public float chainPitchStep;
        [Tooltip("Cao độ tăng tối đa của chuỗi.")]
        [Range(0, 1f)] public float chainPitchMax = 0.5f;

        [NonSerialized] public float lastSound = -999f, lastHaptic = -999f;
        [NonSerialized] public int chain;

        public FeedbackCue() { }

        public FeedbackCue(GameEventType type, float volume, float pitchJitter, float minInterval, HapticType haptic, float hapticInterval)
        {
            this.type = type; this.volume = volume; this.pitchJitter = pitchJitter;
            this.minInterval = minInterval; this.haptic = haptic; this.hapticInterval = hapticInterval;
        }
    }

    /// <summary>
    /// Phản hồi âm thanh/rung: chỉ đọc sự kiện mô phỏng đã phát, không quyết định luật.
    /// Tôn trọng tùy chọn Âm thanh/Rung trong <see cref="Preferences"/>.
    /// </summary>
    public class GameFeedback : MonoBehaviour
    {
        [SerializeField] List<FeedbackCue> cues = new List<FeedbackCue>();
        [SerializeField] AudioClip buttonClick;
        [Range(0, 1)] [SerializeField] float buttonVolume = 0.7f;
        [SerializeField] AudioClip music;
        [Range(0, 1)] [SerializeField] float musicVolume = 0.35f;
        [Tooltip("Số kênh phát tiếng cùng lúc.")]
        [SerializeField] int voices = 8;
        [Tooltip("Khoảng cách tối thiểu giữa hai lần rung bất kỳ (giây).")]
        [SerializeField] float globalHapticInterval = 0.05f;

        AudioSource[] _voices;
        AudioSource _music;
        int _nextVoice;
        float _lastHaptic = -999f;
        readonly Dictionary<GameEventType, FeedbackCue> _byType = new Dictionary<GameEventType, FeedbackCue>();

        void Awake()
        {
            _voices = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _voices.Length; i++) _voices[i] = CreateSource(false);
            _music = CreateSource(true);
            foreach (var c in cues) _byType[c.type] = c;
        }

        void OnEnable()
        {
            Preferences.Changed += OnPreferenceChanged;
            UpdateMusic();
        }

        void OnDisable() => Preferences.Changed -= OnPreferenceChanged;

        AudioSource CreateSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void OnPreferenceChanged(PreferenceKey p)
        {
            if (p == PreferenceKey.Sound) UpdateMusic();
        }

        void UpdateMusic()
        {
            if (_music == null) return;
            if (music != null && Preferences.Sound)
            {
                _music.clip = music;
                _music.volume = musicVolume;
                if (!_music.isPlaying) _music.Play();
            }
            else _music.Stop();
        }

        public void Process(IReadOnlyList<GameEvent> events)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < events.Count; i++)
            {
                if (!_byType.TryGetValue(events[i].Type, out var cue)) continue;
                if (cue.clip != null && now - cue.lastSound >= cue.minInterval)
                {
                    cue.chain = now - cue.lastSound <= ChainWindow ? cue.chain + 1 : 0;
                    cue.lastSound = now;
                    float rise = Mathf.Min(cue.chain * cue.chainPitchStep, cue.chainPitchMax);
                    PlaySound(cue.clip, cue.volume, cue.pitchJitter, 1f + rise);
                }
                if (cue.haptic != HapticType.None && now - cue.lastHaptic >= cue.hapticInterval)
                {
                    cue.lastHaptic = now;
                    Vibrate(cue.haptic, now);
                }
            }
        }

        public void PlayButtonClick()
        {
            if (buttonClick != null) PlaySound(buttonClick, buttonVolume, 0f, 1f);
        }

        const float ChainWindow = 0.4f;

        void PlaySound(AudioClip clip, float volume, float pitchJitter, float basePitch)
        {
            if (!Preferences.Sound) return;
            var s = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            s.pitch = basePitch + (pitchJitter > 0 ? UnityEngine.Random.Range(-pitchJitter, pitchJitter) : 0f);
            s.PlayOneShot(clip, volume);
        }

        void Vibrate(HapticType level, float now)
        {
            if (!Preferences.Vibration) return;
            // Rung mạnh luôn được phát; rung nhẹ bị gộp nếu quá dày.
            if (level != HapticType.Heavy && now - _lastHaptic < globalHapticInterval) return;
            _lastHaptic = now;
            Haptics.Play(level);
        }
    }
}
