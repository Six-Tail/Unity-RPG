#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 전투 데모(샘플 스킬/유닛 + 씬 배치)를 생성합니다.
    /// 에셋은 Assets/_Project/Demo/ 에 저장되고, BattleDemo 씬이 열립니다.
    /// 메뉴: SubcultureRPG > Demo > 전투 데모 생성.
    /// </summary>
    public static class BattleDemoGenerator
    {
        private const string Root = "Assets/_Project/Demo";

        [MenuItem("SubcultureRPG/Demo/전투 데모 생성")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();

            // --- 스킬 에셋 ---
            Skill("기본공격", SkillCategory.Basic, TargetType.SingleEnemy, 1.0f, 1, 20);
            Skill("강타", SkillCategory.Skill, TargetType.SingleEnemy, 1.8f, 1, 30);
            Skill("전체베기", SkillCategory.Skill, TargetType.AllEnemies, 1.1f, 1, 30);
            Skill("필살기", SkillCategory.Ultimate, TargetType.AllEnemies, 3.5f, 1, 0);

            // --- 유닛 에셋 (이름, SPD, HP, ATK, DEF, 치명률, 스킬이름…) ---
            Unit("아군_세라핀", 110, 1200, 135, 60, 0.08f, "기본공격", "강타", "필살기");
            Unit("아군_루나", 100, 1000, 115, 50, 0.05f, "기본공격", "전체베기", "필살기");
            Unit("아군_리오", 125, 850, 125, 45, 0.10f, "기본공격", "강타", "필살기");
            Unit("적_그림자병", 95, 900, 100, 55, 0.05f, "기본공격");
            Unit("적_그림자궁수", 115, 700, 115, 40, 0.05f, "기본공격", "강타");
            Unit("적_어둠군주", 90, 1600, 110, 70, 0.05f, "기본공격", "필살기");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 씬 참조는 경로로 다시 로드한 에셋 사용
            var allies = LoadUnits("아군_세라핀", "아군_루나", "아군_리오");
            var enemies = LoadUnits("적_그림자병", "적_그림자궁수", "적_어둠군주");

            if (allies.Count == 0 || enemies.Count == 0)
            {
                EditorUtility.DisplayDialog("생성 실패", "유닛 에셋 로드 실패. 콘솔 확인.", "확인");
                return;
            }

            // --- 씬 구성 ---
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var controllerGO = new GameObject("Battle");
            var controller = controllerGO.AddComponent<BattleController>();
            // 리플렉션으로 private [SerializeField] List 직접 할당 (인스펙터 드래그와 동일하게 직렬화됨)
            SetField(controller, "allyTeam", allies);
            SetField(controller, "enemyTeam", enemies);
            EditorUtility.SetDirty(controller);

            var hudGO = new GameObject("BattleHUD");
            var hud = hudGO.AddComponent<BattleHUD>();
            SetField(hud, "controller", controller);
            var font = FindFont();
            if (font != null) SetField(hud, "font", font);
            EditorUtility.SetDirty(hud);

            EditorSceneManager.MarkSceneDirty(scene);
            string scenePath = Root + "/BattleDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            Selection.activeGameObject = controllerGO;
            EditorUtility.DisplayDialog(
                "전투 데모 생성 완료",
                "씬이 열렸습니다:\n" + scenePath +
                "\n\n아군 " + allies.Count + " vs 적 " + enemies.Count +
                "\n▶ Play 를 누르면 전투가 시작됩니다." +
                "\n(관전만 보려면 Battle 오브젝트의 autoBattle 를 체크)",
                "확인");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/_Project", "Demo");
        }

        private static void Skill(string name, SkillCategory cat, TargetType target, float power, int hits, int energyGain)
        {
            var s = LoadOrCreate<BattleSkill>(Root + "/Skill_" + name + ".asset");
            s.skillName = name;
            s.category = cat;
            s.target = target;
            s.power = power;
            s.hits = hits;
            s.energyGain = energyGain;
            EditorUtility.SetDirty(s);
        }

        private static void Unit(string name, int spd, int hp, int atk, int def, float crit, params string[] skillNames)
        {
            var u = LoadOrCreate<BattleUnitData>(Root + "/Unit_" + name + ".asset");
            u.character = null;
            u.displayName = name;
            u.level = 1;
            u.baseStats = new BattleStats
            {
                maxHP = hp, atk = atk, def = def, spd = spd,
                critRate = crit, critDmg = 0.6f
            };

            var skills = new List<BattleSkill>();
            foreach (var sn in skillNames)
            {
                var sk = AssetDatabase.LoadAssetAtPath<BattleSkill>(Root + "/Skill_" + sn + ".asset");
                if (sk != null) skills.Add(sk);
            }
            u.skills = skills;
            EditorUtility.SetDirty(u);
        }

        private static List<BattleUnitData> LoadUnits(params string[] names)
        {
            var list = new List<BattleUnitData>();
            foreach (var n in names)
            {
                var u = AssetDatabase.LoadAssetAtPath<BattleUnitData>(Root + "/Unit_" + n + ".asset");
                if (u != null) list.Add(u);
                else Debug.LogError("[BattleDemoGenerator] 유닛 로드 실패: " + n);
            }
            return list;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        /// <summary>private [SerializeField] 필드를 리플렉션으로 설정.</summary>
        private static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
            else Debug.LogError("[BattleDemoGenerator] 필드 못 찾음: " + field);
        }

        private static TMP_FontAsset FindFont()
        {
            foreach (var guid in AssetDatabase.FindAssets("NanumGothic t:TMP_FontAsset"))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            return null;
        }
    }
}
#endif
