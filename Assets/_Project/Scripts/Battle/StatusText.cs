using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>버프/디버프 표시 문구·색(전투 상태 아이콘과 설명 창, 스킬·패시브 설명).</summary>
    public static class StatusText
    {
        public static string Name(StatusType t) => SkillText.Effect(t);

        /// <summary>아이콘에 들어갈 한 글자.</summary>
        public static string Glyph(StatusType t) => t switch
        {
            StatusType.AtkUp or StatusType.AtkDown => "공",
            StatusType.DefUp or StatusType.DefDown => "방",
            StatusType.SpdUp or StatusType.SpdDown => "속",
            StatusType.CritUp => "치",
            StatusType.Dot => "독",
            StatusType.Regen => "생",
            StatusType.Taunt => "도",
            StatusType.Stun => "절",
            _ => "?",
        };

        public static bool IsBuff(StatusType t) =>
            t == StatusType.AtkUp || t == StatusType.DefUp || t == StatusType.SpdUp || t == StatusType.CritUp
            || t == StatusType.Regen || t == StatusType.Taunt;

        public static bool IsDebuff(StatusType t) => t != StatusType.None && !IsBuff(t);

        /// <summary>아이콘 화살표: +1 = 증가(↑) · -1 = 감소(↓) · 0 = 없음.</summary>
        public static int Arrow(StatusType t) => t switch
        {
            StatusType.AtkUp or StatusType.DefUp or StatusType.SpdUp or StatusType.CritUp => 1,
            StatusType.AtkDown or StatusType.DefDown or StatusType.SpdDown => -1,
            _ => 0,
        };

        /// <summary>아이콘 바탕색: 버프 청록 · 디버프 빨강 · 지속 피해 보라 · 재생 초록 · 도발 주황 · 기절 파랑.</summary>
        public static Color ColorOf(StatusType t) => t switch
        {
            StatusType.Dot => new Color(0.58f, 0.32f, 0.82f),
            StatusType.Regen => new Color(0.25f, 0.68f, 0.38f),
            StatusType.Taunt => new Color(0.85f, 0.5f, 0.2f),
            StatusType.Stun => new Color(0.3f, 0.5f, 0.85f),
            _ => IsBuff(t) ? new Color(0.16f, 0.58f, 0.50f) : new Color(0.78f, 0.26f, 0.30f),
        };

        /// <summary>
        /// 설명용 짧은 문구(계수 기준): '공격력 +25% (2턴)' / '지속 피해(매 턴 공격력의 30%) (2턴)' / '도발 (2턴)'.
        /// </summary>
        public static string Short(StatusType t, float mag, int turns)
        {
            string pct = $"{mag * 100f:0.#}%";
            string body = t switch
            {
                StatusType.AtkUp => $"공격력 +{pct}",
                StatusType.AtkDown => $"공격력 -{pct}",
                StatusType.DefUp => $"방어력 +{pct}",
                StatusType.DefDown => $"방어력 -{pct}",
                StatusType.SpdUp => $"속도 +{pct}",
                StatusType.SpdDown => $"속도 -{pct}",
                StatusType.CritUp => $"치명타 확률 +{pct}",
                StatusType.Dot => $"지속 피해(매 턴 공격력의 {pct})",
                StatusType.Regen => $"재생(매 턴 공격력의 {pct} 회복)",
                StatusType.Taunt => "도발(적의 단일 공격을 끌어옴)",
                StatusType.Stun => "기절(행동 불가)",
                _ => "",
            };
            return turns > 0 ? $"{body} ({turns}턴)" : body;
        }

        /// <summary>효과 내용 한 줄 — 전투 중 걸린 효과 기준('공격력 +25%' / '턴 시작마다 피해 120').</summary>
        public static string Detail(StatusEffect e)
        {
            if (e == null) return "";
            string pct = $"{e.magnitude * 100f:0.#}%";
            return e.type switch
            {
                StatusType.AtkUp => $"공격력 +{pct}",
                StatusType.AtkDown => $"공격력 -{pct}",
                StatusType.DefUp => $"방어력 +{pct}",
                StatusType.DefDown => $"방어력 -{pct}",
                StatusType.SpdUp => $"속도 +{pct}",
                StatusType.SpdDown => $"속도 -{pct}",
                StatusType.CritUp => $"치명타 확률 +{pct}",
                StatusType.Dot => $"자기 턴이 시작될 때마다 피해 {Mathf.RoundToInt(e.magnitude):N0}",
                StatusType.Regen => $"자기 턴이 시작될 때마다 체력 {Mathf.RoundToInt(e.magnitude):N0} 회복",
                StatusType.Taunt => "적의 단일 대상 공격이 이 유닛에게 향함",
                StatusType.Stun => "다음 자기 턴에 행동할 수 없음",
                _ => "",
            };
        }
    }
}
