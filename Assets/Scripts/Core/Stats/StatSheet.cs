using System;

namespace HalfAutomatic.Core
{
    public enum StatType
    {
        Health = 0,
        Attack = 1,
        Cooldown = 2,
    }

    /// <summary>스탯별 레벨(0부터 시작, 0 = 보너스 없음).</summary>
    public sealed class StatSheet
    {
        public const int StatCount = 3;

        private readonly int[] _levels = new int[StatCount];

        public int GetLevel(StatType stat)
        {
            return _levels[Index(stat)];
        }

        internal void SetLevel(StatType stat, int level)
        {
            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            _levels[Index(stat)] = level;
        }

        /// <summary>저장된 값으로 복원. 하나라도 범위를 벗어나면 실패하고 상태는 그대로다.</summary>
        public Result Restore(int health, int attack, int cooldown, int cap)
        {
            if (!InRange(health, cap) || !InRange(attack, cap) || !InRange(cooldown, cap))
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            _levels[(int)StatType.Health] = health;
            _levels[(int)StatType.Attack] = attack;
            _levels[(int)StatType.Cooldown] = cooldown;
            return Result.Ok();
        }

        internal static int Index(StatType stat)
        {
            int i = (int)stat;
            if (i < 0 || i >= StatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stat));
            }
            return i;
        }

        private static bool InRange(int level, int cap) => level >= 0 && level <= cap;
    }
}
