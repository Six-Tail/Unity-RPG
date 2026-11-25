#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using SubcultureRPG.Battle;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 주간 보스 밸런스 시뮬레이션: 화면 연출 없이 전투 엔진만 돌려 '제한 시간 동안 준 피해 · 버틴 시간 · 쓰러진 수'를 봅니다.
    /// 메뉴 SubcultureRPG > 주간 보스 밸런스 시뮬레이션 → 결과는 Temp/raid_sim_result.txt.
    ///
    /// 파티: ① 이 PC 세이브의 주력 4명(레벨 · 성급 · 초월 · 장비 그대로) ② 같은 4명을 장비 없이 Lv.40 / 70 / 100 으로.
    /// 시간: 한 행동 = SecPerAction 초(실제 플레이 기록: 300초에 141행동 ≈ 2.13초)로 보고 30초마다 격노를 올림.
    /// 자동 실행: Temp/raid_sim_request.txt 가 생기면 실행(내용 key=value 줄로 보스 수치를 바꿔 볼 수 있음 — 아래 Overrides).
    /// </summary>
    [InitializeOnLoad]
    public static class RaidBalanceSim
    {
        private const string Request = "Temp/raid_sim_request.txt", Result = "Temp/raid_sim_result.txt";
        private const int Seeds = 8;
        private static double _next;

        static RaidBalanceSim()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(Request)) return;
            string text = File.ReadAllText(Request);
            File.Delete(Request);
            try { Run(Parse(text)); }
            catch (Exception e) { File.WriteAllText(Result, "오류: " + e); }
        }

        [MenuItem("SubcultureRPG/주간 보스 밸런스 시뮬레이션", false, 61)]
        private static void Menu()
        {
            Run(new Dictionary<string, float>());
            EditorUtility.RevealInFinder(Path.GetFullPath(Result));
        }

        private static Dictionary<string, float> Parse(string text)
        {
            var d = new Dictionary<string, float>();
            foreach (var line in text.Split('\n'))
            {
                var kv = line.Split('=');
                if (kv.Length == 2 && float.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    d[kv[0].Trim()] = v;
            }
            return d;
        }

        private static float Get(Dictionary<string, float> o, string k, float def) => o.TryGetValue(k, out var v) ? v : def;

        // ---------------- 파티 ----------------

        private class Member { public CharacterData c; public int level, star, transcend, skill; public int[] skills; public StatBonus bonus; }

        private static T Hidden<T>() where T : MonoBehaviour
        {
            var go = new GameObject("RaidSim_" + typeof(T).Name) { hideFlags = HideFlags.HideAndDontSave };
            return go.AddComponent<T>();
        }

        /// <summary>세이브를 읽은 로스터 · 장비(플레이 중이면 실제 것, 아니면 씬에 안 남는 임시 복사본).</summary>
        private static (PlayerRoster roster, PlayerEquipment eq, List<UnityEngine.Object> temp) LoadSave()
        {
            var temp = new List<UnityEngine.Object>();
            var sceneRoster = UnityEngine.Object.FindAnyObjectByType<PlayerRoster>(FindObjectsInactive.Include);
            if (sceneRoster == null) throw new Exception("씬에 PlayerRoster 가 없습니다(Main 씬을 여세요).");
            if (Application.isPlaying)
                return (sceneRoster, UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>(), temp);

            var r = Hidden<PlayerRoster>();
            temp.Add(r.gameObject);
            typeof(PlayerRoster).GetField("database", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(r, sceneRoster.Database);
            r.Load();
            var e = Hidden<PlayerEquipment>();
            temp.Add(e.gameObject);
            typeof(PlayerEquipment).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(e, null);
            return (r, e, temp);
        }

        private static BattleUnitData Build(Member m, GameDatabase db)
        {
            Func<CharacterData, BattleUnitData> lookup = db != null ? db.GetUnit : null;
            var u = RuntimeUnits.FromCharacter(m.c, m.level, lookup, m.skill, m.star, m.transcend, m.skills);
            if (lookup != null && lookup(m.c) == u) u = UnityEngine.Object.Instantiate(u);
            if (m.bonus != null) u.bonus = m.bonus;
            return u;
        }

        // ---------------- 실행 ----------------

        private struct Outcome { public long dmg; public float time; public int dead; public float firstDeath; public long healed, taken; public int actions, bossActions; }

        private static Outcome Simulate(List<BattleUnitData> party, int bossLevel, Dictionary<string, float> o, int seed)
        {
            var boss = WorldRaid.CreateBoss(bossLevel);
            float atkMul = Get(o, "atkMul", 1f);
            boss.baseStats.atk = Mathf.RoundToInt(boss.baseStats.atk * atkMul);
            boss.baseStats.atkGrowth = Mathf.RoundToInt(boss.baseStats.atkGrowth * atkMul);
            if (o.ContainsKey("spd")) boss.baseStats.spd = Mathf.RoundToInt(o["spd"]);
            if (o.ContainsKey("defMul")) { boss.baseStats.def = Mathf.RoundToInt(boss.baseStats.def * o["defMul"]); boss.baseStats.defGrowth = Mathf.RoundToInt(boss.baseStats.defGrowth * o["defMul"]); }
            if (o.ContainsKey("hpMul")) foreach (var s in boss.skills) if (!s.IsUltimate) s.maxHpDamage *= o["hpMul"];

            float limit = Get(o, "time", WorldRaid.TimeLimit), every = Get(o, "every", WorldRaid.EnrageEvery);
            float enrage = Get(o, "enrage", WorldRaid.EnrageAtk), spa = Get(o, "spa", 300f / 141f);

            var rng = new System.Random(seed);
            var eng = new BattleEngine(party, new[] { boss }, null, () => (float)rng.NextDouble());
            var b = eng.enemies[0];
            int baseAtk = b.atk, stage = 0;
            float t = 0f, firstDeath = -1f;
            int actions = 0, bossActs = 0;
            while (!eng.IsOver && t < limit)
            {
                int st = Mathf.FloorToInt(t / every);
                if (st != stage)
                {
                    stage = st;
                    WorldRaid.ApplyEnrage(b, stage, baseAtk);
                    if (o.ContainsKey("enrage")) b.atk = Mathf.RoundToInt(baseAtk * (1f + enrage * stage));
                    if (o.ContainsKey("horizon")) foreach (var s in b.skills) if (s.IsUltimate) s.maxHpDamage = o["horizon"] + Get(o, "horizonStep", WorldRaid.HorizonPerStage) * stage;
                }
                var actor = eng.NextActor();
                if (actor == null) break;
                eng.StartTurn(actor);
                eng.DrainExtras();
                if (eng.IsOver) break;
                if (!actor.IsAlive || actor.skipTurn) { t += spa * 0.4f; continue; }
                var (skill, target) = actor.team == Team.Ally ? eng.PickAutoAction(actor) : eng.PickAction(actor);
                eng.ResolveAction(actor, skill, target);
                eng.DrainExtras();
                actions++;
                if (actor.team == Team.Enemy) bossActs++;
                t += spa * (skill != null && skill.IsUltimate ? 1.6f : 1f);
                while (!eng.IsOver && eng.NextRecast(actor, out var rs, out var rt)) { eng.ResolveAction(actor, rs, rt); eng.DrainExtras(); actions++; t += spa * 0.8f; }
                if (firstDeath < 0f) foreach (var a in eng.allies) if (!a.IsAlive) { firstDeath = t; break; }
            }
            var res = new Outcome { dmg = b.record.taken, time = Mathf.Min(t, limit), firstDeath = firstDeath, actions = actions, bossActions = bossActs };
            foreach (var a in eng.allies) { if (!a.IsAlive) res.dead++; res.healed += a.record.healed; res.taken += a.record.taken; }
            return res;
        }

        public static void Run(Dictionary<string, float> o)
        {
            var sb = new StringBuilder();
            var (roster, eq, temp) = LoadSave();
            try
            {
                var db = roster.Database;
                // 주력 4명: 레벨 → 성급 → 초월 순(CombatPower 는 임시 유닛을 Destroy 해서 에디터 모드에서 경고가 쏟아지므로 쓰지 않음)
                var owned = new List<CharacterData>();
                foreach (var oc in roster.Owned) { var c = db.GetCharacter(oc.id); if (c != null) owned.Add(c); }
                owned.Sort((x, y) =>
                {
                    int k = roster.LevelOf(y).CompareTo(roster.LevelOf(x));
                    if (k == 0) k = roster.StarOf(y).CompareTo(roster.StarOf(x));
                    if (k == 0) k = roster.TranscendOf(y).CompareTo(roster.TranscendOf(x));
                    return k;
                });
                var core = owned.GetRange(0, Mathf.Min(4, owned.Count));
                if (core.Count == 0) throw new Exception("세이브에 보유 캐릭터가 없습니다.");

                sb.AppendLine($"주간 보스 시뮬레이션 — {DateTime.Now:yyyy-MM-dd HH:mm}  (씨앗 {Seeds}개 평균)");
                sb.AppendLine("보스 조정: " + (o.Count == 0 ? "없음(현재 코드 값)" : string.Join(", ", o)));
                sb.AppendLine($"제한 {Get(o, "time", WorldRaid.TimeLimit)}초 · 격노 {Get(o, "every", WorldRaid.EnrageEvery)}초마다 +{Get(o, "enrage", WorldRaid.EnrageAtk) * 100f:0}%");
                sb.AppendLine();

                void Scenario(string title, List<Member> members, int bossLevel)
                {
                    long dmg = 0, healed = 0, taken = 0; float time = 0f, fd = 0f; int dead = 0, wipes = 0, fdN = 0, acts = 0, bacts = 0;
                    for (int s = 0; s < Seeds; s++)
                    {
                        var party = new List<BattleUnitData>();
                        foreach (var m in members) party.Add(Build(m, db));
                        var r = Simulate(party, bossLevel, o, 1000 + s * 7919);
                        dmg += r.dmg; healed += r.healed; taken += r.taken; time += r.time; dead += r.dead; acts += r.actions; bacts += r.bossActions;
                        if (r.dead >= members.Count) wipes++;
                        if (r.firstDeath >= 0f) { fd += r.firstDeath; fdN++; }
                    }
                    var probe = Build(members[0], db);
                    var pu = new BattleUnit(probe, Team.Ally, BattleConfig.CreateDefault());
                    var bu = new BattleUnit(WorldRaid.CreateBoss(bossLevel), Team.Enemy, BattleConfig.CreateDefault());
                    sb.AppendLine($"■ {title}  (보스 Lv.{bossLevel} · 공격 {bu.atk:N0} · 방어 {bu.def:N0} · 속도 {Get(o, "spd", bu.spd):0})");
                    sb.AppendLine($"   파티: {string.Join(", ", members.ConvertAll(m => $"{m.c.displayName} Lv.{m.level} {m.star}★+{m.transcend}{(m.bonus != null ? " 장비" : "")}"))}");
                    var spds = new List<string>();
                    foreach (var m in members) { var x = new BattleUnit(Build(m, db), Team.Ally, BattleConfig.CreateDefault()); spds.Add($"{x.spd:0}"); }
                    sb.AppendLine($"   첫 캐릭터 체력 {pu.maxHP:N0} · 방어 {pu.def:N0} · 파티 속도 {string.Join("/", spds)}");
                    sb.AppendLine($"   준 피해 평균 {dmg / Seeds:N0} · 버틴 시간 {time / Seeds:0}초 · 행동 {acts / Seeds}회 · 쓰러짐 {dead / (float)Seeds:0.0}명 · 전멸 {wipes}/{Seeds}" +
                                  (fdN > 0 ? $" · 첫 쓰러짐 {fd / fdN:0}초" : " · 쓰러짐 없음"));
                    sb.AppendLine($"   받은 피해 {taken / Seeds:N0} · 실제 회복 {healed / Seeds:N0} · 보스 행동 {bacts / Seeds}회");
                    sb.AppendLine();
                }

                var save = new List<Member>();
                int avg = 0;
                foreach (var c in core)
                {
                    save.Add(new Member { c = c, level = roster.LevelOf(c), star = roster.StarOf(c), transcend = roster.TranscendOf(c),
                        skill = roster.SkillLevelOf(c), skills = roster.EffectiveSkillLevels(c), bonus = eq != null ? eq.BonusFor(c) : null });
                    avg += roster.LevelOf(c);
                }
                avg = Mathf.Clamp(avg / core.Count, 10, Leveling.AwakenedMaxLevel);
                Scenario("이 PC 세이브 주력 4명(장비 포함)", save, avg);

                foreach (int lv in new[] { 40, 70, 100 })
                {
                    var bare = new List<Member>();
                    foreach (var c in core)
                        bare.Add(new Member { c = c, level = lv, star = (int)c.rarity, transcend = 0, skill = Mathf.Clamp(lv / 10, 1, 10) });
                    Scenario($"같은 4명 · 장비 없음 · Lv.{lv}", bare, lv);
                }
            }
            finally
            {
                foreach (var t in temp) if (t != null) UnityEngine.Object.DestroyImmediate(t);
            }
            File.WriteAllText(Result, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[RaidBalanceSim] " + Path.GetFullPath(Result));
        }
    }
}
#endif
