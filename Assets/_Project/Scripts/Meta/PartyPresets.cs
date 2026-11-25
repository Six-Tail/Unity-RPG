using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 편성 프리셋(5칸) + 직전 출격 파티. 편성 화면(PartyScreen)에서 저장 · 불러오기.
    /// 캐릭터 id 로 저장하므로 나중에 보유하지 않게 된 캐릭터는 불러올 때 빠집니다. 저장: PlayerPrefs "party.presets".
    /// </summary>
    public static class PartyPresets
    {
        public const int Slots = 5;
        private const string SaveKey = "party.presets";

        [Serializable] private class Slot { public List<string> ids = new(); }
        [Serializable] private class SaveData { public List<Slot> slots = new(); public List<string> last = new(); }

        private static SaveData _d;

        private static SaveData D
        {
            get
            {
                if (_d != null) return _d;
                _d = new SaveData();
                if (SaveStore.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
                _d.slots ??= new List<Slot>();
                _d.last ??= new List<string>();
                while (_d.slots.Count < Slots) _d.slots.Add(new Slot());
                return _d;
            }
        }

        private static void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(D));
            SaveStore.Save();
        }

        private static List<string> Ids(IEnumerable<CharacterData> party)
        {
            var l = new List<string>();
            if (party != null) foreach (var c in party) { var k = PlayerRoster.KeyOf(c); if (!string.IsNullOrEmpty(k)) l.Add(k); }
            return l;
        }

        public static int CountOf(int slot) => slot >= 0 && slot < Slots ? D.slots[slot].ids.Count : 0;
        public static bool IsEmpty(int slot) => CountOf(slot) == 0;

        public static void Set(int slot, IEnumerable<CharacterData> party)
        {
            if (slot < 0 || slot >= Slots) return;
            D.slots[slot].ids = Ids(party);
            Save();
        }

        /// <summary>프리셋의 캐릭터(보유한 캐릭터만, 저장 순서 그대로).</summary>
        public static List<CharacterData> Get(int slot, PlayerRoster roster) =>
            slot >= 0 && slot < Slots ? Resolve(D.slots[slot].ids, roster) : new List<CharacterData>();

        /// <summary>직전에 출격한 파티.</summary>
        public static List<CharacterData> Last(PlayerRoster roster) => Resolve(D.last, roster);

        public static void SetLast(IEnumerable<CharacterData> party)
        {
            D.last = Ids(party);
            Save();
        }

        private static List<CharacterData> Resolve(List<string> ids, PlayerRoster roster)
        {
            var l = new List<CharacterData>();
            if (ids == null || roster == null || roster.Database == null) return l;
            foreach (var id in ids)
            {
                var c = roster.Database.characters.Find(x => x != null && PlayerRoster.KeyOf(x) == id);
                if (c != null && roster.IsOwned(c) && !l.Contains(c)) l.Add(c);
            }
            return l;
        }
    }
}
