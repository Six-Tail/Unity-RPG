using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 캐릭터 1명의 기본 전투 스탯(Lv.1 기준 + 레벨당 성장).
    /// BattleUnitData 안에서 저작하고, 전투 시작 때 BattleUnit으로 해석됩니다.
    /// </summary>
    [System.Serializable]
    public class BattleStats
    {
        [Header("기본치 (Lv.1)")]
        public int maxHP = 1000;
        public int atk = 100;
        public int def = 50;
        [Tooltip("행동 순서(속도). 높을수록 자주 행동.")]
        public int spd = 100;

        [Header("치명타")]
        [Range(0f, 1f)] public float critRate = 0.05f;
        [Tooltip("치명 시 추가 피해 배율 (0.5 = +50%).")]
        public float critDmg = 0.5f;

        [Header("레벨당 성장 (선택, 0이면 고정)")]
        public int hpGrowth = 0;
        public int atkGrowth = 0;
        public int defGrowth = 0;

        public int MaxHPAt(int level) => maxHP + hpGrowth * Mathf.Max(0, level - 1);
        public int AtkAt(int level) => atk + atkGrowth * Mathf.Max(0, level - 1);
        public int DefAt(int level) => def + defGrowth * Mathf.Max(0, level - 1);
    }
}
