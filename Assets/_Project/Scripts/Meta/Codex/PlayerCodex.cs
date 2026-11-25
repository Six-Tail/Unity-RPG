using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Gacha;
using SubcultureRPG.Meta.Missions;

namespace SubcultureRPG.Meta.Codex
{
    /// <summary>수집 보상 1단계: 도감의 캐릭터를 count명 모으면 받는 보상(계정당 1회).</summary>
    public sealed class CodexMilestone
    {
        public int count;
        public MissionReward[] rewards = Array.Empty<MissionReward>();

        public string RewardText
        {
            get
            {
                var parts = new List<string>();
                foreach (var r in rewards) parts.Add(r.Text);
                return string.Join("\n", parts);
            }
        }
    }

    /// <summary>
    /// 캐릭터 도감: 전체 캐릭터 목록(GameDatabase) 대비 수집 현황과 수집 보상 수령 기록(PlayerPrefs).
    /// 보유 여부는 PlayerRoster 가 기준입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Codex")]
    public class PlayerCodex : MonoBehaviour
    {
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField, Tooltip("도감에 실을 전체 캐릭터. 비우면 로스터의 데이터베이스를 씁니다.")]
        private GameDatabase database;
        [SerializeField] private GachaController gacha;
        [SerializeField] private string saveKey = "codex";

        /// <summary>수집 보상(튜닝 지점). count 는 세이브 키이므로 바꾸면 그 단계의 수령 기록이 사라집니다.</summary>
        public static readonly CodexMilestone[] Milestones =
        {
            new() { count = 5, rewards = new[] { Crystal(100) } },
            new() { count = 10, rewards = new[] { Ticket(GachaBannerType.Standard, 3) } },
            new() { count = 15, rewards = new[] { Crystal(300) } },
            new() { count = 20, rewards = new[] { Ticket(GachaBannerType.Pickup, 3) } },
            new() { count = 25, rewards = new[] { Ticket(GachaBannerType.Pickup, 5), Crystal(500) } },
        };

        [Serializable] private class SaveData { public List<int> claimed = new(); }

        private readonly HashSet<int> _claimed = new();
        private bool _subscribed;

        /// <summary>수집 현황이나 수령 상태가 바뀔 때.</summary>
        public event Action OnChanged;

        public PlayerRoster Roster => roster;

        private void Awake()
        {
            FindRefs();
            Load();
        }

        private void FindRefs()
        {
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (gacha == null) gacha = FindAnyObjectByType<GachaController>();
            if (database == null && roster != null) database = roster.Database;
        }

        private void OnEnable()
        {
            if (_subscribed) return;
            _subscribed = true;
            FindRefs();
            if (roster != null) roster.OnRosterChanged += Notify;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (roster != null) roster.OnRosterChanged -= Notify;
        }

        private void Notify() => OnChanged?.Invoke();

        // ---------------- 목록 · 현황 ----------------

        /// <summary>도감의 전체 캐릭터(성급 높은 순 → 데이터베이스 순).</summary>
        public List<CharacterData> All()
        {
            var list = new List<CharacterData>();
            if (database == null) FindRefs();
            if (database != null)
                foreach (var c in database.Characters)
                    if (c != null && !list.Contains(c)) list.Add(c);
            // 안정 정렬(List.Sort 는 불안정) — 성급만 기준으로 삽입 정렬
            for (int i = 1; i < list.Count; i++)
            {
                var c = list[i];
                int j = i - 1;
                while (j >= 0 && (int)list[j].rarity < (int)c.rarity) { list[j + 1] = list[j]; j--; }
                list[j + 1] = c;
            }
            return list;
        }

        public bool IsOwned(CharacterData c) => roster != null && roster.IsOwned(c);

        public int Total => All().Count;

        public int OwnedCount
        {
            get
            {
                int n = 0;
                foreach (var c in All()) if (IsOwned(c)) n++;
                return n;
            }
        }

        /// <summary>획득처 안내(픽업 한정 / 픽업·상시 뽑기 / 스타터 등).</summary>
        public string SourceOf(CharacterData c)
        {
            if (c == null || gacha == null) return "뽑기";
            bool featured = false, pickupPool = false, standardPool = false;
            foreach (var b in gacha.Banners)
            {
                if (b == null) continue;
                if (b.featured6 == c) { featured = true; continue; }
                bool inPool = b.pool6.Contains(c) || b.pool5.Contains(c) || b.pool4.Contains(c) || b.pool3.Contains(c) || b.featured5.Contains(c);
                if (!inPool) continue;
                if (b.IsPickup) pickupPool = true; else standardPool = true;
            }
            if (featured && !standardPool) return "<color=#FFB3E6>픽업 뽑기 한정</color> — 해당 캐릭터의 픽업 배너";
            if (pickupPool && standardPool) return "픽업 뽑기 · 상시 뽑기";
            if (standardPool) return "상시 뽑기";
            if (pickupPool || featured) return "픽업 뽑기";
            return "뽑기에서 획득할 수 없음";
        }

        // ---------------- 수집 보상 ----------------

        public bool IsClaimed(CodexMilestone m) => m != null && _claimed.Contains(m.count);
        public bool IsReached(CodexMilestone m) => m != null && OwnedCount >= m.count;
        public bool CanClaim(CodexMilestone m) => IsReached(m) && !IsClaimed(m);

        /// <summary>지금 받을 수 있는 수집 보상 수(홈 화면 알림용).</summary>
        public int ClaimableCount()
        {
            int owned = OwnedCount, n = 0;
            foreach (var m in Milestones) if (owned >= m.count && !_claimed.Contains(m.count)) n++;
            return n;
        }

        public bool Claim(CodexMilestone m)
        {
            if (!CanClaim(m)) return false;
            _claimed.Add(m.count);
            foreach (var r in m.rewards) r.GrantTo(wallet, inventory);
            Save();
            OnChanged?.Invoke();
            return true;
        }

        // ---------------- 저장 ----------------

        private void Save()
        {
            var data = new SaveData();
            data.claimed.AddRange(_claimed);
            SaveStore.SetString(saveKey, JsonUtility.ToJson(data));
            SaveStore.Save();
        }

        private void Load()
        {
            _claimed.Clear();
            if (!SaveStore.HasKey(saveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(SaveStore.GetString(saveKey));
                if (data?.claimed != null) foreach (int c in data.claimed) _claimed.Add(c);
            }
            catch { }
        }

        [ContextMenu("수집 보상 기록 초기화")]
        public void ResetClaims()
        {
            _claimed.Clear();
            Save();
            OnChanged?.Invoke();
        }

        private static MissionReward Crystal(int n) => new() { kind = MissionRewardKind.Crystal, amount = n };
        private static MissionReward Ticket(GachaBannerType t, int n) => new() { kind = MissionRewardKind.Ticket, ticket = t, amount = n };
    }
}
