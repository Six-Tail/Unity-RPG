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
        [SerializeField, Tooltip("보유 레벨 조회(선택). 없으면 partyLevel 사용.")] private PlayerRoster roster;
        [SerializeField, Tooltip("장착 장비(선택). 비우면 자동 탐색.")] private PlayerEquipment equipment;
        [SerializeField, Tooltip("roster가 없을 때 쓸 기본 레벨.")] private int partyLevel = 1;
        [SerializeField, Tooltip("이 전투의 적 유닛.")] private List<BattleUnitData> enemyTeam = new();

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<BattleController>();
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (equipment == null) equipment = FindAnyObjectByType<PlayerEquipment>();
        }

        /// <summary>파티 캐릭터로 전투 시작(적 = 이 컴포넌트의 enemyTeam).</summary>
        public void Launch(IList<CharacterData> party) => Launch(party, enemyTeam);

        /// <summary>파티 캐릭터로 지정한 적과 전투 시작(스테이지 등).</summary>
        public void Launch(IList<CharacterData> party, List<BattleUnitData> enemies)
        {
            if (controller == null || party == null || party.Count == 0) return;

            System.Func<CharacterData, BattleUnitData> lookup = null;
            if (database != null) lookup = database.GetUnit;

            var allies = new List<BattleUnitData>();
            foreach (var c in party)
                if (c != null)
                {
                    int lvl = roster != null ? roster.LevelOf(c) : partyLevel;
                    int skl = roster != null ? roster.SkillLevelOf(c) : 1;
                    int star = roster != null ? roster.StarOf(c) : 0;
                    int tr = roster != null ? roster.TranscendOf(c) : 0;
                    var sk = roster != null ? roster.EffectiveSkillLevels(c) : null;   // 초월 보너스 레벨 포함
                    var u = RuntimeUnits.FromCharacter(c, lvl, lookup, skl, star, tr, sk);
                    bool owned = roster != null && roster.IsOwned(c);
                    if (owned || (equipment != null && equipment.EquippedOn(c).Count > 0))
                    {
                        if (lookup != null && lookup(c) == u) u = Instantiate(u);   // 저작 에셋은 건드리지 않음
                        u.bonus = PlayerBond.BonusFor(c, equipment);                // 장비 + 호감도
                    }
                    allies.Add(u);
                }

            // 이기면 출전한 캐릭터의 호감도 +10
            _lastParty = new List<CharacterData>(party);
            if (_hooked != controller)
            {
                if (_hooked != null) _hooked.OnBattleEnd -= HandleEnd;
                controller.OnBattleEnd += HandleEnd;
                _hooked = controller;
            }

            controller.SetTeams(allies, new List<BattleUnitData>(enemies ?? enemyTeam));
        }

        private List<CharacterData> _lastParty;
        private BattleController _hooked;

        private void HandleEnd(Team winner)
        {
            if (winner != Team.Ally || _lastParty == null) return;
            var roster = this.roster != null ? this.roster : FindAnyObjectByType<PlayerRoster>();
            var owned = _lastParty.FindAll(c => c != null && (roster == null || roster.IsOwned(c)));
            _lastParty = null;
            PlayerBond.Instance.OnVictory(owned);
        }

        private void OnDestroy()
        {
            if (_hooked != null) _hooked.OnBattleEnd -= HandleEnd;
        }
    }
}
