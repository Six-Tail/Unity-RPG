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
    /// 메인(홈 허브) 씬 생성: 가챠·육성·편성·전투를 한 씬에 배치하고 ScreenNavigator로 연결.
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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 5) 씬에 넣을 참조는 경로로 재로드
            var bannerRef = AssetDatabase.LoadAssetAtPath<GachaBanner>(DataRoot + "/GachaBanner.asset");
            var dbRef = AssetDatabase.LoadAssetAtPath<GameDatabase>(DataRoot + "/MainDatabase.asset");
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

            if (bannerRef == null || dbRef == null || enemies.Count == 0)
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
                "\n\n▶ Play → 홈 화면에서 가챠 / 육성 / 편성·전투로 이동." +
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

        private static void Enemy(string name, int spd, int hp, int atk, int def, BattleSkill basic, int toughness, Element[] weaknesses)
        {
            var u = LoadOrCreate<BattleUnitData>(DataRoot + "/Unit_메인_" + name + ".asset");
            u.character = null;
            u.displayName = name;
            u.level = 1;
            u.element = Element.Physical;
            u.maxToughness = toughness;
            u.weaknesses = new List<Element>(weaknesses);
            u.baseStats = new BattleStats { maxHP = hp, atk = atk, def = def, spd = spd, critRate = 0.05f, critDmg = 0.5f };
            u.skills = new List<BattleSkill> { basic };
            EditorUtility.SetDirty(u);
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
