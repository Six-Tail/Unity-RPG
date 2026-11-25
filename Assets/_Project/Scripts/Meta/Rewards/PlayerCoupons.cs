using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 쿠폰: 쿠폰 번호를 입력하면 보상을 우편함으로 보냄. 같은 쿠폰은 계정마다 한 번(사용 기록은 세이브 → 클라우드에 함께 저장).
    /// · 번호는 대소문자 · 공백 · 하이픈을 무시(stella-2026 = STELLA2026)
    /// · 쿠폰 목록은 아래 <see cref="Catalog"/> 한 곳에서 추가 · 수정(기간 · 보상)
    /// </summary>
    public class PlayerCoupons : MonoBehaviour
    {
        private const string SaveKey = "coupons";

        /// <summary>쿠폰 하나: 번호 · 이름 · 보상 · 사용 기간(로컬 날짜, 비우면 기간 없음).</summary>
        public class Coupon
        {
            public string code, name;
            public Func<RewardBundle> reward;
            public DateTime? from, until;   // until = 그날까지(그날 23:59:59 포함)
        }

        /// <summary>쿠폰 목록(튜닝 지점). 새 쿠폰은 여기에 한 줄 추가.</summary>
        public static readonly Coupon[] Catalog =
        {
            new() { code = "STELLAANCHOR", name = "출시 기념 쿠폰",
                reward = () => new RewardBundle().Crystals(1000).Pickup(5).Gold(100000) },
            new() { code = "WELCOMEFRIENDS", name = "친구 초대 감사 쿠폰",
                reward = () => new RewardBundle().Crystals(500).Mat(MaterialType.StaminaLarge, 3).Mat(MaterialType.ExpLarge, 20) },
            new() { code = "GROWTHBOOST", name = "성장 지원 쿠폰",
                reward = () => new RewardBundle().Gold(200000).Mat(MaterialType.ExpLarge, 30).Mat(MaterialType.SkillBookPurple, 10).Mat(MaterialType.EquipEssence, 120) },
            new() { code = "WINTER2026", name = "겨울 이벤트 쿠폰",
                reward = () => new RewardBundle().Crystals(800).Pickup(3).Mat(MaterialType.RefineStone, 1),
                from = new DateTime(2026, 12, 1), until = new DateTime(2027, 2, 28) },
        };

        [Serializable]
        private class SaveData { public List<string> used = new(); }

        private SaveData _d = new();
        private bool _loaded;

        private static PlayerCoupons _instance;
        public static PlayerCoupons Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerCoupons>();
                if (_instance == null) _instance = new GameObject("PlayerCoupons").AddComponent<PlayerCoupons>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (SaveStore.HasKey(SaveKey))
            {
                try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
                catch { _d = new SaveData(); }
            }
            _d.used ??= new();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        /// <summary>입력 정리: 영문 · 숫자만 남기고 대문자로.</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new StringBuilder(input.Length);
            foreach (char ch in input)
                if (ch < 128 && char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            return sb.ToString();
        }

        public bool IsUsed(string code) => _d.used.Contains(Normalize(code));

        /// <summary>쿠폰 사용. 성공하면 null + 쿠폰(보상은 우편함으로), 실패하면 이유 문구.</summary>
        public string Redeem(string input, out Coupon coupon, out RewardBundle reward)
        {
            coupon = null; reward = null;
            string code = Normalize(input);
            if (code.Length == 0) return "쿠폰 번호를 입력해 주세요.";
            foreach (var c in Catalog)
            {
                if (Normalize(c.code) != code) continue;
                var today = DateTime.Now.Date;
                if (c.from.HasValue && today < c.from.Value.Date) return $"아직 사용할 수 없는 쿠폰입니다. ({c.from.Value:yyyy-MM-dd}부터)";
                if (c.until.HasValue && today > c.until.Value.Date) return "사용 기간이 지난 쿠폰입니다.";
                if (_d.used.Contains(code)) return "이미 사용한 쿠폰입니다.";

                coupon = c;
                reward = c.reward();
                _d.used.Add(code);
                Save();
                PlayerMail.Instance.Send($"쿠폰 보상 — {c.name}", $"쿠폰 '{c.code}' 보상입니다. 즐거운 여정 되세요!", reward);
                return null;
            }
            return "올바르지 않은 쿠폰 번호입니다.";
        }

        [ContextMenu("테스트: 쿠폰 사용 기록 초기화")]
        private void TestReset() { _d.used.Clear(); Save(); }
    }
}
