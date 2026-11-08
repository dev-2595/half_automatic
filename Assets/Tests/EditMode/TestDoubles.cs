using System;
using System.Collections.Generic;
using HalfAutomatic.Core;

namespace HalfAutomatic.Tests
{
    /// <summary>미리 정한 값을 순서대로 돌려주는 난수. 값이 모자라면 예외로 테스트 실수를 드러낸다.</summary>
    internal sealed class ScriptedRandom : IRandom
    {
        private readonly Queue<double> _doubles;
        private readonly Queue<int> _ints;

        public ScriptedRandom(IEnumerable<double> doubles, IEnumerable<int> ints)
        {
            _doubles = new Queue<double>(doubles);
            _ints = new Queue<int>(ints);
        }

        public double NextDouble()
        {
            if (_doubles.Count == 0) throw new InvalidOperationException("no scripted double left");
            return _doubles.Dequeue();
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_ints.Count == 0) throw new InvalidOperationException("no scripted int left");
            int value = _ints.Dequeue();
            if (value < minInclusive || value >= maxExclusive)
            {
                throw new InvalidOperationException($"scripted int {value} outside [{minInclusive},{maxExclusive})");
            }
            return value;
        }
    }

    internal static class Fixtures
    {
        public static BalanceConfig Config() => BalanceConfig.CreateDefault();

        public static Wallet Rich()
        {
            var wallet = new Wallet();
            wallet.AddCoins(1_000_000_000);
            wallet.AddScrolls(WeaponCategory.Melee, 1_000_000);
            wallet.AddScrolls(WeaponCategory.Magic, 1_000_000);
            wallet.AddScrolls(WeaponCategory.Heal, 1_000_000);
            return wallet;
        }
    }
}
