using System;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 밸런스 수치 모음. 기획에서 확정된 값과 임시값(⚠️)을 한곳에 모아 코드 수정 없이 조정할 수 있게 한다.
    /// 서비스들은 생성 시 <see cref="Validate"/>로 값을 검사한다.
    /// </summary>
    public sealed class BalanceConfig
    {
        // ---- 플레이어 레벨 / 경험치 ----
        /// <summary>⚠️ 임시값. 최대 플레이어 레벨.</summary>
        public int MaxPlayerLevel { get; set; } = 100;
        /// <summary>⚠️ 임시값. 레벨 L에서 다음 레벨까지 필요 EXP = ExpBase × L^ExpExponent.</summary>
        public double ExpBase { get; set; } = 10.0;
        public double ExpExponent { get; set; } = 1.6;

        // ---- 무기 ----
        /// <summary>확정: 무기 최대 레벨은 플레이어 레벨과 무관하게 100.</summary>
        public int MaxWeaponLevel { get; set; } = 100;
        /// <summary>확정: 최대 5성.</summary>
        public int MaxStars { get; set; } = 5;
        /// <summary>확정: 무기 레벨 1당 공격력 +0.01배.</summary>
        public double WeaponAttackPerLevel { get; set; } = 0.01;
        /// <summary>⚠️ 임시값. 레벨 L → L+1 비용: 코인 = WeaponCoinBase × L^WeaponCoinExponent.</summary>
        public double WeaponCoinBase { get; set; } = 10.0;
        public double WeaponCoinExponent { get; set; } = 1.5;
        /// <summary>⚠️ 임시값. 레벨 L → L+1 비용: 강화의 서 = WeaponScrollBase × L^WeaponScrollExponent.</summary>
        public double WeaponScrollBase { get; set; } = 1.0;
        public double WeaponScrollExponent { get; set; } = 1.2;

        // ---- 스탯 ----
        /// <summary>확정: 스탯 레벨 1당 체력/공격력 +0.1%, 쿨타임 -0.1%.</summary>
        public double StatPercentPerLevel { get; set; } = 0.001;
        /// <summary>쿨타임 배율의 하한(0 이하로 내려가지 않게 하는 안전장치).</summary>
        public double MinCooldownMultiplier { get; set; } = 0.1;
        /// <summary>⚠️ 임시값. 스탯 재화는 미정이라 코인으로 둔다. 레벨 L → L+1: 코인 = StatCoinBase × (L+1)^StatCoinExponent.</summary>
        public double StatCoinBase { get; set; } = 5.0;
        public double StatCoinExponent { get; set; } = 1.4;

        // ---- 드랍 (⚠️ 전부 임시값) ----
        public int CoinDropMin { get; set; } = 1;
        public int CoinDropMax { get; set; } = 5;
        public double ScrollDropChance { get; set; } = 0.5;
        public int ScrollDropMin { get; set; } = 1;
        public int ScrollDropMax { get; set; } = 2;
        public double WeaponDropChance { get; set; } = 0.1;

        public static BalanceConfig CreateDefault() => new BalanceConfig();

        /// <summary>모든 값이 유효하면 Ok. 하나라도 잘못되면 InvalidArgument.</summary>
        public Result Validate()
        {
            bool ok =
                MaxPlayerLevel >= 1
                && IsPositive(ExpBase) && IsNonNegative(ExpExponent)
                && MaxWeaponLevel >= 1
                && MaxStars >= 1 && MaxStars <= 30 // 2^(성급-1) 배율이 double 정밀도 안에 들도록
                && IsNonNegative(WeaponAttackPerLevel)
                && IsNonNegative(WeaponCoinBase) && IsNonNegative(WeaponCoinExponent)
                && IsNonNegative(WeaponScrollBase) && IsNonNegative(WeaponScrollExponent)
                && IsNonNegative(StatPercentPerLevel)
                && MinCooldownMultiplier > 0.0 && MinCooldownMultiplier <= 1.0
                && IsNonNegative(StatCoinBase) && IsNonNegative(StatCoinExponent)
                && CoinDropMin >= 0 && CoinDropMax >= CoinDropMin
                && IsProbability(ScrollDropChance)
                && ScrollDropMin >= 0 && ScrollDropMax >= ScrollDropMin
                && IsProbability(WeaponDropChance);
            return ok ? Result.Ok() : Result.Fail(ErrorCode.InvalidArgument);
        }

        /// <summary>서비스 생성자용: 잘못된 설정이면 즉시 예외(프로그래머 오류).</summary>
        internal static BalanceConfig RequireValid(BalanceConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (!config.Validate().IsOk)
            {
                throw new ArgumentException("BalanceConfig contains invalid values.", nameof(config));
            }
            return config;
        }

        private static bool IsPositive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0.0;
        private static bool IsNonNegative(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0.0;
        private static bool IsProbability(double v) => !double.IsNaN(v) && v >= 0.0 && v <= 1.0;
    }
}
