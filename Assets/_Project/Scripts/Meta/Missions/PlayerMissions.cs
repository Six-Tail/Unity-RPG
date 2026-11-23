using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Gacha;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta.Missions
{
    /// <summary>
    /// 임무 진행도·보상 수령 기록(PlayerPrefs). 전투 결과 · 뽑기 · <see cref="MissionEvents"/> 알림을 받아 진행도를 올립니다.
    /// 일일은 매일 05:00, 주간은 매주 월요일 05:00(로컬)에 초기화 — 상점과 같은 주기 계산을 씁니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Missions")]
    public class PlayerMissions : MonoBehaviour
    {
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private StageRunner stageRunner;
        [SerializeField] private GachaController gacha;
        [SerializeField] private string saveKey = "missions";

        [Serializable] private class Entry { public string id; public int count; public bool claimed; public string period; }
        [Serializable] private class SaveData { public List<Entry> items = new(); }

        private readonly Dictionary<string, Entry> _entries = new();
        private bool _subscribed;

        /// <summary>진행도나 수령 상태가 바뀔 때.</summary>
        public event Action OnChanged;

        private void Awake()
        {
            FindRefs();
            Load();
        }

        private void FindRefs()
        {
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (stageRunner == null) stageRunner = FindAnyObjectByType<StageRunner>();
            if (gacha == null) gacha = FindAnyObjectByType<GachaController>();
        }

        private void OnEnable()
        {
            if (_subscribed) return;
            _subscribed = true;
            FindRefs();
            MissionEvents.OnProgress += Report;
            if (stageRunner != null) stageRunner.OnStageFinished += OnStageFinished;
            if (gacha != null) gacha.OnPulled += OnPulled;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            MissionEvents.OnProgress -= Report;
            if (stageRunner != null) stageRunner.OnStageFinished -= OnStageFinished;
            if (gacha != null) gacha.OnPulled -= OnPulled;
        }

        private void OnStageFinished(StageResult r)
        {
            if (!r.win) return;
            Report(MissionGoal.BattleWin, 1);
            if (r.stage != null && r.stage.IsDungeon) Report(MissionGoal.DungeonClear, 1);
        }

        private void OnPulled(List<GachaResult> results)
        {
            if (results != null) Report(MissionGoal.GachaPull, results.Count);
        }

        // ---------------- 조회 ----------------

        private static string PeriodKey(MissionPeriod p) =>
            PlayerShop.PeriodKey(p == MissionPeriod.Daily ? ShopReset.Daily : ShopReset.Weekly, DateTime.Now);

        /// <summary>이번 주기의 기록(주기가 지났으면 null).</summary>
        private Entry Current(Mission m) =>
            _entries.TryGetValue(m.id, out var e) && e.period == PeriodKey(m.period) ? e : null;

        /// <summary>진행도(0 ~ target).</summary>
        public int Progress(Mission m)
        {
            if (m == null) return 0;
            if (m.goal == MissionGoal.Login) return m.target;   // 플레이 중이면 접속은 달성
            var e = Current(m);
            return e != null ? Mathf.Min(e.count, m.target) : 0;
        }

        public bool IsClaimed(Mission m) { var e = m != null ? Current(m) : null; return e != null && e.claimed; }
        public bool IsComplete(Mission m) => m != null && Progress(m) >= m.target;
        public bool CanClaim(Mission m) => IsComplete(m) && !IsClaimed(m);

        /// <summary>지금 받을 수 있는 보상 수(홈 화면 알림용).</summary>
        public int ClaimableCount()
        {
            int n = 0;
            foreach (var m in MissionCatalog.All) if (CanClaim(m)) n++;
            return n;
        }

        public int ClaimableCount(MissionPeriod period)
        {
            int n = 0;
            foreach (var m in MissionCatalog.All) if (m.period == period && CanClaim(m)) n++;
            return n;
        }

        /// <summary>다음 초기화까지 남은 시간.</summary>
        public static TimeSpan UntilReset(MissionPeriod p) =>
            PlayerShop.UntilReset(p == MissionPeriod.Daily ? ShopReset.Daily : ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero;

        // ---------------- 진행 ----------------

        /// <summary>행동 n회를 해당 목표의 모든 임무(일일 · 주간)에 반영.</summary>
        public void Report(MissionGoal goal, int amount)
        {
            if (amount <= 0) return;
            bool changed = false;
            foreach (var m in MissionCatalog.All)
            {
                if (m.goal != goal || m.goal == MissionGoal.Login) continue;
                var e = Touch(m);
                if (e.count >= m.target) continue;   // 이미 달성(수령 여부와 무관)
                e.count = Mathf.Min(m.target, e.count + amount);
                changed = true;
            }
            if (!changed) return;
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>이번 주기의 기록을 가져오거나(주기가 바뀌었으면 초기화해) 새로 만듭니다.</summary>
        private Entry Touch(Mission m)
        {
            string period = PeriodKey(m.period);
            if (!_entries.TryGetValue(m.id, out var e) || e.period != period)
            {
                e = new Entry { id = m.id, count = 0, claimed = false, period = period };
                _entries[m.id] = e;
            }
            return e;
        }

        // ---------------- 보상 ----------------

        /// <summary>보상 수령. 성공하면 true.</summary>
        public bool Claim(Mission m) => ClaimInternal(m, notify: true);

        private bool ClaimInternal(Mission m, bool notify)
        {
            if (!CanClaim(m)) return false;
            var e = Touch(m);
            e.count = m.target;
            e.claimed = true;
            foreach (var r in m.rewards) r.GrantTo(wallet, inventory);
            Save();

            // 보너스 임무(일일 N개 완료 등) 진행 — 보너스 임무 자신은 세지 않음
            if (!m.IsBonus)
                Report(m.period == MissionPeriod.Daily ? MissionGoal.DailyClaim : MissionGoal.WeeklyClaim, 1);
            if (notify) OnChanged?.Invoke();
            return true;
        }

        /// <summary>해당 주기에서 받을 수 있는 보상을 모두 수령. 받은 임무 목록을 돌려줍니다.</summary>
        public List<Mission> ClaimAll(MissionPeriod period)
        {
            var got = new List<Mission>();
            // 일반 임무를 받으면 보너스 임무가 달성될 수 있으므로, 더 받을 게 없을 때까지 반복
            bool again = true;
            while (again)
            {
                again = false;
                foreach (var m in MissionCatalog.All)
                    if (m.period == period && ClaimInternal(m, notify: false)) { got.Add(m); again = true; }
            }
            if (got.Count > 0) OnChanged?.Invoke();
            return got;
        }

        // ---------------- 저장 ----------------

        private void Save()
        {
            var data = new SaveData();
            foreach (var kv in _entries) data.items.Add(kv.Value);
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _entries.Clear();
            if (!PlayerPrefs.HasKey(saveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
                if (data?.items == null) return;
                foreach (var e in data.items)
                    if (e != null && !string.IsNullOrEmpty(e.id) && MissionCatalog.Find(e.id) != null) _entries[e.id] = e;   // 삭제된 임무는 버림
            }
            catch { }
        }

        [ContextMenu("임무 기록 초기화")]
        public void ResetMissions()
        {
            _entries.Clear();
            Save();
            OnChanged?.Invoke();
        }

        [ContextMenu("임무 전부 달성 처리 (테스트)")]
        public void CompleteAllForTest()
        {
            foreach (var m in MissionCatalog.All)
            {
                var e = Touch(m);
                if (!e.claimed) e.count = m.target;
            }
            Save();
            OnChanged?.Invoke();
        }
    }
}
