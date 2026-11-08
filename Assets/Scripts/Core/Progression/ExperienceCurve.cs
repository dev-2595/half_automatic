using System;

namespace HalfAutomatic.Core
{
    /// <summary>레벨별 필요 경험치. 1→2는 쉽고 레벨이 오를수록 가파르게 늘어난다.</summary>
    public sealed class ExperienceCurve
    {
        private readonly BalanceConfig _config;

        public ExperienceCurve(BalanceConfig config)
        {
            _config = BalanceConfig.RequireValid(config);
        }

        public int MaxLevel => _config.MaxPlayerLevel;

        /// <summary>레벨 level에서 다음 레벨까지 필요한 EXP. 최대 레벨이면 0.</summary>
        public long ExpToNext(int level)
        {
            if (level < 1 || level > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            if (level == MaxLevel)
            {
                return 0;
            }
            return Math.Max(1L, CostCurve.Evaluate(_config.ExpBase, _config.ExpExponent, level));
        }
    }
}
