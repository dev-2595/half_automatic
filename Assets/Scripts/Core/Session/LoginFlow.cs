using System;

namespace HalfAutomatic.Core
{
    public enum LoginStep
    {
        /// <summary>아이디/비밀번호 입력.</summary>
        Login,
        /// <summary>"비밀번호 바꿀 거야?" [바꾸기]/[안 바꾸기].</summary>
        PasswordChangePrompt,
        /// <summary>새 비밀번호 입력.</summary>
        PasswordChange,
        /// <summary>환생 후 이름(닉네임) 입력.</summary>
        Nickname,
        /// <summary>게임 진행 중.</summary>
        InGame,
    }

    /// <summary>
    /// 로그인부터 게임 시작까지의 흐름(기획서 §4)과 진행 데이터 저장을 조정한다.
    /// 각 메서드는 지정된 단계에서만 동작하며, 다른 단계에서 부르면 InvalidOperation.
    /// </summary>
    public sealed class LoginFlow
    {
        private readonly AccountService _accounts;
        private readonly SaveRepository _saves;
        private readonly GameRules _rules;
        private readonly IClock _clock;

        public LoginStep Step { get; private set; } = LoginStep.Login;
        public AccountRecord Account { get; private set; }
        public GameState State { get; private set; }

        /// <summary>로그인 시 세이브가 손상되어 새로 시작했으면 true(UI 안내용).</summary>
        public bool SaveWasReset { get; private set; }
        /// <summary>로그인 시 세이브를 백업에서 복구했으면 true(UI 안내용).</summary>
        public bool SaveRestoredFromBackup { get; private set; }

        public LoginFlow(AccountService accounts, SaveRepository saves, GameRules rules, IClock clock)
        {
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result SignIn(string username, string password)
        {
            if (Step != LoginStep.Login)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            Result<AccountRecord> account = _accounts.SignIn(username, password);
            if (!account.IsOk)
            {
                return Result.Fail(account.Error);
            }

            Result<GameState> loaded = _saves.Load(account.Value.Key);
            SaveWasReset = false;
            SaveRestoredFromBackup = false;
            GameState state;
            if (loaded.IsOk)
            {
                state = loaded.Value;
                SaveRestoredFromBackup = _saves.LastLoadUsedBackup;
            }
            else if (loaded.Error == ErrorCode.NotFound)
            {
                state = GameState.CreateNew(_rules);
            }
            else if (loaded.Error == ErrorCode.DataCorrupt)
            {
                // 손상된 세이브는 덮어쓰지 않도록 치워 두고 새로 시작한다.
                Result moved = _saves.Quarantine(account.Value.Key);
                if (!moved.IsOk)
                {
                    return moved;
                }
                state = GameState.CreateNew(_rules);
                SaveWasReset = true;
            }
            else
            {
                // UnsupportedVersion(더 새 버전에서 저장됨), StorageFailure 등: 데이터를 지키기 위해 진행하지 않는다.
                return Result.Fail(loaded.Error);
            }

            Account = account.Value;
            State = state;
            Step = LoginStep.PasswordChangePrompt;
            return Result.Ok();
        }

        /// <summary>새 계정 가입. 가입 직후에는 비밀번호 변경을 묻지 않고 바로 닉네임 단계로 간다.</summary>
        public Result Register(string username, string password)
        {
            if (Step != LoginStep.Login)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            Result<AccountRecord> account = _accounts.Register(username, password);
            if (!account.IsOk)
            {
                return Result.Fail(account.Error);
            }
            GameState state = GameState.CreateNew(_rules);
            Result saved = _saves.Save(account.Value.Key, state, _clock.UnixSeconds);
            if (!saved.IsOk)
            {
                return saved;
            }
            Account = account.Value;
            State = state;
            SaveWasReset = false;
            SaveRestoredFromBackup = false;
            Step = LoginStep.Nickname;
            return Result.Ok();
        }

        public Result BeginPasswordChange()
        {
            if (Step != LoginStep.PasswordChangePrompt)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            Step = LoginStep.PasswordChange;
            return Result.Ok();
        }

        public Result DeclinePasswordChange()
        {
            if (Step != LoginStep.PasswordChangePrompt && Step != LoginStep.PasswordChange)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            Step = AfterPassword();
            return Result.Ok();
        }

        public Result ChangePassword(string newPassword, string confirm)
        {
            if (Step != LoginStep.PasswordChange)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            if (!string.Equals(newPassword, confirm, StringComparison.Ordinal))
            {
                return Result.Fail(ErrorCode.PasswordMismatch);
            }
            Result<AccountRecord> changed = _accounts.ChangePassword(Account, newPassword);
            if (!changed.IsOk)
            {
                return Result.Fail(changed.Error);
            }
            Account = changed.Value;
            Step = AfterPassword();
            return Result.Ok();
        }

        /// <summary>닉네임을 정하고 저장한다. 저장에 실패하면 이름을 되돌리고 단계도 그대로 둔다.</summary>
        public Result SetNickname(string raw)
        {
            if (Step != LoginStep.Nickname)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            string previous = State.Nickname;
            Result set = State.SetNickname(raw);
            if (!set.IsOk)
            {
                return set;
            }
            Result saved = _saves.Save(Account.Key, State, _clock.UnixSeconds);
            if (!saved.IsOk)
            {
                State.RestoreNickname(previous);
                return saved;
            }
            Step = LoginStep.InGame;
            return Result.Ok();
        }

        /// <summary>진행 데이터 저장(자동 저장, 앱 종료/일시정지 시).</summary>
        public Result Save()
        {
            if (Account == null || State == null)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            return _saves.Save(Account.Key, State, _clock.UnixSeconds);
        }

        /// <summary>저장 후 로그아웃. 저장에 실패하면 로그아웃하지 않는다(진행 데이터 보호).</summary>
        public Result SignOut()
        {
            if (Account == null)
            {
                return Result.Fail(ErrorCode.InvalidOperation);
            }
            Result saved = Save();
            if (!saved.IsOk)
            {
                return saved;
            }
            Account = null;
            State = null;
            Step = LoginStep.Login;
            return Result.Ok();
        }

        private LoginStep AfterPassword() => State.HasNickname ? LoginStep.InGame : LoginStep.Nickname;
    }
}
