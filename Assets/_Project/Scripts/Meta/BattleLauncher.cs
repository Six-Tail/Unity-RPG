using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 선택한 파티(보유 캐릭터)로 전투를 시작합니다.
    /// 캐릭터 → 전투 유닛 변환(DB 저작 우선, 없으면 성급 기반 생성) 후 BattleController에 투입.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Battle Launcher")]
    public class BattleLauncher : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 자동 탐색.")] private BattleController controller;
        [SerializeField, Tooltip("캐릭터→전투유닛 조회(선택). 없으면 성급 기반 자동 생성.")] private GameDatabase database;
        [SerializeField] private int partyLevel = 1;
        [SerializeField, Tooltip("이 전투의 적 유닛.")] private List<BattleUnitData> enemyTeam = new();

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<BattleController>();
        }

        /// <summary>파티 캐릭터로 전투 시작.</summary>
        public void Launch(IList<CharacterData> party)
        {
            if (controller == null || party == null || party.Count == 0) return;

            System.Func<CharacterData, BattleUnitData> lookup = null;
            if (database != null) lookup = database.GetUnit;

            var allies = new List<BattleUnitData>();
            foreach (var c in party)
                if (c != null)
                    allies.Add(RuntimeUnits.FromCharacter(c, partyLevel, lookup));

            controller.SetTeams(allies, new List<BattleUnitData>(enemyTeam));
        }
    }
}
