using System;
using UnityEngine;

namespace SubcultureRPG.Meta.Missions
{
    /// <summary>
    /// 시즌 패스: 4주(28일)마다 새 시즌, 30단계. 일일 임무를 받을 때마다 +25, 주간 임무 +60(보너스 임무 제외).
    /// 단계마다 무료 보상 + 프리미엄 보상(크리스탈 980으로 열면). 시즌이 바뀌면 받지 않은 보상은 우편으로 보내고 처음부터.
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(SeasonPass.Instance). 문서: docs/season-pass.md
    /// </summary>
    public class SeasonPass : MonoBehaviour
    {
        public const int MaxLevel = 30, ExpPerLevel = 150, DailyExp = 25, WeeklyExp = 60, PremiumPrice = 980, SeasonDays = 28;
        private const string SaveKey = "seasonpass";

        // 시즌 기준: 2024-01-01(월) 05:00부터 28일씩 (기간 한정 이벤트와 같은 기준 시각)
        private static readonly DateTime Epoch = new(2024, 1, 1, 5, 0, 0);

        public static int SeasonIndex(DateTime now) => (int)Math.Floor((now - Epoch).TotalDays / SeasonDays);
        public static int CurrentIndex => SeasonIndex(DateTime.Now);
        public static DateTime EndOf(int index) => Epoch.AddDays((index + 1) * (double)SeasonDays);
        public static TimeSpan UntilEnd => EndOf(CurrentIndex) - DateTime.Now;

        [Serializable]
        private class SaveData
        {
            public int season = -1, first = -1, exp;
            public bool premium;
            public int freeBits, premiumBits;   // 단계 보상 수령 기록(비트 0 = Lv.1)
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static SeasonPass _instance;
        private static bool _booted;

        public static SeasonPass Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<SeasonPass>();
                if (_instance == null) _instance = new GameObject("SeasonPass").AddComponent<SeasonPass>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        /// <summary>게임 시작 시 임무 수령 신호를 받기 시작.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_booted) return;
            _booted = true;
            PlayerMissions.Counted += (goal, amount) =>
            {
                if (goal == MissionGoal.DailyClaim) Instance.AddExp(DailyExp * amount);
                else if (goal == MissionGoal.WeeklyClaim) Instance.AddExp(WeeklyExp * amount);
            };
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (!_loaded)
            {
                _loaded = true;
                if (SaveStore.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
            }
            CheckSeason();
        }

