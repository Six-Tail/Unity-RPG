#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using SubcultureRPG.Characters;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 샘플 캐릭터 특성표(속성·역할). 메인/데모 생성기가 모두 공유합니다.
    /// 캐릭터를 추가·조정할 땐 여기만 고치고 생성기를 다시 실행하세요.
    /// </summary>
    public static class DemoCharacters
    {
        private static readonly Dictionary<string, (Element element, Role role)> Table = new()
        {
            // 6성
            { "c_seraphine", (Element.Fire, Role.Dealer) },
            { "c_ark", (Element.Lightning, Role.Tank) },
            // 5성
            { "c_luna", (Element.Ice, Role.Dealer) },
            { "c_rio", (Element.Wind, Role.Support) },
            { "c_selene", (Element.Ice, Role.Healer) },
            // 4성
            { "c_mina", (Element.Fire, Role.Dealer) },
            { "c_coco", (Element.Physical, Role.Tank) },
            { "c_bella", (Element.Lightning, Role.Support) },
            // 3성
            { "c_riri", (Element.Wind, Role.Healer) },
            { "c_pin", (Element.Physical, Role.Dealer) },
            { "c_momo", (Element.Fire, Role.Tank) },
        };

        /// <summary>프로젝트의 모든 CharacterData에 특성표 적용(씬 재생성 없이).</summary>
        [MenuItem("SubcultureRPG/데모 캐릭터 특성 적용 (속성·역할)", false, 10)]
        public static void ApplyToAllAssets()
        {
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:CharacterData"))
            {
                var c = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g));
                if (c == null || !Table.ContainsKey(c.id ?? "")) continue;
                ApplyTraits(c);
                EditorUtility.SetDirty(c);
                n++;
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("특성 적용", $"캐릭터 {n}명에 속성·역할을 적용했습니다.", "확인");
        }

        /// <summary>id에 해당하는 속성·역할을 캐릭터에 적용(표에 없으면 그대로).</summary>
        public static void ApplyTraits(CharacterData c)
        {
            if (c == null || string.IsNullOrEmpty(c.id)) return;
            if (Table.TryGetValue(c.id, out var t))
            {
                c.element = t.element;
                c.role = t.role;
            }
        }
    }
}
#endif
