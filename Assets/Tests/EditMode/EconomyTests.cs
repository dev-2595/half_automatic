using System;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class BalanceConfigTests
    {
        [Test]
        public void Default_is_valid()
        {
            Assert.IsTrue(BalanceConfig.CreateDefault().Validate().IsOk);
        }

        [Test]
        public void Invalid_values_are_rejected()
        {
            Assert.IsFalse(new BalanceConfig { MaxPlayerLevel = 0 }.Validate().IsOk);
            Assert.IsFalse(new BalanceConfig { ExpBase = double.NaN }.Validate().IsOk);
            Assert.IsFalse(new BalanceConfig { MaxStars = 0 }.Validate().IsOk);
            Assert.IsFalse(new BalanceConfig { WeaponDropChance = 1.5 }.Validate().IsOk);
            Assert.IsFalse(new BalanceConfig { CoinDropMin = 5, CoinDropMax = 1 }.Validate().IsOk);
            Assert.IsFalse(new BalanceConfig { MinCooldownMultiplier = 0 }.Validate().IsOk);
            Assert.IsFalse(new BalanceConfig { StatPercentPerLevel = double.PositiveInfinity }.Validate().IsOk);
        }

        [Test]
        public void Services_refuse_invalid_config()
        {
            Assert.Throws<ArgumentNullException>(() => new ExperienceCurve(null));
            Assert.Throws<ArgumentException>(() => new ExperienceCurve(new BalanceConfig { MaxPlayerLevel = 0 }));
            Assert.Throws<ArgumentException>(() => new StatLevelService(new BalanceConfig { MaxStars = -1 }));
        }
    }

    public class CostTests
    {
        [Test]
        public void Add_sums_and_keeps_scroll_type()
        {
            Result<Cost> r = Cost.Add(new Cost(10, WeaponCategory.Magic, 2), Cost.CoinsOnly(5));
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(15, r.Value.Coins);
            Assert.AreEqual(2, r.Value.Scrolls);
            Assert.AreEqual(WeaponCategory.Magic, r.Value.ScrollType);
        }

        [Test]
        public void Add_rejects_mixed_scroll_types_and_overflow()
        {
            Assert.AreEqual(ErrorCode.InvalidArgument,
                Cost.Add(new Cost(0, WeaponCategory.Magic, 1), new Cost(0, WeaponCategory.Heal, 1)).Error);
            Assert.AreEqual(ErrorCode.Overflow,
                Cost.Add(Cost.CoinsOnly(long.MaxValue), Cost.CoinsOnly(1)).Error);
        }

        [Test]
        public void Negative_amounts_are_clamped_to_zero()
        {
            var cost = new Cost(-5, WeaponCategory.Melee, -1);
            Assert.AreEqual(0, cost.Coins);
            Assert.AreEqual(0, cost.Scrolls);
        }
    }

    public class WalletTests
    {
        [Test]
        public void Add_and_overflow()
        {
            var w = new Wallet();
            Assert.IsTrue(w.AddCoins(5).IsOk);
            Assert.AreEqual(ErrorCode.InvalidArgument, w.AddCoins(-1).Error);
            Assert.AreEqual(ErrorCode.Overflow, w.AddCoins(long.MaxValue).Error);
            Assert.AreEqual(5, w.Coins);
            Assert.IsTrue(w.AddScrolls(WeaponCategory.Heal, 3).IsOk);
            Assert.AreEqual(3, w.GetScrolls(WeaponCategory.Heal));
            Assert.AreEqual(0, w.GetScrolls(WeaponCategory.Melee));
            Assert.AreEqual(ErrorCode.InvalidArgument, w.AddScrolls((WeaponCategory)99, 1).Error);
        }

        [Test]
        public void TrySpend_is_all_or_nothing()
        {
            var w = new Wallet();
            w.AddCoins(100);
            w.AddScrolls(WeaponCategory.Melee, 1);

            Assert.AreEqual(ErrorCode.InsufficientScrolls, w.TrySpend(new Cost(10, WeaponCategory.Melee, 2)).Error);
            Assert.AreEqual(100, w.Coins, "coins must not be spent when scrolls are short");
            Assert.AreEqual(1, w.GetScrolls(WeaponCategory.Melee));

            Assert.AreEqual(ErrorCode.InsufficientCoins, w.TrySpend(Cost.CoinsOnly(101)).Error);
            Assert.AreEqual(ErrorCode.InsufficientScrolls, w.TrySpend(new Cost(0, WeaponCategory.Magic, 1)).Error);

            Assert.IsTrue(w.TrySpend(new Cost(30, WeaponCategory.Melee, 1)).IsOk);
            Assert.AreEqual(70, w.Coins);
            Assert.AreEqual(0, w.GetScrolls(WeaponCategory.Melee));
        }
    }

    public class LevelUpPlannerTests
    {
        private static Cost TenCoinsAndOneScroll(int level) => new Cost(10, WeaponCategory.Melee, 1);

        [Test]
        public void Plans_all_requested_levels_when_affordable()
        {
            LevelUpPlan plan = LevelUpPlanner.Plan(1, 3, 100, TenCoinsAndOneScroll, Fixtures.Rich());
            Assert.AreEqual(3, plan.Levels);
            Assert.AreEqual(4, plan.ToLevel);
            Assert.AreEqual(30, plan.TotalCost.Coins);
            Assert.AreEqual(3, plan.TotalCost.Scrolls);
            Assert.AreEqual(LevelUpStop.None, plan.StoppedBy);
        }

        [Test]
        public void Stops_at_cap()
        {
            LevelUpPlan plan = LevelUpPlanner.Plan(4, 10, 5, TenCoinsAndOneScroll, Fixtures.Rich());
            Assert.AreEqual(1, plan.Levels);
            Assert.AreEqual(LevelUpStop.Cap, plan.StoppedBy);
        }

        [Test]
        public void Stops_when_coins_or_scrolls_run_out()
        {
            var w = new Wallet();
            w.AddCoins(25);
            w.AddScrolls(WeaponCategory.Melee, 10);
            LevelUpPlan byCoins = LevelUpPlanner.Plan(1, 10, 100, TenCoinsAndOneScroll, w);
            Assert.AreEqual(2, byCoins.Levels);
            Assert.AreEqual(LevelUpStop.InsufficientCoins, byCoins.StoppedBy);

            var w2 = new Wallet();
            w2.AddCoins(1000);
            w2.AddScrolls(WeaponCategory.Melee, 1);
            LevelUpPlan byScrolls = LevelUpPlanner.Plan(1, 10, 100, TenCoinsAndOneScroll, w2);
            Assert.AreEqual(1, byScrolls.Levels);
            Assert.AreEqual(LevelUpStop.InsufficientScrolls, byScrolls.StoppedBy);
        }

        [Test]
        public void Rejects_bad_arguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelUpPlanner.Plan(1, 0, 10, TenCoinsAndOneScroll, new Wallet()));
            Assert.Throws<ArgumentNullException>(() => LevelUpPlanner.Plan(1, 1, 10, null, new Wallet()));
            Assert.Throws<ArgumentNullException>(() => LevelUpPlanner.Plan(1, 1, 10, TenCoinsAndOneScroll, null));
        }
    }
}
