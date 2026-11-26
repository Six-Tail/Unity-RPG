using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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
    ///
    /// 보안: 계정 데이터는 PC(레지스트리) · 기기에 <b>암호화(AES) + 위변조 서명(HMAC, 키 이름 포함)</b>으로 저장 — 손으로 고치거나
    /// 다른 키 값을 옮겨 붙이면 서명이 맞지 않아 무시됨(<see cref="TamperCount"/>). 옛 평문 세이브는 처음 한 번 암호화로 옮김(버전 2).
    /// 기기 설정은 평문 그대로.
    /// </summary>
    public static class SaveStore
    {
        private const string IndexKey = "__savestore.keys";   // 계정 데이터 키 목록(JSON)
        private const string OwnerKey = "__savestore.owner";  // 이 PC의 세이브 주인(클라우드 계정 id, 비면 로그인 전)
        private const string VersionKey = "__savestore.v";    // 2 = 계정 데이터 암호화 · 서명 완료

        /// <summary>기기 설정 키(서버에 올리지 않음).</summary>
        private static readonly string[] DeviceLocalPrefixes =
        {
            "__savestore.", "sound.", "display.", "battle.auto", "battle.speed", "stage.multiplier", "stage.repeat", "wallet.restore.v1",
        };

        public static bool IsDeviceLocal(string key)
        {
            foreach (var p in DeviceLocalPrefixes) if (key.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>계정 데이터가 바뀔 때(클라우드 자동 저장이 듣는 신호).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _index = null; Changed = null; _migrated = false; TamperCount = 0; }

        // ---------------- 암호화 · 서명 ----------------

        private const string Prefix = "\u00A7S1";   // 암호화된 값 표시
        private const string Probe = "\u0001__none__";
        private static byte[] _encKey, _macKey;

        /// <summary>서명이 맞지 않아 무시한 값 수(이번 실행).</summary>
        public static int TamperCount { get; private set; }

        private static void Keys()
        {
            if (_encKey != null) return;
            using var sha = SHA256.Create();
            // 실행 파일 안에 흩어 둔 조각으로 키를 만듦(손으로 고치는 것을 막는 정도의 보호)
            string a = "StellaAnchor", b = "::save::", c = "7f3c91e04b", d = "Six-Tail";
            var k1 = sha.ComputeHash(Encoding.UTF8.GetBytes(a + b + c + "enc" + d));
            var k2 = sha.ComputeHash(Encoding.UTF8.GetBytes(d + c + "mac" + b + a));
            _encKey = new byte[16]; Array.Copy(k1, _encKey, 16);
            _macKey = k2;
        }

        private static bool Secure(string key) => !IsDeviceLocal(key);

        private static string Encode(string key, string type, string value)
        {
            Keys();
            var plain = Encoding.UTF8.GetBytes(type + ":" + (value ?? ""));
            using var aes = Aes.Create();
            aes.Key = _encKey; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();
            byte[] cipher;
            using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            var mac = Mac(key, aes.IV, cipher);
            var all = new byte[16 + cipher.Length + 16];
            Array.Copy(aes.IV, 0, all, 0, 16);
            Array.Copy(cipher, 0, all, 16, cipher.Length);
            Array.Copy(mac, 0, all, 16 + cipher.Length, 16);
            return Prefix + Convert.ToBase64String(all);
        }

        private static byte[] Mac(string key, byte[] iv, byte[] cipher)
        {
            using var h = new HMACSHA256(_macKey);
            var k = Encoding.UTF8.GetBytes(key);
            var buf = new byte[k.Length + iv.Length + cipher.Length];
            Array.Copy(k, 0, buf, 0, k.Length);
            Array.Copy(iv, 0, buf, k.Length, iv.Length);
            Array.Copy(cipher, 0, buf, k.Length + iv.Length, cipher.Length);
            return h.ComputeHash(buf);
        }

        private static bool Decode(string key, string raw, out string type, out string value)
        {
            type = null; value = null;
            try
            {
                Keys();
                var all = Convert.FromBase64String(raw.Substring(Prefix.Length));
                if (all.Length < 48) return false;
                var iv = new byte[16]; Array.Copy(all, 0, iv, 0, 16);
                var cipher = new byte[all.Length - 32]; Array.Copy(all, 16, cipher, 0, cipher.Length);
                var mac = Mac(key, iv, cipher);
                int diff = 0;
                for (int i = 0; i < 16; i++) diff |= mac[i] ^ all[16 + cipher.Length + i];
                if (diff != 0) return false;
                using var aes = Aes.Create();
                aes.Key = _encKey; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                byte[] plain;
                using (var dec = aes.CreateDecryptor()) plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                string s = Encoding.UTF8.GetString(plain);
                int colon = s.IndexOf(':');
                if (colon <= 0) return false;
                type = s.Substring(0, colon); value = s.Substring(colon + 1);
                return true;
            }
            catch { return false; }
        }

        /// <summary>PlayerPrefs에 원래 종류(문자열 · 정수 · 실수)로 들어 있는 값.</summary>
        private static (string type, string value) ReadNative(string k)
        {
            string sv = PlayerPrefs.GetString(k, Probe);
            if (sv != Probe) return ("s", sv);
            int iv1 = PlayerPrefs.GetInt(k, int.MinValue), iv2 = PlayerPrefs.GetInt(k, int.MaxValue);
            if (iv1 == iv2) return ("i", iv1.ToString());
            float fv = PlayerPrefs.GetFloat(k, float.NaN);
            if (!float.IsNaN(fv)) return ("f", fv.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            return (null, null);
        }

        private static void Write(string key, string type, string value)
        {
            if (!Secure(key))
            {
                switch (type)
                {
                    case "i": PlayerPrefs.SetInt(key, int.TryParse(value, out int i) ? i : 0); break;
                    case "f": PlayerPrefs.SetFloat(key, ParseF(value)); break;
                    default: PlayerPrefs.SetString(key, value ?? ""); break;
                }
                return;
            }
            EnsureSecure();
            PlayerPrefs.SetString(key, Encode(key, type, value));
        }

        private static float ParseF(string v) =>
            float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;

        /// <summary>값 읽기(계정 데이터는 서명 확인). 없거나 위변조면 false.</summary>
        private static bool TryRead(string key, out string type, out string value)
        {
            type = null; value = null;
            if (!PlayerPrefs.HasKey(key)) return false;
            if (!Secure(key)) { (type, value) = ReadNative(key); return type != null; }
            EnsureSecure();
            string raw = PlayerPrefs.GetString(key, Probe);
            if (raw != Probe && raw.StartsWith(Prefix, StringComparison.Ordinal) && Decode(key, raw, out type, out value)) return true;
            TamperCount++;
            Debug.LogWarning($"[SaveStore] '{key}' 값이 손상 · 변조되어 무시합니다.");
            return false;
        }

        // 옛 평문 세이브 → 암호화(기기마다 처음 한 번)
        private static bool _migrated;

        private static void EnsureSecure()
        {
            if (_migrated) return;
            _migrated = true;
            if (PlayerPrefs.GetInt(VersionKey, 0) >= 2) return;
            foreach (var k in new List<string>(Index)) MigrateKey(k);
            PlayerPrefs.SetInt(VersionKey, 2);
            PlayerPrefs.Save();
        }

        private static void MigrateKey(string k)
        {
            if (!Secure(k) || !PlayerPrefs.HasKey(k)) return;
            var (t, v) = ReadNative(k);
            if (t == null || (t == "s" && v.StartsWith(Prefix, StringComparison.Ordinal))) return;
            PlayerPrefs.SetString(k, Encode(k, t, v));
        }

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

        public static string GetString(string key, string def = "")
        {
            Adopt(key);
            return TryRead(key, out _, out var v) ? v : def;
        }

        public static int GetInt(string key, int def = 0)
        {
            Adopt(key);
            return TryRead(key, out _, out var v) && int.TryParse(v, out int i) ? i : def;
        }

        public static float GetFloat(string key, float def = 0f)
        {
            Adopt(key);
            return TryRead(key, out _, out var v) ? ParseF(v) : def;
        }

        /// <summary>
        /// 옛 세이브(이 저장소 전에 쓴 키): 읽을 때 PC에 값이 있는데 목록에 없으면 목록에 넣음.
        /// 각 시스템은 시작할 때 자기 키를 읽으므로, 한 번 켜면 옛 세이브도 모두 서버에 올릴 수 있게 됩니다.
        /// </summary>
        private static void Adopt(string key)
        {
            if (IsDeviceLocal(key) || Index.Contains(key) || !PlayerPrefs.HasKey(key)) return;
            Index.Add(key);
            _indexDirty = true;
            MigrateKey(key);   // 목록에 없던 옛 키(평문)도 암호화로 옮김
        }

        public static void SetString(string key, string value) { Write(key, "s", value); Track(key); }
        public static void SetInt(string key, int value) { Write(key, "i", value.ToString()); Track(key); }
        public static void SetFloat(string key, float value) { Write(key, "f", value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Track(key); }

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
            foreach (var k in new List<string>(Index))
                if (TryRead(k, out var t, out var v)) snap.entries.Add(new Entry { k = k, t = t, v = v });   // 위변조된 값은 올리지 않음
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
                    Write(e.k, e.t == "i" || e.t == "f" ? e.t : "s", e.v ?? "");
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
                if (!string.IsNullOrEmpty(k) && !IsDeviceLocal(k) && PlayerPrefs.HasKey(k) && Index.Add(k)) { any = true; MigrateKey(k); }
            if (any) { _indexDirty = true; Save(); }
        }
    }
}
