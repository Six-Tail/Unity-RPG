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
    /// 숨겨진 특성 개화 연출(Lv.100 도달): 어두워짐 → 빛이 모여듦(빛줄기 · 입자 · 떨림) → 섬광 → 개화 일러스트로 바뀌며 등장
    /// → '숨겨진 특성 개화' · 특성 이름(무지개) · 효과. 화면을 누르면 닫힘. 개화 일러스트가 있으면 의상을 '개화'로 갈아입힘.
    /// </summary>
    public class BloomCeremony : MonoBehaviour
    {
        public static void Play(CharacterData c, TMP_FontAsset font, System.Action onDone = null)
        {
            if (c == null) { onDone?.Invoke(); return; }
            var go = new GameObject("BloomCeremony");
            var bc = go.AddComponent<BloomCeremony>();
            bc.StartCoroutine(bc.Run(c, font ?? OverlayWindow.DefaultFont(), onDone));
        }

        private bool _tapped;

        private IEnumerator Run(CharacterData c, TMP_FontAsset font, System.Action onDone)
        {
            string key = PlayerRoster.KeyOf(c);
            Color tint = Traits.ColorOf(c.element);

            // ---------------- 무대 ----------------
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;   // 알림 창(450) 위
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            var bg = UIFactory.AddImage("Dim", transform, new Color(0.01f, 0.01f, 0.03f, 0f), null, raycast: true);
            UIFactory.Stretch(bg.rectTransform);
            var tap = bg.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => _tapped = true);

            var stage = UIFactory.NewRect("Stage", transform);
            UIFactory.Anchor(stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            stage.sizeDelta = Vector2.zero; stage.anchoredPosition = new Vector2(0, 40);

            var glow = UIFactory.AddImage("Glow", stage, new Color(tint.r, tint.g, tint.b, 0f), ProceduralSprites.Glow);
            glow.rectTransform.sizeDelta = new Vector2(900, 900);
            var rays = new List<Image>();
            for (int i = 0; i < 14; i++)
            {
                var r = UIFactory.AddImage("Ray", stage, new Color(1f, 0.95f, 0.85f, 0f), ProceduralSprites.Beam);
                r.rectTransform.pivot = new Vector2(0.5f, 0f);
                r.rectTransform.sizeDelta = new Vector2(70, 900);
                r.rectTransform.localEulerAngles = new Vector3(0, 0, i * (360f / 14));
                rays.Add(r);
            }

            var before = GameArt.Load("Characters/" + key);
            var after = GameArt.Load("Characters/" + key + "_bloom");
            var art = UIFactory.AddImage("Art", stage, new Color(1, 1, 1, 0f), before);
            art.preserveAspect = true;
            art.rectTransform.sizeDelta = new Vector2(600, 800);
            art.enabled = before != null;

            var sparks = UIFactory.NewRect("Sparks", stage);
            var flash = UIFactory.AddImage("Flash", transform, new Color(1, 1, 1, 0f));
            UIFactory.Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            // ---------------- 1) 어두워지며 등장 ----------------
            SoundManager.Duck(0.3f);
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.5f;
                bg.color = new Color(0.01f, 0.01f, 0.03f, 0.94f * k);
                art.color = new Color(1, 1, 1, k);
                yield return null;
            }

            // ---------------- 2) 빛이 모여듦(1.9초) ----------------
            SoundManager.Play(Sfx.GachaSummon);
            const float charge = 1.9f;
            float spawn = 0f;
            var live = new List<(RectTransform rt, Image im, Vector2 from, float born, float life)>();
            for (float t = 0; t < charge; t += Time.unscaledDeltaTime)
            {
                float k = t / charge;
                glow.color = new Color(tint.r, tint.g, tint.b, 0.25f + 0.55f * k);
                glow.rectTransform.localScale = Vector3.one * (0.6f + 0.9f * k + 0.05f * Mathf.Sin(t * 20f));
                stage.localEulerAngles = Vector3.zero;
                for (int i = 0; i < rays.Count; i++)
                {
                    rays[i].color = new Color(1f, 0.95f, 0.85f, 0.35f * k);
                    rays[i].rectTransform.localEulerAngles = new Vector3(0, 0, i * (360f / rays.Count) + t * 40f);
                }
                // 떨림(점점 세게)
                art.rectTransform.anchoredPosition = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (6f * k);
                art.color = Color.Lerp(Color.white, new Color(1f, 0.97f, 0.9f), k);
                // 바깥에서 가운데로 빨려 드는 빛 입자
                spawn += Time.unscaledDeltaTime * (20f + 50f * k);
                while (spawn >= 1f)
                {
                    spawn -= 1f;
                    var p = UIFactory.AddImage("Spark", sparks, Color.Lerp(tint, Color.white, Random.value * 0.6f),
                        Random.value < 0.5f ? ProceduralSprites.Star : ProceduralSprites.Circle);
                    float sz = Random.Range(10f, 26f);
                    p.rectTransform.sizeDelta = new Vector2(sz, sz);
                    float ang = Random.value * Mathf.PI * 2f, dist = Random.Range(520f, 900f);
                    var from = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                    p.rectTransform.anchoredPosition = from;
                    live.Add((p.rectTransform, p, from, t, Random.Range(0.45f, 0.8f)));
                }
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    var s = live[i];
                    float u = (t - s.born) / s.life;
                    if (u >= 1f) { Destroy(s.rt.gameObject); live.RemoveAt(i); continue; }
                    s.rt.anchoredPosition = Vector2.Lerp(s.from, Vector2.zero, u * u);
                    s.im.color = new Color(s.im.color.r, s.im.color.g, s.im.color.b, Mathf.Sin(u * Mathf.PI));
                }
                yield return null;
            }
            foreach (var s in live) if (s.rt != null) Destroy(s.rt.gameObject);
            art.rectTransform.anchoredPosition = Vector2.zero;

            // ---------------- 3) 섬광 → 개화 일러스트 ----------------
            SoundManager.Play(Sfx.GachaRare);
            for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime) { flash.color = new Color(1, 1, 1, t / 0.15f); yield return null; }
            flash.color = Color.white;
            if (after != null)
            {
                art.sprite = after;
                if (CharacterOutfits.Unlocked(c, CharacterOutfits.Find(CharacterOutfits.BloomId), Battle.CharacterKits.HiddenLevel))
                    CharacterOutfits.Set(c, CharacterOutfits.BloomId);   // 개화 의상으로 갈아입힘(옷장에서 되돌릴 수 있음)
            }
            art.color = Color.white;
            // 꽃잎이 터져 나감
            for (int i = 0; i < 60; i++)
            {
                var p = UIFactory.AddImage("Petal", sparks, Color.Lerp(tint, new Color(1f, 0.85f, 0.95f), Random.value), ProceduralSprites.Star);
                float sz = Random.Range(12f, 30f);
                p.rectTransform.sizeDelta = new Vector2(sz, sz);
                float ang = Random.value * Mathf.PI * 2f;
                live.Add((p.rectTransform, p, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(500f, 1000f), 0f, Random.Range(0.9f, 1.5f)));
            }
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime) { flash.color = new Color(1, 1, 1, 1f - t / 0.45f); yield return null; }
            flash.color = new Color(1, 1, 1, 0f);

            // ---------------- 4) 등장 + 이름 ----------------
            var head = UIFactory.AddText("Head", transform, "숨겨진 특성 개화", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(head.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            head.rectTransform.sizeDelta = new Vector2(1200, 50); head.rectTransform.anchoredPosition = new Vector2(0, 230);
            head.characterSpacing = 12f;
            head.gameObject.AddComponent<RainbowText>();
            var hidden = CharacterKits.Hidden(c);
            var nameT = UIFactory.AddText("Name", transform, $"{c.displayName}  ·  <b>{hidden.name}</b>", 46, font, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            nameT.richText = true;
            UIFactory.Anchor(nameT.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            nameT.rectTransform.sizeDelta = new Vector2(1400, 64); nameT.rectTransform.anchoredPosition = new Vector2(0, 162);
            var desc = UIFactory.AddText("Desc", transform, hidden.Desc, 22, font, new Color(0.9f, 0.92f, 0.98f), TextAlignmentOptions.Center, FontStyles.Normal);
            desc.richText = true; desc.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Anchor(desc.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            desc.rectTransform.sizeDelta = new Vector2(1300, 90); desc.rectTransform.anchoredPosition = new Vector2(0, 66);
            var hint = UIFactory.AddText("Hint", transform, "화면을 누르면 계속", 18, font, new Color(0.7f, 0.74f, 0.82f), TextAlignmentOptions.Center, FontStyles.Normal);
            UIFactory.Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            hint.rectTransform.sizeDelta = new Vector2(600, 30); hint.rectTransform.anchoredPosition = new Vector2(0, 24);
            var texts = new TMP_Text[] { head, nameT, desc, hint };

            _tapped = false;
            for (float t = 0; ; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.6f);
                // 개화 일러스트: 살짝 커졌다가 자리 잡음 + 은은하게 떠 있음
                float pop = t < 0.35f ? Mathf.Lerp(1.12f, 1f, t / 0.35f) : 1f;
                art.rectTransform.localScale = Vector3.one * pop;
                art.rectTransform.anchoredPosition = new Vector2(0, 6f * Mathf.Sin(t * 1.6f));
                glow.rectTransform.localScale = Vector3.one * (1.5f + 0.06f * Mathf.Sin(t * 2.2f));
                glow.color = new Color(tint.r, tint.g, tint.b, 0.6f);
                for (int i = 0; i < rays.Count; i++)
                {
                    rays[i].color = new Color(1f, 0.95f, 0.85f, 0.22f);
                    rays[i].rectTransform.localEulerAngles = new Vector3(0, 0, i * (360f / rays.Count) + 76f + t * 12f);
                }
                foreach (var tx in texts) tx.alpha = k;
                hint.alpha = k * (0.55f + 0.45f * Mathf.Sin(t * 3f));
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    var s = live[i];
                    float u = t / s.life;
                    if (u >= 1f) { Destroy(s.rt.gameObject); live.RemoveAt(i); continue; }
                    s.rt.anchoredPosition = Vector2.Lerp(Vector2.zero, s.from, 1f - (1f - u) * (1f - u));
                    s.rt.localEulerAngles = new Vector3(0, 0, t * 200f);
                    s.im.color = new Color(s.im.color.r, s.im.color.g, s.im.color.b, 1f - u);
                }
                if (_tapped && t > 0.8f) break;
                _tapped = false;
                yield return null;
            }

            // ---------------- 5) 닫기 ----------------
            var group = gameObject.AddComponent<CanvasGroup>();
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.35f; yield return null; }
            SoundManager.Duck(1f);
            Destroy(gameObject);
            onDone?.Invoke();
        }
    }
}
