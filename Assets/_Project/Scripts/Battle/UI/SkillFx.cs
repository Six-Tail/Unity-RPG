using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 전투 스킬 이펙트(이미지 에셋 없이 코드로 만든 파티클). 전투 화면의 유닛 줄 위 레이어에 그립니다.
    /// - 기본 공격: 속성 색 베기 2줄 + 불꽃
    /// - 스킬: 속성별 연출(화염 불길 · 얼음 파편 · 번개 낙뢰 · 바람 소용돌이 · 물리 충격파)
    /// - 궁극기: 빛기둥 + 큰 속성 연출 + 충격파 2겹 + 화면 번쩍임
    /// - 다단 히트 추가 타격, 시전 모으기, 회복, 버프/디버프/지속 피해 부여
    /// 모든 시간은 실시간(Time.unscaledDeltaTime) × Speed.
    /// </summary>
    public class SkillFx : MonoBehaviour
    {
        private class P
        {
            public RectTransform rt;
            public Image img;
            public float delay, t, life;
            public Vector2 pos, vel, gravity;
            public float drag;
            public Vector2 size0, size1;
            public float rot, rotVel;
            public bool alignToVel;
            public Color c0, c1;
            public float fadeIn = 0.12f;
            public bool flicker;
            // 궤도(소용돌이·모으기)
            public bool orbit;
            public Vector2 center;
            public float radius, radiusVel, angle, angVel;
        }

        private readonly List<P> _live = new();
        private readonly Stack<Image> _pool = new();
        private RectTransform _layer;

        /// <summary>이펙트 재생 속도(배속).</summary>
        public float Speed { get; set; } = 1f;

        public static SkillFx Create(Transform parent)
        {
            var rt = UIFactory.NewRect("SkillFx", parent);
            UIFactory.Stretch(rt);
            var fx = rt.gameObject.AddComponent<SkillFx>();
            fx._layer = rt;
            return fx;
        }

        /// <summary>대상 RectTransform 가운데를 이 레이어 좌표로.</summary>
        public Vector2 ToLocal(RectTransform target)
        {
            if (target == null) return Vector2.zero;
            return _layer.InverseTransformPoint(target.TransformPoint(target.rect.center));
        }

        public void Clear()
        {
            foreach (var p in _live) Release(p);
            _live.Clear();
        }

        // ================= 연출 레시피 =================

        /// <summary>기본 공격: 비스듬한 베기 2줄 + 작은 번쩍임 + 불꽃.</summary>
        public void Basic(Element e, Vector2 pos, float s, float delay)
        {
            Color c = Main(e), hot = Hot(e);
            float a = Random.Range(25f, 45f);
            Slash(pos, hot, a, 240 * s, 30 * s, 0.24f, delay);
            Slash(pos, c, a - 75f, 200 * s, 24 * s, 0.24f, delay + 0.07f);
            Flash(pos, hot, 150 * s, 0.2f, delay + 0.03f);
            Sparks(pos, hot, 7, 420 * s, 16 * s, 0.32f, delay + 0.03f);
        }

        /// <summary>스킬: 속성별 연출.</summary>
        public void Skill(Element e, Vector2 pos, float s, float delay)
        {
            switch (e)
            {
                case Element.Fire: Fire(pos, s, delay); break;
                case Element.Ice: Ice(pos, s, delay); break;
                case Element.Lightning: Lightning(pos, s, delay); break;
                case Element.Wind: Wind(pos, s, delay); break;
                default: Physical(pos, s, delay); break;
            }
        }

        /// <summary>궁극기: 빛기둥 + 큰 속성 연출 + 충격파 2겹 + 금빛 불꽃.</summary>
        public void Ultimate(Element e, Vector2 pos, float s, float delay)
        {
            Color c = Main(e), hot = Hot(e);
            var pillar = Add(ProceduralSprites.Beam, pos + new Vector2(0, 120 * s), 0.65f, delay);
            pillar.size0 = new Vector2(50 * s, 700 * s); pillar.size1 = new Vector2(230 * s, 900 * s);
            pillar.c0 = Color.white; pillar.c1 = WithA(hot, 0f); pillar.fadeIn = 0.12f;
            var core = Add(ProceduralSprites.Beam, pos + new Vector2(0, 120 * s), 0.5f, delay);
            core.size0 = new Vector2(20 * s, 700 * s); core.size1 = new Vector2(80 * s, 900 * s);
            core.c0 = Color.white; core.c1 = WithA(Color.white, 0f);

            Skill(e, pos, s * 1.45f, delay + 0.08f);
            Ring(pos, hot, 40 * s, 460 * s, 0.5f, delay + 0.1f);
            Ring(pos, c, 20 * s, 360 * s, 0.6f, delay + 0.26f);
            Sparks(pos, new Color(1f, 0.88f, 0.45f), 16, 640 * s, 22 * s, 0.6f, delay + 0.1f);
            Sparks(pos, hot, 12, 480 * s, 18 * s, 0.5f, delay + 0.18f);
        }

        /// <summary>화면 전체 번쩍임(궁극기).</summary>
        public void ScreenFlash(Color c, float life, float alpha = 0.4f)
        {
            var p = Add(null, Vector2.zero, life, 0f);
            p.size0 = p.size1 = _layer.rect.size + new Vector2(200, 200);
            p.c0 = WithA(c, alpha); p.c1 = WithA(c, 0f); p.fadeIn = 0.05f;
        }

        /// <summary>다단 히트의 추가 타격(작은 베기/충격 + 불꽃).</summary>
        public void Impact(Element e, SkillCategory cat, Vector2 pos, float s, float delay)
        {
            Color hot = Hot(e);
            Vector2 j = Random.insideUnitCircle * 26f * s;
            if (cat == SkillCategory.Basic || e == Element.Physical)
                Slash(pos + j, hot, Random.Range(-70f, 70f), 190 * s, 22 * s, 0.2f, delay);
            else
                Ring(pos + j, hot, 20 * s, 150 * s, 0.25f, delay);
            Flash(pos + j, hot, 120 * s, 0.16f, delay);
            Sparks(pos + j, hot, 5, 360 * s, 14 * s, 0.28f, delay);
        }

        /// <summary>시전 모으기: 속성 색 빛 알갱이가 시전자 쪽으로 빨려 들며 가운데가 밝아짐.</summary>
        public void Charge(Element e, Vector2 pos, float s, float dur)
        {
            if (dur <= 0.05f) return;
            Color hot = Hot(e);
            for (int i = 0; i < 14; i++)
            {
                var p = Add(ProceduralSprites.Star, pos, dur, Random.Range(0f, dur * 0.25f));
                p.orbit = true; p.center = pos;
                p.radius = Random.Range(120f, 170f) * s; p.radiusVel = -p.radius / (dur * 0.9f);
                p.angle = Random.Range(0f, Mathf.PI * 2f); p.angVel = Random.Range(3f, 5f);
                p.size0 = Vector2.one * 20 * s; p.size1 = Vector2.one * 8 * s;
                p.c0 = hot; p.c1 = WithA(Color.white, 0.6f); p.fadeIn = 0.3f;
            }
            var g = Add(ProceduralSprites.Glow, pos, dur, 0f);
            g.size0 = Vector2.one * 30 * s; g.size1 = Vector2.one * 190 * s;
            g.c0 = WithA(hot, 0.2f); g.c1 = WithA(hot, 0.9f); g.fadeIn = 0.85f;
        }

        /// <summary>회복: 초록 빛 + 위로 떠오르는 반짝임.</summary>
        public void Heal(Vector2 pos, float delay)
        {
            var green = new Color(0.45f, 1f, 0.6f);
            Flash(pos, green, 220, 0.35f, delay);
            Ring(pos, green, 30, 200, 0.4f, delay);
            for (int i = 0; i < 12; i++)
            {
                var p = Add(ProceduralSprites.Star, pos + new Vector2(Random.Range(-85f, 85f), Random.Range(-45f, 15f)),
                    Random.Range(0.7f, 1f), delay + Random.Range(0f, 0.25f));
                p.vel = new Vector2(Random.Range(-15f, 15f), Random.Range(70f, 150f));
                p.size0 = Vector2.one * Random.Range(16f, 26f); p.size1 = Vector2.one * 6f;
                p.c0 = Color.white; p.c1 = WithA(green, 0f); p.fadeIn = 0.25f;
                p.rotVel = Random.Range(-90f, 90f);
            }
        }

        /// <summary>효과 부여: 버프 = 청록 화살표 상승 · 디버프 = 붉은 화살표 하강 · 지속 피해 = 보라 거품.</summary>
        public void Status(StatusType type, Vector2 pos, float delay)
        {
            if (type == StatusType.None) return;
            Color c = StatusText.ColorOf(type);
            Color light = Color.Lerp(c, Color.white, 0.45f);
            Flash(pos, c, 180, 0.35f, delay);
            if (type == StatusType.Dot)
            {
                for (int i = 0; i < 9; i++)
                {
                    var p = Add(ProceduralSprites.Ring, pos + new Vector2(Random.Range(-70f, 70f), Random.Range(-35f, 5f)),
                        Random.Range(0.6f, 0.9f), delay + Random.Range(0f, 0.3f));
                    p.vel = new Vector2(Random.Range(-20f, 20f), Random.Range(50f, 110f));
                    p.size0 = Vector2.one * Random.Range(10f, 22f); p.size1 = p.size0 * 1.4f;
                    p.c0 = light; p.c1 = WithA(c, 0f); p.fadeIn = 0.2f;
                }
                return;
            }
            bool up = StatusText.IsBuff(type);
            for (int i = 0; i < 4; i++)
            {
                float x = -66f + i * 44f;
                var p = Add(ProceduralSprites.Triangle, pos + new Vector2(x, up ? -30f : 30f), 0.7f, delay + i * 0.06f);
                p.vel = new Vector2(0, up ? 130f : -130f);
                p.size0 = new Vector2(24, 26); p.size1 = new Vector2(20, 22);
                p.rot = up ? 0f : 180f;
                p.c0 = light; p.c1 = WithA(c, 0f); p.fadeIn = 0.2f;
            }
        }

        // ---------------- 속성 스킬 ----------------

        private void Fire(Vector2 pos, float s, float delay)
        {
            var yellow = new Color(1f, 0.9f, 0.45f);
            var red = new Color(1f, 0.25f, 0.1f);
            Flash(pos, new Color(1f, 0.55f, 0.2f), 300 * s, 0.4f, delay);
            Ring(pos, new Color(1f, 0.6f, 0.25f), 40 * s, 260 * s, 0.4f, delay);
            for (int i = 0; i < 18; i++)
            {
                var p = Add(ProceduralSprites.Glow, pos + Random.insideUnitCircle * 55f * s, Random.Range(0.5f, 0.85f), delay + Random.Range(0f, 0.15f));
                p.vel = new Vector2(Random.Range(-50f, 50f), Random.Range(110f, 240f)) * s;
                p.gravity = new Vector2(0, 90f * s);
                float sz = Random.Range(45f, 80f) * s;
                p.size0 = Vector2.one * sz; p.size1 = Vector2.one * sz * 0.15f;
                p.c0 = yellow; p.c1 = WithA(red, 0f); p.fadeIn = 0.08f;
            }
            Sparks(pos, yellow, 8, 380 * s, 16 * s, 0.45f, delay + 0.05f);
        }

        private void Ice(Vector2 pos, float s, float delay)
        {
            var cyan = new Color(0.55f, 0.9f, 1f);
            Flash(pos, new Color(0.8f, 0.97f, 1f), 250 * s, 0.3f, delay);
            Ring(pos, cyan, 40 * s, 230 * s, 0.4f, delay);
            for (int i = 0; i < 12; i++)
            {
                float ang = (i * 30f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var p = Add(ProceduralSprites.Triangle, pos + dir * 10f * s, 0.55f, delay + Random.Range(0f, 0.05f));
                p.vel = dir * Random.Range(380f, 620f) * s; p.drag = 5f;
                p.alignToVel = true;
                p.size0 = new Vector2(18, 48) * s; p.size1 = new Vector2(8, 22) * s;
                p.c0 = Color.white; p.c1 = WithA(cyan, 0f); p.fadeIn = 0.05f;
            }
            for (int i = 0; i < 6; i++)
            {
                var p = Add(ProceduralSprites.Star, pos + Random.insideUnitCircle * 90f * s, 0.45f, delay + 0.1f + Random.Range(0f, 0.15f));
                p.size0 = Vector2.one * 8 * s; p.size1 = Vector2.one * 30 * s;
                p.c0 = Color.white; p.c1 = WithA(cyan, 0f); p.fadeIn = 0.3f; p.rotVel = 120f;
            }
        }

        private void Lightning(Vector2 pos, float s, float delay)
        {
            var violet = new Color(0.82f, 0.62f, 1f);
            Bolt(pos, s, violet, delay);
            Bolt(pos, s, Color.white, delay + 0.08f);
            Flash(pos, violet, 290 * s, 0.3f, delay + 0.04f);
            Ring(pos, violet, 30 * s, 220 * s, 0.3f, delay + 0.04f);
            Sparks(pos, new Color(0.9f, 0.8f, 1f), 14, 620 * s, 15 * s, 0.35f, delay + 0.04f);
        }

        /// <summary>위에서 대상으로 떨어지는 지그재그 낙뢰(짧은 빛줄기 여러 개).</summary>
        private void Bolt(Vector2 target, float s, Color c, float delay)
        {
            var start = target + new Vector2(Random.Range(-70f, 70f) * s, 430f * s);
            const int seg = 7;
            Vector2 prev = start;
            for (int i = 1; i <= seg; i++)
            {
                float k = i / (float)seg;
                Vector2 next = Vector2.Lerp(start, target, k);
                if (i < seg) next += new Vector2(Random.Range(-42f, 42f) * s, 0);
                Vector2 d = next - prev;
                var p = Add(ProceduralSprites.Beam, (prev + next) / 2f, 0.28f, delay);
                p.rot = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                p.size0 = p.size1 = new Vector2(18f * s, d.magnitude * 1.25f);
                p.c0 = c; p.c1 = WithA(c, 0f); p.fadeIn = 0.02f; p.flicker = true;
                prev = next;
            }
        }

        private void Wind(Vector2 pos, float s, float delay)
        {
            var green = new Color(0.55f, 1f, 0.7f);
            Flash(pos, new Color(0.75f, 1f, 0.85f), 230 * s, 0.35f, delay);
            Ring(pos, green, 50 * s, 250 * s, 0.45f, delay);
            for (int i = 0; i < 16; i++)
            {
                bool inward = i % 2 == 0;
                var p = Add(ProceduralSprites.Beam, pos, Random.Range(0.45f, 0.6f), delay + Random.Range(0f, 0.12f));
                p.orbit = true; p.center = pos;
                p.radius = (inward ? 150f : 30f) * s; p.radiusVel = (inward ? -170f : 260f) * s;
                p.angle = Random.Range(0f, Mathf.PI * 2f); p.angVel = 9f;
                p.alignToVel = true;
                p.size0 = new Vector2(12, 70) * s; p.size1 = new Vector2(4, 34) * s;
                p.c0 = Color.white; p.c1 = WithA(green, 0f); p.fadeIn = 0.1f;
            }
            Sparks(pos, green, 6, 300 * s, 14 * s, 0.45f, delay + 0.1f);
        }

        private void Physical(Vector2 pos, float s, float delay)
        {
            var white = new Color(0.95f, 0.95f, 1f);
            Ring(pos, white, 30 * s, 250 * s, 0.3f, delay);
            float baseA = Random.Range(0f, 45f);
            for (int i = 0; i < 4; i++)
                Slash(pos, i % 2 == 0 ? white : new Color(1f, 0.85f, 0.55f), baseA + i * 45f, 270 * s, 26 * s, 0.25f, delay + i * 0.035f);
            Flash(pos, white, 210 * s, 0.25f, delay);
            Sparks(pos, new Color(1f, 0.88f, 0.55f), 12, 520 * s, 18 * s, 0.4f, delay + 0.05f);
        }

        // ---------------- 기본 조각 ----------------

        private void Flash(Vector2 pos, Color c, float size, float life, float delay)
        {
            var p = Add(ProceduralSprites.Glow, pos, life, delay);
            p.size0 = Vector2.one * size * 0.5f; p.size1 = Vector2.one * size;
            p.c0 = WithA(c, 0.95f); p.c1 = WithA(c, 0f); p.fadeIn = 0.1f;
        }

        private void Ring(Vector2 pos, Color c, float from, float to, float life, float delay)
        {
            var p = Add(ProceduralSprites.Ring, pos, life, delay);
            p.size0 = Vector2.one * from; p.size1 = Vector2.one * to;
            p.c0 = WithA(c, 0.9f); p.c1 = WithA(c, 0f); p.fadeIn = 0.05f;
        }

        private void Sparks(Vector2 pos, Color c, int count, float speed, float size, float life, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                var p = Add(ProceduralSprites.Star, pos, life * Random.Range(0.7f, 1f), delay);
                p.vel = dir * speed * Random.Range(0.45f, 1f); p.drag = 4f;
                p.size0 = Vector2.one * size * Random.Range(0.8f, 1.3f); p.size1 = Vector2.one * size * 0.2f;
                p.c0 = Color.white; p.c1 = WithA(c, 0f); p.fadeIn = 0.02f;
                p.rotVel = Random.Range(-360f, 360f);
            }
        }

        /// <summary>베기: 가늘게 시작해 길어지며 베는 방향으로 살짝 미끄러지는 빛줄기(+ 흰 심).</summary>
        private void Slash(Vector2 pos, Color c, float angleDeg, float length, float width, float life, float delay)
        {
            float r = (angleDeg + 90f) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            var p = Add(ProceduralSprites.Beam, pos - dir * length * 0.12f, life, delay);
            p.rot = angleDeg; p.vel = dir * length * 1.2f; p.drag = 6f;
            p.size0 = new Vector2(width * 0.35f, length * 0.25f); p.size1 = new Vector2(width, length);
            p.c0 = WithA(c, 1f); p.c1 = WithA(c, 0f); p.fadeIn = 0.15f;
            var core = Add(ProceduralSprites.Beam, pos - dir * length * 0.12f, life * 0.8f, delay);
            core.rot = angleDeg; core.vel = p.vel; core.drag = 6f;
            core.size0 = new Vector2(width * 0.12f, length * 0.2f); core.size1 = new Vector2(width * 0.35f, length * 0.9f);
            core.c0 = Color.white; core.c1 = WithA(Color.white, 0f); core.fadeIn = 0.15f;
        }

        // ================= 파티클 =================

        private P Add(Sprite sprite, Vector2 pos, float life, float delay)
        {
            Image img = _pool.Count > 0 ? _pool.Pop() : UIFactory.AddImage("Fx", _layer, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.SetAsLastSibling();
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.localScale = Vector3.one;
            img.gameObject.SetActive(false);
            var p = new P
            {
                rt = rt, img = img, pos = pos, life = Mathf.Max(0.02f, life), delay = Mathf.Max(0f, delay),
                size0 = Vector2.one * 40, size1 = Vector2.one * 40, c0 = Color.white, c1 = Color.white,
            };
            _live.Add(p);
            return p;
        }

        private void Release(P p)
        {
            if (p.img == null) return;
            p.img.gameObject.SetActive(false);
            _pool.Push(p.img);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime * Mathf.Max(0.1f, Speed);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                if (p.img == null) { _live.RemoveAt(i); continue; }
                if (p.delay > 0f)
                {
                    p.delay -= dt;
                    if (p.delay > 0f) continue;
                }
                if (!p.img.gameObject.activeSelf) p.img.gameObject.SetActive(true);

                p.t += dt;
                float k = p.t / p.life;
                if (k >= 1f) { Release(p); _live.RemoveAt(i); continue; }

                Vector2 moveDir;
                if (p.orbit)
                {
                    p.angle += p.angVel * dt;
                    p.radius = Mathf.Max(0f, p.radius + p.radiusVel * dt);
                    p.pos = p.center + new Vector2(Mathf.Cos(p.angle), Mathf.Sin(p.angle)) * p.radius;
                    moveDir = new Vector2(-Mathf.Sin(p.angle), Mathf.Cos(p.angle));   // 접선
                }
                else
                {
                    p.vel += p.gravity * dt;
                    if (p.drag > 0f) p.vel /= 1f + p.drag * dt;
                    p.pos += p.vel * dt;
                    moveDir = p.vel;
                }
                p.rt.anchoredPosition = p.pos;

                float ease = 1f - (1f - k) * (1f - k);
                p.rt.sizeDelta = Vector2.Lerp(p.size0, p.size1, ease);

                if (p.alignToVel && moveDir.sqrMagnitude > 0.01f)
                    p.rot = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f;
                else
                    p.rot += p.rotVel * dt;
                p.rt.localEulerAngles = new Vector3(0, 0, p.rot);

                var c = Color.Lerp(p.c0, p.c1, k);
                float env = k < p.fadeIn ? k / p.fadeIn : 1f;
                c.a *= env * (p.flicker ? Random.Range(0.35f, 1f) : 1f);
                p.img.color = c;
            }
        }

        private static Color Main(Element e) => Traits.ColorOf(e);
        private static Color Hot(Element e) => Color.Lerp(Traits.ColorOf(e), Color.white, 0.5f);
        private static Color WithA(Color c, float a) { c.a = a; return c; }
    }
}
