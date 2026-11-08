using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 튜토리얼 진행 상태 기계. 화면과 연출은 모르고, "지금 어느 단계인지 / 무엇을 눌러도 되는지 /
    /// 어떤 이벤트에 다음으로 넘어가는지"만 판단한다.
    /// </summary>
    public sealed class TutorialRunner
    {
        private readonly IReadOnlyList<TutorialStep> _steps;

        public int CurrentIndex { get; private set; }
        public int StepCount => _steps.Count;
        public bool IsComplete => CurrentIndex >= _steps.Count;
        /// <summary>완료되었으면 null.</summary>
        public TutorialStep Current => IsComplete ? null : _steps[CurrentIndex];

        public TutorialRunner(IReadOnlyList<TutorialStep> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }
            if (steps.Count == 0 || steps.Any(s => s == null))
            {
                throw new ArgumentException("steps must be non-empty and contain no nulls", nameof(steps));
            }
            if (steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != steps.Count)
            {
                throw new ArgumentException("step ids must be unique", nameof(steps));
            }
            _steps = steps.ToList().AsReadOnly();
        }

        /// <summary>게임 이벤트를 전달한다. 현재 단계를 완료시키면 다음 단계로 넘어가고 true.</summary>
        public bool Notify(string gameEvent)
        {
            if (IsComplete || !Current.CompletesOn(gameEvent))
            {
                return false;
            }
            CurrentIndex++;
            return true;
        }

        /// <summary>강조 대상이 있는 단계에서는 그 대상만 누를 수 있다.</summary>
        public bool IsInputAllowed(string uiTarget)
        {
            if (IsComplete || Current.Highlights.Count == 0)
            {
                return true;
            }
            return uiTarget != null && Current.Highlights.Contains(uiTarget, StringComparer.Ordinal);
        }

        /// <summary>저장된 진행도로 복원한다(0 ~ StepCount). 범위를 벗어나면 실패하고 상태는 그대로다.</summary>
        public Result Restore(int index)
        {
            if (index < 0 || index > _steps.Count)
            {
                return Result.Fail(ErrorCode.InvalidTutorialState);
            }
            CurrentIndex = index;
            return Result.Ok();
        }
    }

    /// <summary>플랫폼별 조작 안내 문구. 대사의 {move} {attack} {run} {dash}를 치환한다.</summary>
    public sealed class ControlHints
    {
        public string Move { get; }
        public string Attack { get; }
        public string Run { get; }
        public string Dash { get; }

        public ControlHints(string move, string attack, string run, string dash)
        {
            Move = move ?? throw new ArgumentNullException(nameof(move));
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
            Run = run ?? throw new ArgumentNullException(nameof(run));
            Dash = dash ?? throw new ArgumentNullException(nameof(dash));
        }

        public static ControlHints Keyboard { get; } = new ControlHints("방향키", "C", "R", "Space");
        public static ControlHints Touch { get; } = new ControlHints("왼쪽 조이스틱", "공격 버튼", "달리기 버튼", "대쉬 버튼");

        public string Apply(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }
            return line.Replace("{move}", Move).Replace("{attack}", Attack)
                .Replace("{run}", Run).Replace("{dash}", Dash);
        }
    }
}
