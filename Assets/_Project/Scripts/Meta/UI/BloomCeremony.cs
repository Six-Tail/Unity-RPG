using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Battle;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 숨겨진 특성 개화 연출(Lv.100 도달) — docs/outfits.md
    /// ① 막 내림: 위아래 검은 띠 · 성운 배경(Backgrounds/bloom)이 밝아지며 캐릭터 등장
    /// ② 각성(2.6초): 캐릭터 뒤 · 발밑에 개화 마법진(Fx/bloom_circle)이 펼쳐지고 빛이 모여듦 — 화면이 점점 세게 떨리고
    ///    깨어나는 대사(CharacterLines.Bloom ①)가 자막으로 한 글자씩
    /// ③ 섬광 → 개화: 섬광(Fx/bloom_burst) · 충격파 · 빛의 꽃(Fx/bloom_flower)이 피고 꽃잎(Fx/bloom_petal)이 흩날리며 개화 일러스트로
    /// ④ 개화: '숨겨진 특성 개화'(무지개) · 특성 이름 · 효과, 아래 대화창에 개화 대사(②)가 한 글자씩. 꽃잎이 계속 내림
    /// 화면을 누르면: 각성 중 = 바로 개화로 · 글자가 나오는 중 = 다 보여 줌 · 다 나왔으면 닫음.
    /// 개화 일러스트가 있으면 의상을 '개화'로 갈아입힘(미리보기면 그대로). 효과 그림은 모두 흰 빛이라 속성 색을 곱해 씀.
    /// </summary>
    public class BloomCeremony : MonoBehaviour
    {
        /// <param name="preview">테스트용 미리보기(육성 화면 '개화 연출 미리보기') — 의상을 바꾸지 않음</param>
        public static void Play(CharacterData c, TMP_FontAsset font, System.Action onDone = null, bool preview = false)
        {
            if (c == null) { onDone?.Invoke(); return; }
            var go = new GameObject("BloomCeremony");
            var bc = go.AddComponent<BloomCeremony>();
            bc._preview = preview;
            bc.StartCoroutine(bc.Run(c, font ?? OverlayWindow.DefaultFont(), onDone));
        }

        private bool _tapped, _preview;
        private TMP_FontAsset _font;
        private Color _tint;
        private RectTransform _fx;   // 입자 놓는 곳
        private readonly List<Particle> _parts = new();

        private class Particle
        {
            public RectTransform rt; public Image im;
            public Vector2 pos, vel; public float life, age, spin, size, alpha = 1f, gravity, sway;
            public bool toCenter; public Vector2 from;
        }

        private bool Tap() { if (!_tapped) return false; _tapped = false; return true; }
        private static float Dt => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // ---------------- 만들기 도우미 ----------------

        private Image Img(string name, Transform parent, Sprite s, Color c, Vector2 size)
        {
            var im = UIFactory.AddImage(name, parent, c, s);
            im.raycastTarget = false;
            im.rectTransform.sizeDelta = size;
            return im;
        }

        private static Sprite Fx(string key, Sprite fallback) => GameArt.Load("Fx/" + key) ?? fallback;
        private Color Tinted(float white, float a) { var c = Color.Lerp(_tint, Color.white, white); c.a = a; return c; }

        private TextMeshProUGUI Label(Transform parent, string s, float size, Color c, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = UIFactory.AddText("Text", parent, s, size, _font, c, align, style);
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        private static void Bottom(RectTransform rt, Vector2 size, Vector2 pos)
        {
            UIFactory.Anchor(rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        // ---------------- 입자 ----------------

        private void Spawn(Sprite s, Vector2 pos, Vector2 vel, float life, float size, Color c, float spin = 0f, float gravity = 0f, float sway = 0f)
        {
            var im = Img("P", _fx, s, c, new Vector2(size, size));
            im.rectTransform.anchoredPosition = pos;
            im.rectTransform.localEulerAngles = new Vector3(0, 0, Random.value * 360f);
            _parts.Add(new Particle { rt = im.rectTransform, im = im, pos = pos, vel = vel, life = life, size = size, spin = spin, gravity = gravity, sway = sway, alpha = c.a });
        }

        /// <summary>바깥에서 가운데로 빨려 드는 빛 입자.</summary>
        private void SpawnInward(Sprite s, float dist, float life, float size, Color c)
        {
            float ang = Random.value * Mathf.PI * 2f;
            var from = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
            var im = Img("P", _fx, s, c, new Vector2(size, size));
            im.rectTransform.anchoredPosition = from;
            _parts.Add(new Particle { rt = im.rectTransform, im = im, from = from, toCenter = true, life = life, size = size, alpha = c.a });
        }

        private void TickParticles(float dt)
        {
            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                var p = _parts[i];
                p.age += dt;
                float u = p.age / p.life;
                if (u >= 1f || p.rt == null) { if (p.rt != null) Destroy(p.rt.gameObject); _parts.RemoveAt(i); continue; }
                if (p.toCenter)
                {
                    p.rt.anchoredPosition = Vector2.Lerp(p.from, Vector2.zero, u * u);
                    p.rt.localScale = Vector3.one * (1f - 0.6f * u);
                    var c = p.im.color; c.a = p.alpha * Mathf.Sin(u * Mathf.PI); p.im.color = c;
                    continue;
                }
                p.vel.y -= p.gravity * dt;
                p.vel *= 1f - 1.4f * dt;   // 공기 저항
                p.pos += p.vel * dt + new Vector2(Mathf.Sin(p.age * 3f + p.size) * p.sway * dt, 0f);
                p.rt.anchoredPosition = p.pos;
                p.rt.localEulerAngles += new Vector3(0, 0, p.spin * dt);
                var col = p.im.color; col.a = p.alpha * (u < 0.15f ? u / 0.15f : 1f - Mathf.InverseLerp(0.6f, 1f, u)); p.im.color = col;
            }
        }

        // ---------------- 연출 ----------------

        private IEnumerator Run(CharacterData c, TMP_FontAsset font, System.Action onDone)
        {
            _font = font;
            string key = PlayerRoster.KeyOf(c);
            _tint = Traits.ColorOf(c.element);
            var (awakenLine, bloomLine) = CharacterLines.Bloom(c);
            var hidden = CharacterKits.Hidden(c);

            var circleS = Fx("bloom_circle", Fx("magic_circle", ProceduralSprites.Ring));
            var flowerS = Fx("bloom_flower", ProceduralSprites.Glow);
            var burstS = Fx("bloom_burst", Fx("scene_rays", ProceduralSprites.Glow));
            var petalS = Fx("bloom_petal", ProceduralSprites.Star);
            var ringS = Fx("shockwave", ProceduralSprites.Ring);
            var sparkS = Fx("spark_star", ProceduralSprites.Star);

            // ---------------- 무대 ----------------
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;   // 알림 창(450) 위
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            var black = UIFactory.AddImage("Black", transform, new Color(0, 0, 0, 0f), null, raycast: true);
            UIFactory.Stretch(black.rectTransform);
            var tap = black.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => _tapped = true);

            // 성운 배경 + 어둡게 덮개 + 속성 빛
            var world = UIFactory.NewRect("World", transform);
            UIFactory.Stretch(world);
            var bgS = GameArt.Load("Backgrounds/bloom");
            var bg = Img("Bg", world, bgS, new Color(1, 1, 1, 0f), new Vector2(2120, 1192));
            bg.enabled = bgS != null;
            var veil = Img("Veil", world, null, new Color(0.02f, 0.01f, 0.06f, 0.62f), Vector2.zero);
            UIFactory.Stretch(veil.rectTransform);
            var wash = Img("Wash", world, ProceduralSprites.Glow, Tinted(0.2f, 0f), new Vector2(2400, 2000));

            var stage = UIFactory.NewRect("Stage", world);
            UIFactory.Anchor(stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            stage.sizeDelta = Vector2.zero; stage.anchoredPosition = new Vector2(0, 50);

            var aura = Img("Aura", stage, ProceduralSprites.Glow, Tinted(0.1f, 0f), new Vector2(1000, 1000));
            var backCircle = Img("CircleBack", stage, circleS, Tinted(0.35f, 0f), new Vector2(980, 980));
            var innerCircle = Img("CircleInner", stage, circleS, Tinted(0.6f, 0f), new Vector2(560, 560));
            var flower = Img("Flower", stage, flowerS, Tinted(0.45f, 0f), new Vector2(900, 900));
            var rays = new List<Image>();
            for (int i = 0; i < 16; i++)
            {
                var r = Img("Ray", stage, ProceduralSprites.Beam, new Color(1f, 0.95f, 0.85f, 0f), new Vector2(60, 1000));
                r.rectTransform.pivot = new Vector2(0.5f, 0f);
                r.rectTransform.localEulerAngles = new Vector3(0, 0, i * (360f / 16));
                rays.Add(r);
            }
            // 발밑 마법진(납작하게 눕힘)
            var floor = Img("CircleFloor", stage, circleS, Tinted(0.4f, 0f), new Vector2(820, 820));
            floor.rectTransform.anchoredPosition = new Vector2(0, -380);

            var before = GameArt.Load("Characters/" + key);
            var after = GameArt.Load("Characters/" + key + "_bloom");
            var art = Img("Art", stage, before, new Color(1, 1, 1, 0f), new Vector2(660, 880));
            art.preserveAspect = true;
            art.enabled = before != null;
            var artRt = art.rectTransform;

            _fx = UIFactory.NewRect("Fx", stage);
            var burst = Img("Burst", stage, burstS, Tinted(0.7f, 0f), new Vector2(900, 900));
            var ring = Img("Ring", stage, ringS, Tinted(0.5f, 0f), new Vector2(600, 600));

            // 영화 띠(위 · 아래)
            var barTop = Img("BarTop", transform, null, Color.black, new Vector2(0, 130));
            UIFactory.Anchor(barTop.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            barTop.rectTransform.sizeDelta = new Vector2(0, 130);
            var barBot = Img("BarBot", transform, null, Color.black, new Vector2(0, 130));
            UIFactory.Anchor(barBot.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            barBot.rectTransform.sizeDelta = new Vector2(0, 130);
            void Bars(float k) { barTop.rectTransform.anchoredPosition = new Vector2(0, 130 * (1f - k)); barBot.rectTransform.anchoredPosition = new Vector2(0, -130 * (1f - k)); }
            Bars(0f);

            // 자막(각성 대사) — 아래 띠 위
            var sub = Label(transform, "", 30, new Color(1f, 0.96f, 0.9f), FontStyles.Italic);
            Bottom(sub.rectTransform, new Vector2(1500, 60), new Vector2(0, 40));
            var subName = Label(transform, $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(_tint, Color.white, 0.4f))}>{c.displayName}</color>", 22, Color.white);
            Bottom(subName.rectTransform, new Vector2(600, 32), new Vector2(0, 92));
            subName.alpha = 0f;

            if (_preview)
            {
                var tag = Label(transform, "<color=#FFB347>개화 연출 미리보기(테스트) — 의상은 바뀌지 않음</color>", 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                UIFactory.Anchor(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
                tag.rectTransform.sizeDelta = new Vector2(700, 30); tag.rectTransform.anchoredPosition = new Vector2(24, -12);
            }

            var flash = Img("Flash", transform, null, new Color(1, 1, 1, 0f), Vector2.zero);
            UIFactory.Stretch(flash.rectTransform);

            // ---------------- ① 막 내림(0.7초) ----------------
            SoundManager.Duck(0.25f);
            _tapped = false;
            bool skip = false;
            for (float t = 0; t < 0.7f; t += Dt)
            {
                float k = t / 0.7f, e = 1f - (1f - k) * (1f - k);
                black.color = new Color(0, 0, 0, Mathf.Min(1f, k * 2f) * (1f - 0.0f));
                bg.color = new Color(1, 1, 1, e);
                bg.rectTransform.localScale = Vector3.one * (1.08f - 0.04f * e);
                Bars(e);
                art.color = new Color(1, 1, 1, e);
                artRt.localScale = Vector3.one * (0.92f + 0.08f * e);
                aura.color = Tinted(0.1f, 0.2f * e);
                if (Tap()) { skip = true; break; }
                yield return null;
            }
            black.color = new Color(0, 0, 0, 1f);   // 성운 배경 밑바탕(화면 밖 가리기)
            bg.color = Color.white; Bars(1f); art.color = Color.white;

            // ---------------- ② 각성(2.6초) ----------------
            SoundManager.Play(Sfx.GachaSummon);
            subName.alpha = 1f;
            const float charge = 2.6f;
            float spawn = 0f;
            float typing = 0f;
            for (float t = 0; t < charge && !skip; t += Dt)
            {
                float dt = Dt, k = t / charge, ease = k * k;
                // 마법진: 뒤(커지며 돎) · 안쪽(반대로 빠르게) · 발밑(납작)
                float open = Mathf.Clamp01(t / 0.6f), o2 = 1f - (1f - open) * (1f - open);
                backCircle.color = Tinted(0.35f, 0.55f * o2 + 0.25f * ease);
                backCircle.rectTransform.localScale = Vector3.one * (0.5f + 0.55f * o2 + 0.05f * Mathf.Sin(t * 6f));
                backCircle.rectTransform.localEulerAngles = new Vector3(0, 0, t * (25f + 140f * ease));
                innerCircle.color = Tinted(0.6f, 0.7f * o2);
                innerCircle.rectTransform.localScale = Vector3.one * (0.4f + 0.6f * o2 - 0.25f * ease);
                innerCircle.rectTransform.localEulerAngles = new Vector3(0, 0, -t * (60f + 260f * ease));
                floor.color = Tinted(0.4f, 0.75f * o2);
                floor.rectTransform.localScale = new Vector3(1f + 0.15f * ease, 0.3f, 1f) * (0.6f + 0.4f * o2);
                floor.rectTransform.localEulerAngles = new Vector3(0, 0, t * 40f);
                aura.color = Tinted(0.1f, 0.25f + 0.5f * ease);
                aura.rectTransform.localScale = Vector3.one * (0.7f + 0.9f * ease + 0.06f * Mathf.Sin(t * 22f));
                wash.color = Tinted(0.2f, 0.18f * ease);
                for (int i = 0; i < rays.Count; i++)
                {
                    rays[i].color = new Color(1f, 0.95f, 0.85f, 0.32f * ease);
                    rays[i].rectTransform.localEulerAngles = new Vector3(0, 0, i * (360f / rays.Count) + t * (30f + 90f * ease));
                }
                // 캐릭터: 떠오르며 떨림, 빛을 머금음
                artRt.anchoredPosition = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (9f * ease) + new Vector2(0, 26f * k);
                art.color = Color.Lerp(Color.white, Tinted(0.75f, 1f), ease * 0.6f);
                stage.anchoredPosition = new Vector2(0, 50) + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (5f * ease * ease);
                // 빨려 드는 빛 입자 + 반짝이
                spawn += dt * (24f + 70f * ease);
                while (spawn >= 1f)
                {
                    spawn -= 1f;
                    bool star = Random.value < 0.35f;
                    SpawnInward(star ? sparkS : ProceduralSprites.Circle, Random.Range(560f, 980f), Random.Range(0.45f, 0.85f),
                        star ? Random.Range(26f, 46f) : Random.Range(8f, 20f), Tinted(Random.Range(0.3f, 0.8f), 1f));
                }
                TickParticles(dt);
                // 자막: 한 글자씩
                typing += dt;
                int n = Mathf.Min(awakenLine.Length, Mathf.FloorToInt(typing / 0.045f));
                sub.text = $"\"{awakenLine.Substring(0, n)}\"";
                if (Tap()) { skip = true; break; }
                yield return null;
            }
            sub.text = ""; subName.alpha = 0f;

            // ---------------- ③ 섬광 → 개화 ----------------
            SoundManager.Play(Sfx.GachaRare);
            for (float t = 0; t < 0.16f; t += Dt)
            {
                float k = t / 0.16f;
                flash.color = new Color(1, 1, 1, k);
                innerCircle.rectTransform.localScale = Vector3.one * Mathf.Lerp(innerCircle.rectTransform.localScale.x, 0.1f, k);
                TickParticles(Dt);
                yield return null;
            }
            flash.color = Color.white;
            foreach (var p in _parts) if (p.rt != null) Destroy(p.rt.gameObject);
            _parts.Clear();
            if (after != null)
            {
                art.sprite = after;
                if (!_preview && CharacterOutfits.Unlocked(c, CharacterOutfits.Find(CharacterOutfits.BloomId), CharacterKits.HiddenLevel))
                    CharacterOutfits.Set(c, CharacterOutfits.BloomId);   // 개화 의상으로 갈아입힘(옷장에서 되돌릴 수 있음)
            }
            art.color = Color.white;
            artRt.anchoredPosition = Vector2.zero;
            stage.anchoredPosition = new Vector2(0, 50);
            innerCircle.color = Tinted(0.6f, 0f);
            // 꽃잎 · 반짝이가 터져 나감
            for (int i = 0; i < 70; i++)
            {
                float ang = Random.value * Mathf.PI * 2f, sp = Random.Range(500f, 1500f);
                bool petal = i < 48;
                Spawn(petal ? petalS : sparkS, Vector2.zero, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp, Random.Range(1.4f, 2.6f),
                    petal ? Random.Range(34f, 64f) : Random.Range(24f, 44f),
                    petal ? Color.Lerp(Tinted(0.35f, 1f), new Color(1f, 0.86f, 0.94f, 1f), Random.value * 0.6f) : Tinted(0.8f, 1f),
                    Random.Range(-260f, 260f), petal ? 120f : 0f, petal ? 60f : 0f);
            }

            // 섬광이 걷히며 빛의 꽃 · 충격파 · 섬광
            for (float t = 0; t < 0.9f; t += Dt)
            {
                float dt = Dt, k = t / 0.9f, e = 1f - (1f - k) * (1f - k) * (1f - k);
                flash.color = new Color(1, 1, 1, Mathf.Clamp01(1f - t / 0.5f));
                burst.color = Tinted(0.7f, 1f - k);
                burst.rectTransform.localScale = Vector3.one * (0.6f + 2.2f * e);
                burst.rectTransform.localEulerAngles = new Vector3(0, 0, t * 30f);
                ring.color = Tinted(0.5f, 0.9f * (1f - k));
                ring.rectTransform.localScale = Vector3.one * (0.3f + 3.4f * e);
                flower.color = Tinted(0.45f, 0.85f * Mathf.Clamp01(k * 2f));
                flower.rectTransform.localScale = Vector3.one * (0.3f + 1.2f * e);
                flower.rectTransform.localEulerAngles = new Vector3(0, 0, -20f + 20f * e);
                float pop = k < 0.35f ? Mathf.Lerp(1.18f, 0.98f, k / 0.35f) : Mathf.Lerp(0.98f, 1f, (k - 0.35f) / 0.65f);
                artRt.localScale = Vector3.one * pop;
                stage.anchoredPosition = new Vector2(0, 50) + (k < 0.3f ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (14f * (1f - k / 0.3f)) : Vector2.zero);
                Bars(1f - e);
                backCircle.color = Tinted(0.35f, 0.5f);
                backCircle.rectTransform.localScale = Vector3.one * (1.1f + 0.3f * e);
                backCircle.rectTransform.localEulerAngles += new Vector3(0, 0, 40f * dt);
                floor.color = Tinted(0.4f, 0.6f);
                aura.color = Tinted(0.1f, 0.6f);
                aura.rectTransform.localScale = Vector3.one * (1.4f + 0.3f * e);
                wash.color = Tinted(0.2f, 0.22f * (1f - k) + 0.1f);
                TickParticles(dt);
                yield return null;
            }
            flash.color = new Color(1, 1, 1, 0f);
            burst.enabled = false; ring.enabled = false;
            stage.anchoredPosition = new Vector2(0, 50);
            artRt.localScale = Vector3.one;

            // ---------------- ④ 개화: 제목 · 특성 · 대사 ----------------
            SoundManager.Play(Sfx.LevelUp, 0.8f);
            // 캐릭터는 왼쪽으로 비켜서고, 오른쪽 어두운 판에 제목 · 특성 · 효과(밝은 일러스트 위에 글자를 얹지 않음)
            const float StageX = -400f;
            var panel = Img("Panel", transform, ProceduralSprites.Rounded, new Color(0.04f, 0.03f, 0.1f, 0.86f), new Vector2(820, 470));
            panel.type = Image.Type.Sliced;
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.rectTransform.anchoredPosition = new Vector2(470, 110);
            var panelEdge = Img("Edge", panel.transform, null, Tinted(0.35f, 0.9f), new Vector2(6, 430));
            panelEdge.rectTransform.anchoredPosition = new Vector2(-404, 0);
            var head = Label(panel.transform, "숨겨진 특성 개화", 44, Color.white, FontStyles.Bold);
            head.characterSpacing = 14f;
            head.rectTransform.sizeDelta = new Vector2(780, 64); head.rectTransform.anchoredPosition = new Vector2(0, 170);
            head.gameObject.AddComponent<RainbowText>();
            var deco = Img("Deco", panel.transform, ProceduralSprites.Beam, Tinted(0.5f, 0f), new Vector2(14, 680));
            deco.rectTransform.localEulerAngles = new Vector3(0, 0, 90);
            deco.rectTransform.anchoredPosition = new Vector2(0, 126);
            string gem = ColorUtility.ToHtmlStringRGB(Color.Lerp(_tint, Color.white, 0.55f));
            var who = Label(panel.transform, $"<color=#{gem}>{c.displayName}</color>", 26, Color.white);
            who.rectTransform.sizeDelta = new Vector2(780, 36); who.rectTransform.anchoredPosition = new Vector2(0, 86);
            var trait = Label(panel.transform, $"<color=#{gem}>◆</color>  <b>{hidden.name}</b>  <color=#{gem}>◆</color>", 48, Color.white);
            trait.rectTransform.sizeDelta = new Vector2(780, 64); trait.rectTransform.anchoredPosition = new Vector2(0, 36);
            TextOutline.Apply(trait, 0.18f, new Color32(10, 6, 24, 255));
            var desc = Label(panel.transform, hidden.Desc, 26, new Color(0.93f, 0.95f, 1f), FontStyles.Normal, TextAlignmentOptions.Top);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.lineSpacing = 10f;
            desc.rectTransform.sizeDelta = new Vector2(740, 200); desc.rectTransform.anchoredPosition = new Vector2(0, -110);
            var topTexts = new TMP_Text[] { head, who, trait, desc };
            foreach (var tx in topTexts) tx.alpha = 0f;
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;

            // 대화창(아래)
            var box = Img("Box", transform, ProceduralSprites.Rounded, new Color(0.04f, 0.03f, 0.09f, 0.92f), new Vector2(1380, 170));
            box.type = Image.Type.Sliced;
            Bottom(box.rectTransform, new Vector2(1380, 170), new Vector2(0, -200));
            var plateCol = Color.Lerp(_tint, new Color(0.06f, 0.05f, 0.12f), 0.5f); plateCol.a = 0.98f;   // 밝은 속성 색(물리 · 얼음)이어도 흰 글자가 보이게 어둡게
            var plate = Img("Plate", box.transform, ProceduralSprites.Rounded, plateCol, new Vector2(300, 50));
            plate.type = Image.Type.Sliced;
            UIFactory.Anchor(plate.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f));
            plate.rectTransform.anchoredPosition = new Vector2(40, 0);
            var plateEdge = Img("PlateEdge", plate.transform, null, Tinted(0.45f, 1f), new Vector2(300, 3));
            UIFactory.Anchor(plateEdge.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            plateEdge.rectTransform.sizeDelta = new Vector2(0, 3);
            var plateText = Label(plate.transform, $"<b>{c.displayName}</b>", 26, Color.white);
            UIFactory.Stretch(plateText.rectTransform);
            TextOutline.Apply(plateText, 0.2f, new Color32(10, 6, 24, 255));
            var line = Label(box.transform, "", 32, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            line.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Stretch(line.rectTransform, 48, 40, 48, 40);   // 왼 · 아래 · 오른 · 위(이름표 아래부터)
            var hint = Label(box.transform, "▼ 화면을 누르면 계속", 19, new Color(0.85f, 0.88f, 0.96f), FontStyles.Normal, TextAlignmentOptions.Right);
            UIFactory.Anchor(hint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            hint.rectTransform.sizeDelta = new Vector2(400, 28); hint.rectTransform.anchoredPosition = new Vector2(-24, 10);
            hint.alpha = 0f;

            _tapped = false;
            float fall = 0f;
            bool typed = false;
            for (float t = 0; ; t += Dt)
            {
                float dt = Dt;
                // 제목 · 특성 · 효과가 차례로
                // 캐릭터가 왼쪽으로 비켜섬 → 오른쪽 판이 밀려 들어옴
                float ks = Mathf.Clamp01(t / 0.5f), es = 1f - (1f - ks) * (1f - ks);
                stage.anchoredPosition = new Vector2(StageX * es, 50);
                float kp = Mathf.Clamp01((t - 0.15f) / 0.4f), ep = 1f - (1f - kp) * (1f - kp);
                panelGroup.alpha = kp;
                panel.rectTransform.anchoredPosition = new Vector2(470 + 120f * (1f - ep), 110);
                float k1 = Mathf.Clamp01((t - 0.3f) / 0.45f), k2 = Mathf.Clamp01((t - 0.5f) / 0.45f), k3 = Mathf.Clamp01((t - 0.75f) / 0.45f);
                head.alpha = k1; head.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, 1f - (1f - k1) * (1f - k1));
                who.alpha = k2;
                trait.alpha = k2; trait.rectTransform.anchoredPosition = new Vector2(0, 36 - 24f * (1f - k2));
                desc.alpha = k3;
                deco.color = Tinted(0.5f, 0.7f * k1);
                deco.rectTransform.sizeDelta = new Vector2(14, 680f * k1);
                // 대화창이 올라오고 대사가 한 글자씩
                float kb = Mathf.Clamp01((t - 0.8f) / 0.35f), eb = 1f - (1f - kb) * (1f - kb);
                box.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(-200f, 28f, eb));
                int n = Mathf.Clamp(Mathf.FloorToInt((t - 1.2f) / 0.04f), 0, bloomLine.Length);
                if (typed) n = bloomLine.Length;
                line.text = bloomLine.Substring(0, n);
                bool done = n >= bloomLine.Length;
                hint.alpha = done ? 0.6f + 0.4f * Mathf.Sin(t * 3f) : 0f;
                // 캐릭터 · 꽃 · 마법진이 숨 쉬듯
                artRt.anchoredPosition = new Vector2(0, 8f * Mathf.Sin(t * 1.5f));
                flower.color = Tinted(0.45f, 0.7f + 0.15f * Mathf.Sin(t * 2f));
                flower.rectTransform.localScale = Vector3.one * (1.5f + 0.05f * Mathf.Sin(t * 1.7f));
                flower.rectTransform.localEulerAngles += new Vector3(0, 0, 6f * dt);
                backCircle.rectTransform.localEulerAngles += new Vector3(0, 0, 12f * dt);
                floor.rectTransform.localEulerAngles += new Vector3(0, 0, -18f * dt);
                aura.rectTransform.localScale = Vector3.one * (1.6f + 0.08f * Mathf.Sin(t * 2.3f));
                for (int i = 0; i < rays.Count; i++)
                {
                    rays[i].color = new Color(1f, 0.95f, 0.85f, 0.16f + 0.06f * Mathf.Sin(t * 2f + i));
                    rays[i].rectTransform.localEulerAngles = new Vector3(0, 0, i * (360f / rays.Count) + t * 10f);
                }
                // 위에서 꽃잎이 천천히 내림
                fall += dt * 7f;
                while (fall >= 1f)
                {
                    fall -= 1f;
                    Spawn(petalS, new Vector2(Random.Range(-1000f, 1000f) - stage.anchoredPosition.x, 620f), new Vector2(Random.Range(-40f, 40f), Random.Range(-160f, -90f)),
                        Random.Range(5f, 8f), Random.Range(26f, 48f), Color.Lerp(Tinted(0.4f, 0.85f), new Color(1f, 0.88f, 0.95f, 0.85f), Random.value * 0.5f),
                        Random.Range(-90f, 90f), 0f, 90f);
                }
                TickParticles(dt);
                if (Tap())
                {
                    if (t < 1.2f) { t = 1.2f; }                 // 제목이 아직이면 바로 다 보이게
                    else if (!done) typed = true;               // 대사가 나오는 중이면 다 보여 줌
                    else break;                                 // 다 나왔으면 닫기
                }
                yield return null;
            }

            // ---------------- 닫기 ----------------
            var group = gameObject.AddComponent<CanvasGroup>();
            for (float t = 0; t < 0.4f; t += Dt) { group.alpha = 1f - t / 0.4f; yield return null; }
            SoundManager.Duck(1f);
            Destroy(gameObject);
            onDone?.Invoke();
        }
    }
}
