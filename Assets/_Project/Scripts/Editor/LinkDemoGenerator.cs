#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 가챠↔전투 연동 데모 생성: 샘플 캐릭터 + 데이터베이스 + 적 + LinkDemo 씬.
    /// Play 하면 파티 편성 화면 → 캐릭터 선택 → 그 캐릭터로 전투.
    /// 메뉴: SubcultureRPG > Demo > 가챠↔전투 연동 데모 생성.
    /// </summary>
    public static class LinkDemoGenerator
    {
        private const string Root = "Assets/_Project/Demo";

        [MenuItem("SubcultureRPG/Demo/가챠↔전투 연동 데모 생성")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolders();

            // 1) 에셋 생성 (참조는 SaveAssets 시점에 정상 직렬화됨)
            var chars = new List<CharacterData>
            {
                Character("c_seraphine", "세라핀", Rarity.R6),
                Character("c_luna", "루나", Rarity.R5),
                Character("c_rio", "리오", Rarity.R5),
                Character("c_mina", "미나", Rarity.R4),
                Character("c_coco", "코코", Rarity.R4),
                Character("c_riri", "리리", Rarity.R3),
            };

            var enemyBasic = Skill("연동_적_기본공격", SkillCategory.Basic, TargetType.SingleEnemy, 1.0f, 20);
            string[] enemyNames = { "그림자병", "그림자궁수", "어둠군주" };
            Enemy("그림자병", 95, 900, 100, 55, enemyBasic, 50, new[] { Element.Fire, Element.Physical });
            Enemy("그림자궁수", 110, 750, 115, 45, enemyBasic, 40, new[] { Element.Ice, Element.Wind });
            Enemy("어둠군주", 90, 1500, 110, 70, enemyBasic, 100, new[] { Element.Lightning, Element.Fire });

            var db = LoadOrCreate<GameDatabase>(Root + "/GameDatabase.asset");
            db.characters = new List<CharacterData>(chars);
            db.units = new List<BattleUnitData>(); // 비움 → 성급 기반 자동 생성
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 2) 씬에 넣을 참조는 경로로 재로드
            var dbRef = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/GameDatabase.asset");
            var enemyRefs = new List<BattleUnitData>();
            foreach (var n in enemyNames)
            {
                var u = AssetDatabase.LoadAssetAtPath<BattleUnitData>(Root + "/Unit_연동_" + n + ".asset");
                if (u != null) enemyRefs.Add(u);
            }
            var font = FindFont();

            // 3) 씬 구성 (private [SerializeField]는 리플렉션으로 주입)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var battleGO = new GameObject("Battle");
            var controller = battleGO.AddComponent<BattleController>();
            SetField(controller, "startOnPlay", false); // 전투는 런처가 시작

            var hudGO = new GameObject("BattleHUD");
            var hud = hudGO.AddComponent<BattleHUD>();
            SetField(hud, "controller", controller);
            if (font != null) SetField(hud, "font", font);

            var metaGO = new GameObject("Meta");
            var roster = metaGO.AddComponent<PlayerRoster>();
            SetField(roster, "database", dbRef);
            SetField(roster, "grantAllOnStart", true); // 테스트용 전원 지급
            var launcher = metaGO.AddComponent<BattleLauncher>();
            SetField(launcher, "controller", controller);
            SetField(launcher, "database", dbRef);
            SetField(launcher, "roster", roster);
            SetField(launcher, "enemyTeam", enemyRefs);

            var partyGO = new GameObject("PartyScreen");
            var party = partyGO.AddComponent<PartyScreen>();
            SetField(party, "roster", roster);
            SetField(party, "database", dbRef);
            SetField(party, "launcher", launcher);
            if (font != null) SetField(party, "font", font);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(roster);
            EditorUtility.SetDirty(launcher);
            EditorUtility.SetDirty(party);
            EditorSceneManager.MarkSceneDirty(scene);

            string scenePath = Root + "/LinkDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = partyGO;

            EditorUtility.DisplayDialog(
                "가챠↔전투 연동 데모 생성 완료",
                "씬: " + scenePath +
                "\n\n▶ Play → 파티 편성 화면에서 캐릭터를 골라 '전투 시작'." +
                "\n(테스트용으로 보유 캐릭터 " + chars.Count + "종을 자동 지급)" +
                "\n실전에서는 PlayerRoster가 가챠 결과를 받아 자동 보유 처리합니다.",
                "확인");
        }

        // ---------------- helpers ----------------

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/_Project", "Demo");
        }

        private static CharacterData Character(string id, string name, Rarity rarity)
        {
            var c = LoadOrCreate<CharacterData>(Root + "/Char_" + name + ".asset");
            c.id = id;
            c.displayName = name;
            c.rarity = rarity;
            EditorUtility.SetDirty(c);
            return c;
        }

        private static void Enemy(string name, int spd, int hp, int atk, int def, BattleSkill basic, int toughness, Element[] weaknesses)
        {
            var u = LoadOrCreate<BattleUnitData>(Root + "/Unit_연동_" + name + ".asset");
            u.character = null;
            u.displayName = name;
            u.level = 1;
            u.element = Element.Physical;
            u.maxToughness = toughness;
            u.weaknesses = weaknesses != null ? new List<Element>(weaknesses) : new List<Element>();
            u.baseStats = new BattleStats { maxHP = hp, atk = atk, def = def, spd = spd, critRate = 0.05f, critDmg = 0.5f };
            u.skills = new List<BattleSkill> { basic };
            EditorUtility.SetDirty(u);
        }

        private static BattleSkill Skill(string name, SkillCategory cat, TargetType target, float power, int energyGain)
        {
            var s = LoadOrCreate<BattleSkill>(Root + "/Skill_" + name + ".asset");
            s.skillName = name;
            s.category = cat;
            s.target = target;
            s.power = power;
            s.hits = 1;
            s.energyGain = energyGain;
            EditorUtility.SetDirty(s);
            return s;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        private static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
            else Debug.LogError("[LinkDemoGenerator] 필드 못 찾음: " + field);
        }

        /// <summary>게임 폰트(공용 규칙: GameFont).</summary>
        private static TMP_FontAsset FindFont() => GameFont.Find();
    }
}
#endif
