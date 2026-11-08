using System;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class StatLevelServiceTests
    {
        private StatLevelService _service;
        private StatSheet _sheet;

        [SetUp]
        public void SetUp()
        {
            _service = new StatLevelService(Fixtures.Config());
            _sheet = new StatSheet();
        }

        [Test]
        public void Multipliers_change_by_point_one_percent_per_level()
        {
            Assert.IsTrue(_sheet.Restore(health: 10, attack: 100, cooldown: 50, cap: 100).IsOk);
            Assert.AreEqual(1.01, _service.Multiplier(_sheet, StatType.Health), 1e-12);
            Assert.AreEqual(1.10, _service.Multiplier(_sheet, StatType.Attack), 1e-12);
            Assert.AreEqual(0.95, _service.Multiplier(_sheet, StatType.Cooldown), 1e-12);
        }

        [Test]
        public void Cooldown_multiplier_has_floor()
        {
            var service = new StatLevelService(new BalanceConfig { StatPercentPerLevel = 0.5 });
            _sheet.Restore(0, 0, 10, 10);
            Assert.AreEqual(0.1, service.Multiplier(_sheet, StatType.Cooldown), 1e-12);
        }

        [Test]
        public void Apply_respects_player_level_cap()
        {
            Result<LevelUpPlan> r = _service.Apply(_sheet, StatType.Attack, 100, 3, Fixtures.Rich());
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(3, _sheet.GetLevel(StatType.Attack));
            Assert.AreEqual(LevelUpStop.Cap, r.Value.StoppedBy);
            Assert.AreEqual(ErrorCode.LevelCapReached, _service.Apply(_sheet, StatType.Attack, 1, 3, Fixtures.Rich()).Error);
        }

        [Test]
        public void Apply_spends_coins_only()
        {
            Wallet w = Fixtures.Rich();
            long before = w.Coins;
            long scrolls = w.GetScrolls(WeaponCategory.Melee);
            _service.Apply(_sheet, StatType.Health, 2, 10, w);
            long expected = _service.CostOfNext(0).Coins + _service.CostOfNext(1).Coins;
            Assert.AreEqual(before - expected, w.Coins);
            Assert.AreEqual(scrolls, w.GetScrolls(WeaponCategory.Melee));
            Assert.AreEqual(2, _sheet.GetLevel(StatType.Health));
            Assert.AreEqual(0, _sheet.GetLevel(StatType.Attack));
        }

        [Test]
        public void No_coins_changes_nothing()
        {
            var w = new Wallet();
            Assert.AreEqual(ErrorCode.InsufficientCoins, _service.Apply(_sheet, StatType.Health, 1, 10, w).Error);
            Assert.AreEqual(0, _sheet.GetLevel(StatType.Health));
        }

        [Test]
        public void Restore_rejects_out_of_range_atomically()
        {
            _sheet.Restore(1, 2, 3, 10);
            Assert.AreEqual(ErrorCode.InvalidArgument, _sheet.Restore(1, 11, 3, 10).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _sheet.Restore(-1, 0, 0, 10).Error);
            Assert.AreEqual(2, _sheet.GetLevel(StatType.Attack));
        }

        [Test]
        public void Undefined_stat_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _sheet.GetLevel((StatType)7));
        }
    }

    public class CombatStatsCalculatorTests
    {
        [Test]
        public void Combines_base_weapon_and_stats()
        {
            BalanceConfig config = Fixtures.Config();
            WeaponCatalog catalog = WeaponCatalog.CreateDefault();
            var calc = new CombatStatsCalculator(new WeaponMath(config, catalog), new StatLevelService(config),
                baseHealth: 100, baseAttack: 1);
            var inv = new WeaponInventory(catalog);
            WeaponInstance sword = inv.Add(WeaponCatalog.OldSwordId).Value;
            var sheet = new StatSheet();
            sheet.Restore(health: 100, attack: 100, cooldown: 100, cap: 100);

            CombatStats s = calc.Compute(sheet, sword);
            Assert.AreEqual(110.0, s.MaxHealth, 1e-9);
            Assert.AreEqual((1 + 3) * 1.1, s.Attack, 1e-9);
            Assert.AreEqual(0.9, s.CooldownMultiplier, 1e-9);

            Assert.AreEqual(1.1, calc.Compute(sheet, null).Attack, 1e-9, "unarmed uses base attack only");
        }

        [Test]
        public void Rejects_invalid_base_values()
        {
            BalanceConfig config = Fixtures.Config();
            var math = new WeaponMath(config, WeaponCatalog.CreateDefault());
            var stats = new StatLevelService(config);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatStatsCalculator(math, stats, baseHealth: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatStatsCalculator(math, stats, baseAttack: double.NaN));
        }
    }

    public class LootServiceTests
    {
        private static LootService Service(IRandom random) =>
            new LootService(Fixtures.Config(), WeaponCatalog.CreateDefault(), random);

        [Test]
        public void Job_determines_scroll_type_and_weapon_category()
        {
            // coins=3, scroll 확률 통과(0.1<0.5), 서 2장, 무기 확률 통과(0.05<0.1), 후보 0번
            var random = new ScriptedRandom(new[] { 0.1, 0.05 }, new[] { 3, 2, 0 });
            LootDrop drop = Service(random).Roll(JobClass.Mage);
            Assert.AreEqual(3, drop.Coins);
            Assert.AreEqual(2, drop.Scrolls);
            Assert.AreEqual(WeaponCategory.Magic, drop.ScrollType);
            Assert.IsTrue(WeaponCatalog.CreateDefault().TryGet(drop.WeaponDefinitionId, out WeaponDefinition def));
            Assert.AreEqual(WeaponCategory.Magic, def.Category);
        }

        [Test]
        public void Failed_rolls_give_coins_only()
        {
            var random = new ScriptedRandom(new[] { 0.9, 0.9 }, new[] { 1 });
            LootDrop drop = Service(random).Roll(JobClass.Warrior);
            Assert.AreEqual(1, drop.Coins);
            Assert.AreEqual(0, drop.Scrolls);
            Assert.IsNull(drop.WeaponDefinitionId);
        }

        [Test]
        public void No_job_means_no_scrolls()
        {
            // None: 서 확률 굴림 자체를 하지 않으므로 double은 무기 확률 1개만 쓴다
            var random = new ScriptedRandom(new[] { 0.9 }, new[] { 2 });
            LootDrop drop = Service(random).Roll(JobClass.None);
            Assert.AreEqual(0, drop.Scrolls);
        }

        [Test]
        public void Undefined_job_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Service(new ScriptedRandom(new double[0], new int[0])).Roll((JobClass)42));
        }
    }
}
