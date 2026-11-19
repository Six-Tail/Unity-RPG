using System;

namespace SubcultureRPG.Gacha
{
    /// <summary>
    /// 배너별 천장/카운터 상태. 세이브 대상(직렬화 가능).
    /// </summary>
    [Serializable]
    public class GachaPityState
    {
        /// <summary>마지막 6성 이후 뽑은 횟수.</summary>
        public int pullsSince6;

        /// <summary>마지막 5성(이상) 이후 뽑은 횟수.</summary>
        public int pullsSince5;

        /// <summary>직전 6성 50/50에서 졌으면 true → 다음 6성은 픽업 확정.</summary>
        public bool guaranteedFeatured6;

        /// <summary>5성 픽업 보장 플래그(선택 기능).</summary>
        public bool guaranteedFeatured5;

        /// <summary>누적 뽑기 수.</summary>
        public int totalPulls;
    }
}
