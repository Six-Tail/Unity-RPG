using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>쿠폰 입력 창(설정 → 쿠폰 입력). 맞는 번호면 보상이 우편함으로 가고 받은 보상을 알림으로 보여 줌.</summary>
    public static class CouponWindow
    {
        public static void Open(TMP_FontAsset font = null)
        {
            var w = OverlayWindow.Open("쿠폰 입력", 820, 420, font);
            var p = w.Panel;
            w.Text(p, "쿠폰 번호를 입력하세요. 보상은 <color=#FFD36B>우편함</color>으로 보내 드립니다.\n<size=85%><color=#9AA3B5>대소문자 · 띄어쓰기 · 하이픈(-)은 구분하지 않습니다. 쿠폰은 계정마다 한 번만 쓸 수 있습니다.</color></size>",
                21, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft, 40, -84, 740, 70).textWrappingMode = TextWrappingModes.Normal;

            var input = Field(p, w.Font, "쿠폰 번호", 30);
            OverlayWindow.Place((RectTransform)input.transform, 40, -172, 740, 62);
            input.characterValidation = TMP_InputField.CharacterValidation.None;

            var msg = w.Text(p, "", 21, new Color(1f, 0.55f, 0.55f), TextAlignmentOptions.Center, 40, -248, 740, 40);

            void Submit()
            {
                string err = PlayerCoupons.Instance.Redeem(input.text, out var coupon, out var reward);
                if (err != null) { msg.text = err; input.ActivateInputField(); return; }
                SoundManager.Play(Sfx.Reward);
                w.Close();
                Popups.Show($"쿠폰 사용 — {coupon.name}", $"보상을 우편함으로 보냈습니다.\n\n{reward.Describe("\n")}", "확인", null, icon: reward.Parts().Count > 0 ? reward.Parts()[0].icon : null);
            }

            w.Btn(p, "사용하기", 26, OverlayWindow.Gold, 40, -310, 740, 70, Submit);
            input.onSubmit.AddListener(s => Submit());
            input.onValueChanged.AddListener(s => msg.text = "");
            input.ActivateInputField();
        }

        private static TMP_InputField Field(Transform parent, TMP_FontAsset font, string placeholder, int limit)
        {
            const float size = 26;
            var bg = UIFactory.AddImage("Input", parent, new Color(1, 1, 1, 0.1f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var area = UIFactory.NewRect("Text Area", bg.transform);
            UIFactory.Stretch(area, 20, 6, 20, 6);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = UIFactory.AddText("Placeholder", area, placeholder, size, font, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Center, FontStyles.Italic);
            UIFactory.Stretch(ph.rectTransform);
            var text = UIFactory.AddText("Text", area, "", size, font, Color.white, TextAlignmentOptions.Center);
            UIFactory.Stretch(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.characterSpacing = 6f;

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            if (font != null) field.fontAsset = font;
            field.pointSize = size;
            InputFocusFx.Attach(field);
            return field;
        }
    }
}
