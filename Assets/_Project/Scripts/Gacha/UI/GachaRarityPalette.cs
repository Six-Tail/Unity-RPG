using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Gacha.UI
{
    /// <summary>
    /// 성급별 색/연출 세기. 숫자·색만 바꿔 비주얼을 튜닝합니다(코드 수정 불필요).
    /// 메뉴: Create > SubcultureRPG > Gacha > Rarity Palette.
    /// 패널에 비워 두면 아래 기본값으로 동작합니다(런타임 기본 인스턴스 생성).
    /// </summary>
    [CreateAssetMenu(fileName = "GachaRarityPalette", menuName = "SubcultureRPG/Gacha/Rarity Palette")]
    public class GachaRarityPalette : ScriptableObject
    {
        [System.Serializable]
        public struct Style
        {
            [Tooltip("프레임(테두리)·하단 바·별 색.")] public Color frame;
            [Tooltip("포트레이트 뒤 글로우 색.")] public Color glow;
            [Tooltip("공개 연출 세기(0~1). 6성일수록 크게 → 플래시/글로우 강해짐.")] public float punch;
        }

        [Header("6성")] public Style r6 = new Style { frame = new Color(1f, 0.78f, 0.25f), glow = new Color(1f, 0.70f, 0.20f), punch = 1f };
        [Header("5성")] public Style r5 = new Style { frame = new Color(0.72f, 0.42f, 1f), glow = new Color(0.60f, 0.30f, 0.95f), punch = 0.6f };
        [Header("4성")] public Style r4 = new Style { frame = new Color(0.33f, 0.62f, 1f), glow = new Color(0.25f, 0.50f, 0.95f), punch = 0.3f };
        [Header("3성")] public Style r3 = new Style { frame = new Color(0.62f, 0.67f, 0.75f), glow = new Color(0.45f, 0.50f, 0.58f), punch = 0.15f };

        /// <summary>성급에 해당하는 스타일.</summary>
        public Style Get(Rarity rarity) => rarity switch
        {
            Rarity.R6 => r6,
            Rarity.R5 => r5,
            Rarity.R4 => r4,
            _ => r3,
        };

        /// <summary>인스펙터에 팔레트를 연결하지 않았을 때 쓰는 런타임 기본 인스턴스(필드 기본값 사용).</summary>
        public static GachaRarityPalette CreateDefault() => CreateInstance<GachaRarityPalette>();
    }
}
