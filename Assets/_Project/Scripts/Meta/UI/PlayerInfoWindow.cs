using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>글자를 클립보드에 복사하는 작은 버튼(UID 등). 누르면 잠깐 '복사됨'.</summary>
    public static class CopyButton
    {
        public static Button Create(Transform parent, TMP_FontAsset font, System.Func<string> text, float x, float y)
        {
            var b = UIFactory.AddButton("Copy", parent, "복사", 18, font, new Color(1f, 1f, 1f, 0.16f), Color.white);
            OverlayWindow.Place((RectTransform)b.transform, x, y, 84, 34);
            var lbl = b.GetComponentInChildren<TextMeshProUGUI>();
            b.onClick.AddListener(() =>
            {
                string s = text?.Invoke();
                if (string.IsNullOrEmpty(s)) return;
                GUIUtility.systemCopyBuffer = s;
                var fb = b.GetComponent<CopyFeedback>();
                if (fb == null) fb = b.gameObject.AddComponent<CopyFeedback>();
                fb.Arm(lbl);
            });
            return b;
        }
    }

    /// <summary>복사 버튼 글자를 잠깐 '복사됨'으로.</summary>
    public class CopyFeedback : MonoBehaviour
    {
        private TextMeshProUGUI _label;

        public void Arm(TextMeshProUGUI label)
        {
            _label = label;
            if (_label != null) { _label.text = "복사됨"; _label.color = new Color(0.49f, 1f, 0.6f); }
            CancelInvoke(nameof(Back));
            Invoke(nameof(Back), 1.5f);
        }

        private void Back()
        {
            if (_label != null) { _label.text = "복사"; _label.color = Color.white; }
        }
    }

    /// <summary>
    /// 다른 지휘관 정보창: 모든 순위표(결투장 · 월드 레이드) · 친구 · 길드원에서 그 지휘관을 누르면 열림.
    /// 테두리 · 아바타 · 이름 · 칭호 · 레벨 · UID · 인사말 · (순위 줄) · 전투력 상위 5명 — 캐릭터를 누르면 육성 · 장비 · 능력치 상세(<see cref="CharInfoWindow"/>).
    /// 실제 플레이어는 서버의 공개 프로필(PublicProfile), 가상 지휘관은 번호(seed)로 늘 같게 만든 프로필.
    /// </summary>
    public static class PlayerInfoWindow
    {
        /// <param name="extra">순위표에서 열 때 덧붙일 줄(순위 · 티어 · 점수 등), 없으면 null</param>
        public static void Open(Commander c, TMP_FontAsset font = null, string extra = null)
        {
            if (c == null) return;
            var db = Object.FindAnyObjectByType<PlayerRoster>()?.Database;
            Open(PublicProfile.FromCommander(c, db), font, extra);
        }

        /// <summary>실제 플레이어: 서버에서 공개 프로필을 불러와 엶.</summary>
        public static async void OpenPlayer(string pid, TMP_FontAsset font = null, string extra = null)
        {
            var snap = await PublicProfile.Instance.LoadAsync(pid);
            if (snap == null) { Popups.Show("지휘관 정보", "이 지휘관의 정보를 불러오지 못했습니다.\n(아직 프로필을 올리지 않았거나 오프라인)"); return; }
            Open(snap, font, extra);
        }

        public static void Open(ProfileSnap s, TMP_FontAsset font = null, string extra = null)
        {
            if (s == null) return;
            var db = Object.FindAnyObjectByType<PlayerRoster>()?.Database;
            var w = OverlayWindow.Open("지휘관 정보", 1100, 800, font);
            var p = w.Panel;

            // 아바타 + 기본 정보
            var av = new ProfileAvatar(p, 190);
            OverlayWindow.Place(av.Rect, 44, -92, 190, 190);
            // 그림은 그 지휘관의 의상(가상 지휘관 = 기본) — 내 옷장 선택(개화 등)을 따르지 않음
            av.Set(db != null && !string.IsNullOrEmpty(s.avatarId) ? db.GetCharacter(s.avatarId) : null, s.frameId, s.avatarOutfit ?? "");

            var title = ProfileRewards.Title(s.titleId);
            string tag = s.real ? "  <size=55%><color=#7CFF9A>● 플레이어</color></size>" : "";
            w.Text(p, $"<b>{s.name}</b>   <size=60%><color=#{ColorUtility.ToHtmlStringRGB(title.color)}>{title.name}</color></size>{tag}", 40, Color.white,
                TextAlignmentOptions.Left, 270, -92, 790, 56);
            string seen = s.real
                ? (s.updated > 0 ? $"<color=#9AA3B5>{Ago(s.updated)} 갱신</color>" : "")
                : (s.seed >= 0 ? Commanders.LastSeen(new Commander { seed = s.seed }) : "");
            var idLine = w.Text(p, $"Lv.<b>{s.level}</b>    <color=#9AA3B5>UID</color> {s.uid}", 24, new Color(0.88f, 0.9f, 0.96f),
                TextAlignmentOptions.Left, 270, -154, 420, 36);
            float idW = idLine.GetPreferredValues(idLine.text).x;
            string uid = s.uid.ToString();
            CopyButton.Create(p, w.Font, () => uid, 270 + idW + 12, -155);
            w.Text(p, seen, 24, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.Left, 270 + idW + 120, -154, 400, 36);
            long power = SnapStats.TotalPower(s, db);
            w.Text(p, $"<color=#FFB347>전투력(상위 5명) <b>{power:N0}</b></color>" + (extra != null ? $"     {extra}" : ""), 25, Color.white,
                TextAlignmentOptions.Left, 270, -196, 790, 40);
            var greet = w.Text(p, string.IsNullOrEmpty(s.greeting) ? "<color=#6B7280>인사말이 없습니다</color>" : $"\"{s.greeting}\"", 22,
                new Color(0.78f, 0.82f, 0.9f), TextAlignmentOptions.Left, 270, -240, 790, 40);
            greet.fontStyle = FontStyles.Italic;

            // 전투력 상위 5명(누르면 상세)
            w.Text(p, "<b>전투력 상위 5명</b>  <size=75%><color=#9AA3B5>캐릭터를 누르면 장비 · 능력치 · 초월을 볼 수 있습니다</color></size>", 24,
                new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 44, -312, 1000, 34);
            var cards = new List<(CharSnap cs, SnapStats.Result r)>();
            if (s.top != null)
                foreach (var cs in s.top) { var r = SnapStats.Evaluate(cs, db); if (r != null) cards.Add((cs, r)); }
            cards.Sort((a, b) => b.r.power.CompareTo(a.r.power));
            for (int i = 0; i < cards.Count && i < 5; i++)
            {
                var (cs, r) = cards[i];
                float x = 44 + i * 204;
                var card = w.Box(p, new Color(1, 1, 1, 0.06f), x, -356, 194, 316, raycast: true);
                var col = StarText.FrameColor(cs.star, cs.transcend);
                var rim = UIFactory.AddImage("Rim", card.transform, new Color(col.r, col.g, col.b, 0.85f), ProceduralSprites.Rounded);
                rim.type = Image.Type.Sliced; rim.preserveAspect = false; rim.raycastTarget = false;
                OverlayWindow.Place(rim.rectTransform, 19, -12, 156, 156);
                var thumb = UIFactory.AddImage("Thumb", rim.transform, Color.white, GameArt.Thumb(r.ch, cs.outfit ?? ""));
                thumb.preserveAspect = true; thumb.raycastTarget = false;
                UIFactory.Stretch(thumb.rectTransform, 3, 3, 3, 3);
                thumb.enabled = thumb.sprite != null;
                var srow = StarRow.Create(card.transform, 22, TextAnchor.MiddleCenter, w.Font);
                OverlayWindow.Place(srow.Rect, 0, -170, 194, 26);
                srow.Set(cs.star, cs.transcend);
                w.Text(card.transform, $"<b>{r.ch.displayName}</b>", 23, Color.white, TextAlignmentOptions.Center, 0, -198, 194, 30);
                w.Text(card.transform, $"<color={Traits.Hex(r.ch.element)}>{Traits.Name(r.ch.element)}</color> · {Traits.Name(r.ch.role)}   Lv.{cs.level}", 16,
                    new Color(0.82f, 0.85f, 0.92f), TextAlignmentOptions.Center, 0, -230, 194, 24);
                w.Text(card.transform, $"<color=#FFB347>{r.power:N0}</color>", 20, Color.white, TextAlignmentOptions.Center, 0, -258, 194, 26);
                w.Text(card.transform, $"<size=80%><color=#9AA3B5>장비 {cs.gear?.Count ?? 0}/6</color></size>", 16, Color.white, TextAlignmentOptions.Center, 0, -284, 194, 22);
                var btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                card.gameObject.AddComponent<ButtonFx>();
                var owner = s.name;
                btn.onClick.AddListener(() => CharInfoWindow.Open(cs, owner, w.Font));
            }
            if (cards.Count == 0)
                w.Text(p, "<color=#6B7280>정보 없음</color>", 22, Color.white, TextAlignmentOptions.Left, 44, -360, 400, 34);

            // 친선 전투(나 말고 · 캐릭터 정보가 있으면)
            bool me = s.real && s.pid == CloudAccount.PlayerId;
            // 실제 플레이어: 위 오른쪽 '신고'(랭킹 · 친구 · 길드 · 채팅 어디서 열어도) · 친구면 '대화'
            if (s.real && !me && !string.IsNullOrEmpty(s.pid))
            {
                w.Btn(p, "신고", 20, new Color(0.6f, 0.25f, 0.25f, 0.9f), 1100 - 20 - 120 - 12 - 110, -20, 110, 44, () =>
                    ReportWindow.Open(s.pid, s.uid, s.name, string.IsNullOrEmpty(s.greeting) ? "" : $"인사말: {s.greeting}", w.Font));
                if (PlayerSocial.Instance.IsRealFriend(s.pid))
                    w.Btn(p, "대화", 24, new Color(0.3f, 0.62f, 0.42f, 0.96f), 1100 - 44 - 240 - 20 - 240 - 20 - 200, -710, 200, 64, () =>
                    {
                        w.Close();
                        ChatWindow.Open(w.Font, s.pid);
                    });
            }
            if (!me && cards.Count > 0)
                w.Btn(p, "친선 전투", 24, OverlayWindow.Gold, 1100 - 44 - 240 - 20 - 240, -710, 240, 64, () =>
                {
                    w.Close();
                    if (!FriendlyBattle.Request(s)) Popups.Show("친선 전투", "지금은 친선 전투를 시작할 수 없습니다.");
                });

            // 친구 추가: 실제 플레이어 · 가상 지휘관
            if (s.real && !me && !string.IsNullOrEmpty(s.pid))
            {
                var social = PlayerSocial.Instance;
                bool friend = social.IsRealFriend(s.pid), pending = social.IsPending(s.pid);
                w.Btn(p, friend ? "친구" : pending ? "신청함" : "친구 신청", 24, friend || pending ? OverlayWindow.Dim : OverlayWindow.Blue, 1100 - 44 - 240, -710, 240, 64, () =>
                {
                    string err = social.SendRequest(s.pid, s.uid, s.name);
                    w.Close();
                    Popups.Show("친구", err ?? $"{s.name} 님에게 친구 신청을 보냈습니다.\n상대가 '받은 신청'에서 수락하면 친구가 됩니다.");
                }, !friend && !pending);
            }
            else if (!s.real && s.seed >= 0)
            {
                var social = PlayerSocial.Instance;
                bool friend = social.IsFriend(s.seed);
                w.Btn(p, friend ? "친구" : "친구 추가", 24, friend ? OverlayWindow.Dim : OverlayWindow.Blue, 1100 - 44 - 240, -710, 240, 64, () =>
                {
                    string err = social.Add(s.seed);
                    w.Close();
                    Popups.Show("친구", err ?? $"{s.name} 님과 친구가 되었습니다.");
                }, !friend);
            }
        }

        internal static string Ago(long ticksUtc)
        {
            var d = System.DateTime.UtcNow - new System.DateTime(ticksUtc, System.DateTimeKind.Utc);
            if (d.TotalMinutes < 1) return "방금";
            if (d.TotalHours < 1) return $"{(int)d.TotalMinutes}분 전";
            if (d.TotalDays < 1) return $"{(int)d.TotalHours}시간 전";
            return $"{(int)d.TotalDays}일 전";
        }

        /// <summary>(옛) 대표 파티 4명 — 다른 곳에서 쓸 수 있게 남겨 둠. 정보창은 상위 5명(PublicProfile.FromCommander)을 씀.</summary>
        public static List<(CharacterData ch, int level, int star)> TeamOf(Commander c, GameDatabase db)
        {
            var team = new List<(CharacterData, int, int)>();
            var snap = PublicProfile.FromCommander(c, db);
            foreach (var cs in snap.top)
            {
                if (team.Count >= 4) break;
                var ch = db != null ? db.GetCharacter(cs.id) : null;
                if (ch != null) team.Add((ch, cs.level, cs.star));
            }
            return team;
        }
    }

    /// <summary>다른 지휘관 캐릭터 상세: 육성(성급 · 초월 · 레벨 · 돌파 · 스킬 · 호감도) · 능력치 · 장착 장비 · 세트 효과.</summary>
    public static class CharInfoWindow
    {
        /// <param name="order">창 그리기 순서(다른 창 위에 띄워야 할 때 — 예: 프로필 창 600 위에서 열면 700)</param>
        public static void Open(CharSnap cs, string owner, TMP_FontAsset font = null, int order = 200)
        {
            var db = Object.FindAnyObjectByType<PlayerRoster>()?.Database;
            var r = SnapStats.Evaluate(cs, db);
            if (r == null) return;
            var ch = r.ch;
            var w = OverlayWindow.Open($"{owner}의 {ch.displayName}", 1340, 840, font, order: order);
            var p = w.Panel;

            // ---- 왼쪽: 그림 · 육성 ----
            var col = StarText.FrameColor(cs.star, cs.transcend);
            var frame = w.Box(p, new Color(col.r, col.g, col.b, 0.7f), 36, -84, 340, 440);
            var art = UIFactory.AddImage("Art", frame.transform, Color.white, GameArt.Portrait(ch, cs.outfit ?? "") ?? GameArt.Thumb(ch, cs.outfit ?? ""));
            art.preserveAspect = true;
            UIFactory.Stretch(art.rectTransform, 4, 4, 4, 4);
            art.enabled = art.sprite != null;
            var srow = StarRow.Create(p, 30, TextAnchor.MiddleCenter, w.Font);
            OverlayWindow.Place(srow.Rect, 36, -530, 340, 34);
            srow.Set(cs.star, cs.transcend);
            w.Text(p, $"<b>{ch.displayName}</b>", 32, Color.white, TextAlignmentOptions.Center, 36, -566, 340, 42);
            w.Text(p, $"<color={Traits.Hex(ch.element)}>{Traits.Name(ch.element)}</color> · {Traits.Name(ch.role)}", 22, new Color(0.85f, 0.88f, 0.95f),
                TextAlignmentOptions.Center, 36, -610, 340, 30);
            int cap = Leveling.LevelCap(cs.asc, cs.star >= Transcendence.MaxStar && cs.transcend >= Transcendence.MaxTranscend);
            var grow = w.Text(p,
                $"Lv.<b>{cs.level}</b> / {cap}   <color=#9AA3B5>돌파</color> {cs.asc}/{Leveling.MaxAscension}\n" +
                $"<color=#9AA3B5>초월</color> {(cs.star >= Transcendence.MaxStar ? $"+{cs.transcend}" : "-")}   <color=#9AA3B5>호감도</color> Lv.{cs.bond}\n" +
                $"<color=#FFB347>전투력 <b>{r.power:N0}</b></color>",
                22, Color.white, TextAlignmentOptions.Center, 36, -646, 340, 110);
            grow.lineSpacing = 8f;

            // ---- 가운데: 능력치 · 스킬 ----
            w.Text(p, "<b>능력치</b>", 24, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 410, -84, 400, 34);
            string Pct(float v) => $"{v * 100f:0.#}%";
            var stats = w.Text(p,
                $"<color=#9AA3B5>체력</color>  {r.hp:N0}\n<color=#9AA3B5>공격력</color>  {r.atk:N0}\n<color=#9AA3B5>방어력</color>  {r.def:N0}\n<color=#9AA3B5>속도</color>  {r.spd}\n" +
                $"<color=#9AA3B5>치명타 확률</color>  {Pct(r.critRate)}\n<color=#9AA3B5>치명타 피해</color>  {Pct(r.critDmg)}\n" +
                $"<color=#9AA3B5>{Traits.Name(ch.element)} 피해</color>  {Pct(r.elementDmg + r.allDmg)}\n<color=#9AA3B5>방어 관통</color>  {Pct(r.penetration)}\n" +
                $"<color=#9AA3B5>브레이크 효율</color>  {Pct(r.breakEffect)}\n<color=#9AA3B5>에너지 회복</color>  {Pct(r.energyRegen)}\n" +
                $"<color=#9AA3B5>치유량</color>  {Pct(r.healBonus)}\n<color=#9AA3B5>효과 명중 · 저항</color>  {Pct(r.effectHit)} · {Pct(r.effectRes)}",
                21, Color.white, TextAlignmentOptions.TopLeft, 410, -124, 400, 400);
            stats.lineSpacing = 6f;

            w.Text(p, "<b>스킬</b>", 24, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 410, -540, 400, 34);
            string[] slot = { "기본 공격", "스킬", "궁극기" };
            var sb = new System.Text.StringBuilder();
            int tr = cs.star >= Transcendence.MaxStar ? cs.transcend : 0;
            for (int i = 0; i < 3; i++)
            {
                int baseLv = cs.skills != null && i < cs.skills.Length ? cs.skills[i] : 1;
                int bonus = r.effectiveSkills[i] - baseLv;
                sb.Append($"<color=#9AA3B5>{slot[i]}</color>  <b>{r.skillNames[i]}</b>  Lv.{baseLv}{(bonus > 0 ? $" <color=#7CFF9A>+{bonus}</color>" : "")}\n");
            }
            var sk = w.Text(p, sb.ToString(), 20, Color.white, TextAlignmentOptions.TopLeft, 410, -580, 400, 140);
            sk.lineSpacing = 8f;
            sk.textWrappingMode = TextWrappingModes.Normal;

            // ---- 오른쪽: 장비 6부위 · 세트 효과 ----
            w.Text(p, "<b>장착 장비</b>", 24, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 840, -84, 460, 34);
            var bySlot = new Dictionary<EquipSlot, EquipmentItem>();
            if (cs.gear != null) foreach (var it in cs.gear) if (it != null) bySlot[it.slot] = it;
            float y = -124;
            foreach (var s in EquipStats.AllSlots)
            {
                var row = w.Box(p, new Color(1, 1, 1, 0.05f), 840, y, 464, 92);
                bySlot.TryGetValue(s, out var it);
                var icon = UIFactory.AddImage("Icon", row.transform, Color.white, it != null ? GameArt.EquipIcon(it.set, s) : GameArt.EquipSlotIcon(s));
                icon.preserveAspect = true;
                OverlayWindow.Place(icon.rectTransform, 8, -8, 76, 76);
                icon.enabled = icon.sprite != null;
                if (it == null)
                {
                    w.Text(row.transform, $"<color=#6B7280>{EquipStats.SlotName(s)} — 없음</color>", 19, Color.white, TextAlignmentOptions.Left, 94, -30, 360, 30);
                }
                else
                {
                    w.Text(row.transform, $"<b>{EquipStats.SlotName(s)}</b> {EquipSets.Rich(it.set)}  {EquipStats.Stars(it.Rarity, it.transcend)} <color=#FFD36B>+{it.level}</color>",
                        17, Color.white, TextAlignmentOptions.Left, 94, -4, 366, 26);
                    w.Text(row.transform, $"<color=#FFE6A8>{EquipStats.Format(it.main, it.MainValue)}</color>", 17, Color.white, TextAlignmentOptions.Left, 94, -30, 366, 24);
                    var subs = new List<string>();
                    foreach (var sub in it.subs) subs.Add(EquipStats.Format(sub.stat, it.SubValue(sub)));
                    var st = w.Text(row.transform, $"<color=#B8C0D0>{string.Join(" · ", subs)}</color>", 14, Color.white, TextAlignmentOptions.TopLeft, 94, -56, 366, 34);
                    st.textWrappingMode = TextWrappingModes.Normal;
                }
                y -= 98;
            }
            // 세트 효과
            var counts = new Dictionary<string, int>();
            if (cs.gear != null) foreach (var it in cs.gear) if (it != null) counts[it.set] = (counts.TryGetValue(it.set, out var n) ? n : 0) + 1;
            var lines = new List<string>();
            foreach (var kv in counts)
            {
                var set = EquipSets.Get(kv.Key);
                if (set == null || kv.Value < 2) continue;
                lines.Add($"{EquipSets.Rich(kv.Key)} {kv.Value}세트  <color=#B8C0D0>{set.desc2}{(kv.Value >= 4 ? " · " + set.desc4 : "")}</color>");
            }
            var se = w.Text(p, lines.Count > 0 ? string.Join("\n", lines) : "<color=#6B7280>세트 효과 없음</color>", 17, Color.white, TextAlignmentOptions.TopLeft, 840, -718, 464, 90);
            se.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}
