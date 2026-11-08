namespace HalfAutomatic.Core
{
    /// <summary>
    /// 한 번의 강화에 드는 비용. 강화의 서는 한 종류만 쓴다(무기 계열에 맞는 서).
    /// Scrolls가 0이면 ScrollType은 의미가 없다.
    /// </summary>
    public readonly struct Cost
    {
        public long Coins { get; }
        public WeaponCategory ScrollType { get; }
        public long Scrolls { get; }

        public Cost(long coins, WeaponCategory scrollType, long scrolls)
        {
            Coins = coins < 0 ? 0 : coins;
            ScrollType = scrollType;
            Scrolls = scrolls < 0 ? 0 : scrolls;
        }

        public static Cost CoinsOnly(long coins) => new Cost(coins, WeaponCategory.Melee, 0);

        /// <summary>두 비용을 더한다. 서 종류가 다르거나 합이 long을 넘으면 실패.</summary>
        public static Result<Cost> Add(Cost a, Cost b)
        {
            if (a.Scrolls > 0 && b.Scrolls > 0 && a.ScrollType != b.ScrollType)
            {
                return Result<Cost>.Fail(ErrorCode.InvalidArgument);
            }
            if (a.Coins > long.MaxValue - b.Coins || a.Scrolls > long.MaxValue - b.Scrolls)
            {
                return Result<Cost>.Fail(ErrorCode.Overflow);
            }
            WeaponCategory type = a.Scrolls > 0 ? a.ScrollType : b.ScrollType;
            return Result<Cost>.Ok(new Cost(a.Coins + b.Coins, type, a.Scrolls + b.Scrolls));
        }
    }
}
