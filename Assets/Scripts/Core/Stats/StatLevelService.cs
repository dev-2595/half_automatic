using System;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 스탯 레벨업과 배율 계산.
    /// 효과: 레벨당 체력/공격력 +0.1%, 쿨타임 -0.1% (합연산). 상한 = 플레이어 레벨.
    /// ⚠️ 스탯 재화는 미정이라 임시로 코인을 쓴다.
    /// </summary>
    public sealed class StatLevelService
    {
        private readonly BalanceConfig _config;

        public StatLevelService(BalanceConfig config)
        {
            _config = BalanceConfig.RequireValid(config);
        }

        public int Cap(int playerLevel) => Math.Max(0, playerLevel);

        /// <summary>스탯 레벨 level → level+1 비용.</summary>
        public Cost CostOfNext(int level)
        {
            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            return Cost.CoinsOnly(CostCurve.Evaluate(_config.StatCoinBase, _config.StatCoinExponent, level + 1));
        }

        /// <summary>스탯 배율. 체력·공격력은 1 + p×L, 쿨타임은 1 - p×L(하한 MinCooldownMultiplier).</summary>
        public double Multiplier(StatSheet sheet, StatType stat)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }
            double bonus = _config.StatPercentPerLevel * sheet.GetLevel(stat);
            if (stat == StatType.Cooldown)
            {
                return Math.Max(_config.MinCooldownMultiplier, 1.0 - bonus);
            }
            return 1.0 + bonus;
        }

        public Result<LevelUpPlan> Preview(StatSheet sheet, StatType stat, int levels, int playerLevel, Wallet wallet)
        {
            if (sheet == null || wallet == null || levels < 1)
            {
                return Result<LevelUpPlan>.Fail(ErrorCode.InvalidArgument);
            }
            StatSheet.Index(stat); // 정의되지 않은 값이면 예외
            LevelUpPlan plan = LevelUpPlanner.Plan(sheet.GetLevel(stat), levels, Cap(playerLevel), CostOfNext, wallet);
            return Result<LevelUpPlan>.Ok(plan);
        }

        /// <summary>가능한 만큼(최대 levels) 올린다. 한 레벨도 못 올리면 실패하고 아무것도 바뀌지 않는다.</summary>
        public Result<LevelUpPlan> Apply(StatSheet sheet, StatType stat, int levels, int playerLevel, Wallet wallet)
        {
            Result<LevelUpPlan> preview = Preview(sheet, stat, levels, playerLevel, wallet);
            if (!preview.IsOk)
            {
                return preview;
            }
            LevelUpPlan plan = preview.Value;
            if (plan.Levels == 0)
            {
                return Result<LevelUpPlan>.Fail(LevelUpPlanner.ToError(plan.StoppedBy));
            }
            Result spent = wallet.TrySpend(plan.TotalCost);
            if (!spent.IsOk)
            {
                return Result<LevelUpPlan>.Fail(spent.Error);
            }
            sheet.SetLevel(stat, plan.ToLevel);
            return Result<LevelUpPlan>.Ok(plan);
        }
    }
}
