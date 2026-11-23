#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Meta;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 육성 데모 생성: 샘플 캐릭터 + DB + GrowthDemo 씬.
    /// Play 하면 보유 캐릭터를 골라 레벨업 → 스탯 성장. 레벨은 로스터에 저장(전투에 반영).
    /// 메뉴: SubcultureRPG > Demo > 육성 데모 생성.
    /// </summary>
    public static class GrowthDemoGenerator
    {
        private const string Root = "Assets/_Project/Demo";

        [MenuItem("SubcultureRPG/Demo/육성 데모 생성")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolders();

            var chars = new List<CharacterData>
            {
                Char("c_seraphine", "세라핀", Rarity.R6),
                Char("c_luna", "루나", Rarity.R5),
                Char("c_rio", "리오", Rarity.R5),
                Char("c_mina", "미나", Rarity.R4),
                Char("c_coco", "코코", Rarity.R4),
                Char("c_riri", "리리", Rarity.R3),
            };

            var db = LoadOrCreate<GameDatabase>(Root + "/GameDatabase.asset");
            db.characters = new List<CharacterData>(chars);
            db.units = new List<BattleUnitData>();
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var dbRef = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/GameDatabase.asset");
            var font = FindFont();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var metaGO = new GameObject("Meta");
            var inventory = metaGO.AddComponent<PlayerInventory>();
            var roster = metaGO.AddComponent<PlayerRoster>();
            SetField(roster, "database", dbRef);
            SetField(roster, "grantAllOnStart", true);

            var screenGO = new GameObject("GrowthScreen");
            var screen = screenGO.AddComponent<GrowthScreen>();
            SetField(screen, "roster", roster);
            SetField(screen, "database", dbRef);
            SetField(screen, "inventory", inventory);
            if (font != null) SetField(screen, "font", font);

            EditorUtility.SetDirty(roster);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);

            string scenePath = Root + "/GrowthDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = screenGO;

            EditorUtility.DisplayDialog(
                "육성 데모 생성 완료",
                "씬: " + scenePath +
                "\n\n▶ Play → 캐릭터 선택 → '레벨업'으로 레벨/스탯 성장." +
                "\n보유 캐릭터 " + chars.Count + "종 + 육성 재료 지급." +
                "\n물약/결정=레벨업, 돌파석=돌파(상한), 교본=스킬 레벨업." +
                "\n레벨/스킬은 로스터에 저장돼 전투(연동 데모)에 반영됩니다.",
                "확인");
        }

        private static CharacterData Char(string id, string name, Rarity rarity)
        {
            var c = LoadOrCreate<CharacterData>(Root + "/Char_" + name + ".asset");
            c.id = id;
            c.displayName = name;
            c.rarity = rarity;
            DemoCharacters.ApplyTraits(c);
            EditorUtility.SetDirty(c);
            return c;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/_Project", "Demo");
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
            else Debug.LogError("[GrowthDemoGenerator] 필드 못 찾음: " + field);
        }

        /// <summary>게임 폰트(공용 규칙: GameFont).</summary>
        private static TMP_FontAsset FindFont() => GameFont.Find();
    }
}
#endif
