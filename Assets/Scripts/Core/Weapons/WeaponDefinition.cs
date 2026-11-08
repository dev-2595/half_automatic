using System;

namespace HalfAutomatic.Core
{
    /// <summary>무기 종류의 불변 정의(데이터). 개별 보유 무기는 <see cref="WeaponInstance"/>.</summary>
    public sealed class WeaponDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public WeaponCategory Category { get; }
        /// <summary>1성, 1레벨 기준 공격력.</summary>
        public double BaseAttack { get; }
        /// <summary>몬스터 드랍 후보에 포함되는지(튜토리얼 전용 무기는 false).</summary>
        public bool Droppable { get; }

        public WeaponDefinition(string id, string name, WeaponCategory category, double baseAttack, bool droppable)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id is required", nameof(id));
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name is required", nameof(name));
            }
            if (!JobRules.IsDefined(category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }
            if (double.IsNaN(baseAttack) || double.IsInfinity(baseAttack) || baseAttack < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseAttack));
            }
            Id = id;
            Name = name;
            Category = category;
            BaseAttack = baseAttack;
            Droppable = droppable;
        }
    }
}
