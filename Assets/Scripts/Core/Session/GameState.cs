using System;

namespace HalfAutomatic.Core
{
    /// <summary>한 계정의 진행 상태 전체. 저장/불러오기의 단위.</summary>
    public sealed class GameState
    {
        /// <summary>환생 후 이름. 아직 정하지 않았으면 null.</summary>
        public string Nickname { get; private set; }
        public JobClass Job { get; private set; }
        public PlayerLevel Level { get; }
        public Wallet Wallet { get; }
        public WeaponInventory Weapons { get; }
        public StatSheet Stats { get; }
        public TutorialRunner Tutorial { get; }

        internal GameState(PlayerLevel level, Wallet wallet, WeaponInventory weapons, StatSheet stats,
            TutorialRunner tutorial, string nickname, JobClass job)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Tutorial = tutorial ?? throw new ArgumentNullException(nameof(tutorial));
            Nickname = nickname;
            Job = job;
        }

        public static GameState CreateNew(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            return new GameState(new PlayerLevel(rules.Curve), new Wallet(), new WeaponInventory(rules.Catalog),
                new StatSheet(), new TutorialRunner(rules.TutorialSteps), null, JobClass.None);
        }

        public bool HasNickname => Nickname != null;

        public Result SetNickname(string raw)
        {
            Result<string> nickname = NicknameRules.Validate(raw);
            if (!nickname.IsOk)
            {
                return Result.Fail(nickname.Error);
            }
            Nickname = nickname.Value;
            return Result.Ok();
        }

        /// <summary>되돌리기용(저장 실패 시 이전 이름으로 복구).</summary>
        internal void RestoreNickname(string previous)
        {
            Nickname = previous;
        }

        /// <summary>직업 선택(설정 창). None으로는 되돌릴 수 없다.</summary>
        public Result SetJob(JobClass job)
        {
            if (job == JobClass.None || !JobRules.IsDefined(job))
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            Job = job;
            return Result.Ok();
        }
    }
}
