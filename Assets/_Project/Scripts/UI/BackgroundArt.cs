using UnityEngine;
using UnityEngine.UI;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 화면 배경 그림. 화면 Root 바로 아래 맨 뒤에 깔리고(비율 유지하며 화면을 가득 채움),
    /// 글자가 읽히도록 위에 어두운 막을 덮습니다. 그림이 없으면 숨겨져 기존 단색 배경이 그대로 보입니다.
    /// </summary>
    public sealed class BackgroundArt
    {
        private readonly Image _image, _dim;
        private readonly AspectRatioFitter _fitter;

        private BackgroundArt(Image image, Image dim, AspectRatioFitter fitter)
        {
            _image = image; _dim = dim; _fitter = fitter;
        }

        /// <param name="root">화면 Root(전체 화면 RectTransform).</param>
        /// <param name="dim">그림 위에 덮는 검은 막의 불투명도(0 = 그대로, 1 = 완전히 가림).</param>
        public static BackgroundArt Create(Transform root, float dim = 0.6f)
        {
            // 그림이 화면보다 커질 수 있으므로(가득 채우기) 잘라 주는 틀을 둡니다.
            var clip = UIFactory.NewRect("BackgroundArt", root);
            UIFactory.Stretch(clip);
            clip.SetAsFirstSibling();
            clip.gameObject.AddComponent<RectMask2D>();

            var img = UIFactory.AddImage("Image", clip, Color.white);
            UIFactory.Stretch(img.rectTransform);
            img.preserveAspect = false;
            var fitter = img.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            var dimImg = UIFactory.AddImage("Dim", clip, new Color(0f, 0f, 0f, Mathf.Clamp01(dim)));
            UIFactory.Stretch(dimImg.rectTransform);

            var art = new BackgroundArt(img, dimImg, fitter);
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
            _image.enabled = on;
            _dim.enabled = on;
            if (on && sprite.rect.height > 0f) _fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            return on;
        }

        /// <summary>Resources/Art/ 기준 경로 후보 중 처음 찾은 그림(예: "Backgrounds/battle_ch3", "Backgrounds/battle").</summary>
        public bool Set(params string[] paths) => Set(GameArt.LoadFirst(paths));
    }
}
