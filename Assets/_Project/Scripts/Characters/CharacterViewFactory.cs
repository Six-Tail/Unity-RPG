using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 성급을 보고 알맞은 ICharacterView를 생성해 주는 팩토리.
    /// 전투/가챠 쪽은 이 팩토리만 호출하면 되고, 정적/Live2D 분기를 몰라도 됩니다.
    ///
    /// 사용 예)
    ///   var view = CharacterViewFactory.Create(characterData, battleSlotTransform);
    ///   view.PlayMotion("attack");
    /// </summary>
    public static class CharacterViewFactory
    {
        public static ICharacterView Create(CharacterData data, Transform parent)
        {
            var go = new GameObject($"CharacterView_{data.id}");
            go.transform.SetParent(parent, false);

            ICharacterView view;
            if (data.rarity.UsesLive2D())
            {
                view = go.AddComponent<Live2DCharacterView>();
            }
            else
            {
                go.AddComponent<SpriteRenderer>();
                view = go.AddComponent<StaticCharacterView>();
            }

            view.Bind(data);
            return view;
        }
    }
}
