using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Battle;
using SubcultureRPG.Gacha;
using SubcultureRPG.Meta;
using SubcultureRPG.Meta.Missions;

namespace SubcultureRPG
{
    /// <summary>
    /// 효과음 종류 — 파일 이름은 Resources/Audio/SFX/{소문자 이름}.wav (예: Sfx.Click → click.wav).
    /// 같은 이름에 _2 · _3 · _4 를 붙인 변형 파일이 있으면 그중 하나를 골라 냄(직전과 다른 것) — 전투 소리가 매번 똑같지 않게.
    /// Resources/Audio/SFX/Lab/ 에 같은 이름의 소리가 있으면 그쪽을 먼저 씀(효과음 라보 — 재배포 금지라 저장소에 없고 각 PC에서 받음).
    /// </summary>
    public enum Sfx
    {
        Click, Confirm, Cancel, Reward, Coin, LevelUp, Error,
        GachaSummon, GachaRare,
        Hit, Crit, Heal, Skill, Ultimate, Break, Defeated,
        Victory, Defeat,
    }

    /// <summary>
    /// 소리: 화면별 BGM(두 채널 크로스페이드) + 효과음 + 볼륨(PlayerPrefs). 씬에 없어도 시작할 때 스스로 생김.
    ///
    ///   Resources/Audio/BGM/{키}   — home · stage · gacha · battle · boss · arena · event · menu (없으면 menu → home 순으로 대체, 그것도 없으면 조용히)
    ///   Resources/Audio/SFX/{이름}  — Sfx 열거형의 소문자 이름
    ///
    /// 파일만 넣으면 연결됩니다. 문서: docs/sound.md
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        private const string BgmKey = "sound.bgm", SfxKey = "sound.sfx", MuteKey = "sound.mute";
        private const float FadeTime = 0.8f;
        private const int SfxVoices = 8;
        private const int MaxVariants = 4;   // {이름} · {이름}_2 … {이름}_4

        private static SoundManager _instance;
        public static SoundManager Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<SoundManager>();
                if (_instance == null)
                {
                    var go = new GameObject("SoundManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SoundManager>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => _ = Instance;

        // ---------------- 볼륨 ----------------

        public static float BgmVolume
        {
            get => SaveStore.GetFloat(BgmKey, 0.6f);
            set { SaveStore.SetFloat(BgmKey, Mathf.Clamp01(value)); SaveStore.Save(); if (_instance != null) _instance.ApplyBgmVolume(); }
        }

        public static float SfxVolume
        {
            get => SaveStore.GetFloat(SfxKey, 0.8f);
            set { SaveStore.SetFloat(SfxKey, Mathf.Clamp01(value)); SaveStore.Save(); }
        }

        /// <summary>전체 음소거.</summary>
        public static bool Muted
        {
            get => SaveStore.GetInt(MuteKey, 0) == 1;
            set { SaveStore.SetInt(MuteKey, value ? 1 : 0); SaveStore.Save(); if (_instance != null) _instance.ApplyBgmVolume(); }
        }

        // ---------------- 재생 ----------------

        private readonly Dictionary<string, AudioClip> _cache = new();
        private AudioSource[] _bgm;
        private int _bgmActive;
        private string _bgmKey;
        private float _bgmLevel = 1f;   // 크로스페이드 중 채널 비율
        private AudioSource[] _voices;
        private int _nextVoice;
        private Coroutine _fade;
        private readonly Dictionary<Sfx, float> _lastPlayed = new();
        private readonly Dictionary<Sfx, AudioClip[]> _variants = new();
        private readonly Dictionary<Sfx, int> _lastVariant = new();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _bgm = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                _bgm[i] = gameObject.AddComponent<AudioSource>();
                _bgm[i].loop = true; _bgm[i].playOnAwake = false; _bgm[i].volume = 0f;
            }
            _voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
        }

        private void Start()
        {
            HookGame();
            // 클라우드 세이브를 받아 씬을 다시 열면 전투 · 가챠가 새로 생기므로 다시 연결(이 관리자만 씬을 넘어 남음)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single) return;
            UnhookGame();
            HookGame();
        }

