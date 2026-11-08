using System;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class ExperienceCurveTests
    {
        [Test]
        public void First_level_is_cheap_and_curve_grows()
        {
            var curve = new ExperienceCurve(Fixtures.Config());
            Assert.AreEqual(10, curve.ExpToNext(1));
            long previous = 0;
            for (int level = 1; level < curve.MaxLevel; level++)
            {
                long need = curve.ExpToNext(level);
                Assert.Greater(need, previous, $"level {level}");
                previous = need;
            }
            Assert.AreEqual(0, curve.ExpToNext(curve.MaxLevel));
        }

        [Test]
        public void Out_of_range_levels_throw()
        {
            var curve = new ExperienceCurve(Fixtures.Config());
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.ExpToNext(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.ExpToNext(curve.MaxLevel + 1));
        }
    }

    public class PlayerLevelTests
    {
        private static PlayerLevel NewLevel(int maxLevel = 100) =>
            new PlayerLevel(new ExperienceCurve(new BalanceConfig { MaxPlayerLevel = maxLevel }));

        [Test]
        public void Starts_at_level_one()
        {
            PlayerLevel p = NewLevel();
            Assert.AreEqual(1, p.Level);
            Assert.AreEqual(0, p.Exp);
            Assert.AreEqual(10, p.ExpRemaining);
        }

        [Test]
        public void AddExp_levels_up_and_carries_over()
        {
            PlayerLevel p = NewLevel();
            Assert.AreEqual(0, p.AddExp(9));
            Assert.AreEqual(1, p.ExpRemaining);
            Assert.AreEqual(1, p.AddExp(3)); // 10 넘김, 2 이월
            Assert.AreEqual(2, p.Level);
            Assert.AreEqual(2, p.Exp);
        }

        [Test]
        public void AddExp_can_gain_multiple_levels()
        {
            PlayerLevel p = NewLevel();
            var curve = new ExperienceCurve(Fixtures.Config());
            long total = curve.ExpToNext(1) + curve.ExpToNext(2) + curve.ExpToNext(3);
            Assert.AreEqual(3, p.AddExp(total));
            Assert.AreEqual(4, p.Level);
            Assert.AreEqual(0, p.Exp);
        }

        [Test]
        public void Max_level_discards_extra_exp()
        {
            PlayerLevel p = NewLevel(maxLevel: 3);
            p.AddExp(long.MaxValue);
            Assert.AreEqual(3, p.Level);
            Assert.AreEqual(0, p.Exp);
            Assert.IsTrue(p.IsMaxLevel);
            Assert.AreEqual(0, p.ExpRemaining);
            Assert.AreEqual(0, p.AddExp(100));
        }

        [Test]
        public void Negative_exp_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NewLevel().AddExp(-1));
        }

        [Test]
        public void Restore_validates_and_keeps_state_on_failure()
        {
            PlayerLevel p = NewLevel();
            Assert.IsTrue(p.Restore(5, 3).IsOk);
            Assert.AreEqual(5, p.Level);
            Assert.AreEqual(ErrorCode.InvalidArgument, p.Restore(0, 0).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, p.Restore(5, -1).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, p.Restore(1, 10).Error); // ExpToNext(1)=10 이상
            Assert.AreEqual(ErrorCode.InvalidArgument, p.Restore(100, 1).Error); // 최대 레벨은 EXP 0
            Assert.AreEqual(5, p.Level);
            Assert.AreEqual(3, p.Exp);
        }
    }
}
