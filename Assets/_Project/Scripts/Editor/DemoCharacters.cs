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
            // 6성 픽업(한정)
            { "c_seraphine", (Element.Fire, Role.Dealer) },
            { "c_ciel", (Element.Ice, Role.Dealer) },
            // 6성 상시
            { "c_ark", (Element.Lightning, Role.Tank) },
            { "c_aurora", (Element.Ice, Role.Support) },
            { "c_kael", (Element.Wind, Role.Dealer) },
            { "c_violet", (Element.Physical, Role.Dealer) },
            { "c_estelle", (Element.Fire, Role.Healer) },
            // 5성
            { "c_luna", (Element.Ice, Role.Dealer) },
            { "c_rio", (Element.Wind, Role.Support) },
            { "c_selene", (Element.Ice, Role.Healer) },
            { "c_haru", (Element.Fire, Role.Support) },
            { "c_rena", (Element.Lightning, Role.Dealer) },
            { "c_grey", (Element.Physical, Role.Tank) },
            // 4성
            { "c_mina", (Element.Fire, Role.Dealer) },
            { "c_coco", (Element.Physical, Role.Tank) },
            { "c_bella", (Element.Lightning, Role.Support) },
            { "c_sora", (Element.Ice, Role.Tank) },
            { "c_yuna", (Element.Wind, Role.Dealer) },
            { "c_teo", (Element.Lightning, Role.Healer) },
            // 3성
            { "c_riri", (Element.Wind, Role.Healer) },
            { "c_pin", (Element.Physical, Role.Dealer) },
            { "c_momo", (Element.Fire, Role.Tank) },
            { "c_dodo", (Element.Ice, Role.Dealer) },
            { "c_ruru", (Element.Lightning, Role.Support) },
            { "c_kiki", (Element.Wind, Role.Tank) },
        };

        /// <summary>6성 대표 대사(획득 연출). 표에 없으면 역할별 기본 대사.</summary>
        private static readonly Dictionary<string, string> Lines = new()
        {
            { "c_seraphine", "타오르는 건 두렵지 않아. 꺼지는 게 두려울 뿐." },
            { "c_ciel", "얼어붙은 하늘 아래, 내 칼날만은 멈추지 않아." },
            { "c_ark", "방패는 부서져도, 나는 물러서지 않는다." },
            { "c_aurora", "밤이 길수록 빛은 더 선명해지는 법이야." },
            { "c_kael", "바람보다 빠르게. 그게 내 유일한 약속이야." },
            { "c_violet", "말은 필요 없어. 결과로 보여 줄게." },
            { "c_estelle", "상처는 내가 태울게. 너는 앞만 봐." },
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
            if (Lines.TryGetValue(c.id, out var line) && string.IsNullOrEmpty(c.signatureLine))
                c.signatureLine = line;
        }
    }
}
#endif
