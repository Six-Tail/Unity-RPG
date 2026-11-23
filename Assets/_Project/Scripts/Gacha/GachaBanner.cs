using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Gacha
{
    /// <summary>배너 종류. 천장·뽑기권은 종류별로 따로 갑니다.</summary>
    public enum GachaBannerType
    {
        Pickup = 0,    // 픽업(한정) — 픽업 6성 50/50, 픽업 배너끼리 천장 공유
        Standard = 1,  // 상시 — 상시 6성 풀에서만
    }

    /// <summary>
    /// 하나의 뽑기 배너(픽업 + 등급별 풀).
    /// 메뉴: Create > SubcultureRPG > Gacha > Banner.
    /// </summary>
    [CreateAssetMenu(fileName = "Banner", menuName = "SubcultureRPG/Gacha/Banner")]
    public class GachaBanner : ScriptableObject
    {
        public string bannerName = "신규 배너";
        [Tooltip("픽업 = 한정 캐릭터 50/50, 상시 = 상시 6성 풀만. 천장·뽑기권이 종류별로 분리됩니다.")]
        public GachaBannerType type = GachaBannerType.Pickup;
        public GachaConfig config;

        [Header("픽업(rate-up)")]
        [Tooltip("이번 배너 픽업 6성(1명). 상시 배너는 비워 둡니다.")]
        public CharacterData featured6;
        [Tooltip("픽업 5성(여럿 가능).")]
        public List<CharacterData> featured5 = new();

        [Header("등급별 풀 (비픽업)")]
        public List<CharacterData> pool6 = new();
        public List<CharacterData> pool5 = new();
        public List<CharacterData> pool4 = new();
        public List<CharacterData> pool3 = new();

        public bool IsPickup => type == GachaBannerType.Pickup;

        /// <summary>천장을 공유하는 묶음 키(픽업 배너끼리, 상시는 따로).</summary>
        public string PityGroup => type == GachaBannerType.Standard ? "standard" : "pickup";

        /// <summary>탭 등에 쓰는 짧은 이름.</summary>
        public string ShortName => IsPickup
            ? (featured6 != null ? featured6.displayName : bannerName)
            : "상시";
    }
}
