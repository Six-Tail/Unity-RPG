using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta
{
    /// <summary>길드 1곳의 소개(가상 길드는 번호로 늘 같은 길드 · 실제 길드는 길드장 공개 데이터).</summary>
    public class GuildInfo
    {
        public int seed;                // 가상 길드 번호(실제 길드면 -1)
        public string gid = "";         // 실제 길드 id(= 길드장 플레이어 id)
        public string name, intro;
        public int level, members, minLevel;
        public bool free = true;        // 자유 가입(아니면 길드장 승인)
        public bool Real => !string.IsNullOrEmpty(gid);
        // 순위표용(이번 주)
        public long bossDamage;
        public int warStars;
        public long exp;
        public string leaderName = "";
        public bool mine;               // 내 길드(순위표 표시용)
    }

    /// <summary>
    /// 가상 길드: 서버가 없어도 모든 PC에서 똑같이 계산되는 길드들(이름 · 레벨 · 길드원 · 이번 주 길드 보스 피해 · 길드전 별).
    /// 길드 랭킹에는 가상 길드와 실제 길드가 함께 오르고, 길드전 상대는 늘 가상 길드입니다.
    /// </summary>
    public static class VirtualGuilds
    {
        /// <summary>순위표에 늘 있는 가상 길드 수(번호 1 ~ RankPool).</summary>
        public const int RankPool = 200;

        private static readonly string[] GA = { "별빛", "새벽", "푸른 닻", "은빛", "황금", "고요한", "붉은 달", "서리", "폭풍", "천둥", "달그림자", "청람" };
        private static readonly string[] GB = { "기사단", "원정대", "항해단", "연합", "수호대", "여단", "길드", "상회", "결사", "기병대" };
        private static readonly string[] Intros =
        {
            "매일 출석만 해도 환영합니다!", "주간 보스 같이 달려요", "초보 지휘관 대환영", "결투장 상위권 목표",
            "느긋하게 즐기는 길드", "기부 열심히 하실 분", "스토리 좋아하는 사람들", "닻을 올리고 별을 향해",
            "길드전 별 세 개가 목표!", "길드 보스 끝까지 잡아요",
        };

        public static GuildInfo Info(int seed)
        {
            var rng = new System.Random(seed * 6151 + 7);
            var g = new GuildInfo { seed = seed };
            g.name = GA[rng.Next(GA.Length)] + " " + GB[rng.Next(GB.Length)];
            g.level = 1 + rng.Next(PlayerGuild.MaxLevel - 1);
            g.members = Mathf.Min(PlayerGuild.MaxMembers - 1, 8 + g.level * 2 + rng.Next(8));
            g.minLevel = new[] { 1, 1, 5, 10, 20 }[rng.Next(5)];
            g.intro = Intros[rng.Next(Intros.Length)];
            g.free = true;
            return g;
        }

        /// <summary>이번 주가 얼마나 지났는가(0 = 월요일 05:00 · 1 = 다음 월요일 05:00).</summary>
        public static float WeekProgress()
        {
            var left = PlayerShop.UntilReset(ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero;
            return Mathf.Clamp01(1f - (float)(left.TotalHours / (7 * 24)));
        }

        private static double Noise(int seed, string week, int salt)
        {
            var rng = new System.Random(seed * 31 + Commanders.Hash(week) * 17 + salt);
            return rng.NextDouble();
        }

        /// <summary>가상 길드원 1명이 한 주 동안 길드 보스에 주는 피해(길드 레벨이 높을수록 강함).</summary>
        public static long MemberBossWeek(int level) => (long)(120_000_000L * (0.6 + 0.25 * level));

        /// <summary>가상 길드의 이번 주 길드 보스 누적 피해(지금 시점, 가상 길드원 전원 — 나 제외).</summary>
        public static long BossDamage(int seed, int members, int level, string week, float progress)
        {
            double n = Noise(seed, week, 1);
            double weekTotal = members * MemberBossWeek(level) * (0.55 + 0.9 * n);
            return (long)(weekTotal * Math.Pow(Mathf.Clamp01(progress), 0.9));
        }

        /// <summary>가상 길드가 이번 주 길드전에서 얻은 별(0 ~ 30, 지금 시점).</summary>
        public static int WarStars(int seed, int level, string week, float progress)
        {
            double n = Noise(seed, week, 2);
            double final = GuildWar.MaxStars * Math.Min(0.97, 0.42 + 0.04 * level + 0.22 * n);
            return Mathf.Clamp((int)Math.Round(final * Math.Min(1.0, progress * 1.35)), 0, GuildWar.MaxStars);
        }

        /// <summary>순위표의 가상 길드(번호 1 ~ RankPool) — 이번 주 기록 포함.</summary>
        public static List<GuildInfo> Pool(string week, float progress)
        {
            var list = new List<GuildInfo>(RankPool);
            for (int s = 1; s <= RankPool; s++) list.Add(WithWeek(Info(s), week, progress));
            return list;
        }

        public static GuildInfo WithWeek(GuildInfo g, string week, float progress)
        {
            g.bossDamage = BossDamage(g.seed, g.members, g.level, week, progress);
            g.warStars = WarStars(g.seed, g.level, week, progress);
            long exp = 0;
            for (int l = 1; l < g.level; l++) exp += PlayerGuild.ExpForNext(l);
            g.exp = exp + (long)(Noise(g.seed, "exp", 3) * PlayerGuild.ExpForNext(g.level) * 0.9);
            return g;
        }
    }
}
