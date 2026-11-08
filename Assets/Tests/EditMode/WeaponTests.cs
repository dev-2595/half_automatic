using System;
using System.Linq;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class WeaponCatalogTests
    {
        [Test]
        public void Default_catalog_has_tutorial_weapons()
        {
            WeaponCatalog c = WeaponCatalog.CreateDefault();
            Assert.IsTrue(c.TryGet(WeaponCatalog.BranchId, out WeaponDefinition branch));
            Assert.IsFalse(branch.Droppable);
            Assert.IsTrue(c.TryGet(WeaponCatalog.OldSwordId, out WeaponDefinition sword));
            Assert.AreEqual(WeaponCategory.Melee, sword.Category);
            Assert.IsFalse(c.TryGet(null, out _));
            Assert.IsFalse(c.TryGet("nope", out _));
        }

        [Test]
        public void Droppable_filters_by_category_and_excludes_tutorial_weapons()
        {
            WeaponCatalog c = WeaponCatalog.CreateDefault();
            Assert.IsTrue(c.Droppable(WeaponCategory.Heal).All(d => d.Category == WeaponCategory.Heal && d.Droppable));
            Assert.IsFalse(c.Droppable(null).Any(d => d.Id == WeaponCatalog.BranchId));
            Assert.Greater(c.Droppable(null).Count, c.Droppable(WeaponCategory.Magic).Count);
        }

        [Test]
        public void Rejects_duplicates_and_invalid_definitions()
        {
            var a = new WeaponDefinition("a", "A", WeaponCategory.Melee, 1, true);
            Assert.Throws<ArgumentException>(() => new WeaponCatalog(new[] { a, a }));
            Assert.Throws<ArgumentException>(() => new WeaponCatalog(new WeaponDefinition[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition("b", "B", WeaponCategory.Melee, -1, true));
            Assert.Throws<ArgumentException>(() => new WeaponDefinition(" ", "B", WeaponCategory.Melee, 1, true));
        }
    }

    public class WeaponInventoryTests
    {
        [Test]
        public void Add_equip_unequip()
        {
            var inv = new WeaponInventory(WeaponCatalog.CreateDefault());
            Assert.AreEqual(ErrorCode.UnknownWeapon, inv.Add("nope").Error);

            WeaponInstance branch = inv.Add(WeaponCatalog.BranchId).Value;
            Assert.AreEqual(1, branch.Stars);
            Assert.AreEqual(1, branch.Level);

            Assert.AreEqual(ErrorCode.NothingEquipped, inv.Unequip().Error);
            Assert.AreEqual(ErrorCode.WeaponNotFound, inv.Equip(999).Error);
            Assert.IsTrue(inv.Equip(branch.Id).IsOk);
            Assert.AreSame(branch, inv.Equipped);
            Assert.AreEqual(ErrorCode.AlreadyEquipped, inv.Equip(branch.Id).Error);
            Assert.IsTrue(inv.Unequip().IsOk);
            Assert.IsNull(inv.Equipped);
        }

        [Test]
        public void Ids_are_unique()
        {
            var inv = new WeaponInventory(WeaponCatalog.CreateDefault());
            long a = inv.Add(WeaponCatalog.OldSwordId).Value.Id;
            long b = inv.Add(WeaponCatalog.OldSwordId).Value.Id;
            Assert.AreNotEqual(a, b);
        }
    }

    public class WeaponMathTests
    {
        [TestCase(1, 1.0)]
        [TestCase(2, 2.0)]
        [TestCase(3, 4.0)]
        [TestCase(4, 8.0)]
        [TestCase(5, 16.0)]
        public void Star_multiplier_doubles(int stars, double expected)
        {
            Assert.AreEqual(expected, WeaponMath.StarMultiplier(stars));
        }

        [Test]
        public void Level_multiplier_adds_one_percent_per_level()
        {
            var math = new WeaponMath(Fixtures.Config(), WeaponCatalog.CreateDefault());
            Assert.AreEqual(1.0, math.LevelMultiplier(1), 1e-12);
            Assert.AreEqual(1.01, math.LevelMultiplier(2), 1e-12);
            Assert.AreEqual(1.99, math.LevelMultiplier(100), 1e-12);
            Assert.Throws<ArgumentOutOfRangeException>(() => math.LevelMultiplier(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => math.LevelMultiplier(101));
        }

        [Test]
        public void Attack_combines_base_stars_and_level()
        {
            WeaponCatalog catalog = WeaponCatalog.CreateDefault();
            var math = new WeaponMath(Fixtures.Config(), catalog);
            var inv = new WeaponInventory(catalog);
            inv.Add(WeaponCatalog.OldSwordId);
            inv.Add(WeaponCatalog.OldSwordId);
            WeaponInstance twoStar = new WeaponSynthesizer(Fixtures.Config()).AutoSynthesize(inv).Created.Single();
            // 낡은 검 기본 3 × 2성(×2) × 1레벨(×1) = 6
            Assert.AreEqual(6.0, math.Attack(twoStar), 1e-12);
        }
    }

    public class WeaponSynthesizerTests
    {
        private WeaponCatalog _catalog;
        private WeaponInventory _inv;
        private WeaponSynthesizer _synth;

        [SetUp]
        public void SetUp()
        {
            _catalog = WeaponCatalog.CreateDefault();
            _inv = new WeaponInventory(_catalog);
            _synth = new WeaponSynthesizer(Fixtures.Config());
        }

        private void AddSwords(int count)
        {
            for (int i = 0; i < count; i++) _inv.Add(WeaponCatalog.OldSwordId);
        }

        [Test]
        public void Two_same_weapons_become_two_star()
        {
            AddSwords(2);
            SynthesisReport r = _synth.AutoSynthesize(_inv);
            Assert.AreEqual(2, r.ConsumedCount);
            Assert.AreEqual(1, _inv.Items.Count);
            Assert.AreEqual(2, _inv.Items[0].Stars);
            Assert.AreEqual(1, r.Created.Count);
        }

        [Test]
        public void Chains_up_through_stars()
        {
            AddSwords(4);
            SynthesisReport r = _synth.AutoSynthesize(_inv);
            Assert.AreEqual(1, _inv.Items.Count);
            Assert.AreEqual(3, _inv.Items[0].Stars);
            Assert.AreEqual(6, r.ConsumedCount, "4 one-stars + 2 intermediate two-stars");
            Assert.AreEqual(1, r.Created.Count, "only the surviving result is reported");
        }

        [Test]
        public void Odd_count_leaves_one_behind()
        {
            AddSwords(3);
            _synth.AutoSynthesize(_inv);
            Assert.AreEqual(new[] { 1, 2 }, _inv.Items.Select(w => w.Stars).OrderBy(s => s).ToArray());
        }

        [Test]
        public void Different_weapons_do_not_merge()
        {
            _inv.Add(WeaponCatalog.OldSwordId);
            _inv.Add("old_axe");
            SynthesisReport r = _synth.AutoSynthesize(_inv);
            Assert.AreEqual(0, r.ConsumedCount);
            Assert.AreEqual(2, _inv.Items.Count);
        }

        [Test]
        public void Equipped_weapon_is_never_consumed()
        {
            AddSwords(2);
            _inv.Equip(_inv.Items[0].Id);
            WeaponInstance equipped = _inv.Equipped;
            SynthesisReport r = _synth.AutoSynthesize(_inv);
            Assert.AreEqual(0, r.ConsumedCount);
            Assert.AreSame(equipped, _inv.Equipped);
            Assert.AreEqual(2, _inv.Items.Count);
        }

        [Test]
        public void Stops_at_max_stars()
        {
            AddSwords(32); // 1성 32 → 2성 16 → 3성 8 → 4성 4 → 5성 2 (더 이상 합성 안 됨)
            _synth.AutoSynthesize(_inv);
            Assert.AreEqual(2, _inv.Items.Count);
            Assert.IsTrue(_inv.Items.All(w => w.Stars == 5));
        }

        [Test]
        public void Result_keeps_higher_level()
        {
            AddSwords(2);
            var wallet = Fixtures.Rich();
            new WeaponLevelService(Fixtures.Config(), _catalog).Apply(_inv, _inv.Items[1].Id, 7, 50, wallet);
            _synth.AutoSynthesize(_inv);
            Assert.AreEqual(8, _inv.Items.Single().Level);
        }

        [Test]
        public void Nothing_to_do_is_ok()
        {
            SynthesisReport r = _synth.AutoSynthesize(_inv);
            Assert.AreEqual(0, r.ConsumedCount);
            Assert.AreEqual(0, r.Created.Count);
            Assert.Throws<ArgumentNullException>(() => _synth.AutoSynthesize(null));
        }
    }

    public class WeaponLevelServiceTests
    {
        private WeaponCatalog _catalog;
        private WeaponInventory _inv;
        private WeaponLevelService _service;
        private WeaponInstance _sword;

        [SetUp]
        public void SetUp()
        {
            _catalog = WeaponCatalog.CreateDefault();
            _inv = new WeaponInventory(_catalog);
            _service = new WeaponLevelService(Fixtures.Config(), _catalog);
            _sword = _inv.Add(WeaponCatalog.OldSwordId).Value;
        }

        [Test]
        public void Cap_is_player_level_but_never_above_100()
        {
            Assert.AreEqual(5, _service.Cap(5));
            Assert.AreEqual(100, _service.Cap(150));
            Assert.AreEqual(1, _service.Cap(0));
        }

        [Test]
        public void Cost_uses_weapon_category_scroll_and_grows()
        {
            Cost c1 = _service.CostOfNext(WeaponCategory.Magic, 1);
            Cost c10 = _service.CostOfNext(WeaponCategory.Magic, 10);
            Assert.AreEqual(WeaponCategory.Magic, c1.ScrollType);
            Assert.AreEqual(10, c1.Coins);
            Assert.AreEqual(1, c1.Scrolls);
            Assert.Greater(c10.Coins, c1.Coins);
            Assert.Greater(c10.Scrolls, c1.Scrolls);
        }

        [Test]
        public void Apply_levels_up_and_deducts_exact_cost()
        {
            Wallet w = Fixtures.Rich();
            long coinsBefore = w.Coins;
            long scrollsBefore = w.GetScrolls(WeaponCategory.Melee);

            Result<LevelUpPlan> r = _service.Apply(_inv, _sword.Id, 3, 10, w);

            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(4, _sword.Level);
            long expectedCoins = _service.CostOfNext(WeaponCategory.Melee, 1).Coins
                + _service.CostOfNext(WeaponCategory.Melee, 2).Coins
                + _service.CostOfNext(WeaponCategory.Melee, 3).Coins;
            Assert.AreEqual(coinsBefore - expectedCoins, w.Coins);
            Assert.AreEqual(scrollsBefore - r.Value.TotalCost.Scrolls, w.GetScrolls(WeaponCategory.Melee));
            Assert.AreEqual(scrollsBefore, w.GetScrolls(WeaponCategory.Magic) + 0, "other scroll types untouched");
        }

        [Test]
        public void Auto_level_up_stops_at_player_level()
        {
            Result<LevelUpPlan> r = _service.Apply(_inv, _sword.Id, 50, 5, Fixtures.Rich());
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(5, _sword.Level);
            Assert.AreEqual(LevelUpStop.Cap, r.Value.StoppedBy);
        }

        [Test]
        public void At_cap_fails_without_spending()
        {
            Wallet w = Fixtures.Rich();
            long before = w.Coins;
            Assert.AreEqual(ErrorCode.LevelCapReached, _service.Apply(_inv, _sword.Id, 1, 1, w).Error);
            Assert.AreEqual(before, w.Coins);
            Assert.AreEqual(1, _sword.Level);
        }

        [Test]
        public void Not_enough_scrolls_changes_nothing()
        {
            var w = new Wallet();
            w.AddCoins(1000);
            Assert.AreEqual(ErrorCode.InsufficientScrolls, _service.Apply(_inv, _sword.Id, 1, 10, w).Error);
            Assert.AreEqual(1000, w.Coins);
            Assert.AreEqual(1, _sword.Level);
        }

        [Test]
        public void Preview_does_not_mutate()
        {
            Wallet w = Fixtures.Rich();
            long before = w.Coins;
            Result<LevelUpPlan> r = _service.Preview(_inv, _sword.Id, 3, 10, w);
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(3, r.Value.Levels);
            Assert.AreEqual(before, w.Coins);
            Assert.AreEqual(1, _sword.Level);
        }

        [Test]
        public void Bad_arguments_fail()
        {
            Wallet w = Fixtures.Rich();
            Assert.AreEqual(ErrorCode.WeaponNotFound, _service.Apply(_inv, 999, 1, 10, w).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _service.Apply(_inv, _sword.Id, 0, 10, w).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _service.Apply(null, _sword.Id, 1, 10, w).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _service.Apply(_inv, _sword.Id, 1, 10, null).Error);
        }
    }
}
