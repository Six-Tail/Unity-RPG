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
using SubcultureRPG.Meta.Shop;
using SubcultureRPG.Meta.Missions;

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

            // 1) 캐릭터 풀 — 6성 픽업(한정) 2 · 6성 상시 5 · 5성 6 · 4성 6 · 3성 6 (속성·역할·대사는 DemoCharacters)
            var pickup1 = Char("c_seraphine", "세라핀", Rarity.R6);
            var pickup2 = Char("c_ciel", "시엘", Rarity.R6);
            var six = new List<CharacterData>
            {
                Char("c_ark", "아크", Rarity.R6), Char("c_aurora", "오로라", Rarity.R6), Char("c_kael", "카엘", Rarity.R6),
                Char("c_violet", "바이올렛", Rarity.R6), Char("c_estelle", "에스텔", Rarity.R6),
            };
            var five = new List<CharacterData>
            {
                Char("c_luna", "루나", Rarity.R5), Char("c_rio", "리오", Rarity.R5), Char("c_selene", "셀레네", Rarity.R5),
                Char("c_haru", "하루", Rarity.R5), Char("c_rena", "레나", Rarity.R5), Char("c_grey", "그레이", Rarity.R5),
            };
            var four = new List<CharacterData>
            {
                Char("c_mina", "미나", Rarity.R4), Char("c_coco", "코코", Rarity.R4), Char("c_bella", "벨라", Rarity.R4),
                Char("c_sora", "소라", Rarity.R4), Char("c_yuna", "유나", Rarity.R4), Char("c_teo", "테오", Rarity.R4),
            };
            var three = new List<CharacterData>
            {
                Char("c_riri", "리리", Rarity.R3), Char("c_pin", "핀", Rarity.R3), Char("c_momo", "모모", Rarity.R3),
                Char("c_dodo", "도도", Rarity.R3), Char("c_ruru", "루루", Rarity.R3), Char("c_kiki", "키키", Rarity.R3),
            };

            // 2) 배너 — 픽업 2개(픽업 실패 시 상시 6성) + 상시 1개. 5성 이하 풀은 공통.
            var config = LoadOrCreate<GachaConfig>(DataRoot + "/GachaConfig.asset");
            EditorUtility.SetDirty(config);
            Banner("GachaBanner", "픽업 배너 — 세라핀", GachaBannerType.Pickup, config, pickup1, six, five, four, three);   // 예전 배너 에셋 재사용(천장 이어받기)
            Banner("GachaBanner_Pickup2", "픽업 배너 — 시엘", GachaBannerType.Pickup, config, pickup2, six, five, four, three);
            Banner("GachaBanner_Standard", "상시 배너 — 별빛 모집", GachaBannerType.Standard, config, null, six, five, four, three);

            // 3) DB — 배너 풀 전체(뽑은 캐릭터가 육성/편성에 모두 보이도록)
            var all = new List<CharacterData> { pickup1, pickup2 };
            all.AddRange(six); all.AddRange(five); all.AddRange(four); all.AddRange(three);
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
            Enemy("종말의 군주", 100, 2200, 125, 95, enemyBasic, 150, new[] { Element.Lightning, Element.Ice });   // 15챕터 보스
            // 금화 던전(단단한 황금 골렘) · 스킬 교본 던전(빠른 마도서)
            Enemy(StageContent.GoldMinion, 100, 900, 100, 70, enemyBasic, 50, new[] { Element.Lightning, Element.Physical });
            Enemy(StageContent.GoldBoss, 80, 2600, 105, 120, enemyBasic, 140, new[] { Element.Lightning, Element.Ice });
            Enemy(StageContent.BookMinion, 125, 700, 112, 40, enemyBasic, 40, new[] { Element.Fire, Element.Wind }, Element.Wind);
            Enemy(StageContent.BookBoss, 105, 1800, 120, 75, enemyBasic, 110, new[] { Element.Fire, Element.Physical }, Element.Lightning);
            // 경험치 던전(느리고 단단한 고목 · 약점 화염·바람)
            Enemy(StageContent.ExpMinion, 100, 850, 104, 55, enemyBasic, 45, new[] { Element.Fire, Element.Ice }, Element.Wind);
            Enemy(StageContent.ExpBoss, 80, 2400, 110, 100, enemyBasic, 130, new[] { Element.Fire, Element.Wind }, Element.Physical);

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
            var scene = SceneSetupUtil.NewSceneWithCamera();

            var gachaGO = new GameObject("Gacha");
            var gachaCtrl = gachaGO.AddComponent<GachaController>();
            SetField(gachaCtrl, "banner", bannerRef);
            var bannerList = new List<GachaBanner> { bannerRef };
            foreach (var n in new[] { "GachaBanner_Pickup2", "GachaBanner_Standard" })
            {
                var b = AssetDatabase.LoadAssetAtPath<GachaBanner>(DataRoot + "/" + n + ".asset");
                if (b != null) bannerList.Add(b);
            }
            SetField(gachaCtrl, "banners", bannerList);

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
            var equipComp = metaGO.AddComponent<PlayerEquipment>();
            SetField(equipComp, "wallet", wallet);
            SetField(equipComp, "inventory", inventory);
            SetField(launcher, "equipment", equipComp);
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
            SetField(runner, "equipment", equipComp);

            var bagGO = new GameObject("BagScreen");
            var bagScr = bagGO.AddComponent<BagScreen>();
            SetField(bagScr, "inventory", inventory);
            SetField(bagScr, "wallet", wallet);
            if (font != null) SetField(bagScr, "font", font);

            var shopComp = metaGO.AddComponent<PlayerShop>();
            SetField(shopComp, "wallet", wallet);
            SetField(shopComp, "inventory", inventory);
            var shopGO = new GameObject("ShopScreen");
            var shopScr = shopGO.AddComponent<ShopScreen>();
            SetField(shopScr, "shop", shopComp);
            SetField(shopScr, "wallet", wallet);
            SetField(shopScr, "inventory", inventory);
            if (font != null) SetField(shopScr, "font", font);

            var missionComp = metaGO.AddComponent<PlayerMissions>();
            SetField(missionComp, "wallet", wallet);
            SetField(missionComp, "inventory", inventory);
            SetField(missionComp, "stageRunner", runner);
            SetField(missionComp, "gacha", gachaCtrl);
            var missionGO = new GameObject("MissionScreen");
            var missionScr = missionGO.AddComponent<MissionScreen>();
            SetField(missionScr, "missions", missionComp);
            SetField(missionScr, "wallet", wallet);
            if (font != null) SetField(missionScr, "font", font);

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
            SetField(nav, "bag", bagScr);
            SetField(nav, "shop", shopScr);
            SetField(nav, "mission", missionScr);
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
                "\n\n▶ Play → 홈 화면에서 스테이지 / 가챠 / 육성 / 임무 / 상점 / 가방으로 이동 (특수 던전은 스테이지 화면 탭)." +
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

        private static GachaBanner Banner(string asset, string title, GachaBannerType type, GachaConfig config,
            CharacterData featured6, List<CharacterData> pool6, List<CharacterData> pool5, List<CharacterData> pool4, List<CharacterData> pool3)
        {
            var b = LoadOrCreate<GachaBanner>(DataRoot + "/" + asset + ".asset");
            b.bannerName = title;
            b.type = type;
            b.config = config;
            b.featured6 = featured6;
            b.featured5 = new List<CharacterData>();
            b.pool6 = new List<CharacterData>(pool6);
            b.pool5 = new List<CharacterData>(pool5);
            b.pool4 = new List<CharacterData>(pool4);
            b.pool3 = new List<CharacterData>(pool3);
            EditorUtility.SetDirty(b);
            return b;
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

        // 돌파 던전 적(속성별 정령 + 수호자) — 이름·약점은 StageContent
        private static void DungeonEnemies(BattleSkill basic)
        {
            foreach (var e in Traits.AllElements)
            {
                int i = (int)e;
                Enemy(StageContent.DungeonMinion[i], 105, 800, 108, 50, basic, 45, StageContent.DungeonWeak[i], e);
                Enemy(StageContent.DungeonGuardian[i], 95, 1600, 116, 80, basic, 110, StageContent.DungeonWeak[i], e);
            }
        }

        /// <summary>메인 스토리 15챕터 × 3난이도 + 돌파 던전 → StageContent.</summary>
        private static void BuildStages() => StageContent.Build();

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
