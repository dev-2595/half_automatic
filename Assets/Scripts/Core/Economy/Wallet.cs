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

        /// <summary>저장값으로 복원. 음수거나 개수가 맞지 않으면 실패하고 상태는 그대로다.</summary>
        public Result Restore(long coins, long[] scrolls)
        {
            if (coins < 0 || scrolls == null || scrolls.Length != JobRules.CategoryCount)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            foreach (long amount in scrolls)
            {
                if (amount < 0)
                {
                    return Result.Fail(ErrorCode.InvalidArgument);
                }
            }
            Coins = coins;
            Array.Copy(scrolls, _scrolls, _scrolls.Length);
            return Result.Ok();
        }

        public long[] ScrollsSnapshot() => (long[])_scrolls.Clone();

        private static void RequireDefined(WeaponCategory category)
        {
            if (!JobRules.IsDefined(category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }
        }
    }
}
