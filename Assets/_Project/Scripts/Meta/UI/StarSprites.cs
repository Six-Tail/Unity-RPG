using UnityEngine;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 별 아이콘 스프라이트를 코드로 생성(아트 에셋 불필요, 최초 1회 캐시).
    /// · Holo  : 초월 별 — 하늘·파랑·라벤더·핑크 홀로그램(무지개빛) + 입체 면 + 하이라이트 + 바깥 글로우
    /// · Plain : 일반 별 — 밝은 회색 입체 별(Image.color로 성급 색을 곱해 사용)
    /// </summary>
    public static class StarSprites
    {
        private const int Size = 96;

        private static Sprite _holo, _plain;

        // 2026-11-26: 생성한 입체 별 그림을 써 봤지만 작은 크기에서 복잡해 잘 안 보여서 코드로 그린 단순한 별로 되돌림
        public static Sprite Holo => _holo != null ? _holo : (_holo = Build(true));
        public static Sprite Plain => _plain != null ? _plain : (_plain = Build(false));

        private static Sprite _twinkle;
        /// <summary>반짝임(✦ 모양 4갈래 빛) — 초월 별 위에 순간적으로 겹쳐 표시.</summary>
        public static Sprite Twinkle => _twinkle != null ? _twinkle : (_twinkle = BuildTwinkle());

        private static Sprite BuildTwinkle()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "Star_Twinkle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(RenderTwinkle(n));
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }

        public static Color32[] RenderTwinkle(int n)
        {
            var px = new Color32[n * n];
            float c = n * 0.5f, len = n * 0.48f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - c), dy = Mathf.Abs(y + 0.5f - c);
                // 가로/세로 빛줄기(끝으로 갈수록 가늘고 흐려짐)
                float h = Mathf.Clamp01(1f - dx / len) * Mathf.Exp(-dy / (n * 0.022f + n * 0.03f * (1f - dx / len)));
                float v = Mathf.Clamp01(1f - dy / len) * Mathf.Exp(-dx / (n * 0.022f + n * 0.03f * (1f - dy / len)));
                float core = Mathf.Exp(-(dx * dx + dy * dy) / (2f * (n * 0.09f) * (n * 0.09f)));
                float a = Mathf.Clamp01(Mathf.Max(h, v) * 1.1f + core * 0.9f);
                // 가운데는 흰색, 바깥은 살짝 하늘빛
                float t = Mathf.Clamp01(core * 1.5f);
                var col = Color.Lerp(new Color(0.80f, 0.93f, 1f), Color.white, t);
                px[y * n + x] = new Color(col.r, col.g, col.b, a);
            }
            return px;
        }

        private static Sprite Build(bool holo)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = holo ? "Star_Holo" : "Star_Plain",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(Render(holo));
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }

        /// <summary>픽셀 계산(테스트/미리보기에서도 재사용 가능하도록 분리).</summary>
        public static Color32[] Render(bool holo)
        {
            var px = new Color32[Size * Size];
            float c = Size * 0.5f;
            float R = Size * 0.40f;          // 바깥 꼭짓점 반지름
            float r = R * 0.50f;             // 안쪽 꼭짓점 반지름(통통한 별)
            float outline = Size * 0.045f;   // 테두리 두께
            float cy = c - Size * 0.02f;     // 시각 중심 보정

            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.Deg2Rad * (90f + 36f * i);
                float rad = (i % 2 == 0) ? R : r;
                pts[i] = new Vector2(c + Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad);
            }

            var glowCol = new Color(0.72f, 0.86f, 1f);
            var lineCol = holo ? new Color(0.10f, 0.13f, 0.30f) : new Color(0.16f, 0.14f, 0.12f);

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = DistToPolygon(p, pts);
                float sd = Inside(p, pts) ? -d : d;   // 음수 = 안쪽

                Color col = new Color(0, 0, 0, 0);

                // 1) 바깥 글로우 (홀로만)
                if (holo && sd > 0f)
                {
                    float g = 0.6f * Mathf.Exp(-sd / (Size * 0.045f));
                    col = Over(col, new Color(glowCol.r, glowCol.g, glowCol.b, g));
                }

                // 2) 테두리(별 전체 실루엣, 바깥 경계 안티앨리어싱)
                float cover = Mathf.Clamp01(0.5f - sd);
                if (cover > 0f) col = Over(col, new Color(lineCol.r, lineCol.g, lineCol.b, cover));

                // 3) 안쪽 채움
                float fill = Mathf.Clamp01(-(sd + outline) + 0.5f);
                if (fill > 0f)
                {
                    var f = FillColor(p, c, cy, R, holo);
                    col = Over(col, new Color(f.r, f.g, f.b, fill));
                }

                px[y * Size + x] = col;
            }
            return px;
        }

        private static Color FillColor(Vector2 p, float cx, float cy, float R, bool holo)
        {
            float u = p.x / Size, v = p.y / Size;     // v: 위로 갈수록 1
            var toC = new Vector2(p.x - cx, p.y - cy);
            float dist = toC.magnitude;

            // 입체 면: 팔마다 한쪽 면은 밝게, 반대 면은 어둡게
            float ang = Mathf.Atan2(toC.y, toC.x) * Mathf.Rad2Deg;
            float rel = Mathf.Repeat(ang - 90f, 72f);
            float facet = rel < 36f ? 1.0f : 0.80f;

            Color baseCol;
            if (holo)
            {
                // 홀로그램: 팔(각도)마다 하늘·파랑·라벤더·핑크로 돌고, 대각선으로 살짝 흐름
                float t = Mathf.Clamp01(0.55f * u + 0.45f * (1f - v));
                float hue = 0.47f + 0.50f * Mathf.Repeat((ang - 90f) / 360f + 0.25f * t, 1f);
                baseCol = Color.HSVToRGB(hue, 0.40f, 1f);
            }
            else
            {
                float g = Mathf.Lerp(0.80f, 1f, v);
                baseCol = new Color(g, g, g);
            }
            baseCol *= facet;
            baseCol.a = 1f;

            // 중심 하이라이트(흰빛)
            float w = 1f - Mathf.Clamp01(dist / (R * 0.6f));
            baseCol = Color.Lerp(baseCol, Color.white, (holo ? 0.70f : 0.35f) * w * w);

            // 좌상단 반짝임
            if (holo)
            {
                var hp = new Vector2(Size * 0.40f, Size * 0.60f);
                float s = Size * 0.07f;
                float sp = Mathf.Exp(-(p - hp).sqrMagnitude / (2f * s * s));
                baseCol = Color.Lerp(baseCol, Color.white, 0.6f * sp);
            }
            baseCol.a = 1f;
            return baseCol;
        }

        private static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return new Color(0, 0, 0, 0);
            float k = dst.a * (1f - src.a);
            return new Color(
                (src.r * src.a + dst.r * k) / a,
                (src.g * src.a + dst.g * k) / a,
                (src.b * src.a + dst.b * k) / a,
                a);
        }

        private static float DistToPolygon(Vector2 p, Vector2[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = (p - (a + ab * t)).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        private static bool Inside(Vector2 p, Vector2[] pts)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].y > p.y) != (pts[j].y > p.y) &&
                    p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
