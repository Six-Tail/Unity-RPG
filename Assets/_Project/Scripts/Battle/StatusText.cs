using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>버프/디버프 표시 문구·색(전투 상태 아이콘과 설명 창).</summary>
    public static class StatusText
    {
        public static string Name(StatusType t) => SkillText.Effect(t);

        /// <summary>아이콘에 들어갈 한 글자.</summary>
        public static string Glyph(StatusType t) => t switch
        {
            StatusType.AtkUp or StatusType.AtkDown => "공",
            StatusType.DefUp or StatusType.DefDown => "방",
            StatusType.Dot => "독",
            _ => "?",
        };

        public static bool IsBuff(StatusType t) => t == StatusType.AtkUp || t == StatusType.DefUp;

        /// <summary>아이콘 바탕색: 버프 청록 · 디버프 빨강 · 지속 피해 보라.</summary>
        public static Color ColorOf(StatusType t) =>
            t == StatusType.Dot ? new Color(0.58f, 0.32f, 0.82f)
            : IsBuff(t) ? new Color(0.16f, 0.58f, 0.50f)
            : new Color(0.78f, 0.26f, 0.30f);

        /// <summary>효과 내용 한 줄('공격력 +25%' / '턴 시작마다 피해 120').</summary>
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
                StatusType.Dot => $"자기 턴이 시작될 때마다 피해 {Mathf.RoundToInt(e.magnitude):N0}",
                _ => "",
            };
        }
    }
}
