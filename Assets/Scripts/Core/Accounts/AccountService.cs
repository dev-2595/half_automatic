using System;
using System.Collections.Generic;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 로컬 계정 가입·로그인·비밀번호 변경.
    /// 로그인 실패 시 아이디가 없는지 비밀번호가 틀렸는지 구분하지 않는다(WrongCredentials).
    /// 연속 실패 MaxFailedAttempts회면 LockSeconds 동안 해당 아이디의 로그인을 막는다.
    /// ⚠️ 로컬 계정은 기기를 가진 사람이 파일을 직접 바꿀 수 있으므로 "본인 확인" 수준의 보호만 제공한다.
    /// </summary>
    public sealed class AccountService
    {
        public const int MaxFailedAttempts = 5;
        public const long LockSeconds = 30;

        private readonly IAccountStore _store;
        private readonly PasswordHasher _hasher;
        private readonly IClock _clock;
        private readonly Dictionary<string, Attempts> _attempts = new Dictionary<string, Attempts>(StringComparer.Ordinal);

        public AccountService(IAccountStore store, PasswordHasher hasher, IClock clock)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<AccountRecord> Register(string username, string password)
        {
            Result<string> key = AccountRules.NormalizeUsername(username);
            if (!key.IsOk)
            {
                return Result<AccountRecord>.Fail(key.Error);
            }
            Result pwOk = AccountRules.ValidatePassword(password);
            if (!pwOk.IsOk)
            {
                return Result<AccountRecord>.Fail(pwOk.Error);
            }

            Result<AccountRecord> existing = _store.Find(key.Value);
            if (existing.IsOk)
            {
                return Result<AccountRecord>.Fail(ErrorCode.UsernameTaken);
            }
            if (existing.Error != ErrorCode.NotFound)
            {
                return Result<AccountRecord>.Fail(existing.Error);
            }

            var record = new AccountRecord(username, _hasher.Hash(password), _clock.UnixSeconds);
            Result saved = _store.Save(record);
            return saved.IsOk ? Result<AccountRecord>.Ok(record) : Result<AccountRecord>.Fail(saved.Error);
        }

        public Result<AccountRecord> SignIn(string username, string password)
        {
            Result<string> key = AccountRules.NormalizeUsername(username);
            if (!key.IsOk)
            {
                return Result<AccountRecord>.Fail(ErrorCode.WrongCredentials);
            }
            if (LockRemainingSeconds(username) > 0)
            {
                return Result<AccountRecord>.Fail(ErrorCode.TooManyAttempts);
            }

            Result<AccountRecord> found = _store.Find(key.Value);
            if (!found.IsOk && found.Error != ErrorCode.NotFound)
            {
                return found; // 저장소 오류는 실패 횟수에 넣지 않는다.
            }
            if (!found.IsOk || !_hasher.Verify(password, found.Value.Password))
            {
                RecordFailure(key.Value);
                return Result<AccountRecord>.Fail(ErrorCode.WrongCredentials);
            }

            _attempts.Remove(key.Value);
            return found;
        }

        /// <summary>로그인 직후 비밀번호 변경. 반드시 SignIn이 돌려준 계정을 넘긴다.</summary>
        public Result<AccountRecord> ChangePassword(AccountRecord signedIn, string newPassword)
        {
            if (signedIn == null)
            {
                return Result<AccountRecord>.Fail(ErrorCode.InvalidArgument);
            }
            Result pwOk = AccountRules.ValidatePassword(newPassword);
            if (!pwOk.IsOk)
            {
                return Result<AccountRecord>.Fail(pwOk.Error);
            }
            if (_hasher.Verify(newPassword, signedIn.Password))
            {
                return Result<AccountRecord>.Fail(ErrorCode.PasswordUnchanged);
            }
            AccountRecord updated = signedIn.WithPassword(_hasher.Hash(newPassword));
            Result saved = _store.Save(updated);
            return saved.IsOk ? Result<AccountRecord>.Ok(updated) : Result<AccountRecord>.Fail(saved.Error);
        }

        /// <summary>남은 잠금 시간(초). 잠겨 있지 않으면 0.</summary>
        public long LockRemainingSeconds(string username)
        {
            Result<string> key = AccountRules.NormalizeUsername(username);
            if (!key.IsOk || !_attempts.TryGetValue(key.Value, out Attempts a))
            {
                return 0;
            }
            return Math.Max(0, a.LockedUntilUnix - _clock.UnixSeconds);
        }

        private void RecordFailure(string key)
        {
            _attempts.TryGetValue(key, out Attempts a);
            long now = _clock.UnixSeconds;
            int failures = (a.LockedUntilUnix > 0 && now >= a.LockedUntilUnix) ? 1 : a.Failures + 1;
            long lockedUntil = failures >= MaxFailedAttempts ? now + LockSeconds : 0;
            _attempts[key] = new Attempts(failures >= MaxFailedAttempts ? 0 : failures, lockedUntil);
        }

        private readonly struct Attempts
        {
            public int Failures { get; }
            public long LockedUntilUnix { get; }

            public Attempts(int failures, long lockedUntilUnix)
            {
                Failures = failures;
                LockedUntilUnix = lockedUntilUnix;
            }
        }
    }
}
