using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 성급 색 / 초월 단계 색 / 텍스트용 별 문자열. 화면의 별 아이콘은 <see cref="StarRow"/>(이미지)를 사용합니다.
    /// Rich()는 토스트·로그 등 텍스트 전용 대체 표기입니다.
    /// </summary>
    public static class StarText
    {
        /// <summary>초월 단계 색(+1~+6) — 홀로 별과 어울리는 하늘 → 라벤더 → 핑크.</summary>
        public static readonly string[] Rainbow = { "#8FE9FF", "#A9D4FF", "#C3BEFF", "#DDB0FF", "#F3A8F0", "#FFB3D9" };

        /// <summary>별 문자열. showPlus면 뒤에 '+N'(무지개 마지막 색)을 붙입니다.</summary>
        public static string Rich(int star, int transcend, bool showPlus = true)
        {
            star = Mathf.Clamp(star, 1, Transcendence.MaxStar);
            transcend = star >= Transcendence.MaxStar ? Mathf.Clamp(transcend, 0, Transcendence.MaxTranscend) : 0;

            var sb = new System.Text.StringBuilder();
            string baseHex = "#" + ColorUtility.ToHtmlStringRGB(ColorOf(star));
            for (int i = 0; i < star; i++)
                sb.Append("<color=").Append(i < transcend ? Rainbow[i] : baseHex).Append(">★</color>");
            if (showPlus && transcend > 0)
                sb.Append(" <color=").Append(Rainbow[transcend - 1]).Append(">+").Append(transcend).Append("</color>");
            return sb.ToString();
        }

        /// <summary>'+N' 만 무지개색으로.</summary>
        public static string Plus(int transcend) =>
            transcend <= 0 ? "" : $"<color={Rainbow[Mathf.Clamp(transcend, 1, 6) - 1]}>+{transcend}</color>";

        /// <summary>성급 색(카드 테두리·별).</summary>
        public static Color ColorOf(int star) => star switch
        {
            >= 6 => new Color(1f, 0.78f, 0.25f),
            5 => new Color(0.72f, 0.42f, 1f),
            4 => new Color(0.33f, 0.62f, 1f),
            _ => new Color(0.62f, 0.67f, 0.75f),
        };

        /// <summary>카드 테두리 색 — 현재 성급 기준, 풀 초월(+6)은 홀로 라벤더.</summary>
        public static Color FrameColor(int star, int transcend) =>
            star >= Transcendence.MaxStar && transcend >= Transcendence.MaxTranscend
                ? new Color(0.78f, 0.80f, 1f)
                : ColorOf(star);
    }
}
