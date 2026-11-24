using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Gacha;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>화면 오른쪽 위 재화 표시 종류.</summary>
    public enum Currency { Crystal, Gold, PickupTicket, StandardTicket, Stamina }

    /// <summary>
    /// 오른쪽 위 재화 표시(모든 화면 공용): 어두운 알약 + 아이콘(Resources/Art/Items/…) + 숫자.
    /// 지갑이 바뀌면 스스로 갱신하고, 행동력은 알약 아래에 다음 회복까지 남은 시간을 보여 줍니다.
    ///
    ///   CurrencyBar.Create(root, font, wallet, new Vector2(-50, -36), Currency.Crystal, Currency.Gold);
    /// </summary>
    public class CurrencyBar : MonoBehaviour
    {
        public const float PillW = 250f, PillH = 52f, Gap = 14f;

        private class Pill
        {
            public Currency type;
            public Image bg;
            public TextMeshProUGUI text, sub;
        }

        private readonly List<Pill> _pills = new();
        private PlayerWallet _wallet;
        private float _clock;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>
        /// 오른쪽 위 기준(pos = 오른쪽 위 모서리 위치)으로 알약을 왼쪽→오른쪽 순서대로 놓음.
        /// </summary>
        public static CurrencyBar Create(Transform parent, TMP_FontAsset font, PlayerWallet wallet, Vector2 pos, params Currency[] items)
        {
            var rt = UIFactory.NewRect("CurrencyBar", parent);
            UIFactory.Anchor(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            float w = items.Length * PillW + Mathf.Max(0, items.Length - 1) * Gap;
            rt.sizeDelta = new Vector2(w, PillH); rt.anchoredPosition = pos;

            var bar = rt.gameObject.AddComponent<CurrencyBar>();
            bar._wallet = wallet != null ? wallet : FindAnyObjectByType<PlayerWallet>();
            for (int i = 0; i < items.Length; i++) bar.AddPill(items[i], i * (PillW + Gap), font);
            if (bar._wallet != null) bar._wallet.OnChanged += bar.Refresh;
            bar.Refresh();
            return bar;
        }

        private void OnDestroy()
        {
            if (_wallet != null) _wallet.OnChanged -= Refresh;
        }

        private static string IconOf(Currency c) => c switch
        {
            Currency.Crystal => "Items/Crystal",
            Currency.Gold => "Items/Gold",
            Currency.PickupTicket => "Items/PickupTicket",
            Currency.StandardTicket => "Items/StandardTicket",
            _ => "Items/Stamina",
        };

        public static Color ColorOf(Currency c) => c switch
        {
            Currency.Crystal => new Color(0.6f, 0.9f, 1f),
            Currency.Gold => new Color(1f, 0.85f, 0.45f),
            Currency.PickupTicket => new Color(1f, 0.72f, 0.9f),
            Currency.StandardTicket => new Color(0.65f, 0.85f, 1f),
            _ => new Color(0.5f, 1f, 0.72f),
        };

        private void AddPill(Currency type, float x, TMP_FontAsset font)
        {
            var bg = UIFactory.AddImage(type.ToString(), transform, new Color(0.04f, 0.05f, 0.09f, 0.78f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var brt = bg.rectTransform;
            UIFactory.Anchor(brt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            brt.sizeDelta = new Vector2(PillW, PillH); brt.anchoredPosition = new Vector2(x, 0);

            var color = ColorOf(type);
            var sp = GameArt.Load(IconOf(type));
            var ic = UIFactory.AddImage("Icon", brt, sp != null ? Color.white : color, sp != null ? sp : ProceduralSprites.Circle);
            ic.preserveAspect = true;
            UIFactory.Anchor(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            ic.rectTransform.sizeDelta = sp != null ? new Vector2(42, 42) : new Vector2(22, 22);
            ic.rectTransform.anchoredPosition = new Vector2(sp != null ? 7 : 16, 0);

            var t = UIFactory.AddText("T", brt, "", 26, font, color, TextAlignmentOptions.Right, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform, 56, 0, 18, 0);
            t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 26;
            t.richText = true;

            TextMeshProUGUI sub = null;
            if (type == Currency.Stamina)
            {
                // 다음 회복까지(알약 아래)
                sub = UIFactory.AddText("Next", brt, "", 16, font, new Color(0.7f, 0.74f, 0.82f), TextAlignmentOptions.Right);
                UIFactory.Anchor(sub.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1));
                sub.rectTransform.sizeDelta = new Vector2(80, 22); sub.rectTransform.anchoredPosition = new Vector2(-6, -2);
            }
            _pills.Add(new Pill { type = type, bg = bg, text = t, sub = sub });
        }

        private void Update()
        {
            // 행동력 회복 시간은 1초마다
            _clock -= Time.unscaledDeltaTime;
            if (_clock > 0f) return;
            _clock = 1f;
            foreach (var p in _pills) if (p.type == Currency.Stamina) { Refresh(); break; }
        }

        public void Refresh()
        {
            if (_wallet == null) return;
            foreach (var p in _pills)
            {
                switch (p.type)
                {
                    case Currency.Crystal: p.text.text = $"{_wallet.Crystals:N0}"; break;
                    case Currency.Gold: p.text.text = $"{_wallet.Gold:N0}"; break;
                    case Currency.PickupTicket: p.text.text = $"{_wallet.PickupTickets:N0}"; break;
                    case Currency.StandardTicket: p.text.text = $"{_wallet.StandardTickets:N0}"; break;
                    case Currency.Stamina:
                        int cur = _wallet.Stamina, max = _wallet.MaxStamina;
                        p.text.text = $"{cur} <size=70%><color=#9AA3B5>/ {max}</color></size>";
                        p.sub.text = cur < max ? $"+1까지 {PlayerWallet.FormatTime(_wallet.SecondsToNextStamina)}" : "";
                        p.sub.rectTransform.sizeDelta = new Vector2(PillW - 12, 22);
                        break;
                }
            }
        }

        /// <summary>강조(예: 가챠에서 지금 쓰는 뽑기권) — 테두리처럼 밝은 바탕.</summary>
        public void Highlight(Currency type, bool on)
        {
            foreach (var p in _pills)
            {
                if (p.type != type) continue;
                var c = ColorOf(type);
                p.bg.color = on ? new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.92f) : new Color(0.04f, 0.05f, 0.09f, 0.78f);
            }
        }
    }
}
