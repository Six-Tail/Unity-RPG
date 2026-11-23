using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 성급·초월 별 줄(이미지). 일반 별 = 성급 색 입체 별, 초월 별 = 홀로그램(무지개빛) 별 + 은은한 반짝임.
    /// 6성에서 초월 단계만큼 앞쪽 별이 홀로 별로 바뀌고(+6 = 전부), 옆에 '+N'.
    /// </summary>
    [DisallowMultipleComponent]
    public class StarRow : MonoBehaviour
    {
        private Image[] _stars;
        private Image[] _twinkles;
        private TextMeshProUGUI _plus;
        private int _transcend;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>별 줄 생성. 위치/앵커는 반환값의 Rect로 지정하세요.</summary>
        public static StarRow Create(Transform parent, float starSize, TextAnchor align, TMP_FontAsset font, bool showPlus = true)
        {
            var rt = UIFactory.NewRect("StarRow", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = align;
            h.spacing = -starSize * 0.14f;   // 글로우 여백만큼 살짝 겹치게
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;

            var row = rt.gameObject.AddComponent<StarRow>();
            row._stars = new Image[Transcendence.MaxStar];
            row._twinkles = new Image[Transcendence.MaxStar];
            for (int i = 0; i < row._stars.Length; i++)
            {
                var img = UIFactory.AddImage("Star", rt, Color.white, StarSprites.Plain);
                img.preserveAspect = true;
                var le = img.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = starSize; le.preferredHeight = starSize;
                row._stars[i] = img;

                // 반짝임(별 왼쪽 위) — 초월 별에서만 켜짐.
                // 왼쪽 위에 두면 앞 별(먼저 그려짐) 위에 겹쳐서 가려지지 않음
                var tw = UIFactory.AddImage("Twinkle", img.transform, new Color(1, 1, 1, 0), StarSprites.Twinkle);
                var twrt = tw.rectTransform;
                UIFactory.Anchor(twrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                twrt.sizeDelta = new Vector2(starSize * 0.85f, starSize * 0.85f);
                twrt.anchoredPosition = new Vector2(-starSize * 0.20f, starSize * 0.24f);
                tw.gameObject.SetActive(false);
                row._twinkles[i] = tw;
            }

            if (showPlus)
            {
                var t = UIFactory.AddText("Plus", rt, "", starSize * 0.62f, font, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                t.margin = new Vector4(starSize * 0.42f, 0, 0, 0);   // 별과 살짝 띄우기(겹침 spacing 상쇄 포함)
                t.enableVertexGradient = true;
                t.colorGradient = new VertexGradient(
                    new Color(0.62f, 0.93f, 1f), new Color(1f, 0.74f, 0.93f),
                    new Color(0.80f, 0.74f, 1f), new Color(1f, 0.86f, 0.95f));
                var le = t.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = starSize * 1.75f; le.preferredHeight = starSize;
                row._plus = t;
            }
            return row;
        }

        /// <summary>현재 성급 / 초월 단계 표시.</summary>
        public void Set(int star, int transcend)
        {
            star = Mathf.Clamp(star, 1, Transcendence.MaxStar);
            _transcend = star >= Transcendence.MaxStar ? Mathf.Clamp(transcend, 0, Transcendence.MaxTranscend) : 0;
            var tint = StarText.ColorOf(star);

            for (int i = 0; i < _stars.Length; i++)
            {
                var img = _stars[i];
                bool on = i < star;
                img.gameObject.SetActive(on);
                if (!on) continue;
                bool holo = i < _transcend;
                img.sprite = holo ? StarSprites.Holo : StarSprites.Plain;
                img.color = holo ? Color.white : tint;
                img.rectTransform.localScale = Vector3.one;
                _twinkles[i].gameObject.SetActive(holo);
            }

            if (_plus != null)
            {
                _plus.text = _transcend > 0 ? "+" + _transcend : "";
                _plus.gameObject.SetActive(_transcend > 0);
            }
        }

        private void Update()
        {
            if (_transcend <= 0 || _stars == null) return;

            // 빛이 왼쪽 → 오른쪽으로 별을 하나씩 훑고 지나간 뒤 잠깐 쉬었다 반복.
            // 지나가는 별은 밝아지고(0.70 → 1.0), 살짝 커지며(최대 +18%), ✦ 반짝임이 번쩍.
            const float speed = 5.5f;     // 초당 지나가는 별 수
            const float pause = 3.0f;     // 한 바퀴 뒤 쉬는 구간(별 개수 단위)
            float pos = Mathf.Repeat(Time.unscaledTime * speed, _transcend + pause) - 0.5f;

            for (int i = 0; i < _transcend && i < _stars.Length; i++)
            {
                float w = Mathf.Clamp01(1f - Mathf.Abs(pos - i) / 1.1f);
                w = w * w * (3f - 2f * w);                       // 부드러운 봉우리

                float k = Mathf.Lerp(0.70f, 1f, w);
                _stars[i].color = new Color(k, k, k, 1f);
                _stars[i].rectTransform.localScale = Vector3.one * (1f + 0.18f * w);

                var tw = _twinkles[i];
                float tk = w * w;
                tw.color = new Color(1f, 1f, 1f, tk);
                tw.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, tk);
                tw.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f * (1f - tk));
            }
        }
    }
}
