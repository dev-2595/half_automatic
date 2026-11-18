using System;

namespace HalfAutomatic.Core
{
    /// <summary>계정 한 개(불변). Key는 소문자 아이디로 저장소와 세이브 파일의 키로 쓴다.</summary>
    public sealed class AccountRecord
    {
        public string Key { get; }
        /// <summary>사용자가 입력한 대소문자 그대로의 아이디(표시용).</summary>
        public string Username { get; }
        public PasswordHash Password { get; }
        public long CreatedAtUnix { get; }

        public AccountRecord(string username, PasswordHash password, long createdAtUnix)
        {
            Result<string> key = AccountRules.NormalizeUsername(username);
            if (!key.IsOk)
            {
                throw new ArgumentException("invalid username", nameof(username));
            }
            if (createdAtUnix < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(createdAtUnix));
            }
            Key = key.Value;
            Username = username;
            Password = password ?? throw new ArgumentNullException(nameof(password));
            CreatedAtUnix = createdAtUnix;
        }

        public AccountRecord WithPassword(PasswordHash password)
        {
            return new AccountRecord(Username, password, CreatedAtUnix);
        }
    }

    /// <summary>계정 저장소. 파일/서버 구현을 바꿔 끼울 수 있다.</summary>
    public interface IAccountStore
    {
        /// <summary>key = 소문자 아이디. 없으면 NotFound.</summary>
        Result<AccountRecord> Find(string key);

        /// <summary>같은 Key가 있으면 덮어쓴다.</summary>
        Result Save(AccountRecord record);
    }
}
