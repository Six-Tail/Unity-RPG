#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Meta;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 스테이지 데이터 생성: 메인 스토리 15챕터 × 6스테이지 × 3난이도(쉬움/하드/극악)
    /// + 특수 던전(돌파 15 · 금화 5 · 스킬 교본 5).
    /// 적 레벨·보상은 아래 공식으로 계산합니다(튜닝 지점). MainSceneGenerator가 호출.
    /// </summary>
    public static class StageContent
    {
        public const int ChapterCount = 15;
        public const int StagesPerChapter = 6;
        private const string Root = "Assets/_Project/Demo";

        // ---------------- 챕터 구성 ----------------

        private struct Chapter
        {
            public string name, place, boss;
            public string[] mobs;
            public Chapter(string name, string place, string boss, params string[] mobs)
            { this.name = name; this.place = place; this.boss = boss; this.mobs = mobs; }
        }

        private static readonly Chapter[] Chapters =
        {
            new("그림자 숲", "숲", "어둠군주", "그림자병", "그림자궁수"),
            new("얼어붙은 성채", "성채", "빙결거인", "얼음망령", "폭풍정령"),
            new("불타는 협곡", "협곡", "화염 수호자", "불꽃 정령", "그림자병"),
            new("폭풍의 고원", "고원", "질풍 수호자", "바람 정령", "폭풍정령"),
            new("뇌명의 탑", "탑", "뇌광 수호자", "번개 정령", "그림자궁수"),
            new("강철 요새", "요새", "강철 골렘", "강철 병정", "얼음망령"),
            new("서리 심연", "심연", "서리 수호자", "서리 정령", "얼음망령", "바람 정령"),
            new("잿빛 폐허", "폐허", "어둠군주", "그림자병", "불꽃 정령", "그림자궁수"),
            new("하늘 균열", "균열", "질풍 수호자", "바람 정령", "번개 정령", "폭풍정령"),
            new("영겁의 빙원", "빙원", "빙결거인", "서리 정령", "얼음망령", "강철 병정"),
            new("홍염 화산", "화산", "화염 수호자", "불꽃 정령", "강철 병정", "그림자병"),
            new("천둥 왕좌", "왕좌", "뇌광 수호자", "번개 정령", "그림자궁수", "바람 정령"),
            new("무너진 신전", "신전", "강철 골렘", "강철 병정", "그림자병", "서리 정령"),
            new("황혼의 경계", "경계", "어둠군주", "그림자궁수", "바람 정령", "불꽃 정령"),
            new("종말의 제단", "제단", "종말의 군주", "얼음망령", "번개 정령", "강철 병정"),
        };

        // 챕터 1·2는 기존 이름 유지, 3챕터부터는 장소 이름으로 생성
        private static readonly string[][] FixedNames =
        {
            new[] { "숲의 입구", "갈림길", "안개 낀 오솔길", "궁수의 둥지", "그림자 야영지", "어둠군주의 제단" },
            new[] { "설원의 길", "폭풍의 계곡", "성채 외벽", "얼어붙은 회랑", "성채 안뜰", "빙결거인의 옥좌" },
        };
        private static readonly string[] NameTemplate = { "{0} 입구", "{0} 외곽", "{0} 깊은 곳", "{0} 전초기지", "{0} 심장부", "{1}의 거처" };

        // ---------------- 난이도 ----------------

        private struct Diff
        {
            public int levelAdd, turnAdd;
            public float statMul, rewardMul, crystalMul;
            public string folder, idPrefix;
        }

        private static Diff DiffOf(StageDifficulty d) => d switch
        {
            StageDifficulty.Hard => new Diff { levelAdd = 8, turnAdd = 1, statMul = 1.25f, rewardMul = 1.6f, crystalMul = 1.5f, folder = "Hard/", idPrefix = "stage_hard_" },
            StageDifficulty.Hell => new Diff { levelAdd = 16, turnAdd = 2, statMul = 1.6f, rewardMul = 2.5f, crystalMul = 2f, folder = "Hell/", idPrefix = "stage_hell_" },
            _ => new Diff { levelAdd = 0, turnAdd = 0, statMul = 1f, rewardMul = 1f, crystalMul = 1f, folder = "", idPrefix = "stage_" },
        };

        // ---------------- 공식 (튜닝 지점) ----------------

        /// <summary>적 레벨: 쉬움 1-1 Lv.1 → 15-6 Lv.82 (챕터당 +5.4, 스테이지당 +1). 난이도 가산, 최대 100.</summary>
        public static int EnemyLevel(int c, int s, StageDifficulty d) =>
            Mathf.Clamp(Mathf.RoundToInt(1 + (c - 1) * 5.4f + (s - 1)) + DiffOf(d).levelAdd, 1, 100);

        private static float StageScale(int s, bool boss) => (1f + 0.1f * (s - 1)) * (boss ? 1.8f : 1f);

        /// <summary>매 클리어 금화: 챕터 1 1,000 → 챕터 15 9,400 (스테이지당 +10%, 보스 ×1.8).</summary>
        public static int Gold(int c, int s, bool boss, StageDifficulty d) =>
            Round(100, (1000 + 600 * (c - 1)) * StageScale(s, boss) * DiffOf(d).rewardMul);

        /// <summary>매 클리어 경험치 재료 총량: 챕터 1 1,000 → 챕터 15 15,000 EXP.</summary>
        public static int ExpReward(int c, int s, bool boss, StageDifficulty d) =>
            Round(100, (1000 + 1000 * (c - 1)) * StageScale(s, boss) * DiffOf(d).rewardMul);

        /// <summary>출전 캐릭터 경험치.</summary>
        private static int CharExp(int c, int s, bool boss, StageDifficulty d) =>
            Round(10, (200 + 150 * (c - 1)) * (1f + 0.1f * (s - 1)) * (boss ? 1.5f : 1f) * DiffOf(d).rewardMul);

        /// <summary>첫 클리어 크리스탈: 챕터 1~5 60 · 6~10 80 · 11~15 100 (보스 160/240/300).</summary>
        private static int Crystals(int c, bool boss, StageDifficulty d)
        {
            int band = (c - 1) / 5;
            int v = boss ? new[] { 160, 240, 300 }[band] : new[] { 60, 80, 100 }[band];
            return Round(10, v * DiffOf(d).crystalMul);
        }

        /// <summary>경험치 총량을 챕터에 맞는 재료 두 종류로 나눔(초반 물약 → 후반 결정).</summary>
        private static MaterialReward[] ExpMaterials(int exp, int c)
        {
            MaterialType big, small;
            if (c <= 3) { big = MaterialType.ExpPotion; small = MaterialType.ExpSmall; }
            else if (c <= 8) { big = MaterialType.ExpLarge; small = MaterialType.ExpPotion; }
            else { big = MaterialType.ExpCrystal; small = MaterialType.ExpLarge; }
            int bv = Materials.ExpValue(big), sv = Materials.ExpValue(small);
            int nb = exp / bv;
            int ns = Mathf.CeilToInt((exp - nb * bv) / (float)sv);
            var list = new List<MaterialReward>();
            if (nb > 0) list.Add(M(big, nb));
            if (ns > 0) list.Add(M(small, ns));
            return list.ToArray();
        }

        private static int Round(int unit, float v) => Mathf.Max(unit, Mathf.RoundToInt(v / unit) * unit);

        // ---------------- 생성 ----------------

        /// <summary>메인 스테이지 전부 + 던전 생성 후 StageDatabase에 기록.</summary>
        public static void Build()
        {
            EnsureFolder(Root, "Stages");
            EnsureFolder(Root + "/Stages", "Hard");
            EnsureFolder(Root + "/Stages", "Hell");

            var main = new List<StageData>();
            foreach (var d in Difficulties.All)
                for (int c = 1; c <= ChapterCount; c++)
                    for (int s = 1; s <= StagesPerChapter; s++)
                        main.Add(MainStage(c, s, d));

            var db = LoadOrCreate<StageDatabase>(Root + "/StageDatabase.asset");
            db.chapterNames = new List<string>();
            foreach (var ch in Chapters) db.chapterNames.Add(ch.name);
            db.stages = main;
            db.dungeons = BuildDungeons(main);
            db.hardUnlockChapter = 7;
            db.hellUnlockChapter = 15;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        /// <summary>특수 던전만 다시 생성(메인 스테이지 · 씬은 그대로). 단계 수 · 보상표를 바꾼 뒤 실행.</summary>
        [MenuItem("SubcultureRPG/특수 던전 데이터 다시 생성", false, 20)]
        public static void RebuildDungeons()
        {
            var db = AssetDatabase.LoadAssetAtPath<StageDatabase>(Root + "/StageDatabase.asset");
            if (db == null || db.stages == null || db.stages.Count == 0) { Debug.LogWarning("[StageContent] StageDatabase가 없습니다 — 먼저 '메인 씬 생성'을 실행하세요."); return; }
            db.dungeons = BuildDungeons(db.stages);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StageContent] 특수 던전 {db.dungeons.Count}개 생성 완료 (장비 · 금화 · 스킬 교본 · 경험치 {TierUnlockChapters.Length}단계)");
        }

        /// <summary>에디터를 열었을 때 던전 단계 수가 코드와 다르면(예: 5 → 10단계) 한 번 자동으로 다시 생성.</summary>
        [InitializeOnLoadMethod]
        private static void AutoUpgradeDungeons()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var db = AssetDatabase.LoadAssetAtPath<StageDatabase>(Root + "/StageDatabase.asset");
                if (db == null || db.stages == null || db.stages.Count == 0) return;
                bool ok = db.Dungeons(StageKind.GoldDungeon).Count == TierUnlockChapters.Length
                          && db.Dungeons(StageKind.EquipmentDungeon).Count == EquipPairs.Length * EquipTierChapters.Length;
                var eq = db.Dungeons(StageKind.EquipmentDungeon);
                if (ok && eq.Count > 0 && (eq[0].equipmentRarityWeights == null || eq[0].equipmentRarityWeights.Count != EquipTierRarity[0].Length)) ok = false;
                var ex = db.Dungeons(StageKind.ExpDungeon);
                if (ok && ex.Count > 0 && ex[ex.Count - 1].rewards != null && ex[ex.Count - 1].rewards.Count > 0 && ex[ex.Count - 1].rewards[0].count != ExpDungeonReward[ExpDungeonReward.Length - 1][0].n) ok = false;
                if (!ok) RebuildDungeons();
            };
        }

        private static List<StageData> BuildDungeons(List<StageData> main)
        {
            // 특수 던전 (개방 = 쉬움 스테이지 클리어)
            StageData Find(int c, int s) => main.Find(x => x != null && x.difficulty == StageDifficulty.Easy && x.chapter == c && x.number == s);
            var dungeons = new List<StageData>();

            // 돌파 던전: 초급 = 3-6, 중급 = 6-6, 상급 = 10-6
            var ascUnlock = new[] { Find(3, 6), Find(6, 6), Find(10, 6) };
            foreach (var e in Traits.AllElements)
                for (int tier = 1; tier <= Materials.AscensionTiers; tier++)
                    dungeons.Add(Dungeon(e, tier, ascUnlock[tier - 1]));

            // 장비 던전: 세트 2종씩 5곳 × 1~10단계 (단계 개방 = EquipTierChapters장 보스)
            for (int k = 1; k <= 5; k++) AssetDatabase.DeleteAsset($"{Root}/Stages/Dungeon_Equip_{k}.asset");   // 예전 단일 단계 에셋 정리
            for (int i = 0; i < EquipPairs.Length; i++)
                for (int t = 1; t <= EquipTierChapters.Length; t++)
                    dungeons.Add(EquipmentDungeon(i, t, Find(EquipTierChapters[t - 1], StagesPerChapter)));

            // 금화 · 스킬 교본 · 경험치 던전: 1~10단계 (단계 개방 = TierUnlockChapters장 보스)
            for (int t = 1; t <= TierUnlockChapters.Length; t++)
            {
                var after = Find(TierUnlockChapters[t - 1], StagesPerChapter);
                dungeons.Add(ResourceDungeon(StageKind.GoldDungeon, t, after));
                dungeons.Add(ResourceDungeon(StageKind.SkillBookDungeon, t, after));
                dungeons.Add(ResourceDungeon(StageKind.ExpDungeon, t, after));
            }
            return dungeons;
        }

        private static StageData MainStage(int c, int s, StageDifficulty d)
        {
            var diff = DiffOf(d);
            var def = Chapters[c - 1];
            bool boss = s == StagesPerChapter;
            int lv = EnemyLevel(c, s, d);

            var st = LoadOrCreate<StageData>($"{Root}/Stages/{diff.folder}Stage_{c}-{s}.asset");
            st.id = $"{diff.idPrefix}{c}_{s}";      // 쉬움은 기존 세이브 키(stage_1_1) 유지
            st.kind = StageKind.Main;
            st.difficulty = d;
            st.enemyStatMultiplier = diff.statMul;
            st.unlockAfter = null;
            st.chapter = c;
            st.number = s;
            st.stageName = c <= FixedNames.Length ? FixedNames[c - 1][s - 1]
                : string.Format(NameTemplate[s - 1], def.place, def.boss);
            st.description = boss ? $"{def.name}의 지배자, {def.boss}." : "";
            st.isBoss = boss;
            st.recommendedLevel = boss ? lv + 2 : lv;
            st.turnLimit = 6 + (s - 1) + (c - 1) / 3 + (boss ? 3 : 0) + diff.turnAdd;
            st.firstClearCrystals = Crystals(c, boss, d);
            st.gold = Gold(c, s, boss, d);
            st.characterExp = CharExp(c, s, boss, d);
            st.rewards = new List<MaterialReward>(ExpMaterials(ExpReward(c, s, boss, d), c));
            st.enemies = Enemies(def, s, lv);
            EditorUtility.SetDirty(st);
            return st;
        }

        /// <summary>적 구성: 1~2 = 2~3명, 3~4 = 3~4명, 5 = 4명, 6 = 보스(+2레벨) + 호위 2.</summary>
        private static List<StageEnemy> Enemies(Chapter def, int s, int lv)
        {
            string Mob(int i) => def.mobs[i % def.mobs.Length];
            var list = new List<StageEnemy>();
            switch (s)
            {
                case 1: list.Add(E(Mob(0), lv)); list.Add(E(Mob(1), lv)); break;
                case 2: list.Add(E(Mob(0), lv)); list.Add(E(Mob(1), lv)); list.Add(E(Mob(0), lv)); break;
                case 3: list.Add(E(Mob(1), lv)); list.Add(E(Mob(2), lv)); list.Add(E(Mob(0), lv)); break;
                case 4: list.Add(E(Mob(0), lv)); list.Add(E(Mob(1), lv)); list.Add(E(Mob(2), lv)); list.Add(E(Mob(1), lv)); break;
                case 5: list.Add(E(Mob(2), lv)); list.Add(E(Mob(0), lv)); list.Add(E(Mob(1), lv)); list.Add(E(Mob(0), lv)); break;
                default: list.Add(E(Mob(0), lv)); list.Add(E(def.boss, lv + 2)); list.Add(E(Mob(1), lv)); break;
            }
            return list;
        }

        // ---------------- 돌파 던전 ----------------

        // 속성별 정령(잡몹) + 수호자(보스). 약점 = 그 속성을 상대하기 좋은 두 속성
        public static readonly string[] DungeonMinion = { "강철 병정", "불꽃 정령", "서리 정령", "번개 정령", "바람 정령" };
        public static readonly string[] DungeonGuardian = { "강철 골렘", "화염 수호자", "서리 수호자", "뇌광 수호자", "질풍 수호자" };
        public static readonly Element[][] DungeonWeak =
        {
            new[] { Element.Lightning, Element.Fire },    // 물리
            new[] { Element.Ice, Element.Wind },          // 화염
            new[] { Element.Fire, Element.Physical },     // 얼음
            new[] { Element.Wind, Element.Ice },          // 번개
            new[] { Element.Physical, Element.Lightning } // 바람
        };

        /// <summary>돌파 던전 1개(속성 × 등급). 매 클리어: 해당 속성·등급 돌파 재료 + 교본 + 금화.</summary>
        private static StageData Dungeon(Element e, int tier, StageData unlockAfter)
        {
            int i = (int)e, t = tier - 1;
            int lv = new[] { 14, 34, 52 }[t];
            var s = LoadOrCreate<StageData>(Root + "/Stages/Dungeon_" + e + "_" + tier + ".asset");
            s.id = "dungeon_" + e.ToString().ToLowerInvariant() + "_" + tier;
            s.kind = StageKind.AscensionDungeon;
            s.difficulty = StageDifficulty.Easy;
            s.enemyStatMultiplier = 1f;
            s.dungeonElement = e;
            s.dungeonTier = tier;
            s.unlockAfter = unlockAfter;
            s.chapter = 0;
            s.number = tier;
            s.stageName = Traits.Name(e) + " 돌파 던전";
            s.description = "";
            s.isBoss = false;
            s.recommendedLevel = lv + 2;
            s.turnLimit = 8 + 2 * tier;
            s.firstClearCrystals = new[] { 30, 50, 80 }[t];
            s.characterExp = new[] { 400, 1500, 3500 }[t];
            s.gold = new[] { 3000, 8000, 15000 }[t];
            s.enemies = new List<StageEnemy> { E(DungeonMinion[i], lv), E(DungeonGuardian[i], lv + 2), E(DungeonMinion[i], lv) };
            s.rewards = new List<MaterialReward> { M(Materials.Ascension(e, tier), new[] { 3, 3, 4 }[t]) };   // 교본은 스킬 교본 던전으로
            EditorUtility.SetDirty(s);
            return s;
        }

        // ---------------- 장비 던전 ----------------

        /// <summary>장비 던전별 드롭 세트(2종)와 개방 챕터.</summary>
        public static readonly string[][] EquipPairs =
        {
            new[] { "assault", "guardian" },
            new[] { "ember", "frost" },
            new[] { "thunder", "gale" },
            new[] { "steel", "breaker" },
            new[] { "spring", "swift" },
        };
        /// <summary>장비 던전 단계별 개방 챕터(쉬움 보스 클리어).</summary>
        public static readonly int[] EquipTierChapters = { 2, 3, 4, 6, 7, 9, 10, 12, 13, 15 };

        /// <summary>
        /// 단계별 드롭 성급 가중치 [1성, 2성, 3성, 4성, 5성, 6성].
        /// 낮은 단계는 1 · 2성 위주(처음 시작한 지휘관이 바로 끼고 스테이지를 밀 수 있게), 6단계부터 5성, 8단계부터 6성.
        /// </summary>
        public static readonly int[][] EquipTierRarity =
        {
            new[] { 50, 35, 15, 0, 0, 0 },
            new[] { 30, 40, 25, 5, 0, 0 },
            new[] { 10, 35, 40, 15, 0, 0 },
            new[] { 0, 20, 50, 30, 0, 0 },
            new[] { 0, 5, 45, 50, 0, 0 },
            new[] { 0, 0, 30, 55, 15, 0 },
            new[] { 0, 0, 15, 50, 35, 0 },
            new[] { 0, 0, 0, 45, 45, 10 },
            new[] { 0, 0, 0, 35, 45, 20 },
            new[] { 0, 0, 0, 20, 50, 30 },
        };
        private static readonly Element[] EquipDungeonElement = { Element.Physical, Element.Fire, Element.Lightning, Element.Wind, Element.Ice };

        private static StageData EquipmentDungeon(int i, int tier, StageData unlockAfter)
        {
            int t = tier - 1;
            int lv = EnemyLevel(EquipTierChapters[t], StagesPerChapter, StageDifficulty.Easy) + 2;
            var el = EquipDungeonElement[i];
            var s = LoadOrCreate<StageData>($"{Root}/Stages/Dungeon_Equip_{i + 1}_{tier}.asset");
            s.id = $"dungeon_equip_{i + 1}_{tier}";
            s.kind = StageKind.EquipmentDungeon;
            s.difficulty = StageDifficulty.Easy;
            s.enemyStatMultiplier = 1f;
            s.dungeonElement = el;
            s.dungeonTier = tier;
            s.unlockAfter = unlockAfter;
            s.chapter = i + 1;      // 던전(세트 묶음) 번호 — 목록 정렬용
            s.number = tier;
            s.stageName = $"{EquipSets.Name(EquipPairs[i][0])} · {EquipSets.Name(EquipPairs[i][1])}";
            s.description = "";
            s.isBoss = false;
            s.recommendedLevel = lv;
            s.turnLimit = 9 + (tier + 1) / 2;
            s.firstClearCrystals = new[] { 20, 25, 30, 35, 40, 45, 50, 55, 60, 80 }[t];
            s.characterExp = TierCharExp[t];
            s.gold = new[] { 2000, 2700, 3500, 4500, 5500, 6700, 8000, 9500, 11000, 13500 }[t];
            s.equipmentSets = new List<string>(EquipPairs[i]);
            s.equipmentDrops = 2;
            s.equipmentRarityWeights = new List<int>(EquipTierRarity[t]);
            s.enemies = new List<StageEnemy>
            {
                E(DungeonMinion[(int)el], lv - 2), E(DungeonGuardian[(int)el], lv), E(DungeonMinion[(int)el], lv - 2),
            };
            s.rewards = new List<MaterialReward> { M(MaterialType.EquipOre, new[] { 3, 4, 5, 6, 7, 8, 10, 12, 14, 18 }[t]) };
            EditorUtility.SetDirty(s);
            return s;
        }

        // ---------------- 금화 · 스킬 교본 던전 ----------------

        /// <summary>금화/교본 던전 단계별 개방 챕터(쉬움 보스 클리어).</summary>
        public static readonly int[] TierUnlockChapters = { 1, 2, 4, 5, 7, 8, 10, 11, 13, 15 };

        public const string GoldMinion = "황금 정령", GoldBoss = "황금 골렘";
        public const string BookMinion = "떠도는 마도서", BookBoss = "금서의 파수꾼";
        public const string ExpMinion = "경험의 정령", ExpBoss = "지혜의 고목";

        private static readonly int[] GoldReward = { 12000, 20000, 30000, 42000, 55000, 70000, 85000, 102000, 120000, 150000 };
        // 스킬 교본 던전: 단계가 오를수록 높은 등급 교본 (전설의 비전서는 이벤트·상점 전용)
        private static readonly (MaterialType type, int n)[][] BookReward =
        {
            new[] { (MaterialType.SkillBook, 4) },
            new[] { (MaterialType.SkillBook, 4), (MaterialType.SkillBookBlue, 1) },
            new[] { (MaterialType.SkillBook, 3), (MaterialType.SkillBookBlue, 2) },
            new[] { (MaterialType.SkillBookBlue, 3) },
            new[] { (MaterialType.SkillBookBlue, 3), (MaterialType.SkillBookPurple, 1) },
            new[] { (MaterialType.SkillBookBlue, 3), (MaterialType.SkillBookPurple, 2) },
            new[] { (MaterialType.SkillBookBlue, 2), (MaterialType.SkillBookPurple, 3) },
            new[] { (MaterialType.SkillBookBlue, 2), (MaterialType.SkillBookPurple, 4) },
            new[] { (MaterialType.SkillBookBlue, 2), (MaterialType.SkillBookPurple, 5) },
            new[] { (MaterialType.SkillBookBlue, 3), (MaterialType.SkillBookPurple, 7) },
        };

        // 경험치 던전: 같은 시점(개방 챕터) 메인 스테이지의 행동력당 경험치 × 약 2배 — 메인은 금화 · 크리스탈도 주므로 경험치만은 던전이 확실히 낫게
        // 총 EXP 5,000 / 10,000 / 24,000 / 32,000 / 48,000 / 58,000 / 78,000 / 90,000 / 115,000 / 140,000
        private static readonly (MaterialType type, int n)[][] ExpDungeonReward =
        {
            new[] { (MaterialType.ExpSmall, 20), (MaterialType.ExpPotion, 6) },
            new[] { (MaterialType.ExpPotion, 10), (MaterialType.ExpLarge, 5) },
            new[] { (MaterialType.ExpLarge, 14), (MaterialType.ExpCrystal, 2) },
            new[] { (MaterialType.ExpLarge, 12), (MaterialType.ExpCrystal, 4) },
            new[] { (MaterialType.ExpLarge, 13), (MaterialType.ExpCrystal, 7) },
            new[] { (MaterialType.ExpLarge, 8), (MaterialType.ExpCrystal, 10) },
            new[] { (MaterialType.ExpLarge, 8), (MaterialType.ExpCrystal, 14) },
            new[] { (MaterialType.ExpCrystal, 18) },
            new[] { (MaterialType.ExpCrystal, 23) },
            new[] { (MaterialType.ExpCrystal, 28) },
        };
        private static readonly int[] ExpGold = { 1000, 1500, 2000, 2500, 3000, 3700, 4500, 5200, 6000, 8000 };
        private static readonly int[] BookGold = { 2000, 3000, 4000, 5000, 6000, 7500, 9000, 10500, 12000, 15000 };
        private static readonly int[] TierCharExp = { 500, 800, 1200, 1800, 2500, 3200, 4000, 5000, 6000, 7500 };
        private static readonly int[] TierCrystals = { 30, 30, 40, 40, 50, 50, 60, 60, 80, 100 };

        /// <summary>금화/교본 던전 1단계분. 적 레벨 = 개방 챕터 보스 레벨 수준.</summary>
        private static StageData ResourceDungeon(StageKind kind, int tier, StageData unlockAfter)
        {
            int t = tier - 1;
            bool gold = kind == StageKind.GoldDungeon, exp = kind == StageKind.ExpDungeon;
            int lv = EnemyLevel(TierUnlockChapters[t], StagesPerChapter, StageDifficulty.Easy) + 2;
            string tag = gold ? "Gold" : exp ? "Exp" : "Book";

            var s = LoadOrCreate<StageData>($"{Root}/Stages/Dungeon_{tag}_{tier}.asset");
            s.id = $"dungeon_{tag.ToLowerInvariant()}_{tier}";
            s.kind = kind;
            s.difficulty = StageDifficulty.Easy;
            s.enemyStatMultiplier = 1f;
            s.dungeonElement = Element.Physical;
            s.dungeonTier = tier;
            s.unlockAfter = unlockAfter;
            s.chapter = 0;
            s.number = tier;
            s.stageName = SpecialDungeons.Name(kind);
            s.description = "";
            s.isBoss = false;
            s.recommendedLevel = lv;
            s.turnLimit = 8 + (tier + 1) / 2;
            s.firstClearCrystals = TierCrystals[t];
            s.characterExp = TierCharExp[t];
            s.gold = gold ? GoldReward[t] : exp ? ExpGold[t] : BookGold[t];
            string minion = gold ? GoldMinion : exp ? ExpMinion : BookMinion;
            string boss = gold ? GoldBoss : exp ? ExpBoss : BookBoss;
            s.enemies = new List<StageEnemy> { E(minion, lv - 2), E(boss, lv), E(minion, lv - 2) };
            s.rewards = new List<MaterialReward>();
            if (!gold)
                foreach (var (type, n) in (exp ? ExpDungeonReward : BookReward)[t]) s.rewards.Add(M(type, n));
            EditorUtility.SetDirty(s);
            return s;
        }

        // ---------------- 헬퍼 ----------------

        private static StageEnemy E(string name, int level)
        {
            var unit = AssetDatabase.LoadAssetAtPath<BattleUnitData>(Root + "/Unit_메인_" + name + ".asset");
            if (unit == null) Debug.LogError("[StageContent] 적 유닛 없음: " + name);
            return new StageEnemy { unit = unit, level = level };
        }

        private static MaterialReward M(MaterialType t, int n) => new MaterialReward { type = t, count = n };

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
