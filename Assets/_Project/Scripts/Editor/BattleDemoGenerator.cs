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

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 전투 데모(샘플 스킬/유닛 + 씬 배치)를 생성합니다.
    /// 속성/약점·브레이크(내구도)·버프·힐·도트까지 보여주는 구성.
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

            // --- 스킬 (속성 / 회복 / 효과) ---
            Skill("기본공격", SkillCategory.Basic, Element.Fire, TargetType.SingleEnemy, 1.0f, 10, 20);
            Skill("강타", SkillCategory.Skill, Element.Fire, TargetType.SingleEnemy, 1.8f, 20, 30);
            Skill("빙결참", SkillCategory.Skill, Element.Ice, TargetType.SingleEnemy, 1.6f, 20, 30);
            Skill("필살기", SkillCategory.Ultimate, Element.Lightning, TargetType.AllEnemies, 3.5f, 20, 0);

            var heal = Skill("치유", SkillCategory.Skill, Element.Physical, TargetType.SingleAlly, 0f, 0, 30);
            heal.heal = 2.2f; EditorUtility.SetDirty(heal);

            var buff = Skill("공격강화", SkillCategory.Skill, Element.Physical, TargetType.AllAllies, 0f, 0, 30);
            buff.applyEffect = StatusType.AtkUp; buff.effectMagnitude = 0.4f; buff.effectDuration = 2; EditorUtility.SetDirty(buff);

            var poison = Skill("맹독", SkillCategory.Skill, Element.Wind, TargetType.SingleEnemy, 1.0f, 10, 30);
            poison.applyEffect = StatusType.Dot; poison.effectMagnitude = 0.6f; poison.effectDuration = 3; EditorUtility.SetDirty(poison);

            // --- 아군 (속성 + 역할) ---
            Unit("아군_세라핀", Element.Fire, 110, 1200, 135, 60, 0.08f, 0, null, "기본공격", "강타", "필살기");
            Unit("아군_루나", Element.Ice, 100, 1000, 115, 50, 0.05f, 0, null, "기본공격", "빙결참", "맹독", "필살기");
            Unit("아군_리오", Element.Physical, 120, 950, 110, 45, 0.08f, 0, null, "기본공격", "치유", "공격강화", "필살기"); // 서포터

            // --- 적 (약점 + 내구도) ---
            Unit("적_그림자병", Element.Physical, 95, 1000, 100, 55, 0.05f, 60, new[] { Element.Fire }, "기본공격");
            Unit("적_그림자궁수", Element.Wind, 115, 800, 115, 40, 0.05f, 40, new[] { Element.Ice }, "기본공격", "강타");
            Unit("적_어둠군주", Element.Lightning, 90, 1800, 110, 70, 0.05f, 120, new[] { Element.Lightning, Element.Fire }, "기본공격", "필살기");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var allies = LoadUnits("아군_세라핀", "아군_루나", "아군_리오");
            var enemies = LoadUnits("적_그림자병", "적_그림자궁수", "적_어둠군주");

            if (allies.Count == 0 || enemies.Count == 0)
            {
                EditorUtility.DisplayDialog("생성 실패", "유닛 에셋 로드 실패. 콘솔 확인.", "확인");
                return;
            }

            // --- 씬 구성 ---
            var scene = SceneSetupUtil.NewSceneWithCamera();

            var controllerGO = new GameObject("Battle");
            var controller = controllerGO.AddComponent<BattleController>();
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
                "\n▶ Play → 약점 적중(분홍)·BREAK·치유(초록)·독(보라)·버프(+n) 확인" +
                "\n(관전만 보려면 Battle 오브젝트의 autoBattle 체크)",
                "확인");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/_Project", "Demo");
        }

        private static BattleSkill Skill(string name, SkillCategory cat, Element element, TargetType target, float power, int breakDamage, int energyGain)
        {
            var s = LoadOrCreate<BattleSkill>(Root + "/Skill_" + name + ".asset");
            s.skillName = name;
            s.category = cat;
            s.element = element;
            s.target = target;
            s.power = power;
            s.hits = 1;
            s.breakDamage = breakDamage;
            s.energyGain = energyGain;
            // 재생성 대비 기본값 리셋(이후 호출측에서 heal/effect만 덮어씀)
            s.heal = 0f;
            s.applyEffect = StatusType.None;
            s.effectMagnitude = 0.3f;
            s.effectDuration = 2;
            s.effectChance = 1f;
            EditorUtility.SetDirty(s);
            return s;
        }

        private static void Unit(string name, Element element, int spd, int hp, int atk, int def, float crit,
            int toughness, Element[] weaknesses, params string[] skillNames)
        {
            var u = LoadOrCreate<BattleUnitData>(Root + "/Unit_" + name + ".asset");
            u.character = null;
            u.displayName = name;
            u.level = 1;
            u.element = element;
            u.maxToughness = toughness;
            u.weaknesses = weaknesses != null ? new List<Element>(weaknesses) : new List<Element>();
            u.baseStats = new BattleStats { maxHP = hp, atk = atk, def = def, spd = spd, critRate = crit, critDmg = 0.6f };

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

        private static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
            else Debug.LogError("[BattleDemoGenerator] 필드 못 찾음: " + field);
        }

        /// <summary>게임 폰트(공용 규칙: GameFont).</summary>
        private static TMP_FontAsset FindFont() => GameFont.Find();
    }
}
#endif
