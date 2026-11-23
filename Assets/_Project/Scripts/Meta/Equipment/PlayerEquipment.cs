using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 보유 장비(PlayerPrefs "equipment"). 장착 · 해제 · 강화(금화 + 장비 강화석) · 분해 · 능력치 합산.
    /// 한 캐릭터는 부위마다 1개씩, 최대 6개 장착.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Equipment")]
    public class PlayerEquipment : MonoBehaviour
    {
        [SerializeField] private string saveKey = "equipment";
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;

        [Serializable] private class SaveData { public List<EquipmentItem> items = new(); }

        private readonly List<EquipmentItem> _items = new();
        private readonly System.Random _rng = new();

        public IReadOnlyList<EquipmentItem> Items => _items;
        public event Action OnChanged;

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            Load();
        }

        // ---------------- 조회 ----------------

        public static string Key(CharacterData c) => PlayerRoster.KeyOf(c) ?? "";

        public List<EquipmentItem> EquippedOn(CharacterData c)
        {
            string k = Key(c);
            return string.IsNullOrEmpty(k) ? new List<EquipmentItem>() : _items.FindAll(i => i.owner == k);
        }

        public EquipmentItem EquippedIn(CharacterData c, EquipSlot slot)
        {
            string k = Key(c);
            return _items.Find(i => i.owner == k && i.slot == slot);
        }

        public List<EquipmentItem> OfSlot(EquipSlot slot) => _items.FindAll(i => i.slot == slot);

        /// <summary>캐릭터 장착 장비의 능력치 + 세트 효과.</summary>
        public StatBonus BonusFor(CharacterData c) => EquipRules.BonusOf(EquippedOn(c));

        // ---------------- 획득 ----------------

        public EquipmentItem Add(EquipmentItem it, bool notify = true)
        {
            if (it == null) return null;
            if (string.IsNullOrEmpty(it.uid)) it.uid = Guid.NewGuid().ToString("N");
            _items.Add(it);
            if (notify) Changed();
            return it;
        }

        /// <summary>세트 후보 중 무작위 세트·부위, 성급 가중치(3·4·5·6성)로 생성해 보유.</summary>
        public EquipmentItem AddRandom(IList<string> sets, IList<int> rarityWeights = null)
        {
            if (sets == null || sets.Count == 0) return null;
            int rarity = EquipRules.RollRarity(rarityWeights, _rng);
            return Add(EquipRules.Generate(sets[_rng.Next(sets.Count)], _rng, rarity));
        }

        // ---------------- 장착 ----------------

        /// <summary>장착. 같은 부위 기존 장비는 해제되고, 다른 캐릭터가 끼고 있던 장비면 서로 맞바꿈.</summary>
        public void Equip(EquipmentItem it, CharacterData c)
        {
            if (it == null || c == null) return;
            string k = Key(c);
            if (it.owner == k) return;
            var current = EquippedIn(c, it.slot);
            string prevOwner = it.owner;
            if (current != null) current.owner = string.IsNullOrEmpty(prevOwner) ? "" : prevOwner;
            it.owner = k;
            Changed();
        }

        public void Unequip(EquipmentItem it)
        {
            if (it == null || !it.IsEquipped) return;
            it.owner = "";
            Changed();
        }

        public void ToggleLock(EquipmentItem it)
        {
            if (it == null) return;
            it.locked = !it.locked;
            Changed();
        }

        // ---------------- 강화 ----------------

        public int Gold => wallet != null ? wallet.Gold : int.MaxValue;
        public int Ore => inventory != null ? inventory.Count(MaterialType.EquipOre) : int.MaxValue;

        public bool CanEnhance(EquipmentItem it) =>
            it != null && !it.IsMaxed &&
            Gold >= EquipRules.EnhanceGold(it.level) && Ore >= EquipRules.EnhanceOre(it.level);

        /// <summary>
        /// 강화: steps번(또는 다음 4강 단위까지 = untilMilestone) 진행. 비용이 모자라면 거기서 멈춤.
        /// 반환: 진행한 단계 수와 보조 옵션 변화 메시지.
        /// </summary>
        public (int done, List<string> notes) Enhance(EquipmentItem it, bool untilMilestone)
        {
            var notes = new List<string>();
            int done = 0;
            while (it != null && CanEnhance(it))
            {
                int gold = EquipRules.EnhanceGold(it.level), ore = EquipRules.EnhanceOre(it.level);
                if (wallet != null && !wallet.SpendGold(gold)) break;
                if (inventory != null && !inventory.Spend(MaterialType.EquipOre, ore)) break;
                string note = EquipRules.EnhanceOnce(it, _rng);
                if (!string.IsNullOrEmpty(note)) notes.Add($"+{it.level}  {note}");
                done++;
                if (!untilMilestone || it.level % EquipStats.RollEvery == 0) break;
            }
            if (done > 0) Changed();
            return (done, notes);
        }

        /// <summary>다음 4강 단위까지의 총 비용.</summary>
        public (int gold, int ore) CostToMilestone(EquipmentItem it)
        {
            int gold = 0, ore = 0;
            if (it == null) return (0, 0);
            int lv = it.level;
            do { gold += EquipRules.EnhanceGold(lv); ore += EquipRules.EnhanceOre(lv); lv++; }
            while (lv < it.MaxLevel && lv % EquipStats.RollEvery != 0);
            return (gold, ore);
        }

        // ---------------- 초월 ----------------

        /// <summary>초월 재료 후보: 같은 세트·부위·성급, 미장착·미잠금, 자기 자신 제외 (강화 낮은 순).</summary>
        public List<EquipmentItem> TranscendFodder(EquipmentItem it)
        {
            var list = new List<EquipmentItem>();
            if (it == null) return list;
            foreach (var x in _items)
                if (x != it && x.set == it.set && x.slot == it.slot && x.Rarity == it.Rarity && !x.IsEquipped && !x.locked)
                    list.Add(x);
            list.Sort((a, b) => a.level != b.level ? a.level.CompareTo(b.level) : a.transcend.CompareTo(b.transcend));
            return list;
        }

        public bool CanTranscend(EquipmentItem it) =>
            it != null && it.CanTranscendMore && TranscendFodder(it).Count > 0 &&
            Gold >= EquipStats.TranscendGold(it.Rarity, it.transcend);

        /// <summary>장비 초월: 재료 장비 1개(가장 낮은 강화) + 금화 소모 → 초월 +1. 재료에 쓴 강화석 일부는 돌려받음.</summary>
        public bool Transcend(EquipmentItem it)
        {
            if (!CanTranscend(it)) return false;
            var fodder = TranscendFodder(it)[0];
            int gold = EquipStats.TranscendGold(it.Rarity, it.transcend);
            if (wallet != null && !wallet.SpendGold(gold)) return false;
            int refund = fodder.level > 0 ? EquipRules.DismantleOre(fodder) - EquipRules.DismantleOre(new EquipmentItem { rarity = fodder.rarity }) : 0;
            _items.Remove(fodder);
            if (refund > 0 && inventory != null) inventory.Add(MaterialType.EquipOre, refund);
            it.transcend++;
            Changed();
            return true;
        }

        // ---------------- 분해 ----------------

        /// <summary>분해(잠금·장착 중이면 불가) → 장비 강화석.</summary>
        public int Dismantle(EquipmentItem it)
        {
            if (it == null || it.locked || it.IsEquipped) return 0;
            int ore = EquipRules.DismantleOre(it);
            _items.Remove(it);
            if (inventory != null) inventory.Add(MaterialType.EquipOre, ore);
            Changed();
            return ore;
        }

        // ---------------- 저장 ----------------

        private void Changed()
        {
            Save();
            OnChanged?.Invoke();
        }

        private void Save()
        {
            var data = new SaveData { items = _items };
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _items.Clear();
            if (!PlayerPrefs.HasKey(saveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
                if (data?.items != null)
                    foreach (var it in data.items)
                        if (it != null && EquipSets.Get(it.set) != null) _items.Add(it);
            }
            catch { }
        }

        // ---------------- 테스트 ----------------

        /// <summary>테스트: 모든 세트에서 무작위 장비 n개 지급.</summary>
        public void GrantTest(int n)
        {
            var ids = new List<string>();
            foreach (var s in EquipSets.All) ids.Add(s.id);
            var weights = new[] { 25, 35, 25, 15 };   // 3·4·5·6성
            for (int i = 0; i < n; i++)
                Add(EquipRules.Generate(ids[_rng.Next(ids.Count)], _rng, EquipRules.RollRarity(weights, _rng)), notify: false);
            Changed();
        }

        [ContextMenu("테스트: 장비 30개 지급")]
        private void GrantTest30() => GrantTest(30);

        [ContextMenu("장비 전부 삭제")]
        private void ClearAll()
        {
            _items.Clear();
            Changed();
        }
    }
}
