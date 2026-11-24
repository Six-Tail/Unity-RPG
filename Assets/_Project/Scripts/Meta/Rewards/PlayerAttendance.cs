using System;
using UnityEngine;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 출석부: 하루(05:00 기준)에 한 칸씩 받습니다. 빠진 날이 있어도 다음 칸부터 이어서 받음.
    /// · 출석부 28일 — 다 채우면 다음 날 1일부터 다시
    /// · 신규 지휘관 출석 7일 — 처음 7번의 출석 동안만. 3일째 태생 5★ 캐릭터 선택권, 7일째 태생 6★ 캐릭터 선택권(픽업 제외)
    /// 두 출석부는 따로 받습니다(같은 날 둘 다 가능).
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerAttendance.Instance).
    /// </summary>
    public class PlayerAttendance : MonoBehaviour
    {
        public const int MonthDays = 28, NewbieDays = 7;
        private const string SaveKey = "attendance";

        [Serializable]
        private class SaveData
        {
            public int month, newbie, cycles;
            public string monthLast = "", newbieLast = "", autoShown = "";
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerAttendance _instance;
        public static PlayerAttendance Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerAttendance>();
                if (_instance == null) _instance = new GameObject("PlayerAttendance").AddComponent<PlayerAttendance>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private static string Today => PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now);

        private void EnsureLoaded()
        {
            if (!_loaded)
            {
                _loaded = true;
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
            }
            // 28일을 다 채운 다음 날부터 새 출석부
            if (_d.month >= MonthDays && _d.monthLast != Today) { _d.month = 0; _d.cycles++; Save(); }
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        // ---------------- 보상표(튜닝 지점) ----------------

        private static RewardBundle R => new RewardBundle();

        /// <summary>출석부 28일 보상(day = 1 ~ 28). 7 · 14 · 21 · 28일은 뽑기권.</summary>
        public static RewardBundle MonthReward(int day) => day switch
        {
            1 => R.Gold(10000),
            2 => R.Mat(MaterialType.ExpSmall, 5),
            3 => R.Crystals(50),
            4 => R.Mat(MaterialType.StaminaSmall, 1),
            5 => R.Mat(MaterialType.SkillBook, 5),
            6 => R.Gold(20000),
            7 => R.Standard(1),
            8 => R.Mat(MaterialType.ExpPotion, 5),
            9 => R.Crystals(50),
            10 => R.Mat(MaterialType.EquipOre, 10),
            11 => R.Gold(20000),
            12 => R.Mat(MaterialType.SkillBookBlue, 3),
            13 => R.Mat(MaterialType.StaminaSmall, 2),
            14 => R.Pickup(1),
            15 => R.Mat(MaterialType.ExpLarge, 3),
            16 => R.Crystals(80),
            17 => R.Gold(30000),
            18 => R.Mat(MaterialType.EquipOre, 20),
            19 => R.Mat(MaterialType.SkillBookPurple, 2),
            20 => R.Mat(MaterialType.StaminaLarge, 1),
            21 => R.Standard(2),
            22 => R.Mat(MaterialType.ExpLarge, 5),
            23 => R.Crystals(100),
            24 => R.Gold(50000),
            25 => R.Mat(MaterialType.SkillBookPurple, 3),
            26 => R.Mat(MaterialType.StaminaLarge, 2),
            27 => R.Mat(MaterialType.ExpCrystal, 1),
            _ => R.Pickup(3).Crystals(200),
        };

        /// <summary>신규 지휘관 출석 7일 보상(day = 1 ~ 7).</summary>
        public static RewardBundle NewbieReward(int day) => day switch
        {
            1 => R.Crystals(300).Gold(30000),
            2 => R.Pickup(3),
            3 => R.Mat(MaterialType.Select5, 1),
            4 => R.Mat(MaterialType.StaminaLarge, 2).Mat(MaterialType.ExpLarge, 10),
            5 => R.Standard(5),
            6 => R.Crystals(500).Mat(MaterialType.SkillBookPurple, 5),
            _ => R.Mat(MaterialType.Select6, 1),
        };

        // ---------------- 상태 ----------------

        /// <summary>이번 출석부에서 받은 칸 수(0 ~ 28).</summary>
        public int MonthClaimed { get { EnsureLoaded(); return _d.month; } }
        public bool CanClaimMonth { get { EnsureLoaded(); return _d.month < MonthDays && _d.monthLast != Today; } }

        public int NewbieClaimed { get { EnsureLoaded(); return _d.newbie; } }
        /// <summary>신규 출석부가 아직 남았는지(7칸을 다 받으면 사라짐).</summary>
        public bool NewbieActive => NewbieClaimed < NewbieDays;
        public bool CanClaimNewbie { get { EnsureLoaded(); return _d.newbie < NewbieDays && _d.newbieLast != Today; } }

        public bool CanClaimAny => CanClaimMonth || CanClaimNewbie;

        /// <summary>오늘 아직 출석 창을 자동으로 띄우지 않았고 받을 칸이 있는지.</summary>
        public bool ShouldAutoOpen => CanClaimAny && _d.autoShown != Today;
        public void MarkAutoShown() { _d.autoShown = Today; Save(); }

        // ---------------- 받기 ----------------

        /// <summary>출석부 오늘 칸 받기(받은 보상, 못 받으면 null).</summary>
        public RewardBundle ClaimMonth()
        {
            if (!CanClaimMonth) return null;
            _d.month++;
            _d.monthLast = Today;
            var r = MonthReward(_d.month);
            r.Grant();
            Changed();
            return r;
        }

        /// <summary>신규 출석부 오늘 칸 받기(받은 보상, 못 받으면 null).</summary>
        public RewardBundle ClaimNewbie()
        {
            if (!CanClaimNewbie) return null;
            _d.newbie++;
            _d.newbieLast = Today;
            var r = NewbieReward(_d.newbie);
            r.Grant();
            Changed();
            return r;
        }

        [ContextMenu("테스트: 하루 넘기기(오늘 칸 다시 받기)")]
        private void TestNextDay() { _d.monthLast = "test"; _d.newbieLast = "test"; _d.autoShown = ""; Changed(); }

        [ContextMenu("테스트: 출석부 초기화")]
        private void TestReset() { _d = new SaveData(); Changed(); }
    }
}
