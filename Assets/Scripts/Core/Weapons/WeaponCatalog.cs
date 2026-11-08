using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfAutomatic.Core
{
    /// <summary>무기 정의 목록(읽기 전용). id 중복을 허용하지 않는다.</summary>
    public sealed class WeaponCatalog
    {
        public const string BranchId = "branch";
        public const string OldSwordId = "old_sword";

        private readonly Dictionary<string, WeaponDefinition> _byId;

        public IReadOnlyList<WeaponDefinition> All { get; }

        public WeaponCatalog(IEnumerable<WeaponDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }
            List<WeaponDefinition> list = definitions.ToList();
            if (list.Count == 0 || list.Any(d => d == null))
            {
                throw new ArgumentException("definitions must be non-empty and contain no nulls", nameof(definitions));
            }
            _byId = new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);
            foreach (WeaponDefinition def in list)
            {
                if (_byId.ContainsKey(def.Id))
                {
                    throw new ArgumentException("duplicate weapon id: " + def.Id, nameof(definitions));
                }
                _byId.Add(def.Id, def);
            }
            All = list.AsReadOnly();
        }

        /// <summary>⚠️ 임시 무기 목록. 이름과 수치는 기획 확정 시 교체한다.</summary>
        public static WeaponCatalog CreateDefault()
        {
            return new WeaponCatalog(new[]
            {
                new WeaponDefinition(BranchId, "나뭇가지", WeaponCategory.Melee, 1.0, droppable: false),
                new WeaponDefinition(OldSwordId, "낡은 검", WeaponCategory.Melee, 3.0, droppable: true),
                new WeaponDefinition("old_axe", "낡은 도끼", WeaponCategory.Melee, 4.0, droppable: true),
                new WeaponDefinition("old_staff", "낡은 지팡이", WeaponCategory.Magic, 3.0, droppable: true),
                new WeaponDefinition("old_chalice", "낡은 성배", WeaponCategory.Heal, 2.0, droppable: true),
                new WeaponDefinition("old_lyre", "낡은 리라", WeaponCategory.Heal, 2.0, droppable: true),
            });
        }

        public bool TryGet(string id, out WeaponDefinition definition)
        {
            if (id == null)
            {
                definition = null;
                return false;
            }
            return _byId.TryGetValue(id, out definition);
        }

        /// <summary>드랍 가능한 무기. category가 null이면 모든 계열.</summary>
        public IReadOnlyList<WeaponDefinition> Droppable(WeaponCategory? category)
        {
            return All.Where(d => d.Droppable && (category == null || d.Category == category.Value))
                .ToList().AsReadOnly();
        }
    }
}
