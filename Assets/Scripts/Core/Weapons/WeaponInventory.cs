using System;
using System.Collections.Generic;

namespace HalfAutomatic.Core
{
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
