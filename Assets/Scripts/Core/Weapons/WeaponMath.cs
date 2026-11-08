using System;

namespace HalfAutomatic.Core
{
    /// <summary>무기 공격력 계산.</summary>
    public sealed class WeaponMath
    {
        private readonly BalanceConfig _config;
        private readonly WeaponCatalog _catalog;

        public WeaponMath(BalanceConfig config, WeaponCatalog catalog)
        {
            _config = BalanceConfig.RequireValid(config);
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>성급 배율 = 2^(성급-1). 1성 ×1, 2성 ×2, 3성 ×4, 4성 ×8, 5성 ×16.</summary>
        public static double StarMultiplier(int stars)
        {
            if (stars < 1 || stars > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(stars));
            }
            return 1L << (stars - 1);
        }

        /// <summary>레벨 배율 = 1 + 0.01 × (레벨 - 1). 1레벨에서 ×1.</summary>
        public double LevelMultiplier(int level)
        {
            if (level < 1 || level > _config.MaxWeaponLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            return 1.0 + _config.WeaponAttackPerLevel * (level - 1);
        }

        /// <summary>무기 공격력 = 기본 공격력 × 성급 배율 × 레벨 배율.</summary>
        public double Attack(WeaponInstance weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }
            if (!_catalog.TryGet(weapon.DefinitionId, out WeaponDefinition def))
            {
                throw new ArgumentException("unknown weapon definition: " + weapon.DefinitionId, nameof(weapon));
            }
            return def.BaseAttack * StarMultiplier(weapon.Stars) * LevelMultiplier(weapon.Level);
        }
    }
}
