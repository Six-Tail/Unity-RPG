using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 캐릭터·전투유닛 데이터베이스. id→캐릭터, 캐릭터→전투유닛 조회.
    /// 메뉴: Create > SubcultureRPG > Meta > Game Database.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "SubcultureRPG/Meta/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public List<CharacterData> characters = new();
        [Tooltip("캐릭터별 전투 유닛(스탯/스킬). 없으면 성급 기반으로 자동 생성됩니다.")]
        public List<BattleUnitData> units = new();

        public IReadOnlyList<CharacterData> Characters => characters;

        public CharacterData GetCharacter(string id)
        {
            foreach (var c in characters)
                if (c != null && (c.id == id || c.name == id)) return c;
            return null;
        }

        /// <summary>캐릭터에 저작된 전투 유닛(없으면 null → 호출측에서 자동 생성).</summary>
        public BattleUnitData GetUnit(CharacterData c)
        {
            if (c == null) return null;
            foreach (var u in units)
                if (u != null && u.character == c) return u;
            return null;
        }
    }
}
