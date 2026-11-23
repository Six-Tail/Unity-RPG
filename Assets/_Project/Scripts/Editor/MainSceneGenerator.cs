#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.Gacha;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 메인(홈 허브) 씬 생성: 스테이지·가챠·육성·편성·전투를 한 씬에 배치하고 ScreenNavigator로 연결.
    /// 빌드 설정의 첫 씬으로 등록합니다.
    /// 메뉴: SubcultureRPG > 메인 씬 생성 (홈 허브).
    /// </summary>
    public static class MainSceneGenerator
    {
        private const string DataRoot = "Assets/_Project/Demo";
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string ScenePath = SceneDir + "/Main.unity";

        [MenuItem("SubcultureRPG/메인 씬 생성 (홈 허브)", false, 0)]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets", "_Project");
            EnsureFolder("Assets/_Project", "Demo");
            EnsureFolder("Assets/_Project", "Scenes");

            // 1) 캐릭터 풀(가챠 배너와 동일)
            var six1 = Char("c_seraphine", "세라핀", Rarity.R6);
            var six2 = Char("c_ark", "아크", Rarity.R6);
            var five = new List<CharacterData> { Char("c_luna", "루나", Rarity.R5), Char("c_rio", "리오", Rarity.R5), Char("c_selene", "셀레네", Rarity.R5) };
            var four = new List<CharacterData> { Char("c_mina", "미나", Rarity.R4), Char("c_coco", "코코", Rarity.R4), Char("c_bella", "벨라", Rarity.R4) };
            var three = new List<CharacterData> { Char("c_riri", "리리", Rarity.R3), Char("c_pin", "핀", Rarity.R3), Char("c_momo", "모모", Rarity.R3) };

            // 2) 배너
            var config = LoadOrCreate<GachaConfig>(DataRoot + "/GachaConfig.asset");
            EditorUtility.SetDirty(config);
            var banner = LoadOrCreate<GachaBanner>(DataRoot + "/GachaBanner.asset");
            banner.bannerName = "픽업 배너 — 세라핀";
            banner.config = config;
            banner.featured6 = six1;
            banner.pool6 = new List<CharacterData> { six2 };
            banner.featured5 = new List<CharacterData>();
            banner.pool5 = five;
            banner.pool4 = four;
            banner.pool3 = three;
            EditorUtility.SetDirty(banner);

            // 3) DB — 배너 풀 전체(뽑은 캐릭터가 육성/편성에 모두 보이도록)
            var all = new List<CharacterData> { six1, six2 };
            all.AddRange(five); all.AddRange(four); all.AddRange(three);
            var db = LoadOrCreate<GameDatabase>(DataRoot + "/MainDatabase.asset");
            db.characters = all;
            db.units = new List<BattleUnitData>();
            EditorUtility.SetDirty(db);

            // 4) 적
            var enemyBasic = Skill("메인_적_기본공격", SkillCategory.Basic, TargetType.SingleEnemy, 1.0f, 20);
            string[] enemyNames = { "그림자병", "그림자궁수", "어둠군주" };
            Enemy("그림자병", 95, 900, 100, 55, enemyBasic, 50, new[] { Element.Fire, Element.Physical });
            Enemy("그림자궁수", 110, 750, 115, 45, enemyBasic, 40, new[] { Element.Ice, Element.Wind });
            Enemy("어둠군주", 90, 1500, 110, 70, enemyBasic, 100, new[] { Element.Lightning, Element.Fire });
            Enemy("얼음망령", 105, 820, 112, 50, enemyBasic, 45, new[] { Element.Fire, Element.Lightning }, Element.Ice);
            Enemy("폭풍정령", 120, 700, 120, 40, enemyBasic, 40, new[] { Element.Physical, Element.Ice }, Element.Wind);
            Enemy("빙결거인", 85, 1800, 118, 85, enemyBasic, 120, new[] { Element.Fire, Element.Wind }, Element.Ice);
            DungeonEnemies(enemyBasic);

            // 4-2) 스테이지 (챕터 1 그림자 숲 · 챕터 2 얼어붙은 성채)
            AssetDatabase.SaveAssets();
            BuildStages();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 5) 씬에 넣을 참조는 경로로 재로드
            var bannerRef = AssetDatabase.LoadAssetAtPath<GachaBanner>(DataRoot + "/GachaBanner.asset");
            var dbRef = AssetDatabase.LoadAssetAtPath<GameDatabase>(DataRoot + "/MainDatabase.asset");
            var stageDbRef = AssetDatabase.LoadAssetAtPath<StageDatabase>(DataRoot + "/StageDatabase.asset");
            var enemies = new List<BattleUnitData>();
            foreach (var n in enemyNames)
            {
                var u = AssetDatabase.LoadAssetAtPath<BattleUnitData>(DataRoot + "/Unit_메인_" + n + ".asset");
                if (u != null) enemies.Add(u);
            }
            var starters = new List<CharacterData>();
            foreach (var n in new[] { "리리", "미나", "코코" })
            {
                var c = AssetDatabase.LoadAssetAtPath<CharacterData>(DataRoot + "/Char_" + n + ".asset");
                if (c != null) starters.Add(c);
            }
            var font = FindFont();

            if (bannerRef == null || dbRef == null || enemies.Count == 0 || stageDbRef == null)
            {
                EditorUtility.DisplayDialog("생성 실패", "배너/DB/적 에셋 로드에 실패했습니다. 콘솔을 확인하세요.", "확인");
                return;
            }

            // 6) 씬 구성
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var gachaGO = new GameObject("Gacha");
            var gachaCtrl = gachaGO.AddComponent<GachaController>();
            SetField(gachaCtrl, "banner", bannerRef);

            var resultGO = new GameObject("GachaResultPanel");
            var result = resultGO.AddComponent<SubcultureRPG.Gacha.UI.GachaResultPanel>();
            SetField(result, "controller", gachaCtrl);
            if (font != null) SetField(result, "font", font);

            var battleGO = new GameObject("Battle");
            var battle = battleGO.AddComponent<BattleController>();
            SetField(battle, "startOnPlay", false);

            var hudGO = new GameObject("BattleHUD");
            var hud = hudGO.AddComponent<BattleHUD>();
            SetField(hud, "controller", battle);
            if (font != null) SetField(hud, "font", font);

            var metaGO = new GameObject("Meta");
            var wallet = metaGO.AddComponent<PlayerWallet>();
            var inventory = metaGO.AddComponent<PlayerInventory>();
            var roster = metaGO.AddComponent<PlayerRoster>();
            SetField(roster, "gacha", gachaCtrl);
            SetField(roster, "database", dbRef);
            SetField(roster, "starterCharacters", starters);
            var launcher = metaGO.AddComponent<BattleLauncher>();
            SetField(launcher, "controller", battle);
            SetField(launcher, "database", dbRef);
            SetField(launcher, "roster", roster);
            SetField(launcher, "enemyTeam", enemies);
            var stageProgress = metaGO.AddComponent<StageProgress>();
            var runner = metaGO.AddComponent<StageRunner>();
            SetField(runner, "controller", battle);
            SetField(runner, "launcher", launcher);
            SetField(runner, "hud", hud);
            SetField(runner, "progress", stageProgress);
            SetField(runner, "wallet", wallet);
            SetField(runner, "inventory", inventory);
            SetField(runner, "roster", roster);
            SetField(runner, "database", stageDbRef);

            var stageScrGO = new GameObject("StageScreen");
            var stageScr = stageScrGO.AddComponent<StageScreen>();
            SetField(stageScr, "database", stageDbRef);
            SetField(stageScr, "progress", stageProgress);
            if (font != null) SetField(stageScr, "font", font);

            var homeGO = new GameObject("HomeScreen");
            var home = homeGO.AddComponent<HomeScreen>();
            SetField(home, "wallet", wallet);
            SetField(home, "inventory", inventory);
            SetField(home, "roster", roster);
            SetField(home, "gacha", gachaCtrl);
            if (font != null) SetField(home, "font", font);

            var gachaScrGO = new GameObject("GachaScreen");
            var gachaScr = gachaScrGO.AddComponent<GachaScreen>();
            SetField(gachaScr, "gacha", gachaCtrl);
            SetField(gachaScr, "wallet", wallet);
            if (font != null) SetField(gachaScr, "font", font);

            var growthGO = new GameObject("GrowthScreen");
            var growth = growthGO.AddComponent<GrowthScreen>();
            SetField(growth, "roster", roster);
            SetField(growth, "database", dbRef);
            SetField(growth, "inventory", inventory);
            if (font != null) SetField(growth, "font", font);

            var partyGO = new GameObject("PartyScreen");
            var party = partyGO.AddComponent<PartyScreen>();
            SetField(party, "roster", roster);
            SetField(party, "database", dbRef);
            SetField(party, "launcher", launcher);
            SetField(party, "showOnStart", false);
            if (font != null) SetField(party, "font", font);

            var navGO = new GameObject("Navigator");
            var nav = navGO.AddComponent<ScreenNavigator>();
            SetField(nav, "home", home);
            SetField(nav, "stage", stageScr);
            SetField(nav, "stageRunner", runner);
            SetField(nav, "gacha", gachaScr);
            SetField(nav, "growth", growth);
            SetField(nav, "party", party);
            SetField(nav, "battleHud", hud);
            SetField(nav, "battle", battle);
            SetField(home, "navigator", nav);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettingsFirst(ScenePath);
            Selection.activeGameObject = navGO;

            EditorUtility.DisplayDialog(
                "메인 씬 생성 완료",
                "씬: " + ScenePath + " (빌드 첫 씬으로 등록)" +
                "\n\n▶ Play → 홈 화면에서 스테이지 / 가챠 / 육성 / 편성으로 이동." +
                "\n스테이지: 챕터 1~2 (각 6스테이지), 승리 시 별 평가 + 보상." +
                "\n새 세이브면 스타터(리리·미나·코코)가 지급됩니다." +
                "\n각 화면 좌상단 '← 홈'으로 복귀, 전투는 '나가기/홈으로'.",
                "확인");
        }

        // ---------------- helpers ----------------

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void AddToBuildSettingsFirst(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static CharacterData Char(string id, string name, Rarity rarity)
        {
            var c = LoadOrCreate<CharacterData>(DataRoot + "/Char_" + name + ".asset");
            c.id = id;
            c.displayName = name;
            c.rarity = rarity;
            DemoCharacters.ApplyTraits(c);
            EditorUtility.SetDirty(c);
            return c;
        }

        private static BattleSkill Skill(string name, SkillCategory cat, TargetType target, float power, int energyGain)
        {
            var s = LoadOrCreate<BattleSkill>(DataRoot + "/Skill_" + name + ".asset");
            s.skillName = name;
            s.category = cat;
            s.target = target;
            s.power = power;
            s.hits = 1;
            s.energyGain = energyGain;
            EditorUtility.SetDirty(s);
            return s;
        }

        private static void Enemy(string name, int spd, int hp, int atk, int def, BattleSkill basic, int toughness, Element[] weaknesses,
            Element element = Element.Physical)
        {
            var u = LoadOrCreate<BattleUnitData>(DataRoot + "/Unit_메인_" + name + ".asset");
            u.character = null;
            u.displayName = name;
            u.level = 1;
            u.element = element;
            u.maxToughness = toughness;
            u.weaknesses = new List<Element>(weaknesses);
            // 레벨당 성장 +7% (스테이지에서 레벨만 바꿔 재사용)
            u.baseStats = new BattleStats
            {
                maxHP = hp, atk = atk, def = def, spd = spd, critRate = 0.05f, critDmg = 0.5f,
                hpGrowth = Mathf.RoundToInt(hp * 0.07f),
                atkGrowth = Mathf.RoundToInt(atk * 0.07f),
                defGrowth = Mathf.RoundToInt(def * 0.07f),
            };
            u.skills = new List<BattleSkill> { basic };
            EditorUtility.SetDirty(u);
        }

        // ---------------- 스테이지 ----------------

        private static BattleUnitData EnemyRef(string name) =>
            AssetDatabase.LoadAssetAtPath<BattleUnitData>(DataRoot + "/Unit_메인_" + name + ".asset");

        private static StageEnemy E(string name, int level) => new StageEnemy { unit = EnemyRef(name), level = level };

        private static MaterialReward M(MaterialType t, int n) => new MaterialReward { type = t, count = n };

        private static StageData Stage(int ch, int no, string title, string desc, bool boss, int recLv, int turnLimit,
            int crystals, int exp, int gold, StageEnemy[] enemies, MaterialReward[] rewards)
        {
            var s = LoadOrCreate<StageData>(DataRoot + "/Stages/Stage_" + ch + "-" + no + ".asset");
            s.id = "stage_" + ch + "_" + no;
            s.kind = StageKind.Main;
            s.unlockAfter = null;
            s.gold = gold;
            s.chapter = ch;
            s.number = no;
            s.stageName = title;
            s.description = desc;
            s.isBoss = boss;
            s.recommendedLevel = recLv;
            s.turnLimit = turnLimit;
            s.firstClearCrystals = crystals;
            s.characterExp = exp;
            s.enemies = new List<StageEnemy>(enemies);
            s.rewards = new List<MaterialReward>(rewards);
            EditorUtility.SetDirty(s);
            return s;
        }

        // 돌파 던전 적: 속성별 정령(잡몹) + 수호자(보스). 약점 = 그 속성을 상대하기 좋은 두 속성
        private static readonly string[] DungeonMinion = { "강철 병정", "불꽃 정령", "서리 정령", "번개 정령", "바람 정령" };
        private static readonly string[] DungeonGuardian = { "강철 골렘", "화염 수호자", "서리 수호자", "뇌광 수호자", "질풍 수호자" };
        private static readonly Element[][] DungeonWeak =
        {
            new[] { Element.Lightning, Element.Fire },   // 물리
            new[] { Element.Ice, Element.Wind },         // 화염
            new[] { Element.Fire, Element.Physical },    // 얼음
            new[] { Element.Wind, Element.Ice },         // 번개
            new[] { Element.Physical, Element.Lightning } // 바람
        };

        private static void DungeonEnemies(BattleSkill basic)
        {
            foreach (var e in Traits.AllElements)
            {
                int i = (int)e;
                Enemy(DungeonMinion[i], 105, 800, 108, 50, basic, 45, DungeonWeak[i], e);
                Enemy(DungeonGuardian[i], 95, 1600, 116, 80, basic, 110, DungeonWeak[i], e);
            }
        }

        /// <summary>돌파 던전 1개(속성 × 등급). 매 클리어: 해당 속성·등급 돌파 재료 + 교본 + 금화.</summary>
        private static StageData Dungeon(Element e, int tier, StageData unlockAfter)
        {
            int i = (int)e, t = tier - 1;
            int lv = new[] { 14, 30, 48 }[t];
            var s = LoadOrCreate<StageData>(DataRoot + "/Stages/Dungeon_" + e + "_" + tier + ".asset");
            s.id = "dungeon_" + e.ToString().ToLowerInvariant() + "_" + tier;
            s.kind = StageKind.AscensionDungeon;
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
            s.characterExp = new[] { 400, 1200, 2500 }[t];
            s.gold = new[] { 3000, 6000, 10000 }[t];
            s.enemies = new List<StageEnemy> { E(DungeonMinion[i], lv), E(DungeonGuardian[i], lv + 2), E(DungeonMinion[i], lv) };
            s.rewards = new List<MaterialReward>
            {
                M(Materials.Ascension(e, tier), new[] { 3, 3, 4 }[t]),
                M(MaterialType.SkillBook, tier),
            };
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>데모 스테이지 2챕터 × 6. 보상: 첫 클리어 크리스탈 + 매 클리어 재료·경험치.</summary>
        private static void BuildStages()
        {
            EnsureFolder(DataRoot, "Stages");
            // 경험치 재료: S +100 · P +500 · L +1000 · C +5000
            const MaterialType S = MaterialType.ExpSmall, P = MaterialType.ExpPotion,
                L = MaterialType.ExpLarge, C = MaterialType.ExpCrystal;

            var list = new List<StageData>
            {
                // 챕터 1 — 그림자 숲 (보상: 경험치 재료 + 금화)
                Stage(1, 1, "숲의 입구", "그림자병이 길을 막고 있다.", false, 1, 6, 60, 200, 1200,
                    new[] { E("그림자병", 1), E("그림자병", 1) }, new[] { M(S, 5), M(P, 2) }),
                Stage(1, 2, "갈림길", "궁수가 나무 위에서 노린다.", false, 3, 7, 60, 300, 1500,
                    new[] { E("그림자병", 3), E("그림자궁수", 3) }, new[] { M(S, 6), M(P, 3) }),
                Stage(1, 3, "안개 낀 오솔길", "", false, 5, 9, 60, 400, 1800,
                    new[] { E("그림자병", 5), E("그림자궁수", 5), E("그림자병", 5) }, new[] { M(P, 4), M(L, 1) }),
                Stage(1, 4, "궁수의 둥지", "", false, 7, 9, 60, 500, 2100,
                    new[] { E("그림자궁수", 7), E("그림자궁수", 7), E("그림자병", 7) }, new[] { M(P, 4), M(L, 2) }),
                Stage(1, 5, "그림자 야영지", "", false, 9, 11, 60, 600, 2400,
                    new[] { E("그림자병", 9), E("그림자궁수", 9), E("그림자병", 9), E("그림자궁수", 9) }, new[] { M(P, 4), M(L, 3) }),
                Stage(1, 6, "어둠군주의 제단", "숲을 뒤덮은 그림자의 근원.", true, 12, 14, 160, 800, 4000,
                    new[] { E("그림자병", 11), E("어둠군주", 13), E("그림자궁수", 11) }, new[] { M(L, 3), M(C, 1) }),

                // 챕터 2 — 얼어붙은 성채
                Stage(2, 1, "설원의 길", "차가운 바람 속에 망령이 떠돈다.", false, 15, 8, 80, 900, 3000,
                    new[] { E("얼음망령", 15), E("얼음망령", 15) }, new[] { M(L, 4), M(C, 1) }),
                Stage(2, 2, "폭풍의 계곡", "", false, 17, 9, 80, 1000, 3300,
                    new[] { E("폭풍정령", 17), E("얼음망령", 17), E("폭풍정령", 17) }, new[] { M(L, 5), M(C, 1) }),
                Stage(2, 3, "성채 외벽", "", false, 19, 10, 80, 1100, 3600,
                    new[] { E("그림자병", 19), E("얼음망령", 19), E("그림자궁수", 19) }, new[] { M(L, 3), M(C, 2) }),
                Stage(2, 4, "얼어붙은 회랑", "", false, 21, 11, 80, 1200, 3900,
                    new[] { E("얼음망령", 21), E("폭풍정령", 21), E("얼음망령", 21), E("폭풍정령", 21) }, new[] { M(L, 4), M(C, 2) }),
                Stage(2, 5, "성채 안뜰", "", false, 23, 12, 80, 1300, 4200,
                    new[] { E("어둠군주", 22), E("폭풍정령", 23), E("얼음망령", 23) }, new[] { M(L, 5), M(C, 2) }),
                Stage(2, 6, "빙결거인의 옥좌", "성채를 얼린 거인이 깨어났다.", true, 26, 15, 240, 1600, 7000,
                    new[] { E("폭풍정령", 25), E("빙결거인", 27), E("얼음망령", 25) }, new[] { M(L, 5), M(C, 4) }),
            };

            // 돌파 던전: 5속성 × 3등급. 초급 = 1-3 클리어, 중급 = 2-3 클리어, 상급 = 2-6 클리어 시 개방
            var unlockBy = new[] { list[2], list[8], list[11] };
            var dungeons = new List<StageData>();
            foreach (var e in Traits.AllElements)
                for (int tier = 1; tier <= Materials.AscensionTiers; tier++)
                    dungeons.Add(Dungeon(e, tier, unlockBy[tier - 1]));

            var db = LoadOrCreate<StageDatabase>(DataRoot + "/StageDatabase.asset");
            db.chapterNames = new List<string> { "그림자 숲", "얼어붙은 성채" };
            db.stages = list;
            db.dungeons = dungeons;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        /// <summary>private [SerializeField]를 리플렉션으로 설정 + dirty 표시.</summary>
        private static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
            else Debug.LogError("[MainSceneGenerator] 필드 못 찾음: " + target.GetType().Name + "." + field);
            if (target is UnityEngine.Object o) EditorUtility.SetDirty(o);
        }

        /// <summary>게임 폰트(공용 규칙: GameFont).</summary>
        private static TMP_FontAsset FindFont() => GameFont.Find();
    }
}
#endif
