using UnityEngine;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 코드로 만든 UI 스프라이트(이미지 에셋 없이). 모두 흰색 계열이라 Image.color로 색을 입힙니다. 한 번 만들고 캐시.
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite _button, _glow, _beam, _circle, _ring, _rounded;

        /// <summary>
        /// 버튼용 둥근 사각형(9-slice): 위가 밝고 아래가 어두운 그라데이션 + 밝은 테두리 + 윗부분 광택.
        /// </summary>
        public static Sprite Button => _button != null ? _button : (_button = MakeRounded(64, 18f, button: true));

        /// <summary>패널·칸용 둥근 사각형(9-slice, 단색).</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = MakeRounded(64, 14f, button: false));

        /// <summary>가운데가 밝고 바깥으로 사라지는 원형 빛.</summary>
        public static Sprite Glow => _glow != null ? _glow : (_glow = MakeGlow(128));

        /// <summary>세로 빛줄기(가운데 밝음, 위아래 끝이 사라짐).</summary>
        public static Sprite Beam => _beam != null ? _beam : (_beam = MakeBeam(48, 256));

        /// <summary>채워진 원.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(64, 0f));

        /// <summary>고리(도넛).</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeCircle(64, 0.62f));

        private static Texture2D NewTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            return tex;
        }

        private static Sprite MakeRounded(int size, float r, bool button)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float qx = Mathf.Abs(fx - h) - (h - r), qy = Mathf.Abs(fy - h) - (h - r);
                    float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                    float a = Mathf.Clamp01(0.5f - outside);
                    float b = 1f;
                    if (button)
                    {
                        float inside = -outside;
                        b = Mathf.Lerp(0.74f, 0.97f, fy / size);              // 위 밝음 → 아래 어두움
                        if (fy > size * 0.56f && inside > 3f) b = Mathf.Min(1f, b + 0.04f);   // 윗부분 광택
                        if (inside < 2.4f) b = 1f;                              // 밝은 테두리
                    }
                    px[y * size + x] = new Color(b, b, b, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            int border = Mathf.CeilToInt(r) + 3;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite MakeGlow(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude / h;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeBeam(int w, int hgt)
        {
            var tex = NewTex(w, hgt);
            var px = new Color[w * hgt];
            float cx = w / 2f;
            for (int y = 0; y < hgt; y++)
            {
                float v = y / (float)(hgt - 1);
                float vy = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.18f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.35f));
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - cx) / cx;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f) * vy;
                    px[y * w + x] = new Color(1, 1, 1, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, hgt), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeCircle(int size, float innerRatio)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float h = size / 2f, inner = h * innerRatio;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude;
                    float a = Mathf.Clamp01(h - 0.5f - d + 0.5f);
                    if (innerRatio > 0f) a *= Mathf.Clamp01(d - inner + 0.5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
