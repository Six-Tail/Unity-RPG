using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 설정 창(홈 위쪽 '설정' 아이콘): 배경음 · 효과음 볼륨, 전체 음소거(이 기기에 저장) · 계정(로그인 상태 · 지금 저장 · 로그아웃).
    /// </summary>
    public static class SettingsWindow
    {
        public static void Open(TMP_FontAsset font = null)
        {
            var w = OverlayWindow.Open("설정", 900, 846, font);

            Row(w, "배경음 (BGM)", -110, SoundManager.BgmVolume, v => SoundManager.BgmVolume = v, false);
            Row(w, "효과음", -200, SoundManager.SfxVolume, v => SoundManager.SfxVolume = v, true);

            // 음소거
            Button muteBtn = null;
            void RefreshMute()
            {
                var lbl = muteBtn.GetComponentInChildren<TextMeshProUGUI>();
                lbl.text = SoundManager.Muted ? "음소거 켜짐  <size=75%>(누르면 소리 켜기)</size>" : "음소거 꺼짐  <size=75%>(누르면 모든 소리 끄기)</size>";
                muteBtn.GetComponent<Image>().color = SoundManager.Muted ? OverlayWindow.Red : new Color(1, 1, 1, 0.14f);
            }
            muteBtn = w.Btn(w.Panel, "", 22, new Color(1, 1, 1, 0.14f), 40, -300, 820, 64, () =>
            {
                SoundManager.Muted = !SoundManager.Muted;
                RefreshMute();
                if (!SoundManager.Muted) SoundManager.Play(Sfx.Confirm);
            });
            muteBtn.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            RefreshMute();

            if (DisplaySettings.Supported) DisplayRow(w);
            else
                w.Text(w.Panel, "<color=#9AA3B5>소리 설정은 이 기기에 저장됩니다.</color>",
                    17, Color.white, TextAlignmentOptions.Left, 40, -380, 820, 60).textWrappingMode = TextWrappingModes.Normal;

            Account(w);

            // 쿠폰 입력
            // PC · 안드로이드는 옆에 '게임 종료'(iOS는 앱이 스스로 끄지 않음)
            bool quit = PlatformRuntime.CanQuit;
            w.Btn(w.Panel, "쿠폰 입력", 22, OverlayWindow.Gold, 40, -670, quit ? 404 : 820, 62, () => CouponWindow.Open(w.Font));
            if (quit) w.Btn(w.Panel, "게임 종료", 22, OverlayWindow.Red, 456, -670, 404, 62, PlatformRuntime.AskQuit);
        }

        /// <summary>PC 화면 줄: [전체 화면][창 모드] · ◀ 해상도 ▶ (이 PC에 저장, 바로 적용).</summary>
        private static void DisplayRow(OverlayWindow w)
        {
            w.Text(w.Panel, "<b>화면</b>", 22, Color.white, TextAlignmentOptions.Left, 40, -384, 100, 52);
            Button full = null, win = null;
            TextMeshProUGUI resLabel = null;

            void Refresh()
            {
                bool fs = DisplaySettings.Fullscreen;
                full.GetComponent<Image>().color = fs ? OverlayWindow.Blue : new Color(1, 1, 1, 0.14f);
                win.GetComponent<Image>().color = !fs ? OverlayWindow.Blue : new Color(1, 1, 1, 0.14f);
                var r = DisplaySettings.Resolution;
                resLabel.text = $"{r.x} × {r.y}";
            }

            void Step(int dir)
            {
                var list = DisplaySettings.Resolutions();
                var cur = DisplaySettings.Resolution;
                int i = list.IndexOf(cur);
                if (i < 0) i = list.Count - 1;
                i = Mathf.Clamp(i + dir, 0, list.Count - 1);
                DisplaySettings.SetResolution(list[i]);
                Refresh();
            }

            full = w.Btn(w.Panel, "전체 화면", 20, OverlayWindow.Blue, 140, -384, 170, 52, () => { DisplaySettings.SetFullscreen(true); Refresh(); });
            win = w.Btn(w.Panel, "창 모드", 20, OverlayWindow.Blue, 318, -384, 170, 52, () => { DisplaySettings.SetFullscreen(false); Refresh(); });
            w.Btn(w.Panel, "<", 24, new Color(1, 1, 1, 0.14f), 510, -384, 60, 52, () => Step(-1));
            w.Box(w.Panel, new Color(0, 0, 0, 0.3f), 576, -384, 218, 52);
            resLabel = w.Text(w.Panel, "", 21, Color.white, TextAlignmentOptions.Center, 576, -384, 218, 52);
            w.Btn(w.Panel, ">", 24, new Color(1, 1, 1, 0.14f), 800, -384, 60, 52, () => Step(1));
            Refresh();
        }

        /// <summary>계정 줄: 로그인 상태 + 지금 저장 · 로그아웃(게스트면 로그인하기).</summary>
        private static void Account(OverlayWindow w)
        {
            var acc = SubcultureRPG.Meta.Cloud.CloudAccount.Instance;
            bool signed = SubcultureRPG.Meta.Cloud.CloudAccount.SignedIn;
            w.Box(w.Panel, new Color(1, 1, 1, 0.06f), 40, -456, 820, 190);
            w.Text(w.Panel, "<b>계정</b>", 24, Color.white, TextAlignmentOptions.Left, 64, -470, 200, 40);
            var status = w.Text(w.Panel, "", 20, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Left, 64, -512, 772, 56);
            status.textWrappingMode = TextWrappingModes.Normal;
            // 오른쪽 위: 마지막으로 서버에 저장된 날짜 · 시간(로그인 중일 때)
            var saved = w.Text(w.Panel, "", 19, new Color(0.72f, 0.76f, 0.84f), TextAlignmentOptions.Right, 436, -474, 400, 32);
            void RefreshStatus(string extra = null)
            {
                status.text = (signed
                    ? $"<color=#7FE3D6>로그인됨</color>  {SubcultureRPG.Meta.Cloud.CloudAccount.LastUsername}   <color=#9AA3B5>{(acc.HasPending ? "· 저장 대기 중" : "· 서버에 저장됨")}</color>"
                    : "<color=#FFD36B>게스트</color>  <color=#9AA3B5>이 PC에만 저장 중 · 로그인하면 서버에 올립니다</color>")
                    + (extra != null ? "\n" + extra : "");
                var last = SubcultureRPG.Meta.Cloud.CloudAccount.LastSavedLocal;
                saved.text = !signed ? ""
                    : last.HasValue ? $"마지막 서버 저장  <color=#FFFFFF>{last.Value:yyyy-MM-dd HH:mm:ss}</color>"
                    : "마지막 서버 저장  <color=#FF8A8A>아직 없음</color>";
            }
            RefreshStatus();

            if (signed)
            {
                w.Btn(w.Panel, "지금 서버에 저장", 22, OverlayWindow.Blue, 64, -574, 248, 58, async () =>
                {
                    RefreshStatus("<color=#C8D0E0>저장하는 중...</color>");
                    bool ok = await acc.UploadAsync();
                    if (w.IsOpen) RefreshStatus(ok ? "<color=#7FE3D6>저장했습니다.</color>" : "<color=#FF8A8A>저장하지 못했습니다. 잠시 뒤 자동으로 다시 시도합니다.</color>");
                });
                w.Btn(w.Panel, "비밀번호 변경", 22, new Color(0.45f, 0.4f, 0.6f, 0.95f), 326, -574, 248, 58, () => SubcultureRPG.Meta.Cloud.PasswordWindow.Open(w.Font));
                w.Btn(w.Panel, "로그아웃", 22, OverlayWindow.Dim, 588, -574, 248, 58, () =>
                    Popups.Show("로그아웃", "서버에 저장한 뒤 로그아웃하고 타이틀로 돌아갑니다.", "로그아웃", () => { w.Close(); _ = acc.SignOutAsync(); }, "취소"));
                // 계정 삭제(스토어 필수): 두 번 확인
                w.Btn(w.Panel, "계정 삭제", 18, new Color(0.45f, 0.2f, 0.2f, 0.85f), 40, -752, 820, 50, () =>
                    Popups.Show("계정 삭제",
                        "계정과 모든 게임 데이터(캐릭터 · 재화 · 친구 · 순위 기록)가 <color=#FF8A8A>서버와 이 기기에서 영구 삭제</color>됩니다.\n삭제한 계정은 되돌릴 수 없습니다.",
                        "계속", () => Popups.Show("정말 삭제할까요?",
                            $"<color=#FF8A8A>{SubcultureRPG.Meta.Cloud.CloudAccount.LastUsername}</color> 계정을 지금 영구 삭제합니다.",
                            "영구 삭제", async () =>
                            {
                                w.Close();
                                string err = await acc.DeleteAccountAsync();
                                if (err != null) Popups.Show("계정 삭제", err);
                            }, "취소"),
                        "취소"));
            }
            else
            {
                w.Btn(w.Panel, "로그인하기", 22, OverlayWindow.Blue, 64, -574, 772, 58, () => { w.Close(); _ = acc.SignOutAsync(); });
            }
        }

        /// <summary>이름 · 슬라이더 · 퍼센트 한 줄. sfxPreview 면 놓을 때 효과음으로 들려줌.</summary>
        private static void Row(OverlayWindow w, string label, float y, float value, System.Action<float> set, bool sfxPreview)
        {
            w.Text(w.Panel, $"<b>{label}</b>", 24, Color.white, TextAlignmentOptions.Left, 40, y, 240, 44);
            var pct = w.Text(w.Panel, "", 22, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Right, 760, y, 100, 44);

            var root = UIFactory.NewRect("Slider", w.Panel);
            OverlayWindow.Place(root, 280, y - 8, 460, 28);
            var bg = UIFactory.AddImage("Bg", root, new Color(1, 1, 1, 0.12f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            UIFactory.Stretch(bg.rectTransform, 0, 6, 0, 6);
            var fillArea = UIFactory.NewRect("FillArea", root);
            UIFactory.Stretch(fillArea, 0, 6, 0, 6);
            var fill = UIFactory.AddImage("Fill", fillArea, new Color(0.85f, 0.62f, 0.22f, 0.96f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false;
            UIFactory.Stretch(fill.rectTransform);
            var handleArea = UIFactory.NewRect("HandleArea", root);
            UIFactory.Stretch(handleArea, 14, 0, 14, 0);
            var handle = UIFactory.AddImage("Handle", handleArea, Color.white, ProceduralSprites.Circle, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(30, 0);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.value = value;
            pct.text = $"{Mathf.RoundToInt(value * 100)}%";
            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                pct.text = $"{Mathf.RoundToInt(v * 100)}%";
            });
            if (sfxPreview)
            {
                var trig = root.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                e.callback.AddListener(_ => SoundManager.Play(Sfx.Confirm));
                trig.triggers.Add(e);
            }
        }
    }
}
