using System;
using System.Collections.Generic;

namespace HalfAutomatic.Core
{
    /// <summary>난수 추상화(테스트에서 결과를 고정하기 위함).</summary>
    public interface IRandom
    {
        /// <summary>[0, 1) 범위의 실수.</summary>
        double NextDouble();
        /// <summary>[minInclusive, maxExclusive) 범위의 정수.</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandom : IRandom
    {
        private readonly Random _random;

        public SystemRandom() : this(new Random()) { }

        public SystemRandom(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public double NextDouble() => _random.NextDouble();
        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }

    /// <summary>몬스터 처치 보상(EXP는 몬스터별 값이라 여기서 다루지 않는다).</summary>
    public readonly struct LootDrop
    {
        public long Coins { get; }
        public long Scrolls { get; }
        /// <summary>Scrolls가 0이면 의미 없음.</summary>
        public WeaponCategory ScrollType { get; }
        /// <summary>드랍된 무기 id. 없으면 null.</summary>
        public string WeaponDefinitionId { get; }

        public LootDrop(long coins, long scrolls, WeaponCategory scrollType, string weaponDefinitionId)
        {
            Coins = coins;
            Scrolls = scrolls;
            ScrollType = scrollType;
            WeaponDefinitionId = weaponDefinitionId;
        }
    }

    /// <summary>
    /// 드랍 계산. 직업에 맞는 계열의 무기와 강화의 서가 나온다.
    /// 직업 미선택(None)이면 강화의 서는 나오지 않고 무기는 모든 계열에서 나온다.
    /// </summary>
    public sealed class LootService
    {
        private readonly BalanceConfig _config;
        private readonly WeaponCatalog _catalog;
        private readonly IRandom _random;

        public LootService(BalanceConfig config, WeaponCatalog catalog, IRandom random)
        {
            _config = BalanceConfig.RequireValid(config);
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public LootDrop Roll(JobClass job)
        {
            if (!JobRules.IsDefined(job))
            {
                throw new ArgumentOutOfRangeException(nameof(job));
            }
            WeaponCategory? category = JobRules.CategoryOf(job);

            long coins = _random.Next(_config.CoinDropMin, _config.CoinDropMax + 1);

            long scrolls = 0;
            if (category.HasValue && _random.NextDouble() < _config.ScrollDropChance)
            {
                scrolls = _random.Next(_config.ScrollDropMin, _config.ScrollDropMax + 1);
            }

            string weaponId = null;
            if (_random.NextDouble() < _config.WeaponDropChance)
            {
                IReadOnlyList<WeaponDefinition> candidates = _catalog.Droppable(category);
                if (candidates.Count > 0)
                {
                    weaponId = candidates[_random.Next(0, candidates.Count)].Id;
                }
            }

            return new LootDrop(coins, scrolls, category ?? WeaponCategory.Melee, weaponId);
        }
    }
}
