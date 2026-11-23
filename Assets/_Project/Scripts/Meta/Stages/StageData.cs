using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Battle;
using SubcultureRPG.Characters;

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

    /// <summary>스테이지 종류.</summary>
    public enum StageKind
    {
        Main,              // 메인 스토리(챕터 진행) — 경험치 재료·금화
        AscensionDungeon,  // 돌파 던전(속성 × 등급) — 속성 돌파 재료, 반복 파밍
    }

    /// <summary>메인 스토리 난이도. 하드 = 쉬움 7챕터 클리어, 극악 = 쉬움 15챕터 클리어 시 개방(StageDatabase에서 변경).</summary>
    public enum StageDifficulty
    {
        Easy,
        Hard,
        Hell,
    }

    public static class Difficulties
    {
        public static readonly StageDifficulty[] All = { StageDifficulty.Easy, StageDifficulty.Hard, StageDifficulty.Hell };

        public static string Name(StageDifficulty d) => d switch
        {
            StageDifficulty.Hard => "하드",
            StageDifficulty.Hell => "극악",
            _ => "쉬움",
        };

        public static Color ColorOf(StageDifficulty d) => d switch
        {
            StageDifficulty.Hard => new Color(0.88f, 0.48f, 0.20f),
            StageDifficulty.Hell => new Color(0.78f, 0.18f, 0.34f),
            _ => new Color(0.32f, 0.66f, 0.44f),
        };

        public static string Hex(StageDifficulty d) => "#" + ColorUtility.ToHtmlStringRGB(ColorOf(d));
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

        [Header("난이도 (메인 스토리)")]
        public StageDifficulty difficulty = StageDifficulty.Easy;
        [Tooltip("적 HP·ATK·DEF 배율(난이도 보정). 1 = 그대로.")]
        [Min(0.1f)] public float enemyStatMultiplier = 1f;

        [Header("종류 (돌파 던전)")]
        public StageKind kind = StageKind.Main;
        [Tooltip("돌파 던전: 드롭하는 돌파 재료의 속성.")] public Element dungeonElement = Element.Physical;
        [Tooltip("돌파 던전 등급 1~3 = 초급(40 돌파) / 중급(60) / 상급(80).")]
        [Range(1, 3)] public int dungeonTier = 1;
        [Tooltip("이 스테이지를 클리어하면 개방(돌파 던전용). 비우면 항상 개방.")]
        public StageData unlockAfter;

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
        [Tooltip("클리어할 때마다 받는 금화.")]
        [Min(0)] public int gold = 1000;

        public bool IsDungeon => kind == StageKind.AscensionDungeon;

        /// <summary>표시용 번호(예: 1-3, 던전은 '초급' 등).</summary>
        public string Code => IsDungeon ? Materials.TierName(dungeonTier)
            : (difficulty == StageDifficulty.Easy ? "" : Difficulties.Name(difficulty) + " ") + $"{chapter}-{number}";

        /// <summary>돌파 던전이 주는 대표 재료.</summary>
        public MaterialType DungeonMaterial => Materials.Ascension(dungeonElement, dungeonTier);
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
