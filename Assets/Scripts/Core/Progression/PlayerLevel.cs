using System;

namespace HalfAutomatic.Core
{
    /// <summary>플레이어 레벨과 현재 레벨 안에서 모은 경험치.</summary>
    public sealed class PlayerLevel
    {
        private readonly ExperienceCurve _curve;

        public int Level { get; private set; }
        /// <summary>현재 레벨에서 모은 EXP. 항상 0 이상, ExpToNext 미만(최대 레벨이면 0).</summary>
        public long Exp { get; private set; }

        public PlayerLevel(ExperienceCurve curve)
        {
            _curve = curve ?? throw new ArgumentNullException(nameof(curve));
            Level = 1;
            Exp = 0;
        }

        public bool IsMaxLevel => Level >= _curve.MaxLevel;
        public long ExpToNext => _curve.ExpToNext(Level);
        /// <summary>프로필의 "LV." 터치 시 보여 줄 남은 EXP.</summary>
        public long ExpRemaining => IsMaxLevel ? 0 : ExpToNext - Exp;

        /// <summary>EXP를 더하고 오른 레벨 수를 돌려준다. 최대 레벨에 도달하면 남는 EXP는 버린다.</summary>
        public int AddExp(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }
            int gained = 0;
            long remaining = amount;
            while (remaining > 0 && !IsMaxLevel)
            {
                long need = ExpToNext - Exp;
                if (remaining < need)
                {
                    Exp += remaining;
                    remaining = 0;
                }
                else
                {
                    remaining -= need;
                    Level++;
                    Exp = 0;
                    gained++;
                }
            }
            return gained;
        }

        /// <summary>저장된 값으로 복원한다. 범위를 벗어나면 실패하고 상태는 그대로다.</summary>
        public Result Restore(int level, long exp)
        {
            if (level < 1 || level > _curve.MaxLevel || exp < 0)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            long need = _curve.ExpToNext(level);
            if (level == _curve.MaxLevel ? exp != 0 : exp >= need)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            Level = level;
            Exp = exp;
            return Result.Ok();
        }
    }
}
