using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>다른 지휘관 1명(가상 — 서버가 없어 번호(seed)로 늘 같은 모습을 만들어 냄).</summary>
    public class Commander
    {
        public int seed;
        public string uid, name, titleId, frameId, avatarId, greeting;
        public int level;
        public long power;      // 주력 4명 전투력
    }

    /// <summary>
    /// 가상 지휘관 생성기: 결투장 랭킹 · 친구 · 길드원이 함께 씀. 같은 번호 → 늘 같은 지휘관.
    /// UID(8자리) = 10000000 + 번호.
    /// </summary>
    public static class Commanders
    {
        public const int MaxSeed = 89_999_999;

        private static readonly string[] A = { "별빛", "새벽", "푸른", "은빛", "붉은", "고요한", "달빛", "서리", "바람", "번개", "황금" };
        private static readonly string[] B = { "여행자", "검사", "고양이", "지휘관", "닻", "날개", "마법사", "수호자", "모험가" };
        private static readonly string[] Greetings =
        {
            "오늘도 출격!", "같이 주간 보스 잡아요", "결투장에서 만나요", "뽑기는 계획적으로...", "별의 가호가 함께하길",
            "천천히 즐기는 중입니다", "친구 환영해요", "닻을 올려라!", "",
        };

        /// <summary>이름 조합 수(11 × 9 = 99, 모두 다름).</summary>
        public const int NameCount = 99;
        public static string NameOf(int index)
        {
            index = ((index % NameCount) + NameCount) % NameCount;
            return A[index % A.Length] + B[(index / A.Length) % B.Length];
        }

        public static int SeedOfUid(string uid) =>
            int.TryParse((uid ?? "").Trim(), out int n) && n >= 10_000_000 && n <= 10_000_000 + MaxSeed ? n - 10_000_000 : -1;

        public static Commander FromSeed(int seed, GameDatabase db)
        {
            seed = Mathf.Clamp(seed, 0, MaxSeed);
            var rng = new System.Random(seed * 7919 + 13);
            var c = new Commander { seed = seed, uid = (10_000_000 + seed).ToString() };
            c.name = NameOf(rng.Next(NameCount)) + (rng.Next(3) == 0 ? rng.Next(1, 100).ToString() : "");
            c.level = 3 + rng.Next(78);
            c.titleId = ProfileRewards.Titles[Mathf.Min(ProfileRewards.Titles.Count - 1, rng.Next(1 + c.level / 8))].id;
            c.frameId = ProfileRewards.Frames[Mathf.Min(6, rng.Next(1 + c.level / 12))].id;
            c.greeting = Greetings[rng.Next(Greetings.Length)];
            c.power = (long)(4 * (12000 + c.level * c.level * 60) * (0.8 + rng.NextDouble() * 0.4));
            if (db != null && db.Characters.Count > 0)
            {
                var ch = db.Characters[rng.Next(db.Characters.Count)];
                if (ch != null) c.avatarId = !string.IsNullOrEmpty(ch.id) ? ch.id : ch.name;
            }
            return c;
        }

        public static CharacterData Avatar(Commander c, GameDatabase db) =>
            c != null && db != null && !string.IsNullOrEmpty(c.avatarId) ? db.GetCharacter(c.avatarId) : null;

        /// <summary>마지막 접속 문구(날마다 달라짐).</summary>
        public static string LastSeen(Commander c)
        {
            var rng = new System.Random(c.seed * 31 + DateTime.Now.DayOfYear * 17 + DateTime.Now.Hour / 6);
            int r = rng.Next(10);
            if (r < 3) return "<color=#7CFF9A>접속 중</color>";
            if (r < 8) return $"{1 + rng.Next(23)}시간 전";
            return $"{1 + rng.Next(5)}일 전";
        }

        /// <summary>문자열 → 늘 같은 수(런타임마다 달라지는 string.GetHashCode 대신).</summary>
        public static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in s ?? "") h = h * 31 + ch;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
