using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 튜토리얼 한 단계(불변 데이터).
    /// Highlights: 황금 원으로 강조하고 입력을 허용할 UI 대상. 비어 있으면 입력을 막지 않는다.
    /// CompleteOn: 이 중 하나의 이벤트가 오면 다음 단계로 넘어간다.
    /// </summary>
    public sealed class TutorialStep
    {
        private static readonly IReadOnlyDictionary<JobClass, string> NoJobLines =
            new Dictionary<JobClass, string>();

        public string Id { get; }
        public string Speaker { get; }
        /// <summary>대사. 없으면 빈 문자열. {move} {attack} {run} {dash}는 조작 안내로 치환된다.</summary>
        public string Line { get; }
        public IReadOnlyList<string> Highlights { get; }
        public IReadOnlyList<string> CompleteOn { get; }
        /// <summary>선택한 직업에 따라 바뀌는 대사(없으면 비어 있음).</summary>
        public IReadOnlyDictionary<JobClass, string> JobLines { get; }

        public TutorialStep(string id, string speaker, string line, IEnumerable<string> highlights,
            IEnumerable<string> completeOn, IReadOnlyDictionary<JobClass, string> jobLines = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id is required", nameof(id));
            }
            List<string> completes = (completeOn ?? Enumerable.Empty<string>()).ToList();
            if (completes.Count == 0 || completes.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("at least one non-empty completion event is required", nameof(completeOn));
            }
            List<string> targets = (highlights ?? Enumerable.Empty<string>()).ToList();
            if (targets.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("highlight targets must be non-empty", nameof(highlights));
            }
            Id = id;
            Speaker = speaker ?? string.Empty;
            Line = line ?? string.Empty;
            Highlights = targets.AsReadOnly();
            CompleteOn = completes.AsReadOnly();
            JobLines = jobLines == null ? NoJobLines : new Dictionary<JobClass, string>(jobLines);
        }

        public bool CompletesOn(string gameEvent)
        {
            return gameEvent != null && CompleteOn.Contains(gameEvent, StringComparer.Ordinal);
        }

        public string LineFor(JobClass job)
        {
            return JobLines.TryGetValue(job, out string line) ? line : Line;
        }
    }
}
