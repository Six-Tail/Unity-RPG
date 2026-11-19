using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Gacha.UI
{
    /// <summary>
    /// 뽑기 결과 1장. 프리팹 없이 코드로 비주얼을 구성합니다.
    /// 성급 색/글로우 + 픽업 뱃지 + 공개(reveal) 팝 애니메이션을 담당.
    /// 공개 전에는 커버(?)로 가려졌다가, Reveal()로 등장합니다.
    /// </summary>
    public class GachaResultCard : MonoBehaviour
    {
        private RectTransform _rt;
        private Image _frame;
        private Image _glow;
        private Image _portrait;
        private Image _bottomBar;
        private Image _flash;
        private Image _cover;
        private GameObject _pickupBadge;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _starsText;
        private TextMeshProUGUI _coverMark;

        private GachaRarityPalette _palette;
        private Coroutine _revealCo;

        public GachaResult Result { get; private set; }
        public bool Revealed { get; private set; }

        /// <summary>카드 비주얼을 코드로 생성해 parent 아래에 붙입니다.</summary>
        public static GachaResultCard Create(Transform parent, GachaRarityPalette palette, TMP_FontAsset font)
        {
            var rt = UIFactory.NewRect("ResultCard", parent);
            var card = rt.gameObject.AddComponent<GachaResultCard>();
            card.Build(palette, font);
            return card;
        }

        private void Build(GachaRarityPalette palette, TMP_FontAsset font)
        {
            _palette = palette;
            _rt = (RectTransform)transform;

            var dark = new Color(0.10f, 0.11f, 0.14f, 1f);

            // 프레임(테두리) = 카드 전체를 채우는 성급색
            _frame = UIFactory.AddImage("Frame", transform, Color.white);
            UIFactory.Stretch(_frame.rectTransform);

            // 내부 패널(프레임 안쪽으로 inset → 프레임이 테두리처럼 보임)
            var inner = UIFactory.AddImage("Inner", _frame.transform, dark);
            UIFactory.Stretch(inner.rectTransform, 5, 5, 5, 5);

            // 글로우(포트레이트 뒤) — 공개 시 알파를 올림
            _glow = UIFactory.AddImage("Glow", inner.transform, Color.clear);
            UIFactory.Stretch(_glow.rectTransform, 6, 44, 6, 6);

            // 포트레이트(썸네일 우선, 없으면 portrait)
            _portrait = UIFactory.AddImage("Portrait", inner.transform, Color.white);
            UIFactory.Stretch(_portrait.rectTransform, 8, 46, 8, 8);
            _portrait.preserveAspect = true;

            // 하단 바(성급색) + 별/이름
            _bottomBar = UIFactory.AddImage("BottomBar", inner.transform, Color.white);
            var barRt = _bottomBar.rectTransform;
            UIFactory.Anchor(barRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            barRt.sizeDelta = new Vector2(0, 42);
            barRt.anchoredPosition = Vector2.zero;

            _starsText = UIFactory.AddText("Stars", _bottomBar.transform, "", 15, font, Color.white, TextAlignmentOptions.Top);
            var starRt = _starsText.rectTransform;
            UIFactory.Anchor(starRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            starRt.sizeDelta = new Vector2(0, 18);
            starRt.anchoredPosition = new Vector2(0, -2);

            _nameText = UIFactory.AddText("Name", _bottomBar.transform, "", 15, font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
            var nameRt = _nameText.rectTransform;
            UIFactory.Anchor(nameRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            nameRt.sizeDelta = new Vector2(0, 22);
            nameRt.anchoredPosition = new Vector2(0, 2);
            _nameText.margin = new Vector4(4, 0, 4, 0);

            // 픽업 뱃지(좌상단)
            var badge = UIFactory.AddImage("PickupBadge", inner.transform, new Color(0.95f, 0.25f, 0.35f, 0.95f));
            _pickupBadge = badge.gameObject;
            var pbRt = badge.rectTransform;
            UIFactory.Anchor(pbRt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            pbRt.sizeDelta = new Vector2(68, 22);
            pbRt.anchoredPosition = new Vector2(4, -4);
            var pbTxt = UIFactory.AddText("Label", badge.transform, "PICK UP", 12, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(pbTxt.rectTransform);

            // 플래시(공개 시 번쩍) — 최상단
            _flash = UIFactory.AddImage("Flash", transform, new Color(1, 1, 1, 0));
            UIFactory.Stretch(_flash.rectTransform);

            // 커버(미공개 상태) — 최상단
            _cover = UIFactory.AddImage("Cover", transform, new Color(0.16f, 0.17f, 0.22f, 1f));
            UIFactory.Stretch(_cover.rectTransform);
            _coverMark = UIFactory.AddText("Mark", _cover.transform, "?", 48, font, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_coverMark.rectTransform);
        }

        /// <summary>결과 데이터를 바인딩하고 미공개 상태로 초기화.</summary>
        public void Bind(GachaResult result)
        {
            Result = result;
            var style = _palette.Get(result.rarity);

            _frame.color = style.frame;
            _bottomBar.color = new Color(style.frame.r, style.frame.g, style.frame.b, 0.92f);

            var glow = style.glow; glow.a = 0f; // 공개 시 올림
            _glow.color = glow;

            int stars = (int)result.rarity;
            _starsText.text = new string('★', stars);
            _starsText.color = style.frame;

            _nameText.text = result.character != null ? result.character.displayName : "???";

            var sprite = result.character != null
                ? (result.character.thumbnail != null ? result.character.thumbnail : result.character.portrait)
                : null;
            _portrait.sprite = sprite;
            _portrait.enabled = sprite != null;

            _pickupBadge.SetActive(result.isFeatured);

            SetHidden();
        }

        /// <summary>미공개(커버) 상태로 되돌림.</summary>
        public void SetHidden()
        {
            Revealed = false;
            if (_revealCo != null) { StopCoroutine(_revealCo); _revealCo = null; }
            _cover.gameObject.SetActive(true);
            _flash.color = new Color(1, 1, 1, 0);
            _rt.localScale = Vector3.one;
            SetGlowAlpha(0f);
        }

        /// <summary>공개. instant=true면 애니메이션 없이 즉시.</summary>
        public void Reveal(bool instant = false)
        {
            if (Revealed) return;
            Revealed = true;
            if (_revealCo != null) StopCoroutine(_revealCo);

            if (instant)
            {
                _cover.gameObject.SetActive(false);
                _flash.color = new Color(1, 1, 1, 0);
                _rt.localScale = Vector3.one;
                SetGlowAlpha(GlowTarget());
                _revealCo = null;
                return;
            }
            _revealCo = StartCoroutine(RevealRoutine());
        }

        private IEnumerator RevealRoutine()
        {
            var style = _palette.Get(Result.rarity);
            float punch = Mathf.Clamp01(style.punch);

            _cover.gameObject.SetActive(false);

            float dur = 0.28f + 0.22f * punch;
            float flashPeak = 0.35f + 0.55f * punch;
            float glowTarget = GlowTarget();
            const float fromScale = 0.6f;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = Mathf.LerpUnclamped(fromScale, 1f, EaseOutBack(k));
                _rt.localScale = new Vector3(s, s, 1f);
                _flash.color = new Color(1, 1, 1, flashPeak * (1f - k));
                SetGlowAlpha(glowTarget * k);
                yield return null;
            }

            _rt.localScale = Vector3.one;
            _flash.color = new Color(1, 1, 1, 0);
            SetGlowAlpha(glowTarget);
            _revealCo = null;
        }

        private float GlowTarget()
        {
            var style = _palette.Get(Result.rarity);
            return Mathf.Lerp(0.15f, 0.55f, Mathf.Clamp01(style.punch));
        }

        private void SetGlowAlpha(float a)
        {
            var c = _glow.color; c.a = a; _glow.color = c;
        }

        /// <summary>끝에서 살짝 넘쳤다 돌아오는 팝 이징(EaseOutBack).</summary>
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
