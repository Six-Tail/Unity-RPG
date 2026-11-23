using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// Live2D(Cubism) 모델을 UI에 띄우기 위한 무대.
    /// 이 게임의 화면은 전부 화면을 덮는 uGUI 캔버스라, 월드에 놓이는 Live2D 모델은 그대로는 보이지 않습니다.
    /// → 화면 밖(먼 좌표)에 모델을 놓고 전용 카메라로 RenderTexture에 그린 뒤, 그 텍스처를 UI(RawImage)에 표시합니다.
    ///
    /// Cubism SDK 타입을 직접 참조하지 않으므로 SDK가 없어도 컴파일됩니다.
    /// 모델 프리팹은 SDK가 .model3.json 을 임포트하며 만든 것을 그대로 인스턴스화하고(모델 갱신은 프리팹의 Cubism 컴포넌트가 처리),
    /// 대기 모션(AnimationClip)은 Playables 로 반복 재생합니다. 사용은 <see cref="SubcultureRPG.UI.Live2DUI"/> 로.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Characters/Live2D Stage")]
    public class Live2DStage : MonoBehaviour
    {
        [SerializeField, Tooltip("그려 넣을 텍스처 크기(세로 일러스트 3:4).")] private int textureWidth = 768;
        [SerializeField] private int textureHeight = 1024;
        [SerializeField, Tooltip("모델 둘레 여백(모델 크기 대비).")] private float margin = 0.04f;
        [SerializeField, Tooltip("Cubism 셰이더는 알파가 곱해진(premultiplied) 색을 그립니다. 모델 가장자리가 어둡거나 밝게 뜨면 꺼 보세요.")]
        private bool premultipliedAlpha = true;
        [SerializeField, Tooltip("눈 깜빡임 컴포넌트(자동 입력)가 없으면 붙입니다(Cubism SDK가 있을 때만).")]
        private bool autoEyeBlink = true;

        private static Live2DStage _instance;
        private static readonly Vector3 Far = new(5000f, 5000f, 0f);   // 메인 카메라에 안 잡히는 곳

        private Camera _cam;
        private RenderTexture _rt;
        private Material _uiMaterial;
        private GameObject _model;
        private CharacterData _current;
        private PlayableGraph _graph;
        private AnimationClipPlayable _clip;
        private bool _manualLoop;
        private int _fitFrames;
        private float _idleSince;
        private readonly List<RawImage> _users = new();

        /// <summary>무대(없으면 만듦).</summary>
        public static Live2DStage Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<Live2DStage>();
                if (_instance == null) _instance = new GameObject("Live2DStage").AddComponent<Live2DStage>();
                return _instance;
            }
        }

        /// <summary>이 캐릭터의 Live2D 모델 프리팹(에셋에 연결된 것 → Resources/Live2D/{id}). 없으면 null.</summary>
        public static GameObject PrefabOf(CharacterData c)
        {
            if (c == null) return null;
            if (c.live2DModelPrefab != null) return c.live2DModelPrefab;
            string key = !string.IsNullOrEmpty(c.id) ? c.id : c.name;
            return Resources.Load<GameObject>("Live2D/" + key);
        }

        public static bool Has(CharacterData c) => PrefabOf(c) != null;

        /// <summary>UI에서 이 텍스처를 그릴 때 쓸 머티리얼(premultiplied 블렌드). null이면 기본 머티리얼.</summary>
        public Material UIMaterial
        {
            get
            {
                if (!premultipliedAlpha) return null;
                if (_uiMaterial == null)
                {
                    var shader = Resources.Load<Shader>("Shaders/UI-Premultiplied");
                    if (shader != null) _uiMaterial = new Material(shader) { name = "Live2D UI (premultiplied)" };
                }
                return _uiMaterial;
            }
        }

        public float Aspect => textureHeight > 0 ? (float)textureWidth / textureHeight : 0.75f;
        public CharacterData Current => _current;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            transform.position = Far;
        }

        private void OnDestroy()
        {
            Clear();
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_uiMaterial != null) Destroy(_uiMaterial);
            if (_instance == this) _instance = null;
        }

        // ---------------- 표시 ----------------

        /// <summary>
        /// 캐릭터의 Live2D 모델을 무대에 올리고 텍스처를 돌려줍니다(모델이 없으면 null).
        /// user = 이 텍스처를 표시하는 RawImage. 표시하는 곳이 모두 꺼지면 무대는 스스로 쉽니다.
        /// </summary>
        public Texture Show(CharacterData c, RawImage user)
        {
            var prefab = PrefabOf(c);
            if (prefab == null) return null;

            if (_current != c || _model == null)
            {
                Clear();
                EnsureRig();
                _current = c;
                _model = Instantiate(prefab, transform);
                _model.name = "Model_" + c.displayName;
                _model.transform.localPosition = Vector3.zero;
                _model.transform.localRotation = Quaternion.identity;
                _fitFrames = 6;   // 메시가 만들어진 뒤 몇 프레임 동안 크기를 다시 맞춤
                PlayIdle(c);
                if (autoEyeBlink) TryAddAutoEyeBlink(_model);
            }

            if (user != null && !_users.Contains(user)) _users.Add(user);
            _cam.enabled = true;
            _idleSince = -1f;
            return _rt;
        }

        /// <summary>표시 중인 곳에서 빠집니다(다른 곳이 쓰고 있으면 무대는 유지). 무대가 없으면 만들지 않습니다.</summary>
        public static void ReleaseUser(RawImage user)
        {
            if (_instance != null && user != null) _instance._users.Remove(user);
        }

        private void Clear()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_model != null) Destroy(_model);
            _model = null;
            _current = null;
            _users.Clear();
            if (_cam != null) _cam.enabled = false;
        }

        private void EnsureRig()
        {
            if (_rt == null)
            {
                _rt = new RenderTexture(Mathf.Max(64, textureWidth), Mathf.Max(64, textureHeight), 16, RenderTextureFormat.ARGB32)
                {
                    name = "Live2D RT",
                    useMipMap = false,
                    filterMode = FilterMode.Bilinear,
                };
            }
            if (_cam == null)
            {
                var go = new GameObject("Live2DCamera");
                go.transform.SetParent(transform, false);
                _cam = go.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _cam.allowHDR = false;
                _cam.allowMSAA = false;
                _cam.nearClipPlane = 0.01f;
                _cam.farClipPlane = 100f;
                _cam.depth = -50;
                _cam.targetTexture = _rt;
                _cam.enabled = false;
            }
        }

        // ---------------- 모션 ----------------

        private void PlayIdle(CharacterData c)
        {
            var animator = _model.GetComponent<Animator>();
            if (animator == null) animator = _model.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 화면 밖에 있어도 계속 움직이게

            // 프리팹에 Animator Controller 를 직접 달았으면 그대로 둠
            if (animator.runtimeAnimatorController != null) return;

            var clip = c.live2DIdleMotion;
            if (clip == null) return;   // 모션 없음 → 물리·눈 깜빡임만(있다면)

            _graph = PlayableGraph.Create("Live2D Idle");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Live2D", animator);
            _clip = AnimationClipPlayable.Create(_graph, clip);
            output.SetSourcePlayable(_clip);
            _manualLoop = !clip.isLooping;   // 반복 설정이 꺼진 클립도 반복되게
            _graph.Play();
        }

        /// <summary>자동 눈 깜빡임 입력(Cubism SDK 컴포넌트)을 리플렉션으로 추가 — SDK가 없으면 아무 일도 안 함.</summary>
        private static void TryAddAutoEyeBlink(GameObject model)
        {
            try
            {
                var controller = Type.GetType("Live2D.Cubism.Framework.CubismEyeBlinkController, Live2D.Cubism");
                var input = Type.GetType("Live2D.Cubism.Framework.CubismAutoEyeBlinkInput, Live2D.Cubism");
                if (controller == null || input == null) return;
                if (model.GetComponent(controller) == null) return;   // 눈 깜빡임 파라미터가 없는 모델
                if (model.GetComponent(input) == null) model.AddComponent(input);
            }
            catch (Exception) { /* SDK 버전이 달라 타입이 없으면 무시 */ }
        }

        // ---------------- 갱신 ----------------

        private void LateUpdate()
        {
            if (_model == null) return;

            if (_manualLoop && _graph.IsValid())
            {
                var clip = _clip.GetAnimationClip();
                if (clip != null && clip.length > 0f && _clip.GetTime() >= clip.length) _clip.SetTime(0);
            }

            if (_fitFrames > 0) { _fitFrames--; FitCamera(); }

            // 표시하는 곳이 모두 꺼졌으면 잠시 뒤 쉼(모델·카메라 정리)
            _users.RemoveAll(u => u == null);
            bool visible = _users.Exists(u => u.isActiveAndEnabled);
            if (visible) { _idleSince = -1f; return; }
            if (_idleSince < 0f) _idleSince = Time.unscaledTime;
            else if (Time.unscaledTime - _idleSince > 1f) Clear();
        }

        /// <summary>모델 전체(또는 <see cref="Live2DFraming"/> 지정 범위)가 텍스처에 꽉 차게 카메라를 맞춥니다.</summary>
        private void FitCamera()
        {
            if (!TryBounds(out var b)) return;
            var framing = _model.GetComponentInChildren<Live2DFraming>();
            float zoom = framing != null ? Mathf.Max(0.1f, framing.zoom) : 1f;
            Vector2 offset = framing != null ? framing.offset : Vector2.zero;

            float halfH = Mathf.Max(b.size.y * 0.5f, b.size.x * 0.5f / Aspect) * (1f + margin) / zoom;
            _cam.orthographicSize = Mathf.Max(0.001f, halfH);
            var center = b.center + new Vector3(offset.x * b.size.y, offset.y * b.size.y, 0f);
            _cam.transform.position = new Vector3(center.x, center.y, b.min.z - 10f);
            _cam.transform.rotation = Quaternion.identity;
        }

        private bool TryBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in _model.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.bounds.size.sqrMagnitude <= 0f) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
