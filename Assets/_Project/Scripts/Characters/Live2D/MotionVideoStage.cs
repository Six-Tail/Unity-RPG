using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 움직이는 일러스트(반복 영상) 무대. 캐릭터 일러스트로 만든 짧은 반복 영상을 RenderTexture 에 재생해 UI(RawImage)에 표시합니다.
    /// Live2D 모델이 없는 캐릭터도 "살아 있는" 일러스트를 가질 수 있게 하는 대안입니다(모델이 있으면 Live2D 가 우선).
    ///
    ///   Assets/_Project/Resources/Art/Motion/{캐릭터 id}.mp4   ← 이 이름으로 넣으면 자동 연결
    ///
    /// 영상은 처음과 끝이 이어지게(앞으로 → 되감기) 만들어 둔 것을 그대로 반복 재생합니다.
    /// 만드는 법: tools/art/generate-motion.js (docs/live2d.md).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Characters/Motion Video Stage")]
    public class MotionVideoStage : MonoBehaviour
    {
        public const string ResourceFolder = "Art/Motion/";

        private static MotionVideoStage _instance;
        private static readonly Dictionary<string, VideoClip> Cache = new();

        private VideoPlayer _player;
        private RenderTexture _rt;
        private CharacterData _current;
        private float _idleSince = -1f;
        private readonly List<RawImage> _users = new();

        /// <summary>무대(없으면 만듦).</summary>
        public static MotionVideoStage Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<MotionVideoStage>();
                if (_instance == null) _instance = new GameObject("MotionVideoStage").AddComponent<MotionVideoStage>();
                return _instance;
            }
        }

        /// <summary>이 캐릭터의 반복 영상(Resources/Art/Motion/{id}). 없으면 null.</summary>
        /// <param name="baseOutfit">true = 입은 의상과 관계없이 기본 의상 영상(뽑기 화면처럼 '아직 내 것이 아닌' 캐릭터를 보여 줄 때)</param>
        public static VideoClip ClipOf(CharacterData c, bool baseOutfit = false)
        {
            if (c == null) return null;
            string key = !string.IsNullOrEmpty(c.id) ? c.id : c.name;
            string sfx = baseOutfit ? "" : CharacterOutfits.SuffixOf(key);
            if (sfx != "")
            {
                var o = Load(key + sfx);   // 입고 있는 의상(개화 등)의 영상
                if (o != null) return o;
                // 그 의상 영상이 없는데 의상 일러스트가 있으면, 기본 의상 영상 대신 정지 그림(옷이 다르게 보이지 않게)
                if (Resources.Load<Sprite>("Art/Characters/" + key + sfx) != null) return null;
            }
            return Load(key);
        }

        private static VideoClip Load(string key)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var clip = Resources.Load<VideoClip>(ResourceFolder + key);
            Cache[key] = clip;
            return clip;
        }

        public static bool Has(CharacterData c, bool baseOutfit = false) => ClipOf(c, baseOutfit) != null;

        /// <summary>에디터에서 영상을 새로 넣은 뒤 다시 찾게 할 때.</summary>
        public static void ClearCache() => Cache.Clear();

        public CharacterData Current => _current;
        public float Aspect => _rt != null && _rt.height > 0 ? (float)_rt.width / _rt.height : 0.75f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 캐릭터의 반복 영상을 재생하고 텍스처를 돌려줍니다(영상이 없으면 null).
        /// still = 첫 프레임이 나오기 전까지 보여 줄 정적 일러스트(빈 화면이 깜빡이지 않게).
        /// user = 이 텍스처를 표시하는 RawImage. 표시하는 곳이 모두 꺼지면 재생을 멈춥니다.
        /// </summary>
        public Texture Show(CharacterData c, RawImage user, Sprite still, bool baseOutfit = false)
        {
            var clip = ClipOf(c, baseOutfit);
            if (clip == null) return null;

            bool changed = _current != c || (_player != null && _player.clip != clip);   // 같은 캐릭터라도 의상을 바꾸면 영상이 다름
            if (changed || _player == null || !_player.isPlaying)
            {
                EnsurePlayer();
                EnsureTexture((int)clip.width, (int)clip.height);
                if (changed)
                {
                    DisplaceUsers(user);
                    _users.Clear();
                    // 영상이 준비되기 전에는 정적 일러스트를 깔아 둠
                    if (still != null && still.texture != null) Graphics.Blit(still.texture, _rt);
                    else ClearTexture();
                    _player.Stop();
                    _player.clip = clip;
                    _player.targetTexture = _rt;
                    _current = c;
                }
                _player.Play();
            }

            if (user != null && !_users.Contains(user)) _users.Add(user);
            _idleSince = -1f;
            return _rt;
        }


        /// <summary>
        /// 무대가 다른 캐릭터로 바뀌어 기존 표시 자리(RawImage)가 밀려났을 때. 그 자리는 정적 일러스트로 돌아가야 합니다.
        /// (무대가 하나라 같은 텍스처를 계속 보여 주면 엉뚱한 캐릭터가 보임)
        /// </summary>
        public static event System.Action<RawImage> Displaced;

        /// <summary>지금 보이는 곳에서 이 무대를 쓰고 있는가.</summary>
        public static bool InUse => _instance != null && _instance._users.Exists(u => u != null && u.isActiveAndEnabled);

        private void DisplaceUsers(RawImage keep)
        {
            if (_users.Count == 0) return;
            var old = new List<RawImage>(_users);
            foreach (var u in old)
                if (u != null && u != keep) Displaced?.Invoke(u);
        }

        /// <summary>표시 중인 곳에서 빠집니다. 무대가 없으면 만들지 않습니다.</summary>
        public static void ReleaseUser(RawImage user)
        {
            if (_instance != null && user != null) _instance._users.Remove(user);
        }

        private void EnsurePlayer()
        {
            if (_player != null) return;
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = true;
            _player.skipOnDrop = true;
            _player.waitForFirstFrame = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.aspectRatio = VideoAspectRatio.Stretch;
        }

        private void EnsureTexture(int width, int height)
        {
            width = Mathf.Max(16, width); height = Mathf.Max(16, height);
            if (_rt != null && _rt.width == width && _rt.height == height) return;
            if (_rt != null)
            {
                if (_player != null) _player.targetTexture = null;
                _rt.Release();
                Destroy(_rt);
            }
            _rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "Motion Video RT", useMipMap = false };
        }

        private void ClearTexture()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            RenderTexture.active = prev;
        }

        private void LateUpdate()
        {
            if (_current == null) return;
            // 표시하는 곳이 모두 꺼졌으면 잠시 뒤 재생을 멈춤
            _users.RemoveAll(u => u == null);
            if (_users.Exists(u => u.isActiveAndEnabled)) { _idleSince = -1f; return; }
            if (_idleSince < 0f) { _idleSince = Time.unscaledTime; return; }
            if (Time.unscaledTime - _idleSince <= 1f) return;
            if (_player != null) _player.Stop();
            _current = null;
            _users.Clear();
        }
    }
}
