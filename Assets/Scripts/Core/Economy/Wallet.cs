using System;

namespace HalfAutomatic.Core
{
    /// <summary>코인과 계열별 강화의 서 보유량. 모든 변경은 검사 후 한 번에 반영된다(원자성).</summary>
    public sealed class Wallet
    {
        private readonly long[] _scrolls = new long[JobRules.CategoryCount];

        public long Coins { get; private set; }

        public long GetScrolls(WeaponCategory category)
        {
            RequireDefined(category);
            return _scrolls[(int)category];
        }

        public Result AddCoins(long amount)
        {
            if (amount < 0)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            if (Coins > long.MaxValue - amount)
            {
                return Result.Fail(ErrorCode.Overflow);
            }
            Coins += amount;
            return Result.Ok();
        }

        public Result AddScrolls(WeaponCategory category, long amount)
        {
            if (amount < 0 || !JobRules.IsDefined(category))
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            int i = (int)category;
            if (_scrolls[i] > long.MaxValue - amount)
            {
                return Result.Fail(ErrorCode.Overflow);
            }
            _scrolls[i] += amount;
            return Result.Ok();
        }

        /// <summary>지불 가능하면 Ok, 아니면 부족한 재화에 해당하는 오류.</summary>
        public Result CheckAfford(Cost cost)
        {
            if (cost.Scrolls > 0 && !JobRules.IsDefined(cost.ScrollType))
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            if (Coins < cost.Coins)
            {
                return Result.Fail(ErrorCode.InsufficientCoins);
            }
            if (cost.Scrolls > 0 && _scrolls[(int)cost.ScrollType] < cost.Scrolls)
            {
                return Result.Fail(ErrorCode.InsufficientScrolls);
            }
            return Result.Ok();
        }

        /// <summary>전부 지불할 수 있을 때만 차감한다. 실패 시 아무것도 바뀌지 않는다.</summary>
        public Result TrySpend(Cost cost)
        {
            Result check = CheckAfford(cost);
            if (!check.IsOk)
            {
                return check;
            }
            Coins -= cost.Coins;
            if (cost.Scrolls > 0)
            {
                _scrolls[(int)cost.ScrollType] -= cost.Scrolls;
            }
            return Result.Ok();
        }

        private static void RequireDefined(WeaponCategory category)
        {
            if (!JobRules.IsDefined(category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }
        }
    }
}
