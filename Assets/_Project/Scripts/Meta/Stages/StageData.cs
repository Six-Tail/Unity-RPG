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
        AscensionDungeon,  // 특수 던전: 돌파 던전(속성 × 등급) — 속성 돌파 재료
        GoldDungeon,       // 특수 던전: 금화 던전(단계) — 금화
        SkillBookDungeon,  // 특수 던전: 스킬 교본 던전(단계) — 스킬 교본
        EquipmentDungeon,  // 특수 던전: 장비 던전(세트 2종) — 장비 + 장비 강화석
        ExpDungeon,        // 특수 던전: 경험치 던전(단계) — 경험치 재료
        // 이후 장비 던전 등은 뒤에 추가(세이브/에셋은 정수값으로 저장)
    }

    /// <summary>특수 던전 종류별 표시 정보.</summary>
    public static class SpecialDungeons
    {
        public static readonly StageKind[] All =
            { StageKind.EquipmentDungeon, StageKind.AscensionDungeon, StageKind.ExpDungeon, StageKind.GoldDungeon, StageKind.SkillBookDungeon };

        public static string Name(StageKind k) => k switch
        {
            StageKind.AscensionDungeon => "돌파 던전",
            StageKind.GoldDungeon => "금화 던전",
            StageKind.SkillBookDungeon => "스킬 교본 던전",
            StageKind.EquipmentDungeon => "장비 던전",
            StageKind.ExpDungeon => "경험치 던전",
            _ => "메인 스토리",
        };

        public static Color ColorOf(StageKind k) => k switch
        {
            StageKind.AscensionDungeon => new Color(0.58f, 0.38f, 0.88f),
            StageKind.GoldDungeon => new Color(0.95f, 0.74f, 0.25f),
            StageKind.SkillBookDungeon => new Color(0.38f, 0.58f, 0.95f),
            StageKind.EquipmentDungeon => new Color(0.3f, 0.72f, 0.68f),
            StageKind.ExpDungeon => new Color(0.45f, 0.8f, 0.4f),
            _ => Color.white,
        };

        public static string Hex(StageKind k) => "#" + ColorUtility.ToHtmlStringRGB(ColorOf(k));

        /// <summary>던전 종류 설명(상세 패널).</summary>
        public static string Summary(StageKind k) => k switch
        {
            StageKind.AscensionDungeon => "캐릭터 속성의 돌파 재료 · 초급 40 / 중급 60 / 상급 80 돌파",
            StageKind.GoldDungeon => "레벨업 · 돌파 · 스킬 · 합성에 필요한 금화를 대량으로 획득",
            StageKind.SkillBookDungeon => "스킬 레벨업에 필요한 스킬 교본 획득",
            StageKind.EquipmentDungeon => "세트 2종 중 무작위 장비 + 장비 강화석",
            StageKind.ExpDungeon => "레벨업에 필요한 경험치 재료 대량 획득",
            _ => "",
        };
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
        [Tooltip("돌파 던전 등급 1~3 = 초급(40 돌파) / 중급(60) / 상급(80). 금화·교본 던전은 단계(1~).")]
        [Range(1, 10)] public int dungeonTier = 1;
        [Tooltip("장비 던전: 드롭하는 장비 세트 id(EquipSets).")]
        public List<string> equipmentSets = new();
        [Tooltip("장비 던전: 클리어마다 얻는 장비 개수.")]
        [Min(0)] public int equipmentDrops = 0;
        [Tooltip("장비 던전: 드롭 성급 가중치 [1성 ~ 6성] (옛 데이터 4칸 = [3성 ~ 6성]).")]
        public List<int> equipmentRarityWeights = new();

        /// <summary>장비 던전에서 나올 수 있는 최고 성급(가중치 > 0).</summary>
        public int MaxEquipRarity
        {
            get
            {
                int max = 0;
                if (equipmentRarityWeights != null)
                    for (int i = 0; i < equipmentRarityWeights.Count; i++)
                        if (equipmentRarityWeights[i] > 0) max = EquipRules.WeightBase(equipmentRarityWeights) + i;
                return max;
            }
        }
        [Tooltip("이 스테이지를 클리어하면 개방(특수 던전용). 비우면 항상 개방.")]
        public StageData unlockAfter;

        [Header("적")]
        public List<StageEnemy> enemies = new();
        [Min(1)] public int recommendedLevel = 1;

        [Header("별 조건")]
        [Tooltip("★3 기준값(옛 '아군 행동 수' 단위). 실제 조건은 StarRoundLimit(턴).")]
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

        /// <summary>특수 던전(돌파·금화·교본 등) 여부.</summary>
        public bool IsDungeon => kind != StageKind.Main;

        /// <summary>
        /// 메인 스테이지 ★3 여유 턴. 처음 하는 사람이 다시 깨는 번거로움이 없도록 앞 장일수록 넉넉하고
        /// 뒤로 갈수록 줄어 기본 기준에 가까워짐: 1 ~ 2장 +2 · 3 ~ 5장 +1 · 6장부터 0.
        /// </summary>
        public static int MainRoundBonus(int chapter) => chapter <= 2 ? 2 : chapter <= 5 ? 1 : 0;

        /// <summary>
        /// ★3 조건: 이 턴 수 이내 클리어. 턴 = 살아 있는 모든 아군 · 적이 한 번씩 행동하면 1턴.
        /// 기본 = (저작 값 turnLimit(옛 '아군 행동 수' 기준) + 2) ÷ 4 올림 → 일반 스테이지 2 ~ 4턴, 장 보스 4 ~ 5턴. 여기에 메인 스테이지 장별 여유를 더함.
        /// </summary>
        public int StarRoundLimit => Mathf.CeilToInt((turnLimit + 2) / 4f) + (IsDungeon ? 0 : MainRoundBonus(chapter));
        public bool IsAscensionDungeon => kind == StageKind.AscensionDungeon;

        /// <summary>표시용 번호(예: 1-3, 던전은 '초급' 등).</summary>
        public string Code => IsAscensionDungeon ? Materials.TierName(dungeonTier)
            : IsDungeon ? $"{dungeonTier}단계"
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
