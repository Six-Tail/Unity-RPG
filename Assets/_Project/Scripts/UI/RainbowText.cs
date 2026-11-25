using TMPro;
using UnityEngine;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 글자마다 6초월 별 색(StarText.Rainbow: 하늘 → 라벤더 → 핑크)이 흐르는 애니메이션 + 은은한 밝기 맥동.
    /// Lv.100(각성 최대 레벨) 표기 등에 붙입니다. 화면에 보일 때만 갱신.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class RainbowText : MonoBehaviour
    {
        public float speed = 0.6f;      // 색이 흐르는 속도(초당 팔레트 바퀴)
        public float spread = 0.18f;    // 글자 사이 색 차이
        public bool starsOnly;          // 켜면 별(★)과 별 바로 뒤의 '+N'만 칠함(이름 · 다른 숫자는 그대로)

        /// <summary>글자에 효과를 켜거나 끔(없으면 붙임). 끄면 원래 색으로 되돌림.</summary>
        public static void Apply(TMP_Text text, bool on, bool starsOnly = false)
        {
            if (text == null) return;
            var fx = text.GetComponent<RainbowText>();
            if (!on)
            {
                if (fx != null && fx.enabled) { fx.enabled = false; text.ForceMeshUpdate(); }
                return;
            }
            if (fx == null) fx = text.gameObject.AddComponent<RainbowText>();
            fx.starsOnly = starsOnly;
            fx.enabled = true;
        }

        private static Color32[] _palette;
        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            if (_palette == null)
            {
                var hex = Meta.UI.StarText.Rainbow;
                _palette = new Color32[hex.Length];
                for (int i = 0; i < hex.Length; i++)
                    _palette[i] = ColorUtility.TryParseHtmlString(hex[i], out var c) ? (Color32)c : new Color32(255, 255, 255, 255);
            }
        }

        private static Color32 Sample(float t)
        {
            t = Mathf.Repeat(t, 1f) * _palette.Length;
            int a = (int)t % _palette.Length, b = (a + 1) % _palette.Length;
            return Color32.Lerp(_palette[a], _palette[b], t - Mathf.Floor(t));
        }

        private void LateUpdate()
        {
            if (_text == null || !_text.isActiveAndEnabled) return;
            _text.ForceMeshUpdate();
            var info = _text.textInfo;
            float now = Time.unscaledTime;
            float glow = 0.85f + 0.15f * Mathf.Sin(now * 3f);   // 밝기 맥동
            int state = 0, k = 0;   // starsOnly: 0 보통 · 1 별 뒤 · 2 별 뒤 '+' 의 숫자
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (starsOnly)
                {
                    char c0 = ch.character;
                    bool paint;
                    if (c0 == '★') { state = 1; paint = true; }
                    else if (state == 1 && c0 == ' ') paint = false;
                    else if (state == 1 && c0 == '+') { state = 2; paint = true; }
                    else if (state == 2 && char.IsDigit(c0)) paint = true;
                    else { state = 0; paint = false; }
                    if (!paint) continue;
                }
                if (!ch.isVisible) continue;
                var cols = info.meshInfo[ch.materialReferenceIndex].colors32;
                Color32 c = Sample(now * speed - (starsOnly ? k++ : i) * spread);
                c = new Color32((byte)(c.r * glow), (byte)(c.g * glow), (byte)(c.b * glow), c.a);
                // 글자 위쪽은 조금 밝게(반짝이는 느낌)
                var top = Color32.Lerp(c, new Color32(255, 255, 255, c.a), 0.35f);
                int v = ch.vertexIndex;
                cols[v] = c; cols[v + 3] = c;          // 아래
                cols[v + 1] = top; cols[v + 2] = top;  // 위
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
