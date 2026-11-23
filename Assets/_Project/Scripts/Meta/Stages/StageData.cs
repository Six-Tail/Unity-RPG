using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta
{
    /// <summary>스테이지 적 1명(유닛 + 레벨). 같은 유닛 에셋을 레벨만 바꿔 재사용.</summary>
    [Serializable]
    public class StageEnemy
    {
        public BattleUnitData unit;
        [Min(1)] public int level = 1;
    }

    /// <summary>재료 보상 1종.</summary>
    [Serializable]
    public class MaterialReward
    {
        public MaterialType type = MaterialType.ExpPotion;
        [Min(1)] public int count = 1;
    }

    /// <summary>
    /// 스테이지 1개. 메뉴: Create > SubcultureRPG > Stage.
    /// 별 평가: ★1 클리어 · ★2 아군 전원 생존 · ★3 아군 행동 turnLimit회 이내 클리어.
    /// 보상: 첫 클리어 크리스탈(1회) + 재료·캐릭터 경험치(클리어마다).
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_", menuName = "SubcultureRPG/Stage")]
    public class StageData : ScriptableObject
    {
        [Tooltip("저장 키. 비우면 에셋 이름.")]
        public string id;
        [Min(1)] public int chapter = 1;
        [Min(1)] public int number = 1;
        public string stageName = "스테이지";
        [TextArea] public string description;
        public bool isBoss;

        [Header("적")]
        public List<StageEnemy> enemies = new();
        [Min(1)] public int recommendedLevel = 1;

        [Header("별 조건")]
        [Tooltip("★3 조건: 아군 행동 횟수가 이 값 이하로 클리어.")]
        [Min(1)] public int turnLimit = 8;

        [Header("보상")]
        [Tooltip("첫 클리어 1회 크리스탈.")]
        [Min(0)] public int firstClearCrystals = 60;
        [Tooltip("클리어할 때마다 받는 재료.")]
        public List<MaterialReward> rewards = new();
        [Tooltip("클리어할 때마다 출전 캐릭터가 받는 경험치.")]
        [Min(0)] public int characterExp = 200;

        /// <summary>표시용 번호(예: 1-3).</summary>
        public string Code => $"{chapter}-{number}";
        public string Key => !string.IsNullOrEmpty(id) ? id : name;

        /// <summary>적 중 최고 레벨.</summary>
        public int MaxEnemyLevel
        {
            get
            {
                int m = 1;
                if (enemies != null) foreach (var e in enemies) if (e != null && e.level > m) m = e.level;
                return m;
            }
        }
    }
}
