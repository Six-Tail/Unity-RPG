using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Gacha
{
    /// <summary>
    /// 씬에 두고 쓰는 뽑기 진입점. 배너/천장 상태 보유, 세이브·로드, 1회·10연 API.
    /// 결과 연출(UI)은 OnPulled 이벤트를 구독해 붙이세요.
    /// </summary>
    public class GachaController : MonoBehaviour
    {
        [SerializeField] private GachaBanner banner;
        [SerializeField] private int multiPullCount = 10;

        public GachaPityState State { get; private set; } = new();
        public GachaBanner Banner => banner;

        /// <summary>뽑기 직후 발생. (단차도 리스트 1개로 전달)</summary>
        public event Action<List<GachaResult>> OnPulled;

        private GachaMachine _machine;

        private void Awake()
        {
            _machine = new GachaMachine();
            Load();
        }

        public List<GachaResult> PullOnce()
        {
            var results = new List<GachaResult> { _machine.Pull(banner, State) };
            Save();
            OnPulled?.Invoke(results);
            return results;
        }

        public List<GachaResult> PullMulti()
        {
            var results = _machine.PullMany(banner, State, multiPullCount);
            Save();
            OnPulled?.Invoke(results);
            return results;
        }

        // ---------------- 세이브 (간단: PlayerPrefs + JSON, 배너별 키 분리) ----------------

        private string SaveKey => $"gacha_pity_{(banner != null ? banner.name : "default")}";

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(State));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) { State = new GachaPityState(); return; }
            try { State = JsonUtility.FromJson<GachaPityState>(PlayerPrefs.GetString(SaveKey)) ?? new GachaPityState(); }
            catch { State = new GachaPityState(); }
        }

        [ContextMenu("천장 상태 초기화")]
        public void ResetPity()
        {
            State = new GachaPityState();
            Save();
        }
    }
}
