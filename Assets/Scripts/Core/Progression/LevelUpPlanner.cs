using System;

namespace HalfAutomatic.Core
{
    /// <summary>레벨업이 멈춘 이유.</summary>
    public enum LevelUpStop
    {
        /// <summary>요청한 만큼 모두 올릴 수 있음.</summary>
        None = 0,
        /// <summary>상한(플레이어 레벨 또는 최대 레벨)에 도달.</summary>
        Cap,
        InsufficientCoins,
        InsufficientScrolls,
        Overflow,
    }

    /// <summary>레벨업 계획: 실제로 올릴 수 있는 레벨 수와 총비용.</summary>
    public readonly struct LevelUpPlan
    {
        public int FromLevel { get; }
        public int Levels { get; }
        public Cost TotalCost { get; }
        public LevelUpStop StoppedBy { get; }

        public LevelUpPlan(int fromLevel, int levels, Cost totalCost, LevelUpStop stoppedBy)
        {
            FromLevel = fromLevel;
            Levels = levels;
            TotalCost = totalCost;
            StoppedBy = stoppedBy;
        }

        public int ToLevel => FromLevel + Levels;
    }

    /// <summary>
    /// 수동(1레벨)과 자동 레벨업(목표 레벨 수)이 공유하는 계산기.
    /// 지갑을 바꾸지 않고, 가진 재화와 상한 안에서 올릴 수 있는 만큼을 계산만 한다.
    /// </summary>
    public static class LevelUpPlanner
    {
        /// <param name="currentLevel">현재 레벨.</param>
        /// <param name="requestedLevels">올리고 싶은 레벨 수(1 이상).</param>
        /// <param name="cap">도달 가능한 최대 레벨.</param>
        /// <param name="costOfNext">레벨 L에서 L+1로 올리는 비용.</param>
        public static LevelUpPlan Plan(int currentLevel, int requestedLevels, int cap,
            Func<int, Cost> costOfNext, Wallet wallet)
        {
            if (costOfNext == null)
            {
                throw new ArgumentNullException(nameof(costOfNext));
            }
            if (wallet == null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }
            if (requestedLevels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedLevels));
            }

            Cost total = Cost.CoinsOnly(0);
            int levels = 0;
            while (levels < requestedLevels)
            {
                int level = currentLevel + levels;
                if (level >= cap)
                {
                    return new LevelUpPlan(currentLevel, levels, total, LevelUpStop.Cap);
                }
                Result<Cost> next = Cost.Add(total, costOfNext(level));
                if (!next.IsOk)
                {
                    return new LevelUpPlan(currentLevel, levels, total, LevelUpStop.Overflow);
                }
                Result affordable = wallet.CheckAfford(next.Value);
                if (!affordable.IsOk)
                {
                    LevelUpStop stop = affordable.Error == ErrorCode.InsufficientScrolls
                        ? LevelUpStop.InsufficientScrolls
                        : LevelUpStop.InsufficientCoins;
                    return new LevelUpPlan(currentLevel, levels, total, stop);
                }
                total = next.Value;
                levels++;
            }
            return new LevelUpPlan(currentLevel, levels, total, LevelUpStop.None);
        }

        /// <summary>한 레벨도 못 올린 계획을 오류 코드로 바꾼다.</summary>
        internal static ErrorCode ToError(LevelUpStop stop)
        {
            switch (stop)
            {
                case LevelUpStop.Cap: return ErrorCode.LevelCapReached;
                case LevelUpStop.InsufficientCoins: return ErrorCode.InsufficientCoins;
                case LevelUpStop.InsufficientScrolls: return ErrorCode.InsufficientScrolls;
                case LevelUpStop.Overflow: return ErrorCode.Overflow;
                default: return ErrorCode.InvalidArgument;
            }
        }
    }
}
