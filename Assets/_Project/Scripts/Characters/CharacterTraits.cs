using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>속성. (캐릭터 고유 특성 — 전투에서 약점 판정에 사용)</summary>
    public enum Element
    {
        Physical, // 물리
        Fire,     // 화염
        Ice,      // 얼음
        Lightning,// 번개
        Wind,     // 바람
    }

    /// <summary>역할(포지션).</summary>
    public enum Role
    {
        Dealer,  // 딜러 — 높은 공격력
        Tank,    // 탱커 — 높은 체력/방어
        Support, // 서포터 — 아군 강화
        Healer,  // 힐러 — 아군 회복
    }

    /// <summary>속성/역할 표시용 이름·색.</summary>
    public static class Traits
    {
        public static readonly Element[] AllElements =
            { Element.Physical, Element.Fire, Element.Ice, Element.Lightning, Element.Wind };

        public static readonly Role[] AllRoles =
            { Role.Dealer, Role.Tank, Role.Support, Role.Healer };

        public static string Name(Element e) => e switch
        {
            Element.Physical => "물리",
            Element.Fire => "화염",
            Element.Ice => "얼음",
            Element.Lightning => "번개",
            Element.Wind => "바람",
            _ => e.ToString(),
        };

        public static string Name(Role r) => r switch
        {
            Role.Dealer => "딜러",
            Role.Tank => "탱커",
            Role.Support => "서포터",
            Role.Healer => "힐러",
            _ => r.ToString(),
        };

        public static Color ColorOf(Element e) => e switch
        {
            Element.Physical => new Color(0.75f, 0.75f, 0.78f),
            Element.Fire => new Color(1f, 0.45f, 0.3f),
            Element.Ice => new Color(0.45f, 0.85f, 1f),
            Element.Lightning => new Color(0.8f, 0.55f, 1f),
            Element.Wind => new Color(0.45f, 0.9f, 0.6f),
            _ => UnityEngine.Color.white,
        };

        /// <summary>리치텍스트 색 코드(#RRGGBB).</summary>
        public static string Hex(Element e) => "#" + ColorUtility.ToHtmlStringRGB(ColorOf(e));
    }
}
