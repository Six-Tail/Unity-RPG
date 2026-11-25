using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG
{
    /// <summary>
    /// 게임 저장소(모든 세이브는 여기를 거침). 지금은 이 PC의 PlayerPrefs에 쓰고,
    /// 클라우드(Unity Gaming Services · Cloud Save)를 연결하면 <see cref="Export"/> / <see cref="Import"/>로 서버와 주고받습니다.
    ///
    /// · 계정 데이터: 캐릭터 · 재화 · 장비 · 진행도 등 — 서버에 올라가는 키. 쓴 키는 목록(색인)에 기록해 둠
    /// · 기기 설정: 소리 · 자동 전투/배속 · 보상 배수 같은 PC별 설정 — 서버에 올리지 않음(<see cref="IsDeviceLocal"/>)
    /// PlayerPrefs와 같은 이름의 함수(GetString · SetInt · HasKey · DeleteKey · Save …)라 그대로 바꿔 쓸 수 있습니다.
    /// </summary>
    public static class SaveStore
    {
        private const string IndexKey = "__savestore.keys";   // 계정 데이터 키 목록(JSON)
        private const string OwnerKey = "__savestore.owner";  // 이 PC의 세이브 주인(클라우드 계정 id, 비면 로그인 전)

        /// <summary>기기 설정 키(서버에 올리지 않음).</summary>
        private static readonly string[] DeviceLocalPrefixes =
        {
            "__savestore.", "sound.", "battle.auto", "battle.speed", "stage.multiplier", "stage.repeat", "wallet.restore.v1",
        };

        public static bool IsDeviceLocal(string key)
        {
            foreach (var p in DeviceLocalPrefixes) if (key.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>계정 데이터가 바뀔 때(클라우드 자동 저장이 듣는 신호).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _index = null; Changed = null; }

        // ---------------- 키 색인 ----------------

        [Serializable] private class IndexData { public List<string> keys = new(); }
        private static HashSet<string> _index;
        private static bool _indexDirty;

        private static HashSet<string> Index
        {
            get
            {
                if (_index != null) return _index;
                _index = new HashSet<string>();
                if (PlayerPrefs.HasKey(IndexKey))
                {
                    try
                    {
                        var d = JsonUtility.FromJson<IndexData>(PlayerPrefs.GetString(IndexKey));
                        if (d?.keys != null) foreach (var k in d.keys) _index.Add(k);
                    }
                    catch { }
                }
                return _index;
            }
        }

        private static void Track(string key)
        {
            if (IsDeviceLocal(key)) return;
            if (Index.Add(key)) _indexDirty = true;
            Changed?.Invoke();
        }

        private static void Untrack(string key)
        {
            if (IsDeviceLocal(key)) return;
            if (Index.Remove(key)) _indexDirty = true;
            Changed?.Invoke();
        }

        private static void FlushIndex()
        {
            if (!_indexDirty) return;
            _indexDirty = false;
            PlayerPrefs.SetString(IndexKey, JsonUtility.ToJson(new IndexData { keys = new List<string>(Index) }));
        }

        // ---------------- PlayerPrefs와 같은 함수 ----------------

        public static bool HasKey(string key) { bool has = PlayerPrefs.HasKey(key); if (has) Adopt(key); return has; }

        public static string GetString(string key, string def = "") { Adopt(key); return PlayerPrefs.GetString(key, def); }
        public static int GetInt(string key, int def = 0) { Adopt(key); return PlayerPrefs.GetInt(key, def); }
        public static float GetFloat(string key, float def = 0f) { Adopt(key); return PlayerPrefs.GetFloat(key, def); }

        /// <summary>
        /// 옛 세이브(이 저장소 전에 쓴 키): 읽을 때 PC에 값이 있는데 목록에 없으면 목록에 넣음.
        /// 각 시스템은 시작할 때 자기 키를 읽으므로, 한 번 켜면 옛 세이브도 모두 서버에 올릴 수 있게 됩니다.
        /// </summary>
        private static void Adopt(string key)
        {
            if (IsDeviceLocal(key) || Index.Contains(key) || !PlayerPrefs.HasKey(key)) return;
            Index.Add(key);
            _indexDirty = true;
        }

        public static void SetString(string key, string value) { PlayerPrefs.SetString(key, value); Track(key); }
        public static void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); Track(key); }
        public static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(key, value); Track(key); }

        public static void DeleteKey(string key) { PlayerPrefs.DeleteKey(key); Untrack(key); }

        public static void Save()
        {
            FlushIndex();
            PlayerPrefs.Save();
        }

        // ---------------- 클라우드 연결용 ----------------

        /// <summary>저장된 값 1개(종류 보존: s 문자열 · i 정수 · f 실수).</summary>
        [Serializable]
        public class Entry
        {
            public string k, t, v;
        }

        [Serializable]
        public class Snapshot
        {
            public int version = 1;
            public long savedAt;           // DateTime.UtcNow.Ticks
            public List<Entry> entries = new();
        }

        /// <summary>
        /// 계정 데이터 전체를 한 덩어리로(서버에 올릴 때). 종류는 값으로 판별: 정수로 읽히면 i, 실수면 f, 그 밖에는 s.
        /// PlayerPrefs는 종류를 알려 주지 않으므로, 같은 키를 다른 기본값으로 두 번 읽어 판별합니다.
        /// </summary>
        public static Snapshot Export()
        {
            var snap = new Snapshot { savedAt = DateTime.UtcNow.Ticks };
            foreach (var k in Index)
            {
                if (!PlayerPrefs.HasKey(k)) continue;
                // 정수/실수로 저장된 키는 GetString이 기본값을 돌려줌
                const string probe = "\u0001__none__";
                string sv = PlayerPrefs.GetString(k, probe);
                if (sv != probe) { snap.entries.Add(new Entry { k = k, t = "s", v = sv }); continue; }
                int iv1 = PlayerPrefs.GetInt(k, int.MinValue), iv2 = PlayerPrefs.GetInt(k, int.MaxValue);
                if (iv1 == iv2) { snap.entries.Add(new Entry { k = k, t = "i", v = iv1.ToString() }); continue; }
                float fv = PlayerPrefs.GetFloat(k, float.NaN);
                if (!float.IsNaN(fv)) snap.entries.Add(new Entry { k = k, t = "f", v = fv.ToString("R", System.Globalization.CultureInfo.InvariantCulture) });
            }
            return snap;
        }

        /// <summary>
        /// 서버에서 받은 계정 데이터로 이 PC의 계정 데이터를 통째로 바꿈(기기 설정은 그대로).
        /// 바꾼 뒤에는 게임 전체를 다시 불러와야 합니다(각 시스템이 처음 한 번만 읽기 때문).
        /// </summary>
        public static void Import(Snapshot snap)
        {
            ClearAccountData();
            if (snap?.entries != null)
                foreach (var e in snap.entries)
                {
                    if (string.IsNullOrEmpty(e.k) || IsDeviceLocal(e.k)) continue;
                    switch (e.t)
                    {
                        case "i": PlayerPrefs.SetInt(e.k, int.TryParse(e.v, out int i) ? i : 0); break;
                        case "f": PlayerPrefs.SetFloat(e.k, float.TryParse(e.v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f); break;
                        default: PlayerPrefs.SetString(e.k, e.v ?? ""); break;
                    }
                    Index.Add(e.k);
                }
            _indexDirty = true;
            Save();
        }

        /// <summary>
        /// <see cref="Import"/> 뒤 게임 전체 다시 불러오기: 지금 씬을 다시 열면 각 시스템이 새 세이브를 읽음
        /// (씬을 넘어 남는 것은 소리 관리자뿐이고, 소리는 기기 설정이라 그대로 둬도 됨).
        /// </summary>
        public static void ReloadGame()
        {
            Time.timeScale = 1f;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        /// <summary>계정 데이터만 모두 지움(기기 설정은 남김).</summary>
        public static void ClearAccountData()
        {
            foreach (var k in new List<string>(Index)) PlayerPrefs.DeleteKey(k);
            Index.Clear();
            _indexDirty = true;
            Save();
        }

        /// <summary>이 PC 세이브의 주인(클라우드 계정 id). 로그인 전이면 "".</summary>
        public static string Owner
        {
            get => PlayerPrefs.GetString(OwnerKey, "");
            set { PlayerPrefs.SetString(OwnerKey, value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>계정 데이터 키 수(확인용).</summary>
        public static int Count => Index.Count;

        /// <summary>
        /// 이 저장소를 쓰기 전의 옛 세이브: 색인이 비어 있으면, 알려진 계정 데이터 키를 찾아 색인에 넣음(한 번만).
        /// PlayerPrefs는 키 목록을 줄 수 없어서, 게임이 쓰는 키 이름을 직접 나열합니다.
        /// </summary>
        public static void AdoptLegacyKeys(IEnumerable<string> knownKeys)
        {
            if (knownKeys == null) return;
            bool any = false;
            foreach (var k in knownKeys)
                if (!string.IsNullOrEmpty(k) && !IsDeviceLocal(k) && PlayerPrefs.HasKey(k) && Index.Add(k)) any = true;
            if (any) { _indexDirty = true; Save(); }
        }
    }
}
