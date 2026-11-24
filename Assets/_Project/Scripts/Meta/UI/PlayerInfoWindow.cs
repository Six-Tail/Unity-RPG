using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 다른 지휘관 정보창: 친구 · 받은 신청 · 친구 찾기 · 길드원 · 결투장 랭킹에서 그 지휘관을 누르면 열림.
    /// 테두리 · 아바타 · 이름 · 칭호 · 레벨 · UID · 인사말 · 마지막 접속 · 전투력 · (결투장 순위) · 대표 파티 4명 · 친구 추가.
    /// 가상 지휘관이라 대표 파티는 번호(seed)로 늘 같게 만들어 냄.
    /// </summary>
    public static class PlayerInfoWindow
    {
        /// <param name="arena">결투장 랭킹에서 열 때 덧붙일 줄(순위 · 티어 · 점수), 없으면 null</param>
        public static void Open(Commander c, TMP_FontAsset font = null, string arena = null)
        {
            if (c == null) return;
            var db = Object.FindAnyObjectByType<PlayerRoster>()?.Database;
            var w = OverlayWindow.Open("지휘관 정보", 1100, 760, font);
            var p = w.Panel;

            // 아바타 + 기본 정보
            var av = new ProfileAvatar(p, 190);
            OverlayWindow.Place(av.Rect, 44, -92, 190, 190);
            av.Set(Commanders.Avatar(c, db), c.frameId);

            var title = ProfileRewards.Title(c.titleId);
            w.Text(p, $"<b>{c.name}</b>   <size=60%><color=#{ColorUtility.ToHtmlStringRGB(title.color)}>{title.name}</color></size>", 40, Color.white,
                TextAlignmentOptions.Left, 270, -92, 780, 56);
            w.Text(p, $"Lv.<b>{c.level}</b>    <color=#9AA3B5>UID</color> {c.uid}    {Commanders.LastSeen(c)}", 24, new Color(0.88f, 0.9f, 0.96f),
                TextAlignmentOptions.Left, 270, -154, 780, 36);
            w.Text(p, $"<color=#FFB347>전투력 <b>{c.power:N0}</b></color>" + (arena != null ? $"     {arena}" : ""), 26, Color.white,
                TextAlignmentOptions.Left, 270, -196, 780, 40);
            var greet = w.Text(p, string.IsNullOrEmpty(c.greeting) ? "<color=#6B7280>인사말이 없습니다</color>" : $"\"{c.greeting}\"", 22,
                new Color(0.78f, 0.82f, 0.9f), TextAlignmentOptions.Left, 270, -240, 780, 40);
            greet.fontStyle = FontStyles.Italic;

            // 대표 파티 4명
            w.Text(p, "<b>대표 파티</b>", 24, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 44, -312, 300, 34);
            var team = TeamOf(c, db);
            for (int i = 0; i < team.Count; i++)
            {
                var (ch, lv, star) = team[i];
                float x = 44 + i * 254;
                var card = w.Box(p, new Color(1, 1, 1, 0.06f), x, -356, 240, 290);
                var thumb = UIFactory.AddImage("Thumb", card.transform, Color.white, GameArt.Thumb(ch));
                thumb.preserveAspect = true;
                OverlayWindow.Place(thumb.rectTransform, 30, -14, 180, 180);
                thumb.enabled = thumb.sprite != null;
                w.Text(card.transform, $"<color={StarHex(star)}>{new string('★', star)}</color>", 20, Color.white, TextAlignmentOptions.Center, 0, -196, 240, 26);
                w.Text(card.transform, $"<b>{ch.displayName}</b>", 24, Color.white, TextAlignmentOptions.Center, 0, -222, 240, 32);
                w.Text(card.transform, $"<color={Traits.Hex(ch.element)}>{Traits.Name(ch.element)}</color> · {Traits.Name(ch.role)}   Lv.{lv}", 17,
                    new Color(0.82f, 0.85f, 0.92f), TextAlignmentOptions.Center, 0, -254, 240, 26);
            }
            if (team.Count == 0)
                w.Text(p, "<color=#6B7280>정보 없음</color>", 22, Color.white, TextAlignmentOptions.Left, 44, -360, 400, 34);

            // 친구 추가
            var social = PlayerSocial.Instance;
            bool friend = social.IsFriend(c.seed);
            w.Btn(p, friend ? "친구" : "친구 추가", 24, friend ? OverlayWindow.Dim : OverlayWindow.Blue, 1100 - 44 - 240, -672, 240, 64, () =>
            {
                string err = social.Add(c.seed);
                w.Close();
                Popups.Show("친구", err ?? $"{c.name} 님과 친구가 되었습니다.");
            }, !friend);
        }

        /// <summary>대표 파티: 번호(seed)로 늘 같은 4명. 지휘관 레벨이 높을수록 높은 성급 · 레벨.</summary>
        public static List<(CharacterData ch, int level, int star)> TeamOf(Commander c, GameDatabase db)
        {
            var team = new List<(CharacterData, int, int)>();
            if (db == null || db.Characters.Count == 0) return team;
            var rng = new System.Random(c.seed * 131 + 7);
            var pool = new List<CharacterData>();
            foreach (var ch in db.Characters)
                if (ch != null && ((int)ch.rarity <= 4 || rng.NextDouble() < Mathf.Clamp01(c.level / 90f + ((int)ch.rarity == 5 ? 0.25f : 0f))))
                    pool.Add(ch);
            // 대표 캐릭터(아바타)가 있으면 맨 앞
            var lead = Commanders.Avatar(c, db);
            if (lead != null) { pool.Remove(lead); pool.Insert(0, lead); }
            for (int i = 1; i < pool.Count; i++) { int j = 1 + rng.Next(pool.Count - 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            for (int i = 0; i < pool.Count && team.Count < 4; i++)
            {
                var ch = pool[i];
                int lv = Mathf.Clamp(c.level - rng.Next(0, 8), 1, 80);
                int star = Mathf.Clamp((int)ch.rarity + (c.level >= 60 && rng.Next(3) == 0 ? 1 : 0), (int)ch.rarity, 6);
                team.Add((ch, lv, star));
            }
            return team;
        }

        private static string StarHex(int star) => "#" + ColorUtility.ToHtmlStringRGB(StarText.ColorOf(star));
    }
}
