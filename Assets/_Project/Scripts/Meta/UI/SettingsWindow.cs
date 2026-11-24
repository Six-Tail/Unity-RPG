using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 설정 창(홈 위쪽 '설정' 아이콘): 배경음 · 효과음 볼륨, 전체 음소거. 값은 SoundManager 가 PlayerPrefs 에 저장.
    /// </summary>
    public static class SettingsWindow
    {
        public static void Open(TMP_FontAsset font = null)
        {
            var w = OverlayWindow.Open("설정", 900, 520, font);

            Row(w, "배경음 (BGM)", -110, SoundManager.BgmVolume, v => SoundManager.BgmVolume = v, false);
            Row(w, "효과음", -200, SoundManager.SfxVolume, v => SoundManager.SfxVolume = v, true);

            // 음소거
            Button muteBtn = null;
            void RefreshMute()
            {
                var lbl = muteBtn.GetComponentInChildren<TextMeshProUGUI>();
                lbl.text = SoundManager.Muted ? "음소거 켜짐  <size=75%>(누르면 소리 켜기)</size>" : "음소거 꺼짐  <size=75%>(누르면 모든 소리 끄기)</size>";
                muteBtn.GetComponent<Image>().color = SoundManager.Muted ? OverlayWindow.Red : new Color(1, 1, 1, 0.14f);
            }
            muteBtn = w.Btn(w.Panel, "", 22, new Color(1, 1, 1, 0.14f), 40, -300, 820, 64, () =>
            {
                SoundManager.Muted = !SoundManager.Muted;
                RefreshMute();
                if (!SoundManager.Muted) SoundManager.Play(Sfx.Confirm);
            });
            muteBtn.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            RefreshMute();

            w.Text(w.Panel, "<color=#9AA3B5>음원은 Resources/Audio/BGM · SFX 폴더의 파일을 씁니다(docs/sound.md). 설정은 이 기기에 저장됩니다.</color>",
                17, Color.white, TextAlignmentOptions.Left, 40, -400, 820, 60).textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>이름 · 슬라이더 · 퍼센트 한 줄. sfxPreview 면 놓을 때 효과음으로 들려줌.</summary>
        private static void Row(OverlayWindow w, string label, float y, float value, System.Action<float> set, bool sfxPreview)
        {
            w.Text(w.Panel, $"<b>{label}</b>", 24, Color.white, TextAlignmentOptions.Left, 40, y, 240, 44);
            var pct = w.Text(w.Panel, "", 22, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Right, 760, y, 100, 44);

            var root = UIFactory.NewRect("Slider", w.Panel);
            OverlayWindow.Place(root, 280, y - 8, 460, 28);
            var bg = UIFactory.AddImage("Bg", root, new Color(1, 1, 1, 0.12f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            UIFactory.Stretch(bg.rectTransform, 0, 6, 0, 6);
            var fillArea = UIFactory.NewRect("FillArea", root);
            UIFactory.Stretch(fillArea, 0, 6, 0, 6);
            var fill = UIFactory.AddImage("Fill", fillArea, new Color(0.85f, 0.62f, 0.22f, 0.96f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false;
            UIFactory.Stretch(fill.rectTransform);
            var handleArea = UIFactory.NewRect("HandleArea", root);
            UIFactory.Stretch(handleArea, 14, 0, 14, 0);
            var handle = UIFactory.AddImage("Handle", handleArea, Color.white, ProceduralSprites.Circle, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(30, 0);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.value = value;
            pct.text = $"{Mathf.RoundToInt(value * 100)}%";
            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                pct.text = $"{Mathf.RoundToInt(v * 100)}%";
            });
            if (sfxPreview)
            {
                var trig = root.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                e.callback.AddListener(_ => SoundManager.Play(Sfx.Confirm));
                trig.triggers.Add(e);
            }
        }
    }
}
