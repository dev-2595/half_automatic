using System;

namespace HalfAutomatic.Core
{
    /// <summary>전투에 쓰는 최종 능력치(계산 결과, 불변).</summary>
    public readonly struct CombatStats
    {
        public double MaxHealth { get; }
        public double Attack { get; }
        /// <summary>스킬 쿨타임에 곱하는 배율(1 미만이면 빨라짐).</summary>
        public double CooldownMultiplier { get; }

        public CombatStats(double maxHealth, double attack, double cooldownMultiplier)
        {
            MaxHealth = maxHealth;
            Attack = attack;
            CooldownMultiplier = cooldownMultiplier;
        }
    }

    /// <summary>
    /// 캐릭터 기본치 + 무기 + 스탯을 합쳐 최종 능력치를 계산한다.
    /// ⚠️ 합산 방식은 임시: 공격력 = (기본 공격력 + 무기 공격력) × 공격력 스탯 배율.
    /// </summary>
    public sealed class CombatStatsCalculator
    {
        private readonly WeaponMath _weaponMath;
        private readonly StatLevelService _stats;

        public double BaseHealth { get; }
        public double BaseAttack { get; }

        public CombatStatsCalculator(WeaponMath weaponMath, StatLevelService stats,
            double baseHealth = 100.0, double baseAttack = 1.0)
        {
            _weaponMath = weaponMath ?? throw new ArgumentNullException(nameof(weaponMath));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            if (!IsNonNegativeFinite(baseHealth) || baseHealth == 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseHealth));
            }
            if (!IsNonNegativeFinite(baseAttack))
            {
                throw new ArgumentOutOfRangeException(nameof(baseAttack));
            }
            BaseHealth = baseHealth;
            BaseAttack = baseAttack;
        }

        public CombatStats Compute(StatSheet sheet, WeaponInstance equipped)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }
            double weaponAttack = equipped == null ? 0.0 : _weaponMath.Attack(equipped);
            return new CombatStats(
                BaseHealth * _stats.Multiplier(sheet, StatType.Health),
                (BaseAttack + weaponAttack) * _stats.Multiplier(sheet, StatType.Attack),
                _stats.Multiplier(sheet, StatType.Cooldown));
        }

        private static bool IsNonNegativeFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0.0;
    }
}
