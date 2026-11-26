using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 전투 연출(화면 전체 위): 보스 필살기 예고(붉은 화면 가장자리 맥동 + WARNING 띠)와 보스 컷인(오른쪽에서 들어오는 보스 그림 · 기술 이름).
    /// 아군 궁극기 컷인(BattleHUD.UltimateCutIn)과 짝 — 보스 쪽은 붉은 색 · 반대 방향.
    /// </summary>
    public class BattleCinematics : MonoBehaviour
    {
        private TMP_FontAsset _font;
        private Image _vignette;
        private RectTransform _warn;
        private CanvasGroup _warnGroup;
        private TextMeshProUGUI _warnTitle, _warnSub;
        private Coroutine _warnAnim;

        private RectTransform _cut, _cutBand, _cutLines;
        private CanvasGroup _cutGroup;
        private Image _cutDim, _cutArt, _cutGlow;
        private TextMeshProUGUI _cutLabel, _cutName, _cutUser;

        public static BattleCinematics Create(Transform root, TMP_FontAsset font)
        {
            var rt = UIFactory.NewRect("Cinematics", root);
            UIFactory.Stretch(rt);
            var c = rt.gameObject.AddComponent<BattleCinematics>();
            c._font = font;
            c.Build(rt);
            return c;
        }

        // ---------------- 만들기 ----------------

        private void Build(RectTransform root)
        {
            // 붉은 화면 가장자리(가운데는 투명)
            _vignette = UIFactory.AddImage("Vignette", root, new Color(1f, 0.1f, 0.1f, 0f), VignetteSprite());
            _vignette.preserveAspect = false; _vignette.raycastTarget = false;
            UIFactory.Stretch(_vignette.rectTransform, -40, -40, -40, -40);

            // WARNING 띠(위쪽)
            _warn = UIFactory.NewRect("Warning", root);
            UIFactory.Anchor(_warn, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _warn.sizeDelta = new Vector2(2400, 150); _warn.anchoredPosition = new Vector2(0, -210);
            _warnGroup = _warn.gameObject.AddComponent<CanvasGroup>();
            _warnGroup.blocksRaycasts = false; _warnGroup.alpha = 0f;
            var band = UIFactory.AddImage("Band", _warn, new Color(0.35f, 0.02f, 0.04f, 0.78f));
            band.raycastTarget = false;
            UIFactory.Stretch(band.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                var edge = UIFactory.AddImage("Edge", _warn, new Color(1f, 0.25f, 0.2f, 0.9f));
                edge.raycastTarget = false;
                UIFactory.Anchor(edge.rectTransform, new Vector2(0, i), new Vector2(1, i), new Vector2(0.5f, i));
                edge.rectTransform.sizeDelta = new Vector2(0, 4); edge.rectTransform.anchoredPosition = Vector2.zero;
            }
            _warnTitle = UIFactory.AddText("Title", _warn, "WARNING", 64, GameFonts.Display(_font), new Color(1f, 0.85f, 0.8f), TextAlignmentOptions.Center, FontStyles.Normal);
            _warnTitle.characterSpacing = 18f; _warnTitle.raycastTarget = false;
            UIFactory.Anchor(_warnTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _warnTitle.rectTransform.sizeDelta = new Vector2(1400, 80); _warnTitle.rectTransform.anchoredPosition = new Vector2(0, -10);
            TextOutline.Apply(_warnTitle, 0.18f, new Color32(80, 0, 0, 255));
            _warnSub = UIFactory.AddText("Sub", _warn, "", 26, _font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _warnSub.richText = true; _warnSub.raycastTarget = false;
            UIFactory.Anchor(_warnSub.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            _warnSub.rectTransform.sizeDelta = new Vector2(1600, 44); _warnSub.rectTransform.anchoredPosition = new Vector2(0, 12);

            // 보스 컷인
            _cut = UIFactory.NewRect("BossCutIn", root);
            UIFactory.Stretch(_cut);
            _cutGroup = _cut.gameObject.AddComponent<CanvasGroup>();
            _cutGroup.blocksRaycasts = false;
            _cutDim = UIFactory.AddImage("Dim", _cut, new Color(0.1f, 0f, 0.02f, 0f));
            _cutDim.raycastTarget = false;
            UIFactory.Stretch(_cutDim.rectTransform);
            var bandImg = UIFactory.AddImage("Band", _cut, new Color(0.16f, 0.02f, 0.06f, 0.95f));
            bandImg.raycastTarget = false;
            _cutBand = bandImg.rectTransform;
            UIFactory.Anchor(_cutBand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _cutBand.sizeDelta = new Vector2(2800, 330); _cutBand.anchoredPosition = new Vector2(0, -10);
            _cutBand.localEulerAngles = new Vector3(0, 0, 6f);   // 아군 컷인(-6°)과 반대로 기울임
            for (int i = 0; i < 2; i++)
            {
                var acc = UIFactory.AddImage("Accent", _cutBand, i == 0 ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.3f, 0.25f, 0.5f));
                acc.raycastTarget = false;
                UIFactory.Anchor(acc.rectTransform, new Vector2(0, i == 0 ? 1 : 0), new Vector2(1, i == 0 ? 1 : 0), new Vector2(0.5f, i == 0 ? 1 : 0));
                acc.rectTransform.sizeDelta = new Vector2(0, i == 0 ? 8 : 3); acc.rectTransform.anchoredPosition = Vector2.zero;
            }
            _cutLines = UIFactory.NewRect("SpeedLines", _cutBand);
            UIFactory.Stretch(_cutLines);
            for (int i = 0; i < 12; i++)
            {
                var l = UIFactory.AddImage("Line", _cutLines, new Color(1f, 0.5f, 0.45f, 0.1f + 0.08f * (i % 3)));
                l.raycastTarget = false;
                UIFactory.Anchor(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                l.rectTransform.sizeDelta = new Vector2(260 + (i * 97) % 420, 3 + i % 3);
                l.rectTransform.anchoredPosition = new Vector2(0, -140 + i * 25);
            }
            _cutGlow = UIFactory.AddImage("Glow", _cut, new Color(0.7f, 0.2f, 1f, 0f), ProceduralSprites.Glow);
            _cutGlow.preserveAspect = false; _cutGlow.raycastTarget = false;
            UIFactory.Anchor(_cutGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _cutGlow.rectTransform.sizeDelta = new Vector2(1100, 1100);
            _cutArt = UIFactory.AddImage("Art", _cut, Color.white);
            _cutArt.preserveAspect = true; _cutArt.raycastTarget = false;
            UIFactory.Anchor(_cutArt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _cutArt.rectTransform.sizeDelta = new Vector2(820, 820);

            _cutLabel = Txt("Label", 28, new Color(1f, 0.55f, 0.5f));
            _cutName = Txt("Name", 72, Color.white);
            _cutName.enableAutoSizing = true; _cutName.fontSizeMin = 30; _cutName.fontSizeMax = 72;
            _cutUser = Txt("User", 28, new Color(1f, 0.82f, 0.8f));
            _cut.gameObject.SetActive(false);
        }

        private TextMeshProUGUI Txt(string name, float size, Color c)
        {
            var t = UIFactory.AddText(name, _cut, "", size, _font, c, TextAlignmentOptions.Right, FontStyles.Bold);
            t.raycastTarget = false; t.richText = true;
            UIFactory.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f));
            t.rectTransform.sizeDelta = new Vector2(900, size + 24);
            return t;
        }

        private static Sprite _vigSprite;
        /// <summary>가운데 투명 → 가장자리로 갈수록 진해지는 테두리 그림(코드로 한 번 만듦).</summary>
        private static Sprite VignetteSprite()
        {
            if (_vigSprite != null) return _vigSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 0.6f + Mathf.Sqrt(dx * dx + dy * dy) * 0.4f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.05f, d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            _vigSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _vigSprite;
        }

        // ---------------- 보스 필살기 예고 ----------------

        /// <summary>붉은 화면 가장자리가 맥동하며 WARNING 띠(title · sub). dur 동안.</summary>
        public void Warning(string title, string sub, float dur)
        {
            if (!isActiveAndEnabled) return;
            if (_warnAnim != null) StopCoroutine(_warnAnim);
            _warnAnim = StartCoroutine(WarningRoutine(title, sub, dur));
        }

        private IEnumerator WarningRoutine(string title, string sub, float dur)
        {
            _warnTitle.text = string.IsNullOrEmpty(title) ? "WARNING" : title;
            _warnSub.text = sub ?? "";
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float inK = Mathf.Clamp01(t / 0.25f), outK = Mathf.Clamp01((dur - t) / 0.35f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                _vignette.color = new Color(1f, 0.1f, 0.1f, (0.25f + 0.35f * pulse) * Mathf.Min(inK, outK));
                _warnGroup.alpha = Mathf.Min(inK, outK);
                _warn.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, 1f - Mathf.Pow(1f - inK, 3f)), 1f);
                _warnTitle.alpha = 0.75f + 0.25f * pulse;
                yield return null;
            }
            _vignette.color = new Color(1f, 0.1f, 0.1f, 0f);
            _warnGroup.alpha = 0f;
            _warnAnim = null;
        }

        // ---------------- 보스 컷인 ----------------

        public IEnumerator BossCutIn(string bossName, Sprite art, string skillName, float dur)
        {
            _cut.gameObject.SetActive(true);
            _cut.SetAsLastSibling();
            _cutArt.sprite = art;
            _cutArt.enabled = art != null;
            _cutLabel.text = "BOSS SKILL";
            _cutName.text = skillName;
            _cutUser.text = bossName;
            var lines = new System.Collections.Generic.List<RectTransform>();
            foreach (RectTransform l in _cutLines) lines.Add(l);
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float inK = Mathf.Clamp01(k / 0.2f);
                float ease = 1f - Mathf.Pow(1f - inK, 3f);
                _cutDim.color = new Color(0.1f, 0f, 0.02f, 0.62f * ease);
                _cutBand.localScale = new Vector3(1f, ease, 1f);
                // 보스 그림은 오른쪽에서, 글자는 왼쪽에서(아군 컷인과 반대)
                _cutArt.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(1300f, 470f, ease) - 50f * k, -10f);
                _cutArt.rectTransform.localScale = Vector3.one * (1f + 0.06f * k);
                _cutGlow.rectTransform.anchoredPosition = _cutArt.rectTransform.anchoredPosition;
                _cutGlow.color = new Color(0.7f, 0.2f, 1f, 0.45f * ease * (0.8f + 0.2f * Mathf.Sin(t * 12f)));
                float tx = Mathf.Lerp(-1100f, -280f, ease) + 30f * k;
                _cutName.rectTransform.anchoredPosition = new Vector2(tx, 6f);
                _cutLabel.rectTransform.anchoredPosition = new Vector2(tx, 82f);
                _cutUser.rectTransform.anchoredPosition = new Vector2(tx, -62f);
                for (int i = 0; i < lines.Count; i++)
                {
                    var l = lines[i];
                    float x = Mathf.Repeat(-1300f + (t * (2600f + i * 180f)) + i * 230f, 2600f) - 1300f;
                    l.anchoredPosition = new Vector2(x, l.anchoredPosition.y);
                }
                _cutGroup.alpha = k > 0.86f ? 1f - (k - 0.86f) / 0.14f : 1f;
                yield return null;
            }
            _cut.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 전장의 떠다니는 입자(유닛 뒤, 배경 위): 별가루 · 불씨 · 빛 먼지가 천천히 떠오르며 반짝임. 색은 배경에 맞춰 SetTheme.
    /// </summary>
    public class AmbientParticles : MonoBehaviour
    {
        private class Dot { public RectTransform rt; public Image img; public Vector2 vel; public float phase, size, life, t; }
        private readonly System.Collections.Generic.List<Dot> _dots = new();
        private RectTransform _layer;
        private Color _a = new(1f, 0.9f, 0.7f), _b = new(0.7f, 0.8f, 1f);
        private const int Count = 46;

        public static AmbientParticles Create(Transform parent)
        {
            var rt = UIFactory.NewRect("Ambient", parent);
            UIFactory.Stretch(rt);
            var a = rt.gameObject.AddComponent<AmbientParticles>();
            a._layer = rt;
            for (int i = 0; i < Count; i++)
            {
                var img = UIFactory.AddImage("Dot", rt, Color.clear, i % 3 == 0 ? ProceduralSprites.Star : ProceduralSprites.Glow);
                img.raycastTarget = false;
                UIFactory.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                var d = new Dot { rt = img.rectTransform, img = img };
                a.Respawn(d, true);
                a._dots.Add(d);
            }
            return a;
        }

        /// <summary>입자 색 두 가지(섞어서 씀).</summary>
        public void SetTheme(Color a, Color b) { _a = a; _b = b; }

        private void Respawn(Dot d, bool anywhere)
        {
            var size = _layer.rect.size;
            if (size.x < 10f) size = new Vector2(1920, 1080);
            float x = Random.Range(-size.x * 0.55f, size.x * 0.55f);
            float y = anywhere ? Random.Range(-size.y * 0.5f, size.y * 0.5f) : -size.y * 0.55f;
            d.rt.anchoredPosition = new Vector2(x, y);
            d.vel = new Vector2(Random.Range(-12f, 12f), Random.Range(14f, 42f));
            d.phase = Random.Range(0f, 10f);
            d.size = Random.Range(5f, 16f);
            d.life = Random.Range(9f, 18f);
            d.t = anywhere ? Random.Range(0f, d.life) : 0f;
            d.rt.sizeDelta = Vector2.one * d.size;
            var c = Color.Lerp(_a, _b, Random.value);
            d.img.color = new Color(c.r, c.g, c.b, 0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            foreach (var d in _dots)
            {
                d.t += dt;
                if (d.t >= d.life) { Respawn(d, false); continue; }
                var p = d.rt.anchoredPosition;
                p += new Vector2(d.vel.x + Mathf.Sin(now * 0.7f + d.phase) * 10f, d.vel.y) * dt;
                d.rt.anchoredPosition = p;
                float k = d.t / d.life;
                float env = Mathf.Clamp01(k / 0.15f) * Mathf.Clamp01((1f - k) / 0.25f);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(now * 2.3f + d.phase * 3f);
                var c = d.img.color;
                c.a = 0.5f * env * twinkle;
                d.img.color = c;
            }
        }
    }
}
