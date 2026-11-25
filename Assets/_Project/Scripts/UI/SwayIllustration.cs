using UnityEngine;
using UnityEngine.UI;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 움직이는 일러스트(셰이더 방식): Image 에 붙으면 'SubcultureRPG/UI Sway' 머티리얼로 그림을 흔들고 눈을 깜빡입니다.
    /// 재료: Resources/Art/Sway/{이름}_mask.png (R 흔들림 · G 호흡 · B 반짝임) + {이름}_blink.png (눈 감은 그림, 알파 = 눈)
    ///   이름 = 일러스트 파일 이름(c_seraphine · 개화 c_seraphine_bloom …). 만들기: tools/art/generate-sway.js — docs/sway.md
    /// </summary>
    [DisallowMultipleComponent]
    public class SwayIllustration : MonoBehaviour
    {
        public const string Folder = "Art/Sway/";
        public float blinkEvery = 4f, blinkDuration = 0.16f;

        private Image _image;
        private Material _mat;
        private string _name;
        private float _phase;

        /// <summary>그 그림의 흔들림 재료가 있는가.</summary>
        public static bool Has(string artName) =>
            !string.IsNullOrEmpty(artName) && Resources.Load<Texture2D>(Folder + artName + "_mask") != null;

        /// <summary>image 에 흔들림을 켬(재료가 없으면 끄고 false).</summary>
        public static bool Apply(Image image, string artName)
        {
            if (image == null) return false;
            var s = image.GetComponent<SwayIllustration>();
            if (!Has(artName)) { if (s != null) s.enabled = false; return false; }
            if (s == null) s = image.gameObject.AddComponent<SwayIllustration>();
            s.Set(artName);
            s.enabled = true;
            return true;
        }

        /// <summary>흔들림을 끔(원래 머티리얼로).</summary>
        public static void Remove(Image image)
        {
            var s = image != null ? image.GetComponent<SwayIllustration>() : null;
            if (s != null) s.enabled = false;
        }

        private void Set(string artName)
        {
            _image = GetComponent<Image>();
            if (_mat == null)
            {
                var shader = Resources.Load<Shader>("Shaders/UI-Sway");
                if (shader == null) return;
                _mat = new Material(shader) { name = "Sway (instance)" };
            }
            if (_name != artName)
            {
                _name = artName;
                _mat.SetTexture("_SwayMask", Resources.Load<Texture2D>(Folder + artName + "_mask"));
                var blink = Resources.Load<Texture2D>(Folder + artName + "_blink");
                _mat.SetTexture("_BlinkTex", blink != null ? blink : Texture2D.blackTexture);
                _phase = Random.Range(0f, blinkEvery);   // 여러 장이 한꺼번에 깜빡이지 않게
            }
        }

        private void OnEnable()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image != null && _mat != null) _image.material = _mat;
        }

        private void OnDisable()
        {
            if (_image != null && _image.material == _mat) _image.material = null;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        private void Update()
        {
            if (_mat == null || _image == null) return;
            if (_image.material != _mat) _image.material = _mat;
            float t = Time.unscaledTime;
            _mat.SetFloat("_T", t);
            _mat.SetFloat("_Blink", BlinkAt(t + _phase, blinkEvery, blinkDuration));
        }

        /// <summary>깜빡임 정도(0 ~ 1): blinkEvery 초마다 blinkDuration 동안 감았다 뜸.</summary>
        public static float BlinkAt(float t, float every, float dur)
        {
            float ph = Mathf.Repeat(t, every);
            return ph < dur ? Mathf.Sin(Mathf.PI * ph / dur) : 0f;
        }
    }
}
