using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 플레이어(지휘관) 프로필: 이름 · 인사말 · 칭호 · 테두리 · 아바타 · 레벨/경험치 · UID. PlayerPrefs 저장.
    /// 씬에 없어도 처음 쓸 때 스스로 만들어집니다(PlayerProfile.Instance).
    /// 지휘관 경험치는 스테이지를 클리어할 때 쓴 행동력 × 10.
    /// </summary>
    public class PlayerProfile : MonoBehaviour
    {
        public const int MaxLevel = 100;
        public const int MaxNameLength = 10, MaxGreetingLength = 30;
        public const int ExpPerStamina = 10;
        private const string SaveKey = "profile";

        private static PlayerProfile _instance;

        public static PlayerProfile Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerProfile>();
                if (_instance == null) _instance = new GameObject("PlayerProfile").AddComponent<PlayerProfile>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        [Serializable]
        private class SaveData
        {
            public string name = "", greeting = "", titleId = ProfileRewards.DefaultTitle, frameId = ProfileRewards.DefaultFrame, avatarId = "";
            public int level = 1, exp;
            public string uid = "";
        }

        private SaveData _d = new();
        private bool _loaded;

        public event Action OnChanged;

        public string Name => _d.name;
        public bool HasName => !string.IsNullOrWhiteSpace(_d.name);
        public string DisplayName => HasName ? _d.name : "지휘관";
        public string Greeting => _d.greeting;
        public string TitleId => _d.titleId;
        public string FrameId => _d.frameId;
        public string AvatarId => _d.avatarId;
        public int Level => _d.level;
        public int Exp => _d.exp;
        public string Uid => _d.uid;

        /// <summary>level → level+1 에 필요한 경험치.</summary>
        public static int ExpForNext(int level) => level >= MaxLevel ? int.MaxValue : 200 + 100 * Mathf.Max(1, level);

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (PlayerPrefs.HasKey(SaveKey))
            {
                try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
                catch { _d = new SaveData(); }
            }
            _d.level = Mathf.Clamp(_d.level, 1, MaxLevel);
            if (string.IsNullOrEmpty(_d.titleId)) _d.titleId = ProfileRewards.DefaultTitle;
            if (string.IsNullOrEmpty(_d.frameId)) _d.frameId = ProfileRewards.DefaultFrame;
            if (string.IsNullOrEmpty(_d.uid))
            {
                // 8자리 UID(한 번만 생성)
                _d.uid = UnityEngine.Random.Range(10000000, 99999999).ToString();
                Save();
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        /// <summary>이름 정리(앞뒤 공백 제거 · 길이 제한). 쓸 수 없는 이름이면 null.</summary>
        public static string CleanName(string raw)
        {
            if (raw == null) return null;
            string s = raw.Trim().Replace("\n", "").Replace("\r", "").Replace("<", "").Replace(">", "");
            if (s.Length < 2) return null;
            return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength) : s;
        }

        public bool SetName(string raw)
        {
            string s = CleanName(raw);
            if (s == null) return false;
            _d.name = s;
            Changed();
            return true;
        }

        public void SetGreeting(string raw)
        {
            string s = (raw ?? "").Replace("\n", " ").Replace("\r", "").Replace("<", "").Replace(">", "").Trim();
            _d.greeting = s.Length > MaxGreetingLength ? s.Substring(0, MaxGreetingLength) : s;
            Changed();
        }

        public void SetTitle(string id) { _d.titleId = id; Changed(); }
        public void SetFrame(string id) { _d.frameId = id; Changed(); }
        public void SetAvatar(CharacterData c) { _d.avatarId = c != null ? (!string.IsNullOrEmpty(c.id) ? c.id : c.name) : ""; Changed(); }

        /// <summary>지휘관 경험치 추가. 오른 레벨 수를 돌려줌.</summary>
        public int AddExp(int amount)
        {
            if (amount <= 0 || _d.level >= MaxLevel) return 0;
            int gained = 0;
            _d.exp += amount;
            while (_d.level < MaxLevel && _d.exp >= ExpForNext(_d.level))
            {
                _d.exp -= ExpForNext(_d.level);
                _d.level++;
                gained++;
            }
            if (_d.level >= MaxLevel) _d.exp = 0;
            Changed();
            return gained;
        }

        [ContextMenu("프로필 초기화(이름 다시 정하기)")]
        private void ResetProfile()
        {
            string uid = _d.uid;
            _d = new SaveData { uid = uid };
            Changed();
        }
    }

    /// <summary>칭호 · 테두리의 해금 판정에 쓰는 현재 진행 상황.</summary>
    public struct ProfileContext
    {
        public int level;
        public int chaptersCleared;   // 쉬움 난이도에서 연속으로 클리어한 마지막 장
        public int owned;             // 보유 캐릭터 수
        public int arenaBest;         // 결투장 역대 최고 점수
        public int arenaRankBits;     // 결투장 시즌 순위 테두리(비트: 1위 · 2위 · 3위)

        public static ProfileContext Capture()
        {
            var ctx = new ProfileContext { level = PlayerProfile.Instance.Level };
            ctx.arenaBest = PlayerArena.Instance.BestPoints;
            ctx.arenaRankBits = PlayerArena.Instance.RankFrameBits;
            var roster = UnityEngine.Object.FindAnyObjectByType<PlayerRoster>();
            if (roster != null) ctx.owned = roster.Owned.Count;
            var progress = UnityEngine.Object.FindAnyObjectByType<StageProgress>();
            var screen = UnityEngine.Object.FindAnyObjectByType<SubcultureRPG.Meta.UI.StageScreen>();
            var db = screen != null ? screen.Database : null;
            if (progress != null && db != null)
                foreach (int ch in db.Chapters(StageDifficulty.Easy))
                {
                    if (!progress.IsChapterCleared(db, ch)) break;
                    ctx.chaptersCleared = ch;
                }
            return ctx;
        }
    }

    /// <summary>칭호 또는 테두리 1개.</summary>
    public class ProfileReward
    {
        public string id, name, condition;
        public Color color = Color.white;          // 칭호 글자색 / 테두리 그림이 없을 때 고리 색
        public Func<ProfileContext, bool> unlocked;
    }

    /// <summary>칭호 · 테두리 목록(튜닝 지점). id는 세이브 키이므로 바꾸지 마세요.</summary>
    public static class ProfileRewards
    {
        public const string DefaultTitle = "t_new", DefaultFrame = "f_basic";

        private static ProfileReward R(string id, string name, string condition, Color color, Func<ProfileContext, bool> ok) =>
            new() { id = id, name = name, condition = condition, color = color, unlocked = ok };

        private static readonly Color Gray = new(0.78f, 0.8f, 0.86f), Green = new(0.55f, 0.9f, 0.6f), Blue = new(0.5f, 0.75f, 1f),
            Purple = new(0.78f, 0.55f, 1f), Gold = new(1f, 0.83f, 0.42f), Red = new(1f, 0.55f, 0.4f), Rainbow = new(1f, 0.75f, 0.95f);

        public static readonly List<ProfileReward> Titles = new()
        {
            R("t_new", "신임 지휘관", "기본 칭호", Gray, c => true),
            R("t_ch1", "그림자 숲의 개척자", "1장 클리어", Green, c => c.chaptersCleared >= 1),
            R("t_ch3", "협곡의 불씨", "3장 클리어", Red, c => c.chaptersCleared >= 3),
            R("t_ch5", "뇌명을 넘은 자", "5장 클리어", Purple, c => c.chaptersCleared >= 5),
            R("t_ch7", "일곱 사슬의 목격자", "7장 클리어", Blue, c => c.chaptersCleared >= 7),
            R("t_ch10", "빙원의 정복자", "10장 클리어", Blue, c => c.chaptersCleared >= 10),
            R("t_ch13", "진실을 본 자", "13장 클리어", Purple, c => c.chaptersCleared >= 13),
            R("t_ch15", "닻을 든 지휘관", "15장 클리어", Gold, c => c.chaptersCleared >= 15),
            R("t_col10", "수집가", "캐릭터 10명 보유", Green, c => c.owned >= 10),
            R("t_col25", "별의 인도자", "캐릭터 25명 보유", Rainbow, c => c.owned >= 25),
            R("t_lv30", "노련한 지휘관", "지휘관 Lv.30", Gold, c => c.level >= 30),
        };

        public static readonly List<ProfileReward> Frames = new()
        {
            R("f_basic", "기본 테두리", "기본 테두리", Gray, c => true),
            R("f_bronze", "청동 테두리", "지휘관 Lv.5", new Color(0.8f, 0.55f, 0.35f), c => c.level >= 5),
            R("f_silver", "은빛 테두리", "지휘관 Lv.15", new Color(0.85f, 0.9f, 1f), c => c.level >= 15),
            R("f_gold", "황금 테두리", "지휘관 Lv.30", Gold, c => c.level >= 30),
            R("f_flame", "홍염 테두리", "3장 클리어", Red, c => c.chaptersCleared >= 3),
            R("f_frost", "서리 테두리", "7장 클리어", Blue, c => c.chaptersCleared >= 7),
            R("f_star", "별무리 테두리", "15장 클리어", Rainbow, c => c.chaptersCleared >= 15),
            // 결투장: 처음 그 티어에 오르면 지급
            R("fa_silver", "결투장 실버", "결투장 실버 달성", PlayerArena.Tiers[1].color, c => c.arenaBest >= PlayerArena.Tiers[1].min),
            R("fa_gold", "결투장 골드", "결투장 골드 달성", PlayerArena.Tiers[2].color, c => c.arenaBest >= PlayerArena.Tiers[2].min),
            R("fa_platinum", "결투장 플래티넘", "결투장 플래티넘 달성", PlayerArena.Tiers[3].color, c => c.arenaBest >= PlayerArena.Tiers[3].min),
            R("fa_diamond", "결투장 다이아", "결투장 다이아 달성", PlayerArena.Tiers[4].color, c => c.arenaBest >= PlayerArena.Tiers[4].min),
            R("fa_nebula", "결투장 성운", "결투장 성운 달성", PlayerArena.Tiers[5].color, c => c.arenaBest >= PlayerArena.Tiers[5].min),
            R("fa_supernova", "결투장 초신성", "결투장 초신성 달성", PlayerArena.Tiers[6].color, c => c.arenaBest >= PlayerArena.Tiers[6].min),
            R("fa_anchor", "결투장 앵커", "결투장 앵커 달성", PlayerArena.Tiers[7].color, c => c.arenaBest >= PlayerArena.Tiers[7].min),
            // 결투장 시즌 순위 한정
            R("fr_1", "시즌 챔피언", "시즌 1위 (한정)", Gold, c => (c.arenaRankBits & 1) != 0),
            R("fr_2", "시즌 2위", "시즌 2위 (한정)", new Color(0.85f, 0.9f, 1f), c => (c.arenaRankBits & 2) != 0),
            R("fr_3", "시즌 3위", "시즌 3위 (한정)", new Color(0.85f, 0.6f, 0.4f), c => (c.arenaRankBits & 4) != 0),
        };

        public static ProfileReward Title(string id) => Titles.Find(t => t.id == id) ?? Titles[0];
        public static ProfileReward Frame(string id) => Frames.Find(f => f.id == id) ?? Frames[0];
    }
}
