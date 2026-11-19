using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Gacha
{
    /// <summary>
    /// 하나의 뽑기 배너(픽업 + 등급별 풀).
    /// 메뉴: Create > SubcultureRPG > Gacha > Banner.
    /// </summary>
    [CreateAssetMenu(fileName = "Banner", menuName = "SubcultureRPG/Gacha/Banner")]
    public class GachaBanner : ScriptableObject
    {
        public string bannerName = "신규 배너";
        public GachaConfig config;

        [Header("픽업(rate-up)")]
        [Tooltip("이번 배너 픽업 6성(1명).")]
        public CharacterData featured6;
        [Tooltip("픽업 5성(여럿 가능).")]
        public List<CharacterData> featured5 = new();

        [Header("등급별 풀 (비픽업)")]
        public List<CharacterData> pool6 = new();
        public List<CharacterData> pool5 = new();
        public List<CharacterData> pool4 = new();
        public List<CharacterData> pool3 = new();
    }
}
