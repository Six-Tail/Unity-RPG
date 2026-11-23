using UnityEngine;
using UnityEngine.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 정적 일러스트 자리(Image)에 "움직이는 일러스트"를 대신 띄우는 도우미.
    ///   1) Live2D 모델이 연결돼 있으면 모델(Live2DStage)
    ///   2) 없고 반복 영상이 있으면 영상(MotionVideoStage — Resources/Art/Motion/{id}.mp4)
    ///   3) 둘 다 없으면 false — 지금까지처럼 정적 일러스트를 쓰면 됩니다.
    /// 표시했으면 true 를 돌려주므로 호출한 쪽은 정적 그림을 끕니다.
    ///
    ///   bool live = Live2DUI.Show(portraitImage, character);
    ///   portraitImage.enabled = !live &amp;&amp; sprite != null;
    /// </summary>
    public static class Live2DUI
    {
        private const string ChildName = "Live2D";

        /// <summary>host(정적 일러스트 Image) 위에 움직이는 일러스트 표시. 표시했으면 true.</summary>
        public static bool Show(Graphic host, CharacterData c)
        {
            if (host == null) return false;
            // 숨겨진 화면이 무대를 가져가지 않도록, 지금 보이는 자리만 표시
            if (c == null || !host.gameObject.activeInHierarchy) { Hide(host); return false; }

            Texture tex = null;
            Material material = null;
            float aspect = 0.75f;
            RawImage raw;

            if (Live2DStage.Has(c))
            {
                var stage = Live2DStage.Instance;
                raw = Ensure(host);
                MotionVideoStage.ReleaseUser(raw);
                tex = stage.Show(c, raw);
                material = stage.UIMaterial;   // Live2D 텍스처는 알파가 곱해진 색
                aspect = stage.Aspect;
            }
            else if (MotionVideoStage.Has(c))
            {
                var stage = MotionVideoStage.Instance;
                raw = Ensure(host);
                Live2DStage.ReleaseUser(raw);
                tex = stage.Show(c, raw, GameArt.Portrait(c));
                aspect = stage.Aspect;
            }
            else { Hide(host); return false; }

            if (tex == null) { Hide(host); return false; }

            raw.texture = tex;
            raw.material = material;
            raw.color = Color.white;
            raw.enabled = true;
            var fitter = raw.GetComponent<AspectRatioFitter>();
            if (fitter != null && aspect > 0f) fitter.aspectRatio = aspect;
            return true;
        }

        /// <summary>움직이는 일러스트 표시를 끔(정적 일러스트로 돌아갈 때).</summary>
        public static void Hide(Graphic host)
        {
            var raw = Find(host);
            if (raw == null) return;
            raw.enabled = false;
            raw.texture = null;
            Live2DStage.ReleaseUser(raw);
            MotionVideoStage.ReleaseUser(raw);
        }

        /// <summary>표시 중이면 투명도 조절(연출용).</summary>
        public static void SetAlpha(Graphic host, float alpha)
        {
            var raw = Find(host);
            if (raw != null && raw.enabled) raw.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        public static bool IsShowing(Graphic host)
        {
            var raw = Find(host);
            return raw != null && raw.enabled;
        }

        /// <summary>이 캐릭터에 움직이는 일러스트(Live2D 모델 또는 반복 영상)가 있는가.</summary>
        public static bool Has(CharacterData c) => Live2DStage.Has(c) || MotionVideoStage.Has(c);

        private static RawImage Find(Graphic host)
        {
            if (host == null) return null;
            var t = host.transform.Find(ChildName);
            return t != null ? t.GetComponent<RawImage>() : null;
        }

        /// <summary>host 영역 안에 비율을 지켜 들어가는 RawImage 자식을 만듭니다(한 번만).</summary>
        private static RawImage Ensure(Graphic host)
        {
            var raw = Find(host);
            if (raw != null) return raw;

            var rt = UIFactory.NewRect(ChildName, host.transform);
            UIFactory.Stretch(rt);
            rt.SetAsFirstSibling();   // 같은 자리의 글자(대체 문구 등)보다 뒤
            raw = rt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 0.75f;
            return raw;
        }
    }
}
