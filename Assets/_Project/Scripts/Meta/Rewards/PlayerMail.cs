using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 우편함: 운영 선물 · 시스템 보상을 받아 두는 곳. 받기 전까지 사라지지 않습니다.
    /// · 처음 접속하면 '신규 지휘관 환영 선물'이 한 번 들어옴(픽업 · 상시 뽑기권 10장씩, 크리스탈 2,000, 소형 행동력 물약 3개, 행동력 물약 1개)
    /// · 다른 시스템이 PlayerMail.Instance.Send(제목, 본문, 보상)으로 보냄(튜토리얼 완료 보상 등)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerMail.Instance).
    /// </summary>
    public class PlayerMail : MonoBehaviour
    {
        public const int MaxMails = 100;
        private const string SaveKey = "mail";

        [Serializable]
        public class Mail
        {
            public string id, title, body;
            public RewardBundle reward = new();
            public long sent;       // DateTime.Ticks
            public bool claimed;
            public DateTime SentAt => new DateTime(sent);
        }

        [Serializable]
        private class SaveData
        {
            public List<Mail> mails = new();
            public bool welcome;
            public int seq;
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerMail _instance;
        public static PlayerMail Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerMail>();
                if (_instance == null) _instance = new GameObject("PlayerMail").AddComponent<PlayerMail>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        /// <summary>신규 유저 보상(튜닝 지점).</summary>
        public static RewardBundle WelcomeReward() => new RewardBundle()
            .Pickup(10).Standard(10).Crystals(2000)
            .Mat(MaterialType.StaminaSmall, 3).Mat(MaterialType.StaminaLarge, 1);

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (PlayerPrefs.HasKey(SaveKey))
            {
                try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
                catch { _d = new SaveData(); }
            }
            _d.mails ??= new();
            if (!_d.welcome)
            {
                _d.welcome = true;
                Add("신규 지휘관 환영 선물", "스텔라 앵커에 오신 것을 환영합니다! 여정의 첫걸음에 보탬이 되길 바랍니다.", WelcomeReward());
                Save();
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        private void Add(string title, string body, RewardBundle reward)
        {
            _d.mails.Insert(0, new Mail { id = "m" + (++_d.seq), title = title, body = body, reward = reward ?? new RewardBundle(), sent = DateTime.Now.Ticks });
            // 가득 차면 이미 받은 오래된 우편부터 지움
            for (int i = _d.mails.Count - 1; i >= 0 && _d.mails.Count > MaxMails; i--)
                if (_d.mails[i].claimed) _d.mails.RemoveAt(i);
        }

        // ---------------- 조회 · 동작 ----------------

        /// <summary>우편 목록(새 우편이 앞).</summary>
        public IReadOnlyList<Mail> Mails { get { EnsureLoaded(); return _d.mails; } }

        public int UnclaimedCount
        {
            get
            {
                EnsureLoaded();
                int n = 0;
                foreach (var m in _d.mails) if (!m.claimed) n++;
                return n;
            }
        }

        public void Send(string title, string body, RewardBundle reward)
        {
            EnsureLoaded();
            Add(title, body, reward);
            Changed();
        }

        public bool Claim(Mail m)
        {
            if (m == null || m.claimed) return false;
            m.claimed = true;
            m.reward?.Grant();
            Changed();
            return true;
        }

        /// <summary>받지 않은 우편을 모두 받음. 반환 = 받은 수(합친 보상은 total).</summary>
        public int ClaimAll(out RewardBundle total)
        {
            total = new RewardBundle();
            int n = 0;
            foreach (var m in _d.mails)
            {
                if (m.claimed) continue;
                m.claimed = true;
                n++;
                var r = m.reward;
                if (r == null) continue;
                r.Grant();
                total.Crystals(r.crystals).Gold(r.gold).Pickup(r.pickup).Standard(r.standard);
                if (r.mats != null) foreach (var mm in r.mats) total.Mat(mm.Type, mm.count);
            }
            if (n > 0) Changed();
            return n;
        }

        /// <summary>이미 받은 우편 지우기. 반환 = 지운 수.</summary>
        public int DeleteClaimed()
        {
            int n = _d.mails.RemoveAll(m => m.claimed);
            if (n > 0) Changed();
            return n;
        }

        [ContextMenu("테스트: 시험 우편 보내기")]
        private void TestSend() => Send("시험 우편", "테스트용 우편입니다.", new RewardBundle().Crystals(100).Gold(5000));

        [ContextMenu("테스트: 환영 선물 다시 보내기")]
        private void TestWelcome() => Send("신규 지휘관 환영 선물", "다시 보낸 환영 선물입니다.", WelcomeReward());
    }
}
