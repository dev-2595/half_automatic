using System;
using System.Collections.Generic;

namespace HalfAutomatic.Core
{
    /// <summary>저장/복원용 무기 값.</summary>
    public readonly struct WeaponSnapshot
    {
        public long Id { get; }
        public string DefinitionId { get; }
        public int Stars { get; }
        public int Level { get; }

        public WeaponSnapshot(long id, string definitionId, int stars, int level)
        {
            Id = id;
            DefinitionId = definitionId;
            Stars = stars;
            Level = level;
        }
    }

    /// <summary>보유 무기 목록과 장착 상태. 장착 슬롯은 1개.</summary>
    public sealed class WeaponInventory
    {
        private readonly WeaponCatalog _catalog;
        private readonly List<WeaponInstance> _items = new List<WeaponInstance>();
        private long _nextId = 1;

        public IReadOnlyList<WeaponInstance> Items => _items.AsReadOnly();
        public WeaponInstance Equipped { get; private set; }

        public WeaponInventory(WeaponCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>1성 1레벨 무기를 추가한다.</summary>
        public Result<WeaponInstance> Add(string definitionId)
        {
            if (!_catalog.TryGet(definitionId, out _))
            {
                return Result<WeaponInstance>.Fail(ErrorCode.UnknownWeapon);
            }
            return Result<WeaponInstance>.Ok(AddInternal(definitionId, 1, 1));
        }

        public WeaponInstance Find(long instanceId)
        {
            return _items.Find(w => w.Id == instanceId);
        }

        public Result Equip(long instanceId)
        {
            WeaponInstance weapon = Find(instanceId);
            if (weapon == null)
            {
                return Result.Fail(ErrorCode.WeaponNotFound);
            }
            if (Equipped == weapon)
            {
                return Result.Fail(ErrorCode.AlreadyEquipped);
            }
            Equipped = weapon;
            return Result.Ok();
        }

        public Result Unequip()
        {
            if (Equipped == null)
            {
                return Result.Fail(ErrorCode.NothingEquipped);
            }
            Equipped = null;
            return Result.Ok();
        }

        /// <summary>다음에 발급할 무기 id(저장용).</summary>
        public long NextId => _nextId;

        /// <summary>
        /// 저장값으로 복원한다. 모두 검증한 뒤에만 반영한다(원자성).
        /// equippedId = 0이면 장착 없음.
        /// </summary>
        public Result Restore(IReadOnlyList<WeaponSnapshot> weapons, long equippedId, long nextId, int maxStars,
            int maxLevel)
        {
            if (weapons == null || nextId < 1 || equippedId < 0)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            var ids = new HashSet<long>();
            var restored = new List<WeaponInstance>(weapons.Count);
            foreach (WeaponSnapshot w in weapons)
            {
                bool valid = w.Id >= 1 && w.Id < nextId && ids.Add(w.Id)
                    && _catalog.TryGet(w.DefinitionId, out _)
                    && w.Stars >= 1 && w.Stars <= maxStars
                    && w.Level >= 1 && w.Level <= maxLevel;
                if (!valid)
                {
                    return Result.Fail(ErrorCode.InvalidArgument);
                }
                restored.Add(new WeaponInstance(w.Id, w.DefinitionId, w.Stars, w.Level));
            }
            WeaponInstance equipped = null;
            if (equippedId != 0)
            {
                equipped = restored.Find(w => w.Id == equippedId);
                if (equipped == null)
                {
                    return Result.Fail(ErrorCode.InvalidArgument);
                }
            }

            _items.Clear();
            _items.AddRange(restored);
            Equipped = equipped;
            _nextId = nextId;
            return Result.Ok();
        }

        internal WeaponInstance AddInternal(string definitionId, int stars, int level)
        {
            var weapon = new WeaponInstance(_nextId++, definitionId, stars, level);
            _items.Add(weapon);
            return weapon;
        }

        /// <summary>장착 중인 무기는 제거할 수 없다(합성 재료에서 제외하는 규칙의 안전장치).</summary>
        internal bool RemoveInternal(WeaponInstance weapon)
        {
            if (weapon == null || weapon == Equipped)
            {
                return false;
            }
            return _items.Remove(weapon);
        }
    }
}
