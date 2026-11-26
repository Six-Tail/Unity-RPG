using System.Collections.Generic;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 길드 랭킹: 가상 길드(번호 1 ~ <see cref="VirtualGuilds.RankPool"/>) + 실제 길드(서버 g_info, 마지막으로 불러온 값) + 내 길드를 한 표에.
    /// 탭 = 이번 주 길드 보스 피해 · 이번 주 길드전 별 · 길드 레벨(경험치).
    /// </summary>
    public static class GuildRanking
    {
        public enum Tab { Boss, War, Level }

        public static string TabName(Tab t) => t switch { Tab.Boss => "길드 보스", Tab.War => "길드전", _ => "길드 레벨" };

        /// <summary>내 길드(지금 기록).</summary>
        public static GuildInfo Mine()
        {
            var g = PlayerGuild.Instance;
            if (!g.Joined) return null;
            long exp = g.Exp;
            for (int l = 1; l < g.Level; l++) exp += PlayerGuild.ExpForNext(l);
            if (g.IsReal && g.RealInfo != null) exp = g.RealInfo.exp;
            return new GuildInfo
            {
                seed = g.IsReal ? -1 : g.Seed, gid = g.Gid, name = g.Name, intro = g.Intro, level = g.Level, members = g.Members,
                bossDamage = g.GuildBossTotal(), warStars = g.GuildWarStars(), exp = exp, mine = true,
            };
        }

        public static long Score(GuildInfo g, Tab t) => t switch { Tab.Boss => g.bossDamage, Tab.War => g.warStars, _ => g.exp };

        /// <summary>순위표(높은 순).</summary>
        public static List<GuildInfo> Build(Tab tab)
        {
            var g = PlayerGuild.Instance;
            var list = VirtualGuilds.Pool(g.Week, VirtualGuilds.WeekProgress());
            var mine = Mine();
            if (mine != null && !mine.Real && mine.seed >= 1 && mine.seed <= VirtualGuilds.RankPool) list.RemoveAll(x => x.seed == mine.seed);
            foreach (var r in GuildLink.Instance.Ranked)
                if (mine == null || r.gid != mine.gid) list.Add(r);
            if (mine != null) list.Add(mine);
            list.Sort((a, b) =>
            {
                int k = Score(b, tab).CompareTo(Score(a, tab));
                if (k == 0) k = b.level.CompareTo(a.level);
                if (k == 0) k = b.members.CompareTo(a.members);
                return k;
            });
            return list;
        }

        /// <summary>내 길드 순위(1부터, 길드가 없으면 0)와 전체 길드 수.</summary>
        public static (int rank, int total) MyRank(Tab tab)
        {
            var list = Build(tab);
            for (int i = 0; i < list.Count; i++) if (list[i].mine) return (i + 1, list.Count);
            return (0, list.Count);
        }
    }
}
