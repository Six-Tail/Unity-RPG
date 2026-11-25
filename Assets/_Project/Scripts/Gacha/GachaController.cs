using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Gacha
{
    /// <summary>
    /// 씬에 두고 쓰는 뽑기 진입점. 여러 배너(픽업 · 상시) 중 선택한 배너로 1회·10연을 뽑습니다.
    /// 천장 상태는 배너 종류별 묶음(픽업끼리 공유 · 상시 따로)으로 세이브·로드.
    /// 결과 연출(UI)은 OnPulled 이벤트를 구독해 붙이세요.
    /// </summary>
    public class GachaController : MonoBehaviour
    {
        [SerializeField, Tooltip("단일 배너(예전 씬 호환). banners가 비어 있으면 이것만 사용.")]
        private GachaBanner banner;
        [SerializeField, Tooltip("뽑기 화면에 표시할 배너들(픽업 · 상시). 첫 번째가 기본 선택.")]
        private List<GachaBanner> banners = new();
        [SerializeField] private int multiPullCount = 10;

        private readonly List<GachaBanner> _list = new();
        private readonly Dictionary<string, GachaPityState> _states = new();
        private GachaBanner _current;
        private GachaMachine _machine;
        private bool _inited;

        public IReadOnlyList<GachaBanner> Banners { get { Init(); return _list; } }
        public GachaBanner Banner { get { Init(); return _current; } }
        public int MultiPullCount => multiPullCount;

        /// <summary>현재 배너의 천장 상태.</summary>
        public GachaPityState State => StateOf(Banner);

        /// <summary>뽑기 직후 발생. (단차도 리스트 1개로 전달)</summary>
        public event Action<List<GachaResult>> OnPulled;

        /// <summary>선택 배너가 바뀔 때.</summary>
        public event Action OnBannerChanged;

        private void Awake() => Init();

        /// <summary>다른 컴포넌트가 먼저 조회해도 되도록 지연 초기화.</summary>
        private void Init()
        {
            if (_inited) return;
            _inited = true;
            _machine = new GachaMachine();
            _list.Clear();
            if (banners != null)
                foreach (var b in banners) if (b != null && !_list.Contains(b)) _list.Add(b);
            if (_list.Count == 0 && banner != null) _list.Add(banner);
            _current = _list.Count > 0 ? _list[0] : null;
        }

        /// <summary>종류별 배너 목록.</summary>
        public List<GachaBanner> BannersOf(GachaBannerType type)
        {
            var l = new List<GachaBanner>();
            foreach (var b in Banners) if (b.type == type) l.Add(b);
            return l;
        }

        /// <summary>대표 픽업 배너(홈 화면 표시용). 없으면 현재 배너.</summary>
        public GachaBanner FeaturedBanner
        {
            get
            {
                foreach (var b in Banners) if (b.IsPickup && b.featured6 != null) return b;
                return Banner;
            }
        }

        public void Select(GachaBanner b)
        {
            Init();
            if (b == null || b == _current || !_list.Contains(b)) return;
            _current = b;
            OnBannerChanged?.Invoke();
        }

        public List<GachaResult> PullOnce()
        {
            Init();
            if (_current == null) return new List<GachaResult>();
            var state = State;
            var results = new List<GachaResult> { _machine.Pull(_current, state) };
            Save(_current);
            OnPulled?.Invoke(results);
            return results;
        }

        public List<GachaResult> PullMulti()
        {
            Init();
            if (_current == null) return new List<GachaResult>();
            var results = _machine.PullMany(_current, State, multiPullCount);
            Save(_current);
            OnPulled?.Invoke(results);
            return results;
        }

        // ---------------- 세이브 (PlayerPrefs + JSON, 천장 묶음별 키) ----------------

        private static string KeyOf(GachaBanner b) => $"gacha_pity_{(b != null ? b.PityGroup : "default")}";

        public GachaPityState StateOf(GachaBanner b)
        {
            if (b == null) return new GachaPityState();
            if (_states.TryGetValue(b.PityGroup, out var s)) return s;
            s = LoadState(b);
            _states[b.PityGroup] = s;
            return s;
        }

        private static GachaPityState LoadState(GachaBanner b)
        {
            string key = KeyOf(b);
            // 배너 종류 도입 전 세이브(배너 에셋 이름별 키) → 픽업 천장으로 이어받기
            if (!SaveStore.HasKey(key) && b.IsPickup && SaveStore.HasKey($"gacha_pity_{b.name}"))
                key = $"gacha_pity_{b.name}";
            if (!SaveStore.HasKey(key)) return new GachaPityState();
            try { return JsonUtility.FromJson<GachaPityState>(SaveStore.GetString(key)) ?? new GachaPityState(); }
            catch { return new GachaPityState(); }
        }

        private void Save(GachaBanner b)
        {
            if (b == null) return;
            SaveStore.SetString(KeyOf(b), JsonUtility.ToJson(StateOf(b)));
            SaveStore.Save();
        }

        public void Save() => Save(_current);

        /// <summary>현재 배너 묶음의 천장 상태를 다시 읽음.</summary>
        public void Load()
        {
            if (_current == null) return;
            _states[_current.PityGroup] = LoadState(_current);
        }

        [ContextMenu("천장 상태 초기화 (전체)")]
        public void ResetPity()
        {
            foreach (var g in new[] { "pickup", "standard" })
            {
                _states[g] = new GachaPityState();
                SaveStore.SetString($"gacha_pity_{g}", JsonUtility.ToJson(_states[g]));
            }
            SaveStore.Save();
        }
    }
}
