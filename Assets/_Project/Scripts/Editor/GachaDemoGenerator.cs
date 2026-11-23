#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.Gacha;
using SubcultureRPG.Meta;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 가챠 뽑기 데모 생성: 캐릭터 풀 + GachaConfig/Banner + GachaDemo 씬.
    /// Play 하면 뽑기 화면 → 1회/10연 → 결과 연출 → 로스터 반영.
    /// 메뉴: SubcultureRPG > Demo > 가챠 뽑기 데모 생성.
    /// </summary>
    public static class GachaDemoGenerator
    {
        private const string Root = "Assets/_Project/Demo";

        [MenuItem("SubcultureRPG/Demo/가챠 뽑기 데모 생성")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolders();

            // 캐릭터 풀
            var six1 = Char("c_seraphine", "세라핀", Rarity.R6);
            var six2 = Char("c_ark", "아크", Rarity.R6);
            var five = new List<CharacterData> { Char("c_luna", "루나", Rarity.R5), Char("c_rio", "리오", Rarity.R5), Char("c_selene", "셀레네", Rarity.R5) };
            var four = new List<CharacterData> { Char("c_mina", "미나", Rarity.R4), Char("c_coco", "코코", Rarity.R4), Char("c_bella", "벨라", Rarity.R4) };
            var three = new List<CharacterData> { Char("c_riri", "리리", Rarity.R3), Char("c_pin", "핀", Rarity.R3), Char("c_momo", "모모", Rarity.R3) };

            var config = LoadOrCreate<GachaConfig>(Root + "/GachaConfig.asset");
            EditorUtility.SetDirty(config);

            var banner = LoadOrCreate<GachaBanner>(Root + "/GachaBanner.asset");
            banner.bannerName = "픽업 배너 — 세라핀";
            banner.config = config;
            banner.featured6 = six1;
            banner.pool6 = new List<CharacterData> { six2 };
            banner.featured5 = new List<CharacterData>();
            banner.pool5 = five;
            banner.pool4 = four;
            banner.pool3 = three;
            EditorUtility.SetDirty(banner);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var bannerRef = AssetDatabase.LoadAssetAtPath<GachaBanner>(Root + "/GachaBanner.asset");
            var font = FindFont();

            // --- 씬 ---
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var gachaGO = new GameObject("Gacha");
            var controller = gachaGO.AddComponent<GachaController>();
            SetField(controller, "banner", bannerRef);
            EditorUtility.SetDirty(controller);

            var resultGO = new GameObject("GachaResultPanel");
            var panel = resultGO.AddComponent<SubcultureRPG.Gacha.UI.GachaResultPanel>();
            SetField(panel, "controller", controller);
            if (font != null) SetField(panel, "font", font);
            EditorUtility.SetDirty(panel);

            var metaGO = new GameObject("Meta");
            var wallet = metaGO.AddComponent<PlayerWallet>();
            metaGO.AddComponent<PlayerRoster>(); // 가챠 결과 자동 보유(저장)

            var screenGO = new GameObject("GachaScreen");
            var screen = screenGO.AddComponent<GachaScreen>();
            SetField(screen, "gacha", controller);
            SetField(screen, "wallet", wallet);
            if (font != null) SetField(screen, "font", font);
            EditorUtility.SetDirty(screen);

            EditorSceneManager.MarkSceneDirty(scene);
            string scenePath = Root + "/GachaDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = screenGO;

            EditorUtility.DisplayDialog(
                "가챠 뽑기 데모 생성 완료",
                "씬: " + scenePath +
                "\n\n▶ Play → 1회/10연 뽑기 → 결과 연출(카드 공개)." +
                "\n크리스탈 16000 지급(충전 버튼 있음)." +
                "\n뽑은 캐릭터는 로스터(PlayerPrefs)에 저장돼, 연동 데모의 파티 편성에서도 보입니다.",
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
            else Debug.LogError("[GachaDemoGenerator] 필드 못 찾음: " + field);
        }

        /// <summary>게임 폰트(공용 규칙: GameFont).</summary>
        private static TMP_FontAsset FindFont() => GameFont.Find();
    }
}
#endif
