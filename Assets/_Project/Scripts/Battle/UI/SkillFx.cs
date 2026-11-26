using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 전투 스킬 이펙트: 움직임(날아가기 · 커지기 · 회전 · 사라지기)은 코드, 모양은 생성한 이펙트 텍스처(Resources/Art/Fx).
    /// 텍스처는 흰색 + 투명도라 속성 색을 곱해 한 장을 모든 속성에 씀. 텍스처가 없으면 예전 코드 도형(ProceduralSprites)으로 그림.
    /// - 기본 공격: 초승달 검기 2줄 + 타격 섬광 + 반짝이
    /// - 스킬: 속성별 연출(화염 불길 · 얼음 파편 · 낙뢰 · 바람 칼날 소용돌이 · 물리 연속 베기)
    /// - 궁극기: 바닥 마법진(눕혀서 회전하며 펼쳐짐) + 빛기둥 + 큰 속성 연출 + 충격파 2겹 + 떠오르는 룬
    /// - 다단 히트 추가 타격, 시전 모으기(발밑 작은 마법진), 회복, 버프/디버프/지속 피해 부여
    /// 텍스처 생성: tools/art/generate-fx.js. 모든 시간은 실시간(Time.unscaledDeltaTime) × Speed.
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
            public float fadeOutFrom = 0f;   // 0이면 처음부터 색이 c1으로 바뀌며 옅어짐, 0.6이면 60%까지는 c0 유지
            public bool flicker;
            // 궤도(소용돌이·모으기)
            public bool orbit;
            public Vector2 center;
            public float radius, radiusVel, angle, angVel;
            // 바닥에 눕힌 모양(마법진): 세로를 이 비율로 납작하게. 회전은 납작해지기 전에 적용
            public float squash;
            public RectTransform holder;
            public float alphaMul = 1f;   // 만들 때의 Dim(적이 쓰는 기술은 옅게)
        }

        private readonly List<P> _live = new();
        private readonly Stack<Image> _pool = new();
        private RectTransform _layer;

        /// <summary>이펙트 재생 속도(배속).</summary>
        public float Speed { get; set; } = 1f;

        /// <summary>
        /// 이펙트 진하기(0~1). 적(보스 등)이 아군에게 쓰는 기술은 옅게 해서 아군 그림이 흰 빛에 묻히지 않게 — BattleHUD가 행동마다 정함.
        /// 이후에 만드는 이펙트에만 적용.
        /// </summary>
        public float Dim { get; set; } = 1f;

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

        // ================= 텍스처 =================

        private static readonly Dictionary<string, Sprite> Tex = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Tex.Clear();

        /// <summary>Fx/{key} 텍스처, 없으면 fallback(코드 도형).</summary>
        private static Sprite T(string key, Sprite fallback)
        {
            if (!Tex.TryGetValue(key, out var sp)) Tex[key] = sp = GameArt.Load("Fx/" + key);
            return sp != null ? sp : fallback;
        }

        private static bool Has(string key) => T(key, null) != null;

        // ================= 연출 레시피 =================

        /// <summary>기본 공격: 초승달 검기 2줄 + 타격 섬광 + 불꽃.</summary>
        public void Basic(Element e, Vector2 pos, float s, float delay)
        {
            Color c = Main(e), hot = Hot(e);
            float a = Random.Range(-20f, 20f);
            Arc(pos, hot, a, 300 * s, 0.3f, delay, 1);
            Arc(pos, c, a + 150f, 250 * s, 0.3f, delay + 0.08f, -1);
            Hit(pos, hot, 190 * s, 0.24f, delay + 0.04f);
            Sparks(pos, hot, 7, 420 * s, 20 * s, 0.32f, delay + 0.03f);
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

        /// <summary>
        /// 궁극기: 바닥 마법진 + 빛기둥 + 큰 속성 연출 + 충격파 2겹 + 떠오르는 룬 + 금빛 불꽃.
        /// signature = 캐릭터 id(예: c_seraphine) — Fx/ult_{id} 문양 그림이 있으면 대상 위에 크게 펼쳐짐(6★ 전용 연출).
        /// </summary>
        public void Ultimate(Element e, Vector2 pos, float s, float delay, string signature = null)
        {
            Color c = Main(e), hot = Hot(e);
            var feet = pos + new Vector2(0, -120 * s);

            // 6★ 전용 문양: 작게 시작해 돌며 크게 펼쳐졌다 사라짐 + 뒤에 옅은 큰 잔상
            var sig = !string.IsNullOrEmpty(signature) ? T("ult_" + signature, null) : null;
            if (sig != null)
            {
                var m = Add(sig, pos + new Vector2(0, 30 * s), 0.95f, delay + 0.05f);
                m.size0 = Vector2.one * 220 * s; m.size1 = Vector2.one * 640 * s;
                m.rot = -10f; m.rotVel = 18f;
                m.c0 = WithA(Color.Lerp(hot, Color.white, 0.3f), 1f); m.c1 = WithA(c, 0f); m.fadeIn = 0.08f; m.fadeOutFrom = 0.5f;
                var g = Add(sig, pos + new Vector2(0, 30 * s), 0.8f, delay + 0.12f);
                g.size0 = Vector2.one * 520 * s; g.size1 = Vector2.one * 900 * s;
                g.rot = 8f; g.rotVel = -10f;
                g.c0 = WithA(c, 0.45f); g.c1 = WithA(c, 0f); g.fadeIn = 0.1f;
            }

            // 바닥 마법진: 눕혀서 회전하며 펼쳐졌다가 사라짐
            if (Has("magic_circle"))
            {
                var mc = Add(T("magic_circle", null), feet, 1.1f, delay);
                mc.squash = 0.34f; mc.rotVel = 70f;
                mc.size0 = Vector2.one * 160 * s; mc.size1 = Vector2.one * 560 * s;
                mc.c0 = WithA(hot, 1f); mc.c1 = WithA(c, 0f); mc.fadeIn = 0.15f; mc.fadeOutFrom = 0.55f;
                var mc2 = Add(T("magic_circle", null), feet, 0.9f, delay + 0.1f);
                mc2.squash = 0.34f; mc2.rotVel = -110f; mc2.rot = 20f;
                mc2.size0 = Vector2.one * 80 * s; mc2.size1 = Vector2.one * 330 * s;
                mc2.c0 = WithA(Color.white, 0.8f); mc2.c1 = WithA(hot, 0f); mc2.fadeIn = 0.2f; mc2.fadeOutFrom = 0.5f;
            }

            // 빛기둥
            var pillar = Add(T("light_pillar", ProceduralSprites.Beam), pos + new Vector2(0, 120 * s), 0.7f, delay);
            bool pillarTex = Has("light_pillar");   // 텍스처는 가운데 좁은 띠라 넓게 깔아야 같은 굵기
            pillar.size0 = new Vector2((pillarTex ? 300 : 90) * s, 760 * s); pillar.size1 = new Vector2((pillarTex ? 900 : 300) * s, 940 * s);
            pillar.c0 = Color.white; pillar.c1 = WithA(hot, 0f); pillar.fadeIn = 0.1f;
            var core = Add(ProceduralSprites.Beam, pos + new Vector2(0, 120 * s), 0.5f, delay);
            core.size0 = new Vector2(20 * s, 700 * s); core.size1 = new Vector2(70 * s, 900 * s);
            core.c0 = Color.white; core.c1 = WithA(Color.white, 0f);

            Skill(e, pos, s * 1.45f, delay + 0.08f);
            Ring(pos, hot, 60 * s, 520 * s, 0.55f, delay + 0.1f);
            Ring(pos, c, 30 * s, 400 * s, 0.65f, delay + 0.26f);
            Sparks(pos, new Color(1f, 0.88f, 0.45f), 16, 640 * s, 26 * s, 0.6f, delay + 0.1f);
            Sparks(pos, hot, 12, 480 * s, 22 * s, 0.5f, delay + 0.18f);

            // 떠오르는 룬
            if (Has("rune_glyph"))
                for (int i = 0; i < 6; i++)
                {
                    var r = Add(T("rune_glyph", null), feet + new Vector2(Random.Range(-200f, 200f) * s, Random.Range(-20f, 30f) * s),
                        Random.Range(0.8f, 1.1f), delay + 0.15f + i * 0.05f);
                    r.vel = new Vector2(0, Random.Range(120f, 200f) * s);
                    r.size0 = Vector2.one * 40 * s; r.size1 = Vector2.one * 70 * s;
                    r.c0 = WithA(hot, 0.95f); r.c1 = WithA(c, 0f); r.fadeIn = 0.25f; r.rotVel = Random.Range(-40f, 40f);
                }
        }

        /// <summary>
        /// 블랙홀(보스 필살기 '사건의 지평선' 직전): 가운데 어둠이 커지고, 보랏빛 · 검은 알갱이가 소용돌이치며 빨려 듦 + 줄어드는 고리.
        /// </summary>
        public void Vortex(Vector2 center, float s, float dur)
        {
            if (dur <= 0.05f) return;
            var violet = new Color(0.62f, 0.3f, 1f);
            var dark = new Color(0.08f, 0.02f, 0.14f);
            var core = Add(T("glow_orb", ProceduralSprites.Glow), center, dur, 0f);
            core.size0 = Vector2.one * 60 * s; core.size1 = Vector2.one * 420 * s; core.rotVel = -90f;
            core.c0 = WithA(dark, 0.3f); core.c1 = WithA(dark, 0.85f); core.fadeIn = 0.6f;
            var rim = Add(T("shockwave", ProceduralSprites.Ring), center, dur, 0f);
            rim.size0 = Vector2.one * 760 * s; rim.size1 = Vector2.one * 120 * s; rim.rotVel = 160f;
            rim.c0 = WithA(violet, 0.2f); rim.c1 = WithA(violet, 0.9f); rim.fadeIn = 0.4f;
            var swirl = T("wind_swirl", ProceduralSprites.Ring);
            var w = Add(swirl, center, dur, 0f);
            w.size0 = Vector2.one * 640 * s; w.size1 = Vector2.one * 160 * s; w.rotVel = -360f;
            w.c0 = WithA(violet, 0.15f); w.c1 = WithA(Color.Lerp(violet, Color.white, 0.4f), 0.8f); w.fadeIn = 0.5f;
            for (int i = 0; i < 26; i++)
            {
                var p = Add(i % 3 == 0 ? T("spark_star", ProceduralSprites.Star) : ProceduralSprites.Glow, center, dur, Random.Range(0f, dur * 0.3f));
                p.orbit = true; p.center = center;
                p.radius = Random.Range(260f, 520f) * s; p.radiusVel = -p.radius / (dur * 0.85f);
                p.angle = Random.Range(0f, Mathf.PI * 2f); p.angVel = Random.Range(4f, 7f);
                p.size0 = Vector2.one * Random.Range(14f, 30f) * s; p.size1 = Vector2.one * 6f * s;
                p.c0 = i % 2 == 0 ? WithA(violet, 0.9f) : WithA(dark, 0.9f); p.c1 = WithA(Color.white, 0.5f); p.fadeIn = 0.25f;
            }
        }

        /// <summary>치명타: 금빛 타격 섬광 크게 + 엇갈린 검기 2줄 + 충격파 + 금빛 불꽃이 사방으로.</summary>
        public void Crit(Vector2 pos, float s, float delay)
        {
            var gold = new Color(1f, 0.84f, 0.35f);
            var hot = new Color(1f, 0.96f, 0.8f);
            Hit(pos, gold, 300 * s, 0.3f, delay);
            float a = Random.Range(20f, 50f);
            Slash(pos, hot, a, 360 * s, 30 * s, 0.24f, delay);
            Slash(pos, gold, a + 90f, 300 * s, 24 * s, 0.24f, delay + 0.04f);
            Ring(pos, gold, 60 * s, 380 * s, 0.35f, delay + 0.02f);
            Sparks(pos, gold, 16, 720 * s, 22 * s, 0.45f, delay + 0.02f);
        }

        /// <summary>약점 공격: 분홍빛 충격파 + 작은 파편 반짝임.</summary>
        public void Weak(Vector2 pos, float s, float delay)
        {
            var pink = new Color(1f, 0.55f, 0.85f);
            Ring(pos, pink, 40 * s, 260 * s, 0.3f, delay);
            Sparks(pos, Color.Lerp(pink, Color.white, 0.4f), 8, 480 * s, 18 * s, 0.35f, delay);
        }

        /// <summary>브레이크: 내구도가 깨지며 파편(얼음 파편 그림을 주황으로)이 사방으로 튀고 큰 충격파 2겹 + 섬광.</summary>
        public void BreakShatter(Vector2 pos, float s, float delay)
        {
            var orange = new Color(1f, 0.72f, 0.28f);
            Hit(pos, orange, 380 * s, 0.35f, delay);
            Ring(pos, orange, 60 * s, 520 * s, 0.45f, delay);
            Ring(pos, Color.white, 30 * s, 360 * s, 0.35f, delay + 0.08f);
            var shard = T("ice_shard", ProceduralSprites.Triangle);
            for (int i = 0; i < 14; i++)
            {
                float ang = (i * (360f / 14f) + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var p = Add(shard, pos + dir * 20f * s, Random.Range(0.5f, 0.75f), delay + Random.Range(0f, 0.05f));
                p.vel = dir * Random.Range(520f, 900f) * s; p.drag = 3.5f; p.gravity = new Vector2(0, -600f * s);
                p.alignToVel = true;
                p.size0 = new Vector2(34, 80) * s * Random.Range(0.7f, 1.2f); p.size1 = p.size0 * 0.4f;
                p.c0 = Color.Lerp(orange, Color.white, 0.5f); p.c1 = WithA(orange, 0f); p.fadeIn = 0.03f; p.fadeOutFrom = 0.4f;
            }
            Sparks(pos, orange, 14, 760 * s, 22 * s, 0.5f, delay + 0.03f);
        }

        /// <summary>화면 전체 번쩍임(궁극기).</summary>
        public void ScreenFlash(Color c, float life, float alpha = 0.4f)
        {
            var p = Add(null, Vector2.zero, life, 0f);
            p.size0 = p.size1 = _layer.rect.size + new Vector2(200, 200);
            p.c0 = WithA(c, alpha); p.c1 = WithA(c, 0f); p.fadeIn = 0.05f;
        }

        /// <summary>다단 히트의 추가 타격(작은 베기/충격 + 섬광 + 불꽃).</summary>
        public void Impact(Element e, SkillCategory cat, Vector2 pos, float s, float delay)
        {
            Color hot = Hot(e);
            Vector2 j = Random.insideUnitCircle * 26f * s;
            if (cat == SkillCategory.Basic || e == Element.Physical)
                Slash(pos + j, hot, Random.Range(-70f, 70f), 220 * s, 22 * s, 0.2f, delay);
            else
                Ring(pos + j, hot, 30 * s, 190 * s, 0.25f, delay);
            Hit(pos + j, hot, 150 * s, 0.18f, delay);
            Sparks(pos + j, hot, 5, 360 * s, 16 * s, 0.28f, delay);
        }

        /// <summary>
        /// 시전 모으기: 발밑 작은 마법진 + 빛 알갱이가 시전자 쪽으로 빨려 들며 가운데가 밝아짐.
        /// circleDur = 마법진이 떠 있는 시간(시전자가 돌진하기 전까지만 — 돌진 뒤 빈자리에 남지 않게). 음수면 dur.
        /// </summary>
        public void Charge(Element e, Vector2 pos, float s, float dur, float circleDur = -1f)
        {
            if (dur <= 0.05f) return;
            Color hot = Hot(e);
            if (circleDur < 0f) circleDur = dur;
            if (Has("magic_circle") && circleDur > 0.1f)
            {
                var mc = Add(T("magic_circle", null), pos + new Vector2(0, -120 * s), circleDur + 0.12f, 0f);
                mc.squash = 0.34f; mc.rotVel = 90f;
                mc.size0 = Vector2.one * 120 * s; mc.size1 = Vector2.one * 300 * s;
                mc.c0 = WithA(hot, 0.9f); mc.c1 = WithA(hot, 0f); mc.fadeIn = 0.35f; mc.fadeOutFrom = 0.75f;
            }
            for (int i = 0; i < 14; i++)
            {
                var p = Add(T("spark_star", ProceduralSprites.Star), pos, dur, Random.Range(0f, dur * 0.25f));
                p.orbit = true; p.center = pos;
                p.radius = Random.Range(120f, 170f) * s; p.radiusVel = -p.radius / (dur * 0.9f);
                p.angle = Random.Range(0f, Mathf.PI * 2f); p.angVel = Random.Range(3f, 5f);
                p.size0 = Vector2.one * 30 * s; p.size1 = Vector2.one * 12 * s;
                p.c0 = hot; p.c1 = WithA(Color.white, 0.6f); p.fadeIn = 0.3f;
            }
            var g = Add(ProceduralSprites.Glow, pos, dur, 0f);   // 부드러운 빛(glow_orb 텍스처는 속이 보여 '공'처럼 보임)
            g.size0 = Vector2.one * 30 * s; g.size1 = Vector2.one * 200 * s;
            g.c0 = WithA(hot, 0.2f); g.c1 = WithA(hot, 0.9f); g.fadeIn = 0.85f;
        }

        /// <summary>회복: 초록 빛 + 위로 떠오르는 회복 십자 · 반짝임.</summary>
        public void Heal(Vector2 pos, float delay)
        {
            var green = new Color(0.45f, 1f, 0.6f);
            Flash(pos, green, 220, 0.35f, delay);
            Ring(pos, green, 40, 240, 0.45f, delay);
            for (int i = 0; i < 10; i++)
            {
                bool cross = i % 2 == 0 && Has("heal_cross");
                var p = Add(cross ? T("heal_cross", null) : T("spark_star", ProceduralSprites.Star),
                    pos + new Vector2(Random.Range(-85f, 85f), Random.Range(-45f, 15f)), Random.Range(0.7f, 1f), delay + Random.Range(0f, 0.25f));
                p.vel = new Vector2(Random.Range(-15f, 15f), Random.Range(70f, 150f));
                float sz = cross ? Random.Range(34f, 48f) : Random.Range(20f, 30f);
                p.size0 = Vector2.one * sz; p.size1 = Vector2.one * sz * 0.4f;
                p.c0 = Color.white; p.c1 = WithA(green, 0f); p.fadeIn = 0.25f;
                if (!cross) p.rotVel = Random.Range(-90f, 90f);
            }
        }

        /// <summary>효과 부여: 버프 = 청록 화살표 상승 · 디버프 = 붉은 화살표 하강 · 지속 피해 = 보라 연기 · 거품.</summary>
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
                    bool smoke = i < 4 && Has("smoke_puff");
                    var p = Add(smoke ? T("smoke_puff", null) : ProceduralSprites.Ring,
                        pos + new Vector2(Random.Range(-70f, 70f), Random.Range(-35f, 5f)), Random.Range(0.6f, 0.9f), delay + Random.Range(0f, 0.3f));
                    p.vel = new Vector2(Random.Range(-20f, 20f), Random.Range(50f, 110f));
                    p.size0 = Vector2.one * (smoke ? Random.Range(60f, 90f) : Random.Range(10f, 22f)); p.size1 = p.size0 * 1.4f;
                    p.c0 = smoke ? WithA(c, 0.7f) : light; p.c1 = WithA(c, 0f); p.fadeIn = 0.2f;
                    if (smoke) p.rotVel = Random.Range(-40f, 40f);
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
            var orange = new Color(1f, 0.55f, 0.2f);
            var red = new Color(1f, 0.25f, 0.1f);
            Flash(pos, orange, 300 * s, 0.4f, delay);
            if (Has("fire_burst"))
            {
                var b = Add(T("fire_burst", null), pos, 0.45f, delay);
                b.size0 = Vector2.one * 120 * s; b.size1 = Vector2.one * 380 * s; b.rotVel = Random.Range(-60f, 60f);
                b.c0 = WithA(yellow, 1f); b.c1 = WithA(red, 0f); b.fadeIn = 0.06f; b.fadeOutFrom = 0.3f;
            }
            else Ring(pos, new Color(1f, 0.6f, 0.25f), 40 * s, 260 * s, 0.4f, delay);

            bool tex = Has("flame");
            for (int i = 0; i < (tex ? 11 : 18); i++)
            {
                var p = Add(T("flame", ProceduralSprites.Glow), pos + Random.insideUnitCircle * 60f * s, Random.Range(0.5f, 0.85f), delay + Random.Range(0f, 0.15f));
                p.vel = new Vector2(Random.Range(-50f, 50f), Random.Range(110f, 240f)) * s;
                p.gravity = new Vector2(0, 90f * s);
                float sz = (tex ? Random.Range(90f, 150f) : Random.Range(45f, 80f)) * s;
                p.size0 = tex ? new Vector2(sz * 0.8f, sz) : Vector2.one * sz;
                p.size1 = p.size0 * 0.25f;
                p.rot = tex ? Random.Range(-15f, 15f) : 0f;
                p.c0 = yellow; p.c1 = WithA(red, 0f); p.fadeIn = 0.08f;
            }
            Sparks(pos, yellow, 8, 380 * s, 18 * s, 0.45f, delay + 0.05f);
        }

        private void Ice(Vector2 pos, float s, float delay)
        {
            var cyan = new Color(0.55f, 0.9f, 1f);
            Flash(pos, new Color(0.8f, 0.97f, 1f), 250 * s, 0.3f, delay);
            if (Has("ice_burst"))
            {
                var b = Add(T("ice_burst", null), pos, 0.5f, delay);
                b.size0 = Vector2.one * 110 * s; b.size1 = Vector2.one * 340 * s;
                b.c0 = WithA(Color.white, 1f); b.c1 = WithA(cyan, 0f); b.fadeIn = 0.05f; b.fadeOutFrom = 0.35f;
            }
            else Ring(pos, cyan, 40 * s, 230 * s, 0.4f, delay);

            bool tex = Has("ice_shard");
            for (int i = 0; i < 12; i++)
            {
                float ang = (i * 30f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var p = Add(T("ice_shard", ProceduralSprites.Triangle), pos + dir * 10f * s, 0.55f, delay + Random.Range(0f, 0.05f));
                p.vel = dir * Random.Range(380f, 620f) * s; p.drag = 5f;
                p.alignToVel = true;
                p.size0 = (tex ? new Vector2(46, 110) : new Vector2(18, 48)) * s; p.size1 = p.size0 * 0.45f;
                p.c0 = Color.white; p.c1 = WithA(cyan, 0f); p.fadeIn = 0.05f;
            }
            for (int i = 0; i < 6; i++)
            {
                var p = Add(T("spark_star", ProceduralSprites.Star), pos + Random.insideUnitCircle * 90f * s, 0.45f, delay + 0.1f + Random.Range(0f, 0.15f));
                p.size0 = Vector2.one * 10 * s; p.size1 = Vector2.one * 40 * s;
                p.c0 = Color.white; p.c1 = WithA(cyan, 0f); p.fadeIn = 0.3f; p.rotVel = 120f;
            }
        }

        private void Lightning(Vector2 pos, float s, float delay)
        {
            var violet = new Color(0.82f, 0.62f, 1f);
            Bolt(pos, s, violet, delay);
            Bolt(pos, s, Color.white, delay + 0.08f);
            Flash(pos, violet, 290 * s, 0.3f, delay + 0.04f);
            if (Has("lightning_arc"))
            {
                var a = Add(T("lightning_arc", null), pos, 0.35f, delay + 0.04f);
                a.size0 = Vector2.one * 160 * s; a.size1 = Vector2.one * 300 * s; a.rot = Random.Range(0f, 360f);
                a.c0 = WithA(Color.white, 1f); a.c1 = WithA(violet, 0f); a.fadeIn = 0.02f; a.flicker = true;
            }
            else Ring(pos, violet, 30 * s, 220 * s, 0.3f, delay + 0.04f);
            Sparks(pos, new Color(0.9f, 0.8f, 1f), 14, 620 * s, 17 * s, 0.35f, delay + 0.04f);
        }

        /// <summary>위에서 대상으로 떨어지는 낙뢰(텍스처 한 장, 없으면 지그재그 빛줄기 여러 개).</summary>
        private void Bolt(Vector2 target, float s, Color c, float delay)
        {
            var start = target + new Vector2(Random.Range(-70f, 70f) * s, 460f * s);
            if (Has("lightning_bolt"))
            {
                Vector2 d = target - start;
                var b = Add(T("lightning_bolt", null), (start + target) / 2f, 0.3f, delay);
                b.rot = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                if (Random.value < 0.5f) b.rot += 180f;   // 그림 방향을 섞어 매번 다른 모양
                b.size0 = b.size1 = new Vector2(220f * s, d.magnitude * 1.15f);
                b.c0 = c; b.c1 = WithA(c, 0f); b.fadeIn = 0.02f; b.flicker = true;
                return;
            }
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
            if (Has("wind_swirl"))
            {
                var w = Add(T("wind_swirl", null), pos, 0.55f, delay);
                w.size0 = Vector2.one * 140 * s; w.size1 = Vector2.one * 340 * s; w.rotVel = 420f;
                w.c0 = WithA(Color.white, 0.95f); w.c1 = WithA(green, 0f); w.fadeIn = 0.08f; w.fadeOutFrom = 0.4f;
            }
            else Ring(pos, green, 50 * s, 250 * s, 0.45f, delay);

            bool tex = Has("wind_blade");
            for (int i = 0; i < (tex ? 10 : 16); i++)
            {
                bool inward = i % 2 == 0;
                var p = Add(T("wind_blade", ProceduralSprites.Beam), pos, Random.Range(0.45f, 0.6f), delay + Random.Range(0f, 0.12f));
                p.orbit = true; p.center = pos;
                p.radius = (inward ? 150f : 30f) * s; p.radiusVel = (inward ? -170f : 260f) * s;
                p.angle = Random.Range(0f, Mathf.PI * 2f); p.angVel = 9f;
                p.alignToVel = true;
                p.size0 = (tex ? new Vector2(70, 110) : new Vector2(12, 70)) * s; p.size1 = p.size0 * 0.5f;
                p.c0 = Color.white; p.c1 = WithA(green, 0f); p.fadeIn = 0.1f;
            }
            Sparks(pos, green, 6, 300 * s, 16 * s, 0.45f, delay + 0.1f);
        }

        private void Physical(Vector2 pos, float s, float delay)
        {
            var white = new Color(0.95f, 0.95f, 1f);
            var gold = new Color(1f, 0.85f, 0.55f);
            Ring(pos, white, 40 * s, 300 * s, 0.32f, delay);
            float baseA = Random.Range(0f, 45f);
            for (int i = 0; i < 4; i++)
                Slash(pos, i % 2 == 0 ? white : gold, baseA + i * 45f, 300 * s, 26 * s, 0.25f, delay + i * 0.035f);
            if (Has("slash_arc")) Arc(pos, white, baseA + 90f, 340 * s, 0.32f, delay + 0.16f, 1);
            Hit(pos, white, 230 * s, 0.26f, delay);
            Sparks(pos, gold, 12, 520 * s, 20 * s, 0.4f, delay + 0.05f);
        }

        // ---------------- 기본 조각 ----------------

        /// <summary>부드러운 빛 번짐(코드 도형 — 텍스처보다 부드러워 바탕 빛으로 씀).</summary>
        private void Flash(Vector2 pos, Color c, float size, float life, float delay)
        {
            var p = Add(ProceduralSprites.Glow, pos, life, delay);
            p.size0 = Vector2.one * size * 0.5f; p.size1 = Vector2.one * size;
            p.c0 = WithA(c, 0.95f); p.c1 = WithA(c, 0f); p.fadeIn = 0.1f;
        }

        /// <summary>타격 섬광: 뾰족한 별 모양이 커지며 사라짐(+ 부드러운 빛). 텍스처가 없으면 빛 번짐만.</summary>
        private void Hit(Vector2 pos, Color c, float size, float life, float delay)
        {
            Flash(pos, c, size * 0.8f, life, delay);
            if (!Has("impact_hit")) return;
            var p = Add(T("impact_hit", null), pos, life, delay);
            p.size0 = Vector2.one * size * 0.45f; p.size1 = Vector2.one * size * 1.1f;
            p.rot = Random.Range(0f, 360f); p.rotVel = Random.Range(-60f, 60f);
            p.c0 = WithA(Color.Lerp(c, Color.white, 0.5f), 1f); p.c1 = WithA(c, 0f); p.fadeIn = 0.03f; p.fadeOutFrom = 0.25f;
        }

        private void Ring(Vector2 pos, Color c, float from, float to, float life, float delay)
        {
            bool tex = Has("shockwave");
            var p = Add(T("shockwave", ProceduralSprites.Ring), pos, life, delay);
            p.size0 = Vector2.one * from; p.size1 = Vector2.one * to * (tex ? 1.1f : 1f);
            p.rot = tex ? Random.Range(0f, 360f) : 0f;
            p.c0 = WithA(c, 0.9f); p.c1 = WithA(c, 0f); p.fadeIn = 0.05f;
        }

        private void Sparks(Vector2 pos, Color c, int count, float speed, float size, float life, float delay)
        {
            var sp = T("spark_star", ProceduralSprites.Star);
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                var p = Add(sp, pos, life * Random.Range(0.7f, 1f), delay);
                p.vel = dir * speed * Random.Range(0.45f, 1f); p.drag = 4f;
                p.size0 = Vector2.one * size * Random.Range(0.8f, 1.3f); p.size1 = Vector2.one * size * 0.2f;
                p.c0 = Color.white; p.c1 = WithA(c, 0f); p.fadeIn = 0.02f;
                p.rotVel = Random.Range(-360f, 360f);
            }
        }

        /// <summary>
        /// 초승달 검기: 텍스처(slash_arc)를 베는 방향으로 돌리며 커지게. dir = 회전 방향(1 · -1).
        /// 텍스처가 없으면 일직선 베기로 대신.
        /// </summary>
        private void Arc(Vector2 pos, Color c, float angleDeg, float size, float life, float delay, int dir)
        {
            if (!Has("slash_arc")) { Slash(pos, c, angleDeg, size * 0.8f, size * 0.1f, life * 0.8f, delay); return; }
            var p = Add(T("slash_arc", null), pos, life, delay);
            p.rot = angleDeg - 25f * dir; p.rotVel = 160f * dir;
            p.size0 = new Vector2(size * 0.7f * dir, size * 0.7f); p.size1 = new Vector2(size * 1.08f * dir, size * 1.08f);   // dir<0 이면 좌우 뒤집기
            p.c0 = WithA(Color.Lerp(c, Color.white, 0.35f), 1f); p.c1 = WithA(c, 0f); p.fadeIn = 0.06f; p.fadeOutFrom = 0.35f;
        }

        /// <summary>일직선 베기: 가늘게 시작해 길어지며 베는 방향으로 살짝 미끄러지는 검기(+ 흰 심).</summary>
        private void Slash(Vector2 pos, Color c, float angleDeg, float length, float width, float life, float delay)
        {
            float r = (angleDeg + 90f) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            bool tex = Has("slash_line");
            var p = Add(T("slash_line", ProceduralSprites.Beam), pos - dir * length * 0.12f, life, delay);
            p.rot = angleDeg; p.vel = dir * length * 1.2f; p.drag = 6f;
            float w = tex ? width * 3.2f : width;   // 텍스처는 가운데 심이 가늘어 넓게 깔아야 같은 굵기
            p.size0 = new Vector2(w * 0.35f, length * 0.25f); p.size1 = new Vector2(w, length);
            p.c0 = WithA(c, 1f); p.c1 = WithA(c, 0f); p.fadeIn = 0.15f;
            var core = Add(ProceduralSprites.Beam, pos - dir * length * 0.12f, life * 0.8f, delay);
            core.rot = angleDeg; core.vel = p.vel; core.drag = 6f;
            core.size0 = new Vector2(width * 0.12f, length * 0.2f); core.size1 = new Vector2(width * 0.3f, length * 0.85f);
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
                size0 = Vector2.one * 40, size1 = Vector2.one * 40, c0 = Color.white, c1 = Color.white, alphaMul = Mathf.Clamp01(Dim),
            };
            _live.Add(p);
            return p;
        }

        private void Release(P p)
        {
            if (p.img == null) return;
            if (p.holder != null)
            {
                p.img.rectTransform.SetParent(_layer, false);
                Destroy(p.holder.gameObject);
                p.holder = null;
            }
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
                if (!p.img.gameObject.activeSelf)
                {
                    if (p.squash > 0f && p.holder == null)
                    {
                        // 납작하게 누르는 틀(바닥에 눕힌 모양): 그림은 틀 안에서 돌고, 틀이 세로를 누름
                        p.holder = UIFactory.NewRect("Flat", _layer);
                        UIFactory.Anchor(p.holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                        p.holder.sizeDelta = Vector2.zero;
                        p.holder.localScale = new Vector3(1f, p.squash, 1f);
                        p.holder.SetSiblingIndex(p.rt.GetSiblingIndex());
                        p.rt.SetParent(p.holder, false);
                        p.rt.anchoredPosition = Vector2.zero;
                    }
                    p.img.gameObject.SetActive(true);
                }

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
                if (p.holder != null) p.holder.anchoredPosition = p.pos;
                else p.rt.anchoredPosition = p.pos;

                float ease = 1f - (1f - k) * (1f - k);
                var size = Vector2.Lerp(p.size0, p.size1, ease);
                // 음수 너비 = 좌우 뒤집기(크기는 절댓값, 뒤집기는 scale로)
                p.rt.sizeDelta = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
                p.rt.localScale = new Vector3(size.x < 0f ? -1f : 1f, 1f, 1f);

                if (p.alignToVel && moveDir.sqrMagnitude > 0.01f)
                    p.rot = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f;
                else
                    p.rot += p.rotVel * dt;
                p.rt.localEulerAngles = new Vector3(0, 0, p.rot);

                float ck = p.fadeOutFrom > 0f ? Mathf.Clamp01((k - p.fadeOutFrom) / (1f - p.fadeOutFrom)) : k;
                var c = Color.Lerp(p.c0, p.c1, ck);
                float env = k < p.fadeIn ? k / p.fadeIn : 1f;
                c.a *= env * p.alphaMul * (p.flicker ? Random.Range(0.35f, 1f) : 1f);
                p.img.color = c;
            }
        }

        private static Color Main(Element e) => Traits.ColorOf(e);
        private static Color Hot(Element e) => Color.Lerp(Traits.ColorOf(e), Color.white, 0.5f);
        private static Color WithA(Color c, float a) { c.a = a; return c; }
    }
}
