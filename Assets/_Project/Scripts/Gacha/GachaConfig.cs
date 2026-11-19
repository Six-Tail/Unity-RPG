using UnityEngine;

namespace SubcultureRPG.Gacha
{
    /// <summary>
    /// 확률·천장 설정. 메뉴: Create > SubcultureRPG > Gacha > Gacha Config.
    /// 숫자만 바꿔 밸런스를 조정합니다(코드 수정 불필요).
    /// </summary>
    [CreateAssetMenu(fileName = "GachaConfig", menuName = "SubcultureRPG/Gacha/Gacha Config")]
    public class GachaConfig : ScriptableObject
    {
        [Header("기본 확률 (%)  — 3성은 자동으로 나머지")]
        [Range(0f, 100f)] public float rate6 = 0.6f;
        [Range(0f, 100f)] public float rate5 = 5.1f;
        [Range(0f, 100f)] public float rate4 = 30f;

        [Header("6성 천장")]
        [Tooltip("이 뽑기 번호부터 6성 확률이 매 뽑기 상승(소프트 천장).")]
        public int sixStarSoftPityStart = 74;
        [Tooltip("소프트 천장 구간에서 매 뽑기 증가하는 6성 확률(%).")]
        public float sixStarSoftPityIncrement = 6f;
        [Tooltip("이 번호에서 6성 확정(하드 천장).")]
        public int sixStarHardPity = 90;

        [Header("5성 천장")]
        [Tooltip("이 횟수 내 5성 이상 확정. (예: 10 → 10연 보장)")]
        public int fiveStarHardPity = 10;

        [Header("픽업(rate-up)")]
        [Range(0f, 1f)] public float featured6Chance = 0.5f; // 6성 50/50
        [Range(0f, 1f)] public float featured5Chance = 0.5f;

        /// <summary>3성 확률(자동 계산).</summary>
        public float Rate3 => Mathf.Max(0f, 100f - rate6 - rate5 - rate4);

        private void OnValidate()
        {
            if (sixStarSoftPityStart < 1) sixStarSoftPityStart = 1;
            if (sixStarHardPity < sixStarSoftPityStart) sixStarHardPity = sixStarSoftPityStart;
            if (fiveStarHardPity < 1) fiveStarHardPity = 1;
            if (rate6 + rate5 + rate4 > 100f)
                Debug.LogWarning("[GachaConfig] 6+5+4 확률 합이 100%를 넘습니다. 3성 확률이 0으로 처리됩니다.");
        }
    }
}