        private AudioClip Load(string path)
        {
            if (_cache.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>(path);
            _cache[path] = c;
            return c;
        }

        /// <summary>효과음. 같은 소리를 아주 짧은 간격으로 겹쳐 내지 않음(다단 히트가 한꺼번에 울리지 않게).</summary>
        public static void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            var m = Instance;
            if (Muted || SfxVolume <= 0f) return;
            float now = Time.unscaledTime;
            if (m._lastPlayed.TryGetValue(s, out var last) && now - last < 0.05f) return;
            m._lastPlayed[s] = now;
            var clip = m.Pick(s);
            if (clip == null) return;
            var v = m._voices[m._nextVoice];
            m._nextVoice = (m._nextVoice + 1) % SfxVoices;
            v.pitch = pitch;
            v.PlayOneShot(clip, Mathf.Clamp01(volume) * SfxVolume);
        }

        /// <summary>그 소리의 변형 중 하나(직전에 낸 것은 피함). 변형이 없으면 기본 파일.</summary>
        private AudioClip Pick(Sfx s)
        {
            if (!_variants.TryGetValue(s, out var list))
            {
                string lower = s.ToString().ToLowerInvariant();
                list = LoadVariants("Audio/SFX/Lab/" + lower);                // 이 PC에 받아 둔 소리(있으면 우선)
                if (list.Length == 0) list = LoadVariants("Audio/SFX/" + lower);
                _variants[s] = list;
            }
            if (list.Length == 0) return null;
            if (list.Length == 1) return list[0];
            int last = _lastVariant.TryGetValue(s, out var l) ? l : -1;
            int pick = Random.Range(0, list.Length - (last >= 0 ? 1 : 0));
            if (last >= 0 && pick >= last) pick++;
            _lastVariant[s] = pick;
            return list[pick];
        }

        /// <summary>{name} · {name}_2 … {name}_4 중 있는 것.</summary>
        private AudioClip[] LoadVariants(string name)
        {
            var found = new List<AudioClip>();
            var first = Load(name);
            if (first != null) found.Add(first);
            for (int i = 2; i <= MaxVariants; i++)
            {
                var v = Load(name + "_" + i);
                if (v != null) found.Add(v);
            }
            return found.ToArray();
        }

        /// <summary>잠시 뒤 효과음.</summary>
        public static void PlayDelayed(Sfx s, float delay, float volume = 1f) => Instance.StartCoroutine(Instance.Later(s, delay, volume));

        private IEnumerator Later(Sfx s, float delay, float volume)
        {
            yield return new WaitForSecondsRealtime(delay);
            Play(s, volume);
        }

        /// <summary>BGM 바꾸기(같은 곡이면 그대로). 파일이 없으면 menu → home 순으로 대체.</summary>
        public static void Bgm(string key)
        {
            var m = Instance;
            AudioClip clip = null;
            string used = key;
            foreach (var k in new[] { key, "menu", "home" })
            {
                if (string.IsNullOrEmpty(k)) continue;
                clip = m.Load("Audio/BGM/" + k);
                if (clip != null) { used = k; break; }
            }
            if (clip == null) used = null;
            if (used == m._bgmKey && (clip == null || m._bgm[m._bgmActive].clip == clip)) return;
            m._bgmKey = used;
            if (m._fade != null) m.StopCoroutine(m._fade);
            m._fade = m.StartCoroutine(m.CrossFade(clip));
        }

        private IEnumerator CrossFade(AudioClip clip)
        {
            var from = _bgm[_bgmActive];
            int next = 1 - _bgmActive;
            var to = _bgm[next];
            if (clip != null)
            {
                to.clip = clip;
                to.volume = 0f;
                to.Play();
            }
            _bgmActive = next;
            float fromStart = from.volume;
            for (float t = 0; t < FadeTime; t += Time.unscaledDeltaTime)
            {
                float k = t / FadeTime;
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (clip != null) to.volume = k * TargetBgmVolume;
                yield return null;
            }
            from.volume = 0f; from.Stop(); from.clip = null;
            if (clip != null) to.volume = TargetBgmVolume;
            _fade = null;
        }

