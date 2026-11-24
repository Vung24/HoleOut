using System.Collections.Generic;
using HoleOut.Core;
using HoleOut.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HoleOut.EditorTools
{
    /// <summary>
    /// Gắn âm thanh/rung vào scene chơi và thêm hai dòng Âm thanh/Rung vào bảng Cài đặt ở Home.
    /// Chạy lại an toàn: phần đã có thì chỉ cập nhật.
    /// </summary>
    public static class FeedbackSetup
    {
        const string GameplayScene = "Assets/01scene/HoleOut.unity";
        const string HomeScene = "Assets/01scene/Home.unity";
        const string ClipFolder = "Assets/06audio/AudioClip/";
        const float RowPitch = 160f;

        /// <summary>Chỉ gắn âm thanh/rung vào scene chơi (không đụng Home).</summary>
        public static void SetupGameplayOnly() => SetupGameplay();

        [MenuItem("Tools/HoleOut/Setup Sound And Vibration")]
        static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupGameplay();
            SetupHomeSettings();
            Debug.Log("[HoleOut] Đã gắn âm thanh/rung vào HoleOut.unity và thêm tùy chọn vào Cài đặt ở Home.unity.");
        }

        static void SetupGameplay()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            // Công cụ Editor chạy một lần: tìm controller duy nhất trong scene vừa mở.
            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null) { Debug.LogError("[HoleOut] Không thấy GameController trong " + GameplayScene); return; }

            var feedback = controller.GetComponent<GameFeedback>();
            if (feedback == null) feedback = Undo.AddComponent<GameFeedback>(controller.gameObject);

            var so = new SerializedObject(feedback);
            so.FindProperty("buttonClick").objectReferenceValue = Clip("Click");
            so.FindProperty("music").objectReferenceValue = Clip("BG");
            var cues = so.FindProperty("cues");
            var defaults = DefaultCues();
            cues.arraySize = defaults.Count;
            for (int i = 0; i < defaults.Count; i++)
            {
                var (cue, clip) = defaults[i];
                var p = cues.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("type").enumValueIndex = (int)cue.type;
                p.FindPropertyRelative("clip").objectReferenceValue = clip == null ? null : Clip(clip);
                p.FindPropertyRelative("volume").floatValue = cue.volume;
                p.FindPropertyRelative("pitchJitter").floatValue = cue.pitchJitter;
                p.FindPropertyRelative("minInterval").floatValue = cue.minInterval;
                p.FindPropertyRelative("haptic").enumValueIndex = (int)cue.haptic;
                p.FindPropertyRelative("hapticInterval").floatValue = cue.hapticInterval;
                p.FindPropertyRelative("chainPitchStep").floatValue = cue.chainPitchStep;
                p.FindPropertyRelative("chainPitchMax").floatValue = cue.chainPitchMax;
            }
            so.ApplyModifiedProperties();

            var cso = new SerializedObject(controller);
            cso.FindProperty("_feedback").objectReferenceValue = feedback;
            cso.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static List<(FeedbackCue, string)> DefaultCues() => new List<(FeedbackCue, string)>
        {
            (new FeedbackCue(GameEventType.BlockMoved, 0f, 0f, 0f, HapticType.Light, 0.06f), null),
            // Chạm tường: chỉ rung nhẹ, không tiếng để khỏi ồn khi kéo dọc tường.
            (new FeedbackCue(GameEventType.BlockBlocked, 0f, 0f, 0f, HapticType.Light, 0.25f), null),
            (new FeedbackCue(GameEventType.BlockExited, 0.9f, 0.05f, 0f, HapticType.Medium, 0f), "Hole Tap"),
            (new FeedbackCue(GameEventType.BlockRevealed, 0.8f, 0.05f, 0.08f, HapticType.Light, 0.1f), "Hiden tile"),
            // Băng nứt nhẹ khi mất một lớp, vỡ to khi tan hẳn.
            (new FeedbackCue(GameEventType.BlockIceCracked, 0.45f, 0.08f, 0.05f, HapticType.Light, 0.1f), "Ice brake"),
            (new FeedbackCue(GameEventType.BlockThawed, 1f, 0.05f, 0.05f, HapticType.Medium, 0f), "Ice brake"),
            (new FeedbackCue(GameEventType.HoleDocked, 0f, 0f, 0f, HapticType.Light, 0.1f), null),
            (new FeedbackCue(GameEventType.RunnerLanded, 0.45f, 0.04f, 0.05f, HapticType.Light, 0.12f) { chainPitchStep = 0.015f, chainPitchMax = 0.2f }, "Hole jump"),
            (new FeedbackCue(GameEventType.HoleClosing, 0.9f, 0f, 0f, HapticType.Medium, 0f), "Hole complete"),
            (new FeedbackCue(GameEventType.MagnetPulled, 0.9f, 0f, 0f, HapticType.Medium, 0f), "Magnet"),
            (new FeedbackCue(GameEventType.BlockLifted, 0.8f, 0.05f, 0f, HapticType.Medium, 0f), "Hole Tap"),
            (new FeedbackCue(GameEventType.SlotAdded, 0.8f, 0f, 0f, HapticType.Medium, 0f), "Click"),
            (new FeedbackCue(GameEventType.Continued, 0.8f, 0f, 0f, HapticType.Medium, 0f), "Click"),
            (new FeedbackCue(GameEventType.Won, 1f, 0f, 0f, HapticType.Heavy, 0f), "Win"),
            (new FeedbackCue(GameEventType.Jammed, 1f, 0f, 0f, HapticType.Heavy, 0f), "Lose"),
        };

        static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipFolder + name + ".ogg");
            if (clip == null) Debug.LogWarning("[HoleOut] Không thấy clip " + ClipFolder + name + ".ogg");
            return clip;
        }

        static void SetupHomeSettings()
        {
            var scene = EditorSceneManager.OpenScene(HomeScene, OpenSceneMode.Single);
            var home = Object.FindFirstObjectByType<HomeScreen>();
            if (home == null) { Debug.LogError("[HoleOut] Không thấy HomeScreen trong " + HomeScene); return; }

            var hso = new SerializedObject(home);
            var settings = hso.FindProperty("settings").objectReferenceValue as GameObject;
            var onColor = hso.FindProperty("optionOnColor").colorValue;
            var offColor = hso.FindProperty("optionOffColor").colorValue;
            var card = settings != null ? settings.transform.Find("Card") as RectTransform : null;
            var template = card != null ? card.Find("Entrance Animation") as RectTransform : null;
            if (template == null) { Debug.LogError("[HoleOut] Không thấy dòng 'Entrance Animation' trong bảng Cài đặt ở Home."); return; }

            int added = 0;
            added += AddRow(card, template, "Sound", "Âm thanh", "Nhạc nền và hiệu ứng", PreferenceKey.Sound, onColor, offColor, 1);
            added += AddRow(card, template, "Vibration", "Rung", "Rung nhẹ khi thao tác", PreferenceKey.Vibration, onColor, offColor, 2);

            if (added > 0)
            {
                // Bố cục đặt tay: nới thẻ, đẩy tiêu đề lên và nút Đóng xuống để chừa chỗ cho các dòng mới.
                float grow = added * RowPitch;
                card.sizeDelta += new Vector2(0, grow);
                Shift(card.Find("Title"), grow * 0.5f);
                Shift(template, grow * 0.5f);
                Shift(card.Find("Sound"), grow * 0.5f);
                Shift(card.Find("Vibration"), grow * 0.5f);
                Shift(card.Find("Close"), -grow * 0.5f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static int AddRow(RectTransform card, RectTransform template, string name, string label, string description,
            PreferenceKey pref, Color onColor, Color offColor, int index)
        {
            var existing = card.Find(name);
            if (existing != null) { ConfigureToggle(existing, pref, onColor, offColor); return 0; }

            var row = (RectTransform)Object.Instantiate(template.gameObject, card).transform;
            Undo.RegisterCreatedObjectUndo(row.gameObject, "Add " + name);
            row.name = name;
            row.SetSiblingIndex(template.GetSiblingIndex() + index);
            row.anchoredPosition = template.anchoredPosition - new Vector2(0, RowPitch * index);
            SetText(row.Find("Option"), label);
            SetText(row.Find("Description"), description);
            var toggle = row.Find("Toggle Entrance Animation");
            if (toggle != null) toggle.name = "Toggle " + name;
            ConfigureToggle(row, pref, onColor, offColor);
            return 1;
        }

        static void ConfigureToggle(Transform row, PreferenceKey pref, Color onColor, Color offColor)
        {
            var button = row.GetComponentInChildren<Button>(true);
            if (button == null) return;
            // Bỏ listener sao chép từ dòng mẫu (đang gọi ToggleEntranceAnimation).
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);

            var toggle = button.GetComponent<PreferenceToggle>();
            if (toggle == null) toggle = Undo.AddComponent<PreferenceToggle>(button.gameObject);
            var so = new SerializedObject(toggle);
            so.FindProperty("preference").enumValueIndex = (int)pref;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("value").objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("onColor").colorValue = onColor;
            so.FindProperty("offColor").colorValue = offColor;
            so.ApplyModifiedProperties();
        }

        static void SetText(Transform t, string s)
        {
            var text = t != null ? t.GetComponent<TMP_Text>() : null;
            if (text == null) return;
            Undo.RecordObject(text, "Set text");
            text.text = s;
        }

        static void Shift(Transform t, float dy)
        {
            if (t is RectTransform rt) rt.anchoredPosition += new Vector2(0, dy);
        }
    }
}