        /// <summary>시즌이 바뀌었으면 지난 시즌의 받지 않은 보상을 우편으로 보내고 새로 시작.</summary>
        private void CheckSeason()
        {
            int now = CurrentIndex;
            if (_d.season == now) return;
            if (_d.season >= 0)
            {
                var left = new RewardBundle();
                int lv = Level;
                for (int l = 1; l <= lv; l++)
                {
                    if (!Has(_d.freeBits, l)) Merge(left, FreeReward(l));
                    if (_d.premium && !Has(_d.premiumBits, l)) Merge(left, PremiumReward(l));
                }
                if (!left.IsEmpty)
                    PlayerMail.Instance.Send($"시즌 {SeasonNumber} 패스 — 받지 않은 보상", "지난 시즌 패스에서 받지 않은 단계 보상입니다.", left);
            }
            int first = _d.first >= 0 ? _d.first : now;
            _d = new SaveData { season = now, first = first };
            Save();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        private static bool Has(int bits, int lv) => (bits & (1 << (lv - 1))) != 0;

        // ---------------- 진행 ----------------

        /// <summary>시즌 번호(이 계정의 첫 시즌 = 1).</summary>
        public int SeasonNumber => _d.season - Mathf.Max(0, _d.first >= 0 ? _d.first : _d.season) + 1;
        public int Exp => _d.exp;
        public bool Premium => _d.premium;
        public int Level => Mathf.Min(MaxLevel, 1 + _d.exp / ExpPerLevel);

        /// <summary>이번 단계 안에서의 진행(최대 단계면 (0, 0)).</summary>
        public (int cur, int need) Progress => Level >= MaxLevel ? (0, 0) : (_d.exp % ExpPerLevel, ExpPerLevel);

        public void AddExp(int amount)
        {
            CheckSeason();
            if (amount <= 0 || _d.exp >= (MaxLevel - 1) * ExpPerLevel) return;
            _d.exp = Mathf.Min((MaxLevel - 1) * ExpPerLevel, _d.exp + amount);
            Changed();
        }

        // ---------------- 보상표(튜닝 지점) ----------------

        /// <summary>
        /// 무료 보상(30단계):
        /// · 10 · 20단계 크리스탈 150, 30단계 크리스탈 200 + 픽업 뽑기권 1
        /// · 5 · 15 · 25단계 장비 정수 30(장비 선택 제작)
        /// · 그 밖에는 금화 · 소탕권 · 경험치 물약 · (행동력 물약 / 장비 강화석 번갈아) 순환
        /// </summary>
        public static RewardBundle FreeReward(int lv)
        {
            var r = new RewardBundle();
            if (lv == MaxLevel) return r.Crystals(200).Pickup(1);
            if (lv % 10 == 0) return r.Crystals(150);
            if (lv % 5 == 0) return r.Mat(MaterialType.EquipEssence, 30);
            return (lv % 5) switch
            {
                1 => r.Gold(20000),
                2 => r.Mat(MaterialType.SweepTicket, 5),
                3 => r.Mat(MaterialType.ExpPotion, 8),
                _ => (lv / 5) % 2 == 0 ? r.Mat(MaterialType.StaminaSmall, 1) : r.Mat(MaterialType.EquipOre, 15),
            };
        }

        /// <summary>
        /// 프리미엄 보상(30단계):
        /// · 10 · 20단계 픽업 뽑기권 1 + 재련석 1, 30단계 픽업 뽑기권 2 + 크리스탈 300 + 재련석 1
        /// · 5 · 15 · 25단계 전설의 비전서 1 + 장비 정수 60
        /// · 그 밖에는 크리스탈 60 · 대형 경험치 물약 · 호감도 선물(별빛 브로치) · (행동력 물약 / 상급 스킬 교본 번갈아) 순환
        /// </summary>
        public static RewardBundle PremiumReward(int lv)
        {
            var r = new RewardBundle();
            if (lv == MaxLevel) return r.Pickup(2).Crystals(300).Mat(MaterialType.RefineStone, 1);
            if (lv % 10 == 0) return r.Pickup(1).Mat(MaterialType.RefineStone, 1);
            if (lv % 5 == 0) return r.Mat(MaterialType.SkillLegend, 1).Mat(MaterialType.EquipEssence, 60);
            return (lv % 5) switch
            {
                1 => r.Crystals(60),
                2 => r.Mat(MaterialType.ExpLarge, 6),
                3 => r.Mat(MaterialType.GiftLarge, 1),
                _ => (lv / 5) % 2 == 0 ? r.Mat(MaterialType.StaminaLarge, 1) : r.Mat(MaterialType.SkillBookPurple, 3),
            };
        }

        private static void Merge(RewardBundle into, RewardBundle r)
        {
            into.crystals += r.crystals; into.gold += r.gold; into.pickup += r.pickup; into.standard += r.standard;
            if (r.mats != null) foreach (var m in r.mats) into.Mat(m.Type, m.count);
        }

        // ---------------- 수령 ----------------

        public bool Claimed(int lv, bool premium) => Has(premium ? _d.premiumBits : _d.freeBits, lv);

        public bool CanClaim(int lv, bool premium) =>
            lv >= 1 && lv <= Level && !Claimed(lv, premium) && (!premium || _d.premium);

        public int ClaimableCount()
        {
            CheckSeason();
            int n = 0;
            for (int l = 1; l <= Level; l++) { if (CanClaim(l, false)) n++; if (CanClaim(l, true)) n++; }
            return n;
        }

        /// <summary>한 칸 받기(받았으면 그 보상, 못 받으면 null).</summary>
        public RewardBundle Claim(int lv, bool premium)
        {
            CheckSeason();
            if (!CanClaim(lv, premium)) return null;
            var r = premium ? PremiumReward(lv) : FreeReward(lv);
            if (premium) _d.premiumBits |= 1 << (lv - 1); else _d.freeBits |= 1 << (lv - 1);
            r.Grant();
            Changed();
            return r;
        }

        /// <summary>받을 수 있는 칸을 모두 받음(합친 보상, 없으면 null).</summary>
        public RewardBundle ClaimAll()
        {
            CheckSeason();
            var total = new RewardBundle();
            bool any = false;
            for (int l = 1; l <= Level; l++)
                for (int p = 0; p < 2; p++)
                {
                    bool prem = p == 1;
                    if (!CanClaim(l, prem)) continue;
                    Merge(total, prem ? PremiumReward(l) : FreeReward(l));
                    if (prem) _d.premiumBits |= 1 << (l - 1); else _d.freeBits |= 1 << (l - 1);
                    any = true;
                }
            if (!any) return null;
            total.Grant();
            Changed();
            return total;
        }

        /// <summary>프리미엄 열기(크리스탈). 이유 문자열을 돌려주고, 성공하면 null.</summary>
        public string BuyPremium()
        {
            CheckSeason();
            if (_d.premium) return "이미 프리미엄 패스를 열었습니다";
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet == null || !wallet.Spend(PremiumPrice)) return $"크리스탈이 부족합니다 (필요 {PremiumPrice})";
            _d.premium = true;
            Changed();
            return null;
        }

        // ---------------- 테스트 ----------------

        [ContextMenu("테스트: 패스 경험치 +500")]
        private void TestExp() => AddExp(500);

        [ContextMenu("테스트: 이번 시즌 초기화")]
        private void TestReset()
        {
            int first = _d.first;
            _d = new SaveData { season = CurrentIndex, first = first };
            Changed();
        }
    }
}