        private float TargetBgmVolume => Muted ? 0f : BgmVolume * 0.55f * _bgmLevel;

        private void ApplyBgmVolume()
        {
            if (_bgm == null || _fade != null) return;
            _bgm[_bgmActive].volume = TargetBgmVolume;
        }

        /// <summary>BGM을 잠시 작게(승리 · 패배 효과음이 묻히지 않게). 1 = 원래대로.</summary>
        public static void Duck(float level)
        {
            var m = Instance;
            m._bgmLevel = Mathf.Clamp01(level);
            m.ApplyBgmVolume();
        }

        // ---------------- 게임 이벤트 연결 ----------------

        private BattleController _battle;
        private GachaController _gacha;

        private void HookGame()
        {
            PlayerMissions.Counted -= OnCounted;
            PlayerMissions.Counted += OnCounted;
            _battle = FindAnyObjectByType<BattleController>();
            if (_battle != null)
            {
                _battle.OnBattleStart += OnBattleStart;
                _battle.OnActionStarting += OnActionStarting;
                _battle.OnActionResolved += OnActionResolved;
                _battle.OnBattleEnd += OnBattleEnd;
            }
            _gacha = FindAnyObjectByType<GachaController>();
            if (_gacha != null) _gacha.OnPulled += OnPulled;
        }

        private void UnhookGame()
        {
            PlayerMissions.Counted -= OnCounted;
            if (_gacha != null) _gacha.OnPulled -= OnPulled;
            if (_battle != null)
            {
                _battle.OnBattleStart -= OnBattleStart;
                _battle.OnActionStarting -= OnActionStarting;
                _battle.OnActionResolved -= OnActionResolved;
                _battle.OnBattleEnd -= OnBattleEnd;
            }
            _gacha = null;
            _battle = null;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookGame();
        }

        private static void OnBattleStart(BattleEngine _) => Duck(1f);

        private static void OnCounted(MissionGoal goal, int amount)
        {
            if (goal == MissionGoal.Growth) Play(Sfx.LevelUp, 0.9f);       // 레벨업 · 돌파 · 스킬 레벨업
            else if (goal == MissionGoal.ShopBuy) Play(Sfx.Coin, 0.9f);
        }

        private void OnActionStarting(BattleUnit actor, BattleSkill skill, BattleUnit target)
        {
            if (skill == null) return;
            if (skill.IsUltimate) Play(Sfx.Ultimate);
            else if (skill.category == SkillCategory.Skill) Play(Sfx.Skill, 0.8f);
        }

        private void OnActionResolved(ActionResult r)
        {
            if (r.hits == null) return;
            bool hit = false, crit = false, heal = false, broke = false, down = false;
            foreach (var h in r.hits)
            {
                if (h.healed > 0) heal = true;
                if (h.totalDamage > 0) hit = true;
                if (h.anyCrit) crit = true;
                if (h.broke) broke = true;
                if (h.defeated) down = true;
            }
            if (heal) Play(Sfx.Heal, 0.8f);
            if (crit) Play(Sfx.Crit, 0.9f, Random.Range(0.96f, 1.04f));
            else if (hit) Play(Sfx.Hit, 0.85f, Random.Range(0.92f, 1.08f));
            if (broke) Play(Sfx.Break, 0.9f);
            if (down) PlayDelayed(Sfx.Defeated, 0.12f, 0.8f);
        }

        private void OnBattleEnd(Team winner)
        {
            Duck(0.25f);
            Play(winner == Team.Ally ? Sfx.Victory : Sfx.Defeat);
            Instance.StartCoroutine(UnDuck(3.5f));
        }

        private IEnumerator UnDuck(float after)
        {
            yield return new WaitForSecondsRealtime(after);
            Duck(1f);
        }

        private void OnPulled(List<GachaResult> results)
        {
            Play(Sfx.GachaSummon);
            if (results == null) return;
            foreach (var r in results)
                if (r.rarity == Characters.Rarity.R6) { PlayDelayed(Sfx.GachaRare, 1.1f); break; }
        }
    }
}
