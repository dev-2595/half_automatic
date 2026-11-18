using System;
using System.Collections.Generic;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 계정 목록을 파일 하나에 저장한다. 파일이 손상되었으면 덮어쓰지 않고 DataCorrupt를 돌려준다
    /// (앱 시작 시 <see cref="CheckIntegrity"/> → <see cref="Quarantine"/>로 처리).
    /// </summary>
    public sealed class FileAccountStore : IAccountStore
    {
        private readonly ChecksummedFileStore _file;
        private readonly ISerializer<AccountsData> _serializer;

        public FileAccountStore(ChecksummedFileStore file, ISerializer<AccountsData> serializer)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>파일이 없거나 정상이면 Ok. 손상이면 DataCorrupt, 미래 버전이면 UnsupportedVersion.</summary>
        public Result CheckIntegrity()
        {
            Result<Dictionary<string, AccountRecord>> all = LoadAll();
            return all.IsOk ? Result.Ok() : Result.Fail(all.Error);
        }

        public Result Quarantine() => _file.Quarantine();

        public Result<AccountRecord> Find(string key)
        {
            Result<Dictionary<string, AccountRecord>> all = LoadAll();
            if (!all.IsOk)
            {
                return Result<AccountRecord>.Fail(all.Error);
            }
            return key != null && all.Value.TryGetValue(key, out AccountRecord record)
                ? Result<AccountRecord>.Ok(record)
                : Result<AccountRecord>.Fail(ErrorCode.NotFound);
        }

        public Result Save(AccountRecord record)
        {
            if (record == null)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            Result<Dictionary<string, AccountRecord>> all = LoadAll();
            if (!all.IsOk)
            {
                return Result.Fail(all.Error);
            }
            all.Value[record.Key] = record;

            var list = new List<AccountData>(all.Value.Count);
            foreach (AccountRecord r in all.Value.Values)
            {
                list.Add(new AccountData
                {
                    username = r.Username,
                    salt = Convert.ToBase64String(r.Password.Salt),
                    hash = Convert.ToBase64String(r.Password.Hash),
                    iterations = r.Password.Iterations,
                    createdAtUnix = r.CreatedAtUnix,
                });
            }
            var data = new AccountsData { version = AccountsData.CurrentVersion, accounts = list.ToArray() };
            return _file.Write(_serializer.Serialize(data));
        }

        private Result<Dictionary<string, AccountRecord>> LoadAll()
        {
            var result = new Dictionary<string, AccountRecord>(StringComparer.Ordinal);
            Result<string> text = _file.Read();
            if (!text.IsOk)
            {
                return text.Error == ErrorCode.NotFound
                    ? Result<Dictionary<string, AccountRecord>>.Ok(result)
                    : Result<Dictionary<string, AccountRecord>>.Fail(text.Error);
            }
            Result<AccountsData> data = _serializer.Deserialize(text.Value);
            if (!data.IsOk || data.Value == null || data.Value.accounts == null || data.Value.version < 1)
            {
                return Corrupt();
            }
            if (data.Value.version > AccountsData.CurrentVersion)
            {
                return Result<Dictionary<string, AccountRecord>>.Fail(ErrorCode.UnsupportedVersion);
            }
            foreach (AccountData a in data.Value.accounts)
            {
                AccountRecord record = TryMap(a);
                if (record == null || result.ContainsKey(record.Key))
                {
                    return Corrupt();
                }
                result.Add(record.Key, record);
            }
            return Result<Dictionary<string, AccountRecord>>.Ok(result);
        }

        private static AccountRecord TryMap(AccountData a)
        {
            if (a == null || !AccountRules.NormalizeUsername(a.username).IsOk || a.createdAtUnix < 0
                || a.iterations < PasswordHasher.MinIterations)
            {
                return null;
            }
            try
            {
                var hash = new PasswordHash(Convert.FromBase64String(a.salt ?? string.Empty),
                    Convert.FromBase64String(a.hash ?? string.Empty), a.iterations);
                return new AccountRecord(a.username, hash, a.createdAtUnix);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static Result<Dictionary<string, AccountRecord>> Corrupt() =>
            Result<Dictionary<string, AccountRecord>>.Fail(ErrorCode.DataCorrupt);
    }
}
