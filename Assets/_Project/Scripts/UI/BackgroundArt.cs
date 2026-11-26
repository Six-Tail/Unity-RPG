using UnityEngine;
using UnityEngine.UI;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 화면 배경 그림. 화면 Root 바로 아래 맨 뒤에 깔리고(비율 유지하며 화면을 가득 채움),
    /// 글자가 읽히도록 위에 어두운 막을 덮습니다. 그림이 없으면 숨겨져 기존 단색 배경이 그대로 보입니다.
    /// keepWhole(인물 그림 — 타이틀 키 아트 · 뽑기 배너): 화면이 그림보다 훨씬 넓으면(접은 폴더블 · 21:9 휴대폰 · 울트라와이드 —
    /// 그림 비율의 1.25배 넘게) 위아래를 크게 잘라 얼굴이 잘리는 대신, 그림을 높이에 맞춰 가운데에 온전히 두고 양옆은
    /// <see cref="SetBack"/>로 준 인물 없는 배경(없으면 어두운 바탕)을 깔아 부드럽게 이어 줍니다. 풍경 배경은 원래대로 꽉 채움.
    /// </summary>
    public sealed class BackgroundArt
    {
        private readonly Image _image, _back, _dim;
        private readonly AspectRatioFitter _fitter, _backFitter;
        private readonly Fit _fit;

        /// <summary>이 배율을 넘게 화면이 넓으면 '가운데 온전히 + 양옆 어둡게'.</summary>
        public const float WideRatio = 1.25f;

        private static readonly Color BackTint = new(0.62f, 0.62f, 0.66f, 1f), BackDark = new(0.04f, 0.05f, 0.09f, 1f);

        private BackgroundArt(Image image, Image back, Image dim, AspectRatioFitter fitter, AspectRatioFitter backFitter, Fit fit)
        {
            _image = image; _back = back; _dim = dim; _fitter = fitter; _backFitter = backFitter; _fit = fit;
        }

        /// <param name="root">화면 Root(전체 화면 RectTransform).</param>
        /// <param name="dim">그림 위에 덮는 검은 막의 불투명도(0 = 그대로, 1 = 완전히 가림).</param>
        /// <param name="keepWhole">아주 넓은 화면에서 그림을 자르지 않고 가운데에 온전히(인물 그림용).</param>
        public static BackgroundArt Create(Transform root, float dim = 0.6f, bool keepWhole = false)
        {
            // 그림이 화면보다 커질 수 있으므로(가득 채우기) 잘라 주는 틀을 둡니다.
            var clip = UIFactory.NewRect("BackgroundArt", root);
            UIFactory.Stretch(clip);
            clip.SetAsFirstSibling();
            clip.gameObject.AddComponent<RectMask2D>();

            // 넓은 화면용 뒤 그림(꽉 채우고 어둡게)
            var back = UIFactory.AddImage("Back", clip, BackDark);
            UIFactory.Stretch(back.rectTransform);
            back.preserveAspect = false; back.raycastTarget = false;
            var backFitter = back.gameObject.AddComponent<AspectRatioFitter>();
            backFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backFitter.aspectRatio = 16f / 9f;

            // 앞 그림(보통은 꽉 채움, 넓은 화면은 높이에 맞춰 가운데 + 양옆 가장자리를 흐림)
            var frame = UIFactory.NewRect("Front", clip);
            UIFactory.Stretch(frame);
            var soft = frame.gameObject.AddComponent<RectMask2D>();
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            var img = UIFactory.AddImage("Image", frame, Color.white);
            UIFactory.Stretch(img.rectTransform);
            img.preserveAspect = false; img.raycastTarget = false;

            var dimImg = UIFactory.AddImage("Dim", clip, new Color(0f, 0f, 0f, Mathf.Clamp01(dim)));
            UIFactory.Stretch(dimImg.rectTransform);

            Fit fit = null;
            if (keepWhole)
            {
                fit = clip.gameObject.AddComponent<Fit>();
                fit.Init(clip, frame, soft, fitter, back);
            }
            else { soft.enabled = false; back.enabled = false; }

            var art = new BackgroundArt(img, back, dimImg, fitter, backFitter, fit);
            art.Set((Sprite)null);
            return art;
        }

        public bool HasImage => _image != null && _image.enabled;

        /// <summary>그림 지정(null이면 숨김). 표시 여부를 돌려줍니다.</summary>
        public bool Set(Sprite sprite)
        {
            if (_image == null) return false;
            bool on = sprite != null;
            _image.sprite = sprite;
            if (_fit == null) _back.enabled = false;
            _image.enabled = on;
            _dim.enabled = on;
            if (on && sprite.rect.height > 0f)
            {
                float a = sprite.rect.width / sprite.rect.height;
                _fitter.aspectRatio = a; _backFitter.aspectRatio = a;
            }
            if (_fit != null) _fit.Apply(on);
            return on;
        }

        /// <summary>Resources/Art/ 기준 경로 후보 중 처음 찾은 그림(예: "Backgrounds/battle_ch3", "Backgrounds/battle").</summary>
        public bool Set(params string[] paths) => Set(GameArt.LoadFirst(paths));

        /// <summary>
        /// 아주 넓은 화면에서 양옆에 깔 그림(keepWhole일 때만) — 같은 인물 그림을 다시 깔면 손 · 무기가 한 번 더 보여 어색하므로
        /// 인물 없는 풍경을 줌. null이면 어두운 바탕.
        /// </summary>
        public BackgroundArt SetBack(Sprite sprite)
        {
            if (_back == null) return this;
            _back.sprite = sprite;
            _back.color = sprite != null ? BackTint : BackDark;
            if (sprite != null && sprite.rect.height > 0f) _backFitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            return this;
        }

        public BackgroundArt SetBack(params string[] paths) => SetBack(GameArt.LoadFirst(paths));

        /// <summary>화면 비율이 바뀌면(회전 · 창 크기 · 폴더블 접고 펴기) 채우는 방식을 다시 고름.</summary>
        private sealed class Fit : MonoBehaviour
        {
            private RectTransform _clip, _frame;
            private RectMask2D _soft;
            private AspectRatioFitter _fitter;
            private Image _back;
            private bool _hasImage;

            public void Init(RectTransform clip, RectTransform frame, RectMask2D soft, AspectRatioFitter fitter, Image back)
            {
                _clip = clip; _frame = frame; _soft = soft; _fitter = fitter; _back = back;
            }

            public void Apply(bool hasImage)
            {
                _hasImage = hasImage;
                Refresh();
            }

            private void OnRectTransformDimensionsChange() => Refresh();

            private void Refresh()
            {
                if (_clip == null || _fitter == null) return;
                var r = _clip.rect;
                bool wide = _hasImage && r.height > 1f && r.width / r.height > _fitter.aspectRatio * WideRatio;
                var mode = wide ? AspectRatioFitter.AspectMode.HeightControlsWidth : AspectRatioFitter.AspectMode.EnvelopeParent;
                if (wide)
                {
                    // 높이에 맞춤(세로 전체가 보임) — 가운데 정렬
                    _frame.anchorMin = new Vector2(0.5f, 0f); _frame.anchorMax = new Vector2(0.5f, 1f);
                    _frame.pivot = new Vector2(0.5f, 0.5f);
                    _frame.offsetMin = new Vector2(_frame.offsetMin.x, 0f); _frame.offsetMax = new Vector2(_frame.offsetMax.x, 0f);
                }
                else
                {
                    _frame.anchorMin = Vector2.zero; _frame.anchorMax = Vector2.one;
                    _frame.pivot = new Vector2(0.5f, 0.5f);
                    _frame.offsetMin = Vector2.zero; _frame.offsetMax = Vector2.zero;
                }
                if (_fitter.aspectMode != mode) _fitter.aspectMode = mode;
                _soft.softness = wide ? new Vector2Int(Mathf.RoundToInt(r.height * 0.18f), 0) : Vector2Int.zero;
                _soft.enabled = wide;
                if (_back != null) _back.enabled = wide;
            }
        }
    }
}
