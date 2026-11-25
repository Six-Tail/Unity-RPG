using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>스킬 3종(개별 레벨업).</summary>
    public enum SkillSlot
    {
        Basic = 0,     // 기본 공격
        Skill = 1,     // 스킬
        Ultimate = 2,  // 궁극기
    }

    /// <summary>스킬 1단계 레벨업 비용.</summary>
    public struct SkillCost
    {
        public MaterialType book;   // 등급 교본
        public int books;
        public int legend;          // 전설의 비전서
        public int gold;
    }

    /// <summary>레벨/돌파/스킬 규칙(튜닝 지점).</summary>
    public static class Leveling
    {
        public const int MaxLevel = 80;
        /// <summary>풀 초월(6★ +6) 캐릭터의 레벨 상한(돌파 3단계 뒤 80 → 100).</summary>
        public const int AwakenedMaxLevel = 100;
        public const int MaxAscension = 3;   // 돌파 0~3 → 레벨 상한 20/40/60/80
        public const int MaxSkillLevel = 10;   // 재료로 올릴 수 있는 최대
        /// <summary>초월 보너스까지 더한 최대 레벨(10 + 3).</summary>
        public const int MaxSkillLevelAbsolute = MaxSkillLevel + SubcultureRPG.Characters.Transcendence.SkillLevelStep;

        /// <summary>초월로 재료 없이 더해지는 스킬 레벨: +2(기본 공격) · +4(스킬) · +5(궁극기)에서 각각 +3.</summary>
        public static int SkillBonus(int transcend, SkillSlot slot) =>
            SubcultureRPG.Characters.Transcendence.SkillLevelBonus(transcend, (int)slot);
        public static readonly SkillSlot[] SkillSlots = { SkillSlot.Basic, SkillSlot.Skill, SkillSlot.Ultimate };

        public static string SkillSlotName(SkillSlot s) => s switch
        {
            SkillSlot.Basic => "기본 공격",
            SkillSlot.Skill => "스킬",
            _ => "궁극기",
        };

        /// <summary>
        /// 스킬 Lv → Lv+1 비용(기본 공격·스킬·궁극기 각각).
        /// 1→3 초급(초록) · 3→5 중급(파랑) · 5→10 상급(보라) 교본, 8 · 9 · 10으로 올릴 때는 전설의 비전서(금색)도 필요.
        /// 초월(+2 · +4 · +5)로 얻는 +3 레벨은 재료 없이 바로 더해집니다(SkillBonus).
        /// </summary>
        public static SkillCost SkillUpgrade(int level)
        {
            int lv = Mathf.Clamp(level, 1, MaxSkillLevel - 1);
            var c = lv switch
            {
                1 => new SkillCost { book = MaterialType.SkillBook, books = 2 },
                2 => new SkillCost { book = MaterialType.SkillBook, books = 3 },
                3 => new SkillCost { book = MaterialType.SkillBookBlue, books = 2 },
                4 => new SkillCost { book = MaterialType.SkillBookBlue, books = 3 },
                5 => new SkillCost { book = MaterialType.SkillBookPurple, books = 2 },
                6 => new SkillCost { book = MaterialType.SkillBookPurple, books = 3 },
                7 => new SkillCost { book = MaterialType.SkillBookPurple, books = 4, legend = 1 },
                8 => new SkillCost { book = MaterialType.SkillBookPurple, books = 5, legend = 1 },
                _ => new SkillCost { book = MaterialType.SkillBookPurple, books = 6, legend = 2 },
            };
            c.gold = Economy.SkillUpGold(lv);
            return c;
        }

        /// <summary>돌파 단계별 레벨 상한. awakened(풀 초월)이고 돌파를 모두 마쳤으면 100.</summary>
        public static int LevelCap(int ascension, bool awakened = false)
        {
            int cap = Mathf.Min(MaxLevel, 20 + 20 * Mathf.Clamp(ascension, 0, MaxAscension));
            return awakened && ascension >= MaxAscension ? AwakenedMaxLevel : cap;
        }

        /// <summary>
        /// level → level+1 에 필요한 경험치. Lv.80부터(풀 초월 전용 구간)는 레벨당 1.5배.
        /// 누적: Lv.80까지 약 31.6만 · 80 → 100 약 26.7만
        /// </summary>
        public static int ExpForNext(int level) =>
            level >= AwakenedMaxLevel ? int.MaxValue : (level >= MaxLevel ? 150 : 100) * Mathf.Max(1, level);

        // 돌파 비용(속성별 돌파 재료 + 금화)은 Economy 참고.

        /// <summary>
        /// 레벨업 금화: 경험치 1당 금화. 레벨이 높을수록 비쌈.
        /// Lv.1 0.68 · Lv.20 1.3 · Lv.40 1.95 · Lv.60 2.6 · Lv.79 3.22
        /// 누적: Lv.20까지 약 2만 · 40까지 +9.7만 · 60까지 +22.6만 · 80까지 +40.6만 = 총 약 75만 금화
        /// </summary>
        public static float GoldPerExp(int level) => GoldPerExpBase * (1f + Mathf.Max(1, level) / 20f);

        /// <summary>레벨업 금화 기본 단가(이 값 하나로 전체 레벨업 금화가 비례해서 바뀜).</summary>
        public const float GoldPerExpBase = 0.65f;

        /// <summary>경험치 투입 결과 미리보기.</summary>
        public struct LevelPreview
        {
            public int level, exp;       // 결과 레벨 / 남은 경험치
            public int usedExp;          // 실제로 들어간 경험치(상한 초과분 제외)
            public int wastedExp;        // 상한 초과로 버려지는 경험치
            public int gold;             // 필요한 금화
            public bool atCap;           // 결과가 레벨 상한
        }

        /// <summary>level/exp 상태에서 addExp를 넣으면? (상한 cap까지만, 금화는 들어간 경험치만큼 레벨별 단가로)</summary>
        public static LevelPreview Preview(int level, int exp, int cap, int addExp)
        {
            double gold = 0;
            int used = 0, add = Mathf.Max(0, addExp);
            while (add > 0 && level < cap)
            {
                int need = ExpForNext(level) - exp;
                int take = Mathf.Min(need, add);
                gold += take * (double)GoldPerExp(level);
                exp += take; add -= take; used += take;
                if (exp >= ExpForNext(level)) { exp = 0; level++; }
            }
            if (level >= cap) exp = 0;
            return new LevelPreview
            {
                level = level, exp = exp, usedExp = used, wastedExp = add,
                gold = (int)System.Math.Ceiling(gold - 1e-6), atCap = level >= cap,
            };
        }

        /// <summary>현재 상태에서 상한까지 필요한 경험치.</summary>
        public static int ExpToCap(int level, int exp, int cap)
        {
            long sum = 0;
            for (int l = level; l < cap; l++) sum += ExpForNext(l);
            return (int)Mathf.Max(0, Mathf.Min(int.MaxValue, sum - exp));
        }

            }
}
