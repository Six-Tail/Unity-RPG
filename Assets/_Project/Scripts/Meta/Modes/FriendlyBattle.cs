using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 친선 전투: 다른 지휘관(실제 플레이어 · 가상 지휘관)의 전투력 상위 4명과 겨룸 — 그 사람이 키운 그대로(레벨 · 성급 · 초월 · 스킬 · 장비 · 호감도).
    /// 상대는 AI가 조종. 도전권 · 행동력 없이 몇 번이든, 점수 · 보상 없음. 결과 화면 '다시'로 같은 상대와 바로 재대결.
    /// 실제 플레이어는 마지막으로 접속했을 때 올린 공개 프로필(pp)의 육성 상태로 나옴.
    /// 친구 목록 · 지휘관 정보창의 '친선 전투' → <see cref="Request"/> → 편성(ScreenNavigator) → <see cref="Fight"/>.
    /// </summary>
    public class FriendlyBattle : MonoBehaviour
    {
        public const int TeamSize = 4;

        /// <summary>'친선 전투'를 눌렀을 때(편성 화면을 여는 쪽이 구독).</summary>
        public static event Action<ProfileSnap> Requested;

        /// <summary>그 지휘관과 친선 전투 요청. 상대 캐릭터 정보가 없으면 false.</summary>
        public static bool Request(ProfileSnap s)
        {
            if (s == null || s.top == null || s.top.Count == 0 || Requested == null) return false;
            Requested.Invoke(s);
            return true;
        }

        private static FriendlyBattle _instance;
        public static FriendlyBattle Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<FriendlyBattle>();
                if (_instance == null) _instance = new GameObject("FriendlyBattle").AddComponent<FriendlyBattle>();
                return _instance;
            }
        }

        private void Awake() { if (_instance == null) _instance = this; }

        /// <summary>상대로 나오는 4명(전투력 높은 순)과 전투력.</summary>
        public static List<(CharSnap cs, int power)> Lineup(ProfileSnap s, GameDatabase db)
        {
            var list = new List<(CharSnap, int)>();
            if (s?.top == null || db == null) return list;
            foreach (var cs in s.top)
            {
                var r = SnapStats.Evaluate(cs, db);
                if (r != null) list.Add((cs, r.power));
            }
            list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            if (list.Count > TeamSize) list.RemoveRange(TeamSize, list.Count - TeamSize);
            return list;
        }

        public static long LineupPower(ProfileSnap s, GameDatabase db)
        {
            long sum = 0;
            foreach (var (_, p) in Lineup(s, db)) sum += p;
            return sum;
        }

        // ---------------- 전투 ----------------

        private ProfileSnap _foe;
        private List<CharacterData> _party;
        private bool _pending, _active;
        private int _wins, _losses;   // 이번 상대와의 전적(같은 상대와 계속하는 동안)
        private readonly List<BattleUnitData> _spawned = new();
        private BattleController _controller;
        private BattleHUD _hud;

        /// <summary>편성 화면의 '대결!'. 시작하지 못하면 false.</summary>
        public bool Fight(ProfileSnap s, IList<CharacterData> party)
        {
            if (s == null || party == null || party.Count == 0) return false;
            var launcher = FindAnyObjectByType<BattleLauncher>();
            var roster = FindAnyObjectByType<PlayerRoster>();
            if (launcher == null || roster == null || roster.Database == null) return false;

            Hook();
            Cleanup();
            var db = roster.Database;
            var enemies = new List<BattleUnitData>();
            foreach (var (cs, _) in Lineup(s, db))
            {
                var u = SnapStats.BuildUnit(cs, db);
                if (u != null) enemies.Add(u);
            }
            if (enemies.Count == 0) return false;
            if (_foe == null || _foe.pid != s.pid || _foe.uid != s.uid) { _wins = 0; _losses = 0; }
            _spawned.AddRange(enemies);
            _foe = s;
            _party = new List<CharacterData>(party);
            _pending = true;
            if (_hud != null)
            {
                _hud.SetBackground("Backgrounds/battle_arena", "Backgrounds/arena", "Backgrounds/battle");
                _hud.SetRetryHidden(false);
                _hud.SetRetryHandler(() => Fight(_foe, _party) ? null : "대결을 시작할 수 없습니다");
                _hud.SetExitLabel("돌아가기");
                _hud.SetRoundLimit(0);
            }
            launcher.Launch(_party, enemies);
            return true;
        }

        /// <summary>전투 화면을 떠날 때.</summary>
        public void Leave()
        {
            bool mine = _active || _pending || _foe != null;
            _active = false; _pending = false;
            if (mine && _hud != null) _hud.SetRetryHandler(null);
            _foe = null;
        }

        private void Hook()
        {
            if (_controller != null) return;
            _controller = FindAnyObjectByType<BattleController>();
            _hud = FindAnyObjectByType<BattleHUD>();
            if (_controller == null) return;
            _controller.OnBattleStart += _ => { _active = _pending; _pending = false; };
            _controller.OnBattleEnd += HandleEnd;
        }

        private void Cleanup()
        {
            foreach (var u in _spawned)
            {
                if (u == null) continue;
                if (u.skills != null) foreach (var sk in u.skills) if (sk != null) Destroy(sk);
                Destroy(u);
            }
            _spawned.Clear();
        }

        private void HandleEnd(Team winner)
        {
            if (!_active || _foe == null) return;
            _active = false;
            bool win = winner == Team.Ally;
            if (win) _wins++; else _losses++;
            string tag = _foe.real ? "  <size=70%><color=#7CFF9A>● 플레이어</color></size>" : "";
            string detail = $"<size=110%>친선 전투 — {_foe.name}{tag}</size>\n" +
                            (win ? "<color=#7CFF9A>승리!</color>" : "<color=#FF8A8A>패배</color>") +
                            $"   <color=#9AA3B5>이번 상대와 {_wins}승 {_losses}패</color>\n" +
                            "<size=80%><color=#9AA3B5>친선 전투는 점수 · 보상이 없습니다 · '다시'로 바로 재대결</color></size>";
            if (_hud != null) _hud.SetResultDetail(detail);
        }
    }
}
