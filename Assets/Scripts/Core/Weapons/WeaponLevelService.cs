using System;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 무기 레벨업(코인 + 무기 계열에 맞는 강화의 서).
    /// 상한 = min(플레이어 레벨, MaxWeaponLevel). 미리보기와 실행이 같은 계산을 쓴다.
    /// </summary>
    public sealed class WeaponLevelService
    {
        private readonly BalanceConfig _config;
        private readonly WeaponCatalog _catalog;

        public WeaponLevelService(BalanceConfig config, WeaponCatalog catalog)
        {
            _config = BalanceConfig.RequireValid(config);
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int Cap(int playerLevel)
        {
            return Math.Max(1, Math.Min(playerLevel, _config.MaxWeaponLevel));
        }

        /// <summary>레벨 level → level+1 비용.</summary>
        public Cost CostOfNext(WeaponCategory category, int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            long coins = CostCurve.Evaluate(_config.WeaponCoinBase, _config.WeaponCoinExponent, level);
            long scrolls = CostCurve.Evaluate(_config.WeaponScrollBase, _config.WeaponScrollExponent, level);
            return new Cost(coins, category, scrolls);
        }

        /// <summary>지갑을 바꾸지 않고 계획만 계산한다(자동 레벨업 확인 창용).</summary>
        public Result<LevelUpPlan> Preview(WeaponInventory inventory, long instanceId, int levels,
            int playerLevel, Wallet wallet)
        {
            if (inventory == null || wallet == null || levels < 1)
            {
                return Result<LevelUpPlan>.Fail(ErrorCode.InvalidArgument);
            }
            WeaponInstance weapon = inventory.Find(instanceId);
            if (weapon == null)
            {
                return Result<LevelUpPlan>.Fail(ErrorCode.WeaponNotFound);
            }
            if (!_catalog.TryGet(weapon.DefinitionId, out WeaponDefinition def))
            {
                return Result<LevelUpPlan>.Fail(ErrorCode.UnknownWeapon);
            }
            LevelUpPlan plan = LevelUpPlanner.Plan(weapon.Level, levels, Cap(playerLevel),
                level => CostOfNext(def.Category, level), wallet);
            return Result<LevelUpPlan>.Ok(plan);
        }

        /// <summary>
        /// 가능한 만큼(최대 levels) 올린다. 한 레벨도 못 올리면 실패하고 아무것도 바뀌지 않는다.
        /// 비용 차감이 성공한 뒤에만 레벨을 바꾼다.
        /// </summary>
        public Result<LevelUpPlan> Apply(WeaponInventory inventory, long instanceId, int levels,
            int playerLevel, Wallet wallet)
        {
            Result<LevelUpPlan> preview = Preview(inventory, instanceId, levels, playerLevel, wallet);
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
            inventory.Find(instanceId).Level = plan.ToLevel;
            return Result<LevelUpPlan>.Ok(plan);
        }
    }
}
