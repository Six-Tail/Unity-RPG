using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta
{
    /// <summary>장비 부위(6종). 세트 효과는 부위와 상관없이 같은 세트 개수로 판정.</summary>
    public enum EquipSlot
    {
        Head,   // 머리 — 주옵션 HP 고정
        Hands,  // 손 — 주옵션 공격력 고정
        Body,   // 몸 — 비율·치명·치유·효과 명중
        Feet,   // 발 — 비율·속도
        Neck,   // 목걸이 — 비율·속성 피해
        Ring,   // 반지 — 비율·브레이크·에너지·관통
    }

    /// <summary>장비 옵션 종류. 세이브에 정수로 저장되므로 뒤에만 추가.</summary>
    public enum EquipStat
    {
        HP, HPPct, Atk, AtkPct, Def, DefPct, Spd,
        CritRate, CritDmg, Penetration, BreakEffect, EnergyRegen, HealBonus, EffectHit, EffectRes,
        PhysicalDmg, FireDmg, IceDmg, LightningDmg, WindDmg,
    }

    /// <summary>보조 옵션 1줄(강화 시 roll 횟수만큼 수치 상승).</summary>
    [Serializable]
    public class EquipSub
    {
        public EquipStat stat;
        public float value;
        public int rolls = 1;
    }

    /// <summary>장비 1개(보유 인벤토리 저장 단위).</summary>
    [Serializable]
    public class EquipmentItem
    {
        public string uid;
        public string set;
        public int rarity = 6;          // 성급 3~6 (최대 강화 9 / 12 / 16 / 20)
        public EquipSlot slot;
        public EquipStat main;
        public int level;
        public int initialSubs = 3;     // 태생 3옵션 / 4옵션
        public List<EquipSub> subs = new();
        public string owner = "";       // 장착 캐릭터 키(PlayerRoster.KeyOf), 비면 미장착
        public bool locked;
        public int transcend;           // 장비 초월 0~5 (최대 강화 후, 같은 세트·부위·성급 장비 소모)

        public bool IsEquipped => !string.IsNullOrEmpty(owner);
        /// <summary>성급(옛 세이브 = 0 → 6성 취급).</summary>
        public int Rarity => rarity >= EquipStats.MinRarity && rarity <= EquipStats.MaxRarity ? rarity : EquipStats.MaxRarity;
        public int MaxLevel => EquipStats.MaxLevelOf(Rarity);
        public bool IsMaxed => level >= MaxLevel;
        public float MainValue => EquipStats.MainValue(main, level, Rarity) * EquipStats.TranscendMainMul(transcend) * EquipStats.FlatScale(main);

        /// <summary>보조 옵션 실제 수치(초월 보정 포함).</summary>
        public float SubValue(EquipSub s) => s.value * EquipStats.TranscendSubMul(transcend) * EquipStats.FlatScale(s.stat);
        public bool CanTranscendMore => IsMaxed && transcend < EquipStats.MaxTranscend;
    }

    /// <summary>옵션 이름·표시·수치 규칙.</summary>
    public static class EquipStats
    {
        public const int MaxLevel = 20;   // 6성 기준 최대
        public const int RollEvery = 4;   // 4강마다 보조 옵션 1회
        public const int MinRarity = 3, MaxRarity = 6;

        /// <summary>성급별 최대 강화: 3성 9 · 4성 12 · 5성 16 · 6성 20.</summary>
        public static int MaxLevelOf(int rarity) => rarity switch { 3 => 9, 4 => 12, 5 => 16, _ => 20 };

        /// <summary>성급별 수치 배율(주옵션·보조 옵션): 3성 50% · 4성 65% · 5성 80% · 6성 100%.</summary>
        public static float RarityScale(int rarity) => rarity switch { 3 => 0.5f, 4 => 0.65f, 5 => 0.8f, _ => 1f };

        /// <summary>성급 색 = 아이템 등급 색(3 고급 초록 · 4 희귀 파랑 · 5 영웅 보라 · 6 전설 금색).</summary>
        public static Color RarityColor(int rarity) => Grades.ColorOf(rarity);

        // ---------------- 장비 초월 ----------------

        public const int MaxTranscend = 5;

        /// <summary>초월 단계별 보정: 주옵션 +6%/단계, 보조 옵션 +4%/단계.</summary>
        public static float TranscendMainMul(int t) => 1f + 0.06f * Mathf.Clamp(t, 0, MaxTranscend);
        public static float TranscendSubMul(int t) => 1f + 0.04f * Mathf.Clamp(t, 0, MaxTranscend);

        /// <summary>초월 1단계 금화: 5,000 × (성급-2) × (다음 단계).</summary>
        public static int TranscendGold(int rarity, int currentTranscend) => 5000 * Mathf.Max(1, rarity - 2) * (currentTranscend + 1);

        /// <summary>
        /// '★★★★' (성급 색). 초월하면 앞에서부터 초월 단계만큼 별이 무지개 색 + '+N' (캐릭터 초월과 같은 표시).
        /// </summary>
        public static string Stars(int rarity, int transcend = 0)
        {
            int n = Mathf.Clamp(rarity, 1, 6);
            int t = Mathf.Clamp(transcend, 0, MaxTranscend);
            string baseHex = "#" + ColorUtility.ToHtmlStringRGB(RarityColor(rarity));
            var rainbow = SubcultureRPG.Meta.UI.StarText.Rainbow;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
                sb.Append("<color=").Append(i < t ? rainbow[i % rainbow.Length] : baseHex).Append(">★</color>");
            if (t > 0) sb.Append(" <color=").Append(rainbow[(t - 1) % rainbow.Length]).Append(">+").Append(t).Append("</color>");
            return sb.ToString();
        }

        public static string GradeTag(int rarity) => Grades.Tag(rarity);

        public static readonly EquipSlot[] AllSlots =
            { EquipSlot.Head, EquipSlot.Hands, EquipSlot.Body, EquipSlot.Feet, EquipSlot.Neck, EquipSlot.Ring };

        public static string SlotName(EquipSlot s) => s switch
        {
            EquipSlot.Head => "머리", EquipSlot.Hands => "손", EquipSlot.Body => "몸",
            EquipSlot.Feet => "발", EquipSlot.Neck => "목걸이", _ => "반지",
        };

        public static string Name(EquipStat s) => s switch
        {
            EquipStat.HP => "체력", EquipStat.HPPct => "체력%",
            EquipStat.Atk => "공격력", EquipStat.AtkPct => "공격력%",
            EquipStat.Def => "방어력", EquipStat.DefPct => "방어력%",
            EquipStat.Spd => "속도",
            EquipStat.CritRate => "치명타 확률", EquipStat.CritDmg => "치명타 피해",
            EquipStat.Penetration => "방어 관통", EquipStat.BreakEffect => "브레이크 효율",
            EquipStat.EnergyRegen => "에너지 회복", EquipStat.HealBonus => "치유량",
            EquipStat.EffectHit => "효과 명중", EquipStat.EffectRes => "효과 저항",
            EquipStat.PhysicalDmg => "물리 피해", EquipStat.FireDmg => "화염 피해",
            EquipStat.IceDmg => "얼음 피해", EquipStat.LightningDmg => "번개 피해", EquipStat.WindDmg => "바람 피해",
            _ => s.ToString(),
        };

        /// <summary>고정 체력 · 공격력 · 방어력은 능력치 규모 배율을 따라감(저장된 값은 그대로, 읽을 때 곱함).</summary>
        public static float FlatScale(EquipStat s) =>
            s == EquipStat.HP || s == EquipStat.Atk || s == EquipStat.Def ? SubcultureRPG.Battle.BattleStats.Scale : 1f;

        public static bool IsPercent(EquipStat s) => s != EquipStat.HP && s != EquipStat.Atk && s != EquipStat.Def && s != EquipStat.Spd;

        /// <summary>표시: '공격력% +12.4%' / '체력 +350' / '속도 +6.5'.</summary>
        public static string Format(EquipStat s, float v)
        {
            string n = Name(s).Replace("%", "");
            if (IsPercent(s)) return $"{n} +{v * 100f:0.#}%";
            return s == EquipStat.Spd ? $"{n} +{v:0.#}" : $"{n} +{Mathf.RoundToInt(v)}";
        }

        public static string ValueText(EquipStat s, float v) =>
            IsPercent(s) ? $"+{v * 100f:0.#}%" : (s == EquipStat.Spd ? $"+{v:0.#}" : $"+{Mathf.RoundToInt(v)}");

        // ---------------- 주옵션 ----------------

        public static EquipStat[] MainPool(EquipSlot slot) => slot switch
        {
            EquipSlot.Head => new[] { EquipStat.HP },
            EquipSlot.Hands => new[] { EquipStat.Atk },
            EquipSlot.Body => new[] { EquipStat.HPPct, EquipStat.AtkPct, EquipStat.DefPct, EquipStat.CritRate, EquipStat.CritDmg, EquipStat.HealBonus, EquipStat.EffectHit },
            EquipSlot.Feet => new[] { EquipStat.HPPct, EquipStat.AtkPct, EquipStat.DefPct, EquipStat.Spd },
            EquipSlot.Neck => new[] { EquipStat.HPPct, EquipStat.AtkPct, EquipStat.DefPct, EquipStat.PhysicalDmg, EquipStat.FireDmg, EquipStat.IceDmg, EquipStat.LightningDmg, EquipStat.WindDmg },
            _ => new[] { EquipStat.HPPct, EquipStat.AtkPct, EquipStat.DefPct, EquipStat.BreakEffect, EquipStat.EnergyRegen, EquipStat.Penetration },
        };

        /// <summary>주옵션 +0 / +20 수치. 강화할 때마다 일정하게 증가.</summary>
        private static (float min, float max) MainRange(EquipStat s) => s switch
        {
            EquipStat.HP => (110f, 1500f),
            EquipStat.Atk => (55f, 300f),
            EquipStat.HPPct or EquipStat.AtkPct => (0.04f, 0.40f),
            EquipStat.DefPct => (0.05f, 0.50f),
            EquipStat.Spd => (3f, 25f),
            EquipStat.CritRate => (0.03f, 0.30f),
            EquipStat.CritDmg => (0.06f, 0.60f),
            EquipStat.HealBonus => (0.035f, 0.35f),
            EquipStat.EffectHit => (0.04f, 0.40f),
            EquipStat.BreakEffect => (0.06f, 0.60f),
            EquipStat.EnergyRegen => (0.02f, 0.18f),
            EquipStat.Penetration => (0.025f, 0.25f),
            _ => (0.04f, 0.40f),   // 속성 피해
        };

        /// <summary>주옵션 수치: 6성 +0 → +20 범위 × 성급 배율, 해당 성급의 최대 강화에서 최대치.</summary>
        public static float MainValue(EquipStat s, int level, int rarity = MaxRarity)
        {
            var (min, max) = MainRange(s);
            int cap = MaxLevelOf(rarity);
            return Mathf.Lerp(min, max, Mathf.Clamp(level, 0, cap) / (float)cap) * RarityScale(rarity);
        }

        // ---------------- 보조 옵션 ----------------

        public static readonly EquipStat[] SubPool =
        {
            EquipStat.HP, EquipStat.HPPct, EquipStat.Atk, EquipStat.AtkPct, EquipStat.Def, EquipStat.DefPct, EquipStat.Spd,
            EquipStat.CritRate, EquipStat.CritDmg, EquipStat.BreakEffect, EquipStat.EffectHit, EquipStat.EffectRes, EquipStat.Penetration,
        };

        /// <summary>보조 옵션 1회 최대 상승치(실제는 80/90/100% 중 무작위).</summary>
        public static float SubUnit(EquipStat s) => s switch
        {
            EquipStat.HP => 40f,
            EquipStat.Atk or EquipStat.Def => 20f,
            EquipStat.HPPct or EquipStat.AtkPct or EquipStat.EffectHit or EquipStat.EffectRes => 0.04f,
            EquipStat.DefPct => 0.05f,
            EquipStat.Spd => 2.5f,
            EquipStat.CritRate => 0.03f,
            EquipStat.CritDmg or EquipStat.BreakEffect => 0.06f,
            EquipStat.Penetration => 0.025f,
            _ => 0.04f,
        };

        public static float RollSub(EquipStat s, System.Random rng, int rarity = MaxRarity) =>
            SubUnit(s) * RarityScale(rarity) * new[] { 0.8f, 0.9f, 1f }[rng.Next(3)];

        // ---------------- 능력치 적용 ----------------

        public static void Apply(StatBonus b, EquipStat s, float v)
        {
            switch (s)
            {
                case EquipStat.HP: b.hp += v; break;
                case EquipStat.HPPct: b.hpPct += v; break;
                case EquipStat.Atk: b.atk += v; break;
                case EquipStat.AtkPct: b.atkPct += v; break;
                case EquipStat.Def: b.def += v; break;
                case EquipStat.DefPct: b.defPct += v; break;
                case EquipStat.Spd: b.spd += v; break;
                case EquipStat.CritRate: b.critRate += v; break;
                case EquipStat.CritDmg: b.critDmg += v; break;
                case EquipStat.Penetration: b.penetration += v; break;
                case EquipStat.BreakEffect: b.breakEffect += v; break;
                case EquipStat.EnergyRegen: b.energyRegen += v; break;
                case EquipStat.HealBonus: b.healBonus += v; break;
                case EquipStat.EffectHit: b.effectHit += v; break;
                case EquipStat.EffectRes: b.effectRes += v; break;
                case EquipStat.PhysicalDmg: b.AddElementDmg(Element.Physical, v); break;
                case EquipStat.FireDmg: b.AddElementDmg(Element.Fire, v); break;
                case EquipStat.IceDmg: b.AddElementDmg(Element.Ice, v); break;
                case EquipStat.LightningDmg: b.AddElementDmg(Element.Lightning, v); break;
                case EquipStat.WindDmg: b.AddElementDmg(Element.Wind, v); break;
            }
        }

        /// <summary>능력치 묶음을 읽기 좋은 줄들로(0이 아닌 것만).</summary>
        public static List<string> Describe(StatBonus b)
        {
            var list = new List<string>();
            void P(string n, float v, bool pct) { if (Mathf.Abs(v) > 0.0001f) list.Add(pct ? $"{n} +{v * 100f:0.#}%" : $"{n} +{v:0.#}"); }
            P("체력", b.hp, false); P("체력", b.hpPct, true);
            P("공격력", b.atk, false); P("공격력", b.atkPct, true);
            P("방어력", b.def, false); P("방어력", b.defPct, true);
            P("속도", b.spd, false);
            P("치명타 확률", b.critRate, true); P("치명타 피해", b.critDmg, true);
            P("방어 관통", b.penetration, true); P("피해", b.allDmg, true);
            foreach (var e in Traits.AllElements) P($"{Traits.Name(e)} 피해", b.ElementDmg(e), true);
            P("브레이크 효율", b.breakEffect, true); P("에너지 회복", b.energyRegen, true); P("치유량", b.healBonus, true);
            P("효과 명중", b.effectHit, true); P("효과 저항", b.effectRes, true);
            if (b.startEnergy != 0) list.Add($"시작 에너지 +{b.startEnergy}");
            return list;
        }
    }

    // ======================================================================
    //  세트
    // ======================================================================

    /// <summary>장비 세트 1종: 2세트 / 4세트 효과.</summary>
    public class EquipSet
    {
        public string id, name, desc2, desc4;
        public Color color;
        public Action<StatBonus> bonus2, bonus4;
    }

    public static class EquipSets
    {
        public static readonly EquipSet[] All =
        {
            Make("assault", "맹공의 문장", new Color(0.95f, 0.45f, 0.35f),
                "공격력 +12%", b => b.atkPct += 0.12f,
                "치명타 확률 +12%", b => b.critRate += 0.12f),
            Make("guardian", "수호의 서약", new Color(0.55f, 0.7f, 0.85f),
                "방어력 +15%", b => b.defPct += 0.15f,
                "체력 +12% · 효과 저항 +15%", b => { b.hpPct += 0.12f; b.effectRes += 0.15f; }),
            Make("ember", "화염의 낙인", Traits.ColorOf(Element.Fire),
                "화염 피해 +10%", b => b.AddElementDmg(Element.Fire, 0.10f),
                "화염 피해 +15% · 치명타 피해 +16%", b => { b.AddElementDmg(Element.Fire, 0.15f); b.critDmg += 0.16f; }),
            Make("frost", "서리의 결정", Traits.ColorOf(Element.Ice),
                "얼음 피해 +10%", b => b.AddElementDmg(Element.Ice, 0.10f),
                "얼음 피해 +15% · 효과 명중 +15%", b => { b.AddElementDmg(Element.Ice, 0.15f); b.effectHit += 0.15f; }),
            Make("thunder", "뇌명의 인장", Traits.ColorOf(Element.Lightning),
                "번개 피해 +10%", b => b.AddElementDmg(Element.Lightning, 0.10f),
                "번개 피해 +15% · 방어 관통 +10%", b => { b.AddElementDmg(Element.Lightning, 0.15f); b.penetration += 0.10f; }),
            Make("gale", "질풍의 깃털", Traits.ColorOf(Element.Wind),
                "바람 피해 +10%", b => b.AddElementDmg(Element.Wind, 0.10f),
                "바람 피해 +15% · 속도 +8", b => { b.AddElementDmg(Element.Wind, 0.15f); b.spd += 8f; }),
            Make("steel", "강철의 의지", Traits.ColorOf(Element.Physical),
                "물리 피해 +10%", b => b.AddElementDmg(Element.Physical, 0.10f),
                "물리 피해 +15% · 브레이크 효율 +20%", b => { b.AddElementDmg(Element.Physical, 0.15f); b.breakEffect += 0.20f; }),
            Make("breaker", "파쇄의 망치", new Color(0.85f, 0.6f, 0.3f),
                "브레이크 효율 +16%", b => b.breakEffect += 0.16f,
                "브레이크 효율 +24% · 방어 관통 +8%", b => { b.breakEffect += 0.24f; b.penetration += 0.08f; }),
            Make("spring", "생명의 샘", new Color(0.45f, 0.85f, 0.55f),
                "치유량 +10%", b => b.healBonus += 0.10f,
                "에너지 회복 +10% · 전투 시작 에너지 +20", b => { b.energyRegen += 0.10f; b.startEnergy += 20; }),
            Make("swift", "신속의 바람", new Color(0.6f, 0.9f, 0.95f),
                "속도 +6", b => b.spd += 6f,
                "공격력 +10% · 전투 시작 에너지 +15", b => { b.atkPct += 0.10f; b.startEnergy += 15; }),
        };

        private static EquipSet Make(string id, string name, Color c, string d2, Action<StatBonus> b2, string d4, Action<StatBonus> b4) =>
            new EquipSet { id = id, name = name, color = c, desc2 = d2, bonus2 = b2, desc4 = d4, bonus4 = b4 };

        public static EquipSet Get(string id)
        {
            foreach (var s in All) if (s.id == id) return s;
            return null;
        }

        public static string Name(string id) => Get(id)?.name ?? id;
        public static string Hex(string id) => "#" + ColorUtility.ToHtmlStringRGB(Get(id)?.color ?? Color.white);
        public static string Rich(string id) => $"<color={Hex(id)}>{Name(id)}</color>";

        /// <summary>짧은 이름('맹공의 문장' → '맹공').</summary>
        public static string Short(string id)
        {
            string n = Name(id);
            int sp = n.IndexOf(' ');
            if (sp > 0) n = n.Substring(0, sp);
            return n.EndsWith("의") ? n.Substring(0, n.Length - 1) : n;
        }
    }

    // ======================================================================
    //  생성 · 강화 · 능력치 합산
    // ======================================================================

    public static class EquipRules
    {
        /// <summary>태생 4옵션 확률(나머지는 3옵션).</summary>
        public const float FourLineChance = 0.25f;

        /// <summary>+lv → +lv+1 강화 금화.</summary>
        public static int EnhanceGold(int level) => 500 + 250 * Mathf.Clamp(level, 0, EquipStats.MaxLevel);

        /// <summary>+lv → +lv+1 장비 강화석.</summary>
        public static int EnhanceOre(int level) => 1 + Mathf.Clamp(level, 0, EquipStats.MaxLevel) / 4;

        /// <summary>분해 시 돌려받는 강화석(성급 기본 1/2/3/5 + 투입분의 60%).</summary>
        public static int DismantleOre(EquipmentItem it)
        {
            int spent = 0;
            for (int l = 0; l < it.level; l++) spent += EnhanceOre(l);
            int baseOre = it.Rarity switch { 3 => 1, 4 => 2, 5 => 3, _ => 5 };
            return baseOre + Mathf.FloorToInt(spent * 0.6f);
        }

        /// <summary>성급 가중치(3·4·5·6성 순)로 성급 하나 뽑기.</summary>
        public static int RollRarity(IList<int> weights3to6, System.Random rng)
        {
            int total = 0;
            if (weights3to6 != null) foreach (var w in weights3to6) total += Mathf.Max(0, w);
            if (total <= 0) return EquipStats.MaxRarity;
            int r = rng.Next(total);
            for (int i = 0; i < weights3to6.Count; i++)
            {
                r -= Mathf.Max(0, weights3to6[i]);
                if (r < 0) return EquipStats.MinRarity + i;
            }
            return EquipStats.MaxRarity;
        }

        /// <summary>무작위 장비 생성(부위를 지정하지 않으면 무작위).</summary>
        public static EquipmentItem Generate(string set, System.Random rng, int rarity = EquipStats.MaxRarity, EquipSlot? slot = null)
        {
            var it = new EquipmentItem
            {
                uid = Guid.NewGuid().ToString("N"),
                set = set,
                rarity = Mathf.Clamp(rarity, EquipStats.MinRarity, EquipStats.MaxRarity),
                slot = slot ?? EquipStats.AllSlots[rng.Next(EquipStats.AllSlots.Length)],
                level = 0,
            };
            var pool = EquipStats.MainPool(it.slot);
            it.main = pool[rng.Next(pool.Length)];
            it.initialSubs = rng.NextDouble() < FourLineChance ? 4 : 3;
            for (int i = 0; i < it.initialSubs; i++) AddRandomSub(it, rng);
            return it;
        }

        private static void AddRandomSub(EquipmentItem it, System.Random rng)
        {
            var candidates = new List<EquipStat>();
            foreach (var s in EquipStats.SubPool)
                if (s != it.main && !it.subs.Exists(x => x.stat == s)) candidates.Add(s);
            if (candidates.Count == 0) return;
            var stat = candidates[rng.Next(candidates.Count)];
            it.subs.Add(new EquipSub { stat = stat, value = EquipStats.RollSub(stat, rng, it.Rarity), rolls = 1 });
        }

        /// <summary>
        /// 1단계 강화(비용 차감은 호출자). 4강마다: 보조 옵션이 3줄이면 새 옵션 1줄 추가, 4줄이면 무작위 1줄 수치 상승.
        /// 반환: 보조 옵션 변화 설명(없으면 빈 문자열).
        /// </summary>
        public static string EnhanceOnce(EquipmentItem it, System.Random rng)
        {
            if (it.IsMaxed) return "";
            it.level++;
            if (it.level % EquipStats.RollEvery != 0) return "";

            if (it.subs.Count < 4)
            {
                AddRandomSub(it, rng);
                var added = it.subs[it.subs.Count - 1];
                return $"새 보조 옵션: {EquipStats.Format(added.stat, added.value)}";
            }
            var sub = it.subs[rng.Next(it.subs.Count)];
            float gain = EquipStats.RollSub(sub.stat, rng, it.Rarity);
            sub.value += gain;
            sub.rolls++;
            return $"{EquipStats.Name(sub.stat).Replace("%", "")} {EquipStats.ValueText(sub.stat, gain)}";
        }

        /// <summary>장비 목록(한 캐릭터 장착분)의 능력치 + 세트 효과.</summary>
        public static StatBonus BonusOf(IEnumerable<EquipmentItem> items)
        {
            var b = new StatBonus();
            var counts = new Dictionary<string, int>();
            foreach (var it in items)
            {
                if (it == null) continue;
                EquipStats.Apply(b, it.main, it.MainValue);
                foreach (var s in it.subs) EquipStats.Apply(b, s.stat, it.SubValue(s));
                counts[it.set] = counts.TryGetValue(it.set, out var n) ? n + 1 : 1;
            }
            foreach (var kv in counts)
            {
                var set = EquipSets.Get(kv.Key);
                if (set == null) continue;
                if (kv.Value >= 2) set.bonus2?.Invoke(b);
                if (kv.Value >= 4) set.bonus4?.Invoke(b);
            }
            return b;
        }

        /// <summary>세트별 장착 개수.</summary>
        public static Dictionary<string, int> SetCounts(IEnumerable<EquipmentItem> items)
        {
            var counts = new Dictionary<string, int>();
            foreach (var it in items)
                if (it != null) counts[it.set] = counts.TryGetValue(it.set, out var n) ? n + 1 : 1;
            return counts;
        }
    }
}
