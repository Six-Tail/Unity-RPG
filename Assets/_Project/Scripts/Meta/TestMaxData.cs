using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 밸런스 테스트용 '최대 육성' 데이터(임시). 적용하기 전에 지금 세이브 전체를 이 PC 파일로 백업해 두고, 언제든 원래대로 되돌림.
    ///
    /// 최대 육성 = 게임에 있는 모든 캐릭터를
    ///  · 6★ 초월 +6 · 돌파 3 · Lv.100 · 스킬(기본 · 스킬 · 궁극기) 10 (+초월 보너스 3) · 호감도 Lv.10
    ///  · 장비 6부위: 6★ +20 · 장비 초월 +5, 역할에 맞는 세트(4+2) · 주옵션 · 보조 옵션(4줄, 강화 5회 모두 핵심 옵션에, 수치 최대)
    ///    딜러 = 자기 속성 세트 4 + 맹공 2 · 몸 치명타 피해 · 발 공격력% · 목걸이 속성 피해 · 반지 공격력%
    ///    탱커 = 수호 4 + 생명의 샘 2 · 몸 방어력% · 발 속도 · 목걸이 체력% · 반지 에너지 회복
    ///    서포터 = 신속 4 + 생명의 샘 2 · 몸 효과 명중 · 발 속도 · 목걸이 공격력% · 반지 에너지 회복
    ///    힐러 = 생명의 샘 4 + 수호 2 · 몸 치유량 · 발 속도 · 목걸이 체력% · 반지 에너지 회복
    /// 원래 끼고 있던 장비는 빼서 가방에 남김(되돌리면 원래대로).
    /// </summary>
    public static class TestMaxData
    {
        private static string BackupPath => Path.Combine(Application.persistentDataPath, "test-max-backup.json");

        /// <summary>되돌릴 백업이 있는지(= 지금 최대 육성 테스트 중).</summary>
        public static bool HasBackup => File.Exists(BackupPath);

        public static DateTime BackupTime => HasBackup ? File.GetLastWriteTime(BackupPath) : DateTime.MinValue;

        /// <summary>최대 육성 적용. 백업이 없을 때만 새로 백업(여러 번 눌러도 처음 세이브가 남음). 적용한 캐릭터 수.</summary>
        public static int Apply()
        {
            var roster = UnityEngine.Object.FindAnyObjectByType<PlayerRoster>();
            var equip = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>();
            if (roster == null || roster.Database == null) return 0;

            if (!HasBackup)
            {
                SaveStore.Save();
                File.WriteAllText(BackupPath, JsonUtility.ToJson(SaveStore.Export()));
            }

            var bond = PlayerBond.Instance;
            var rng = new System.Random(12345);
            int n = 0;
            foreach (var c in roster.Database.Characters)
            {
                if (c == null || string.IsNullOrEmpty(PlayerRoster.KeyOf(c))) continue;
                roster.TestMaxOut(c, false);
                bond.Add(c, PlayerBond.MaxExp, true);
                if (equip != null) GearUp(equip, c, rng);
                n++;
            }
            roster.Save();
            SaveStore.Save();
            // 모든 화면 · 시스템이 새 값으로 다시 읽도록 게임 다시 불러오기
            SaveStore.ReloadGame();
            return n;
        }

        /// <summary>원래 세이브로 되돌리고 게임을 다시 불러옴. 백업이 없으면 false.</summary>
        public static bool Restore()
        {
            if (!HasBackup) return false;
            SaveStore.Snapshot snap;
            try { snap = JsonUtility.FromJson<SaveStore.Snapshot>(File.ReadAllText(BackupPath)); }
            catch (Exception e) { Debug.LogError($"[TestMaxData] 백업을 읽지 못했습니다: {e.Message}"); return false; }
            if (snap == null || snap.entries == null || snap.entries.Count == 0) return false;
            SaveStore.Import(snap);
            File.Delete(BackupPath);
            // 다시 불러온 뒤 클라우드에도 원래 세이브가 올라가도록 '바뀜' 표시
            SaveStore.SetString("__testmax.restored", DateTime.Now.Ticks.ToString());
            SaveStore.DeleteKey("__testmax.restored");
            SaveStore.Save();
            SaveStore.ReloadGame();
            return true;
        }

        // ---------------- 장비 ----------------

        private struct Build
        {
            public string set4, set2;
            public EquipStat body, feet, neck, ring;
            public EquipStat[] subs;   // 우선순위 순(앞의 둘에 강화 몰아줌)
        }

        private static Build BuildFor(CharacterData c)
        {
            string elemSet = c.element switch
            {
                Element.Fire => "ember", Element.Ice => "frost", Element.Lightning => "thunder", Element.Wind => "gale", _ => "steel",
            };
            return c.role switch
            {
                Role.Tank => new Build
                {
                    set4 = "guardian", set2 = "spring",
                    body = EquipStat.DefPct, feet = EquipStat.Spd, neck = EquipStat.HPPct, ring = EquipStat.EnergyRegen,
                    subs = new[] { EquipStat.DefPct, EquipStat.HPPct, EquipStat.Spd, EquipStat.EffectRes, EquipStat.Def, EquipStat.HP },
                },
                Role.Support => new Build
                {
                    set4 = "swift", set2 = "spring",
                    body = EquipStat.EffectHit, feet = EquipStat.Spd, neck = EquipStat.AtkPct, ring = EquipStat.EnergyRegen,
                    subs = new[] { EquipStat.Spd, EquipStat.EffectHit, EquipStat.AtkPct, EquipStat.HPPct, EquipStat.EffectRes, EquipStat.DefPct },
                },
                Role.Healer => new Build
                {
                    set4 = "spring", set2 = "guardian",
                    body = EquipStat.HealBonus, feet = EquipStat.Spd, neck = EquipStat.HPPct, ring = EquipStat.EnergyRegen,
                    subs = new[] { EquipStat.HPPct, EquipStat.Spd, EquipStat.DefPct, EquipStat.EffectRes, EquipStat.HP, EquipStat.Def },
                },
                _ => new Build
                {
                    set4 = elemSet, set2 = "assault",
                    body = EquipStat.CritDmg, feet = EquipStat.AtkPct, neck = EquipStat.ElementDmg, ring = EquipStat.AtkPct,
                    subs = new[] { EquipStat.CritRate, EquipStat.CritDmg, EquipStat.AtkPct, EquipStat.Spd, EquipStat.Penetration, EquipStat.Atk },
                },
            };
        }

        private static void GearUp(PlayerEquipment equip, CharacterData c, System.Random rng)
        {
            var b = BuildFor(c);
            string key = PlayerEquipment.Key(c);
            // 원래 끼던 장비는 가방으로
            foreach (var old in equip.EquippedOn(c)) old.owner = "";

            int i = 0;
            foreach (var slot in EquipStats.AllSlots)
            {
                var main = slot switch
                {
                    EquipSlot.Head => EquipStat.HP, EquipSlot.Hands => EquipStat.Atk,
                    EquipSlot.Body => b.body, EquipSlot.Feet => b.feet, EquipSlot.Neck => b.neck, _ => b.ring,
                };
                var it = new EquipmentItem
                {
                    uid = Guid.NewGuid().ToString("N"),
                    set = i < 4 ? b.set4 : b.set2,
                    rarity = EquipStats.MaxRarity, slot = slot, main = main,
                    level = EquipStats.MaxLevel, initialSubs = 4, transcend = EquipStats.MaxTranscend,
                    owner = key, locked = true,
                };
                // 보조 옵션 4줄: 우선순위에서 주옵션과 겹치지 않는 것. 강화 5회는 앞의 두 줄에 3 · 2회
                foreach (var s in b.subs)
                {
                    if (it.subs.Count >= 4) break;
                    if (s == main) continue;
                    it.subs.Add(new EquipSub { stat = s, value = EquipStats.SubUnit(s), rolls = 1 });
                }
                int[] extra = { 3, 2, 0, 0 };
                for (int k = 0; k < it.subs.Count; k++)
                {
                    it.subs[k].rolls += extra[k];
                    it.subs[k].value = EquipStats.SubUnit(it.subs[k].stat) * it.subs[k].rolls;
                }
                equip.Add(it, true);   // 알림 = 저장
                i++;
            }
        }
    }
}
