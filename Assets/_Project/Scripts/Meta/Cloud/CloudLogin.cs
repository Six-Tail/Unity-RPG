using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.Meta.Cloud
{
    /// <summary>
    /// 타이틀에서 'TAP TO START' 다음에 도는 계정 흐름:
    /// 서버 연결 → (저장된 로그인 정보로 자동 로그인 또는) 로그인 · 회원가입 창 → 세이브 맞추기(필요하면 어느 쪽을 쓸지 묻기).
    /// 서버에 못 붙으면 '오프라인으로' 할 수 있음. 창은 타이틀(순서 500) 위에 뜨도록 순서 600.
    /// </summary>
    public static class CloudLogin
    {
        private const int Order = 600;

        /// <param name="localHasProgress">이 PC 세이브에 진행이 있는지(지휘관 이름을 정했는지)</param>
        /// <param name="onNeedReload">서버 세이브로 바뀌어 게임을 다시 불러와야 할 때</param>
        public static IEnumerator Run(TMP_FontAsset font, Func<bool> localHasProgress, Action onNeedReload)
        {
            if (CloudAccount.Synced) yield break;
            var acc = CloudAccount.Instance;

            while (true)
            {
                // 1) 서버 연결
                var init = acc.InitAsync();
                while (!init.IsCompleted) yield return null;
                if (init.Result != null)
                {
                    bool? retry = null;
                    Ask(font, "서버 연결", $"{init.Result}\n\n<size=85%><color=#9AA3B5>게스트로 하면 이 PC에만 저장되고, 다음에 로그인할 때 서버에 올립니다.</color></size>",
                        "다시 시도", () => retry = true, "게스트로", () => retry = false);
                    while (retry == null) yield return null;
                    if (retry == true) continue;
                    acc.GoOffline();
                    yield break;
                }

                // 2) 자동 로그인 → 안 되면 로그인 창
                if (!CloudAccount.SignedIn)
                {
                    var restore = acc.TryRestoreAsync();
                    while (!restore.IsCompleted) yield return null;
                }
                if (!CloudAccount.SignedIn)
                {
                    bool done = false;
                    LoginWindow.Open(font, () => done = true);
                    while (!done) yield return null;
                    if (CloudAccount.Offline) yield break;
                }

                // 3) 세이브 맞추기 — 옛 세이브에서 아직 안 읽힌 키(스토리 · 튜토리얼 기록)도 목록에 넣어 빠짐없이 옮김
                SaveStore.AdoptLegacyKeys(StoryLibrary.SaveKeys());
                SaveStore.AdoptLegacyKeys(Tutorial.SaveKeys());
                var sync = acc.SyncAsync(localHasProgress != null && localHasProgress());
                while (!sync.IsCompleted) yield return null;
                var r = sync.Result;

                if (r == CloudAccount.SyncResult.Conflict)
                {
                    bool? useCloud = null;
                    string when = acc.PendingCloudTime.ToString("yyyy-MM-dd HH:mm");
                    string name = PlayerProfile.Instance.HasName ? PlayerProfile.Instance.Name : "(이름 없음)";
                    Ask(font, "어느 세이브로 할까요?",
                        "이 계정의 서버 세이브와 이 PC의 세이브가 서로 다릅니다.\n\n" +
                        $"<color=#7FC8FF>서버 세이브</color>  {when} 저장\n" +
                        $"<color=#FFD36B>이 PC 세이브</color>  지휘관 {name}\n\n" +
                        "<size=85%><color=#FF8A8A>고르지 않은 쪽은 사라집니다.</color></size>",
                        "서버 세이브", () => useCloud = true, "이 PC 세이브", () => useCloud = false);
                    while (useCloud == null) yield return null;
                    var res = acc.ResolveAsync(useCloud.Value);
                    while (!res.IsCompleted) yield return null;
                    r = res.Result;
                }

                if (r == CloudAccount.SyncResult.Error)
                {
                    bool? retry = null;
                    Ask(font, "서버 세이브", $"{acc.LastError ?? "서버와 세이브를 맞추지 못했습니다."}\n\n<size=85%><color=#9AA3B5>게스트로 하면 이 PC에만 저장되고, 다음에 로그인할 때 서버에 올립니다.</color></size>",
                        "다시 시도", () => retry = true, "게스트로", () => retry = false);
                    while (retry == null) yield return null;
                    if (retry == true) continue;
                    acc.GoOffline();
                    yield break;
                }

                if (r == CloudAccount.SyncResult.Reload) onNeedReload?.Invoke();
                yield break;
            }
        }

        /// <summary>두 버튼 질문 창(타이틀 위).</summary>
        public static void Ask(TMP_FontAsset font, string title, string body, string a, Action onA, string b, Action onB)
        {
            var w = OverlayWindow.Open(title, 860, 460, font, dimCloses: false, order: Order, closeButton: false);
            var t = w.Text(w.Panel, body, 25, new Color(0.9f, 0.92f, 0.96f), TextAlignmentOptions.Center, 44, -84, 772, 260);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = 8f;
            t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 25;
            w.Btn(w.Panel, a, 24, OverlayWindow.Blue, 160, -370, 260, 62, () => { w.Close(); onA?.Invoke(); });
            w.Btn(w.Panel, b, 24, OverlayWindow.Dim, 440, -370, 260, 62, () => { w.Close(); onB?.Invoke(); });
        }
    }

    /// <summary>
    /// 시작 선택 → 로그인 창 / 회원가입 창.
    ///  · 시작 선택: 첫 줄 [로그인] [회원가입], 둘째 줄 [게스트로](서버 없이 이 PC에만 저장)
    ///  · 회원가입이 끝나면 아이디를 채운 로그인 창으로 넘어감
    /// </summary>
    public class LoginWindow
    {
        private static readonly Regex UserRule = new(@"^[A-Za-z0-9.\-@_]{3,20}$");
        private const int Order = 600;

        private TMP_FontAsset _font;
        private Action _onDone;

        // 지금 떠 있는 폼
        private OverlayWindow _w;
        private TMP_InputField _user, _pass, _pass2;
        private TextMeshProUGUI _msg;
        private Button _submitBtn, _backBtn;
        private bool _busy;

        public static void Open(TMP_FontAsset font, Action onDone)
        {
            var lw = new LoginWindow { _font = font, _onDone = onDone };
            lw.ShowChoice();
        }

        private void Finish()
        {
            if (_w != null && _w.IsOpen) _w.Close();
            _onDone?.Invoke();
        }

        // ---------------- 시작 선택 ----------------

        private void ShowChoice()
        {
            _w = OverlayWindow.Open("시작하기", 760, 420, _font, dimCloses: false, order: Order, closeButton: false);
            var p = _w.Panel;
            var info = _w.Text(p, "계정으로 로그인하면 진행 상황이 서버에 저장되어\n어느 PC에서든 이어지고 친구와 함께할 수 있습니다.",
                22, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Center, 40, -84, 680, 70);
            info.textWrappingMode = TextWrappingModes.Normal;

            var w = _w;
            w.Btn(p, "로그인", 28, OverlayWindow.Blue, 40, -180, 330, 76, () => { w.Close(); ShowForm(false); });
            w.Btn(p, "회원가입", 28, OverlayWindow.Green, 390, -180, 330, 76, () => { w.Close(); ShowForm(true); });
            w.Btn(p, "게스트로", 26, OverlayWindow.Dim, 40, -276, 680, 70, Guest);
            var note = w.Text(p, "게스트는 이 PC에만 저장됩니다. 나중에 로그인하면 서버에 올릴 수 있습니다.",
                17, new Color(0.62f, 0.66f, 0.74f), TextAlignmentOptions.Center, 40, -356, 680, 40);
            note.textWrappingMode = TextWrappingModes.Normal;
        }

        private void Guest()
        {
            CloudLogin.Ask(_font, "게스트로 시작",
                "서버 없이 이 PC 세이브로 플레이합니다.\n친구 기능은 쓸 수 없고, 나중에 로그인하면 바뀐 내용을 서버에 올립니다.",
                "게스트로 시작", () =>
                {
                    CloudAccount.Instance.GoOffline();
                    Finish();
                }, "돌아가기", null);
        }

        // ---------------- 로그인 · 회원가입 폼 ----------------

        private void ShowForm(bool signUp, string notice = null, string prefillUser = null)
        {
            _busy = false;
            float h = signUp ? 600 : 470;
            _w = OverlayWindow.Open(signUp ? "회원가입" : "로그인", 900, h, _font, dimCloses: false, order: Order, closeButton: false);
            var p = _w.Panel;

            Label("아이디", -100);
            _user = Input(p, _font, "아이디", 20, -90);
            _user.text = prefillUser ?? CloudAccount.LastUsername;
            Label("비밀번호", -170);
            _pass = Input(p, _font, "비밀번호", CloudAccount.MaxPassword, -160, true);
            _pass2 = null;
            float y = -240;
            if (signUp)
            {
                Label("비밀번호 확인", -240);
                _pass2 = Input(p, _font, "비밀번호를 한 번 더", CloudAccount.MaxPassword, -230, true);
                var rules = _w.Text(p, "아이디: 3 ~ 20자 (영문 · 숫자 · . - @ _)\n비밀번호: 8 ~ 26자, 영문 · 숫자 · 기호를 각각 1개 이상 (대문자 필요 없음)",
                    18, new Color(0.62f, 0.66f, 0.74f), TextAlignmentOptions.TopLeft, 44, -306, 812, 56);
                rules.textWrappingMode = TextWrappingModes.Normal;
                y = -374;
            }

            _msg = _w.Text(p, notice ?? "", 21, new Color(1f, 0.55f, 0.55f), TextAlignmentOptions.Center, 44, y, 812, 64);
            _msg.textWrappingMode = TextWrappingModes.Normal;

            float by = -h + 100;
            var w = _w;
            _submitBtn = w.Btn(p, signUp ? "가입하기" : "로그인", 26, signUp ? OverlayWindow.Green : OverlayWindow.Blue, 44, by, 520, 70,
                () => _ = Submit(signUp));
            _backBtn = w.Btn(p, "뒤로", 24, OverlayWindow.Dim, 580, by, 276, 70, () =>
            {
                if (_busy) return;
                w.Close();
                ShowChoice();
            });

            _user.onSubmit.AddListener(s => { if (!_busy) _pass.ActivateInputField(); });
            if (signUp)
            {
                _pass.onSubmit.AddListener(s => { if (!_busy) _pass2.ActivateInputField(); });
                _pass2.onSubmit.AddListener(s => { if (!_busy) _ = Submit(true); });
            }
            else _pass.onSubmit.AddListener(s => { if (!_busy) _ = Submit(false); });
            if (string.IsNullOrEmpty(_user.text)) _user.ActivateInputField(); else _pass.ActivateInputField();
        }

        private void Label(string s, float y) =>
            _w.Text(_w.Panel, s, 22, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Left, 44, y, 170, 40);

        private void SetBusy(bool on, string msg = null)
        {
            _busy = on;
            _submitBtn.interactable = !on; _backBtn.interactable = !on;
            if (msg != null) _msg.text = msg;
        }

        private async Task Submit(bool signUp)
        {
            if (_busy) return;
            string user = (_user.text ?? "").Trim(), pass = _pass.text ?? "";
            string err = signUp ? CheckSignUp(user, pass, _pass2 != null ? _pass2.text ?? "" : "")
                : (user.Length == 0 || pass.Length == 0 ? "아이디와 비밀번호를 입력해 주세요." : null);
            if (err != null) { _msg.text = err; return; }

            var w = _w;
            SetBusy(true, $"<color=#C8D0E0>{(signUp ? "가입하는 중..." : "로그인하는 중...")}</color>");
            var acc = CloudAccount.Instance;
            err = signUp ? await acc.SignUpAsync(user, pass) : await acc.SignInAsync(user, pass);
            if (w == null || !w.IsOpen) return;
            if (err != null) { SetBusy(false, err); return; }

            if (signUp)
            {
                // 가입하면 자동으로 로그인된 상태 → 로그아웃하고 로그인 창으로(아이디 채움)
                acc.SignOutOnly();
                w.Close();
                ShowForm(false, "<color=#7FE3D6>가입이 완료되었습니다. 로그인해 주세요.</color>", user);
                return;
            }
            Finish();
        }

        private static string CheckSignUp(string user, string pass, string pass2)
        {
            if (!UserRule.IsMatch(user)) return "아이디는 3 ~ 20자, 영문 · 숫자 · . - @ _ 만 쓸 수 있습니다.";
            if (pass.Length < CloudAccount.MinPassword || pass.Length > CloudAccount.MaxPassword)
                return $"비밀번호는 {CloudAccount.MinPassword} ~ {CloudAccount.MaxPassword}자여야 합니다.";
            bool letter = false, dig = false, sym = false;
            foreach (char c in pass)
            {
                if (c > 126 || c < 33) return "비밀번호에는 영문 · 숫자 · 기호만 쓸 수 있습니다(한글 · 띄어쓰기 불가).";
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) letter = true;
                else if (c >= '0' && c <= '9') dig = true;
                else sym = true;
            }
            if (!(letter && dig && sym)) return "비밀번호에 영문 · 숫자 · 기호가 각각 1개 이상 있어야 합니다.";
            if (pass2.Length == 0) return "가입하려면 '비밀번호 확인'에도 같은 비밀번호를 입력해 주세요.";
            if (pass != pass2) return "비밀번호 확인이 맞지 않습니다.";
            return null;
        }

        private static TMP_InputField Input(RectTransform parent, TMP_FontAsset font, string placeholder, int limit, float y, bool password = false)
        {
            const float size = 24;
            var bg = UIFactory.AddImage("Input", parent, new Color(1, 1, 1, 0.1f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            OverlayWindow.Place(bg.rectTransform, 220, y, 636, 56);
            var area = UIFactory.NewRect("Text Area", bg.transform);
            UIFactory.Stretch(area, 20, 6, 20, 6);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = UIFactory.AddText("Placeholder", area, placeholder, size, font, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Left, FontStyles.Italic);
            UIFactory.Stretch(ph.rectTransform);
            var text = UIFactory.AddText("Text", area, "", size, font, Color.white, TextAlignmentOptions.Left);
            UIFactory.Stretch(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            if (password) { field.contentType = TMP_InputField.ContentType.Password; field.asteriskChar = '*'; }
            else field.contentType = TMP_InputField.ContentType.Standard;
            if (font != null) field.fontAsset = font;
            field.pointSize = size;
            field.caretColor = Color.white;
            field.customCaretColor = true;
            field.selectionColor = new Color(0.4f, 0.6f, 1f, 0.5f);
            InputFocusFx.Attach(field);   // 입력 중 표시(밝은 칸 · 금빛 밑줄 · 굵은 커서)
            return field;
        }
    }
}
