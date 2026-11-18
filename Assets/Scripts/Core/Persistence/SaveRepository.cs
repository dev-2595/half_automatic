using System;

namespace HalfAutomatic.Core
{
    /// <summary>계정별 진행 데이터 저장소. 파일 이름은 "save_{계정 키}.dat".</summary>
    public sealed class SaveRepository
    {
        private readonly string _directory;
        private readonly ISerializer<SaveData> _serializer;
        private readonly GameRules _rules;

        /// <summary>직전 Load가 백업 파일에서 복구했으면 true.</summary>
        public bool LastLoadUsedBackup { get; private set; }

        public SaveRepository(string directory, ISerializer<SaveData> serializer, GameRules rules)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("directory is required", nameof(directory));
            }
            _directory = directory;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>없으면 NotFound, 손상이면 DataCorrupt, 미래 버전이면 UnsupportedVersion.</summary>
        public Result<GameState> Load(string accountKey)
        {
            LastLoadUsedBackup = false;
            Result<ChecksummedFileStore> file = FileFor(accountKey);
            if (!file.IsOk)
            {
                return Result<GameState>.Fail(file.Error);
            }
            Result<string> text = file.Value.Read();
            if (!text.IsOk)
            {
                return Result<GameState>.Fail(text.Error);
            }
            LastLoadUsedBackup = file.Value.LastReadUsedBackup;
            Result<SaveData> data = _serializer.Deserialize(text.Value);
            if (!data.IsOk)
            {
                return Result<GameState>.Fail(ErrorCode.DataCorrupt);
            }
            return SaveMapper.FromData(data.Value, _rules);
        }

        public Result Save(string accountKey, GameState state, long nowUnix)
        {
            if (state == null)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            Result<ChecksummedFileStore> file = FileFor(accountKey);
            if (!file.IsOk)
            {
                return Result.Fail(file.Error);
            }
            return file.Value.Write(_serializer.Serialize(SaveMapper.ToData(state, nowUnix)));
        }

        public Result Quarantine(string accountKey)
        {
            Result<ChecksummedFileStore> file = FileFor(accountKey);
            return file.IsOk ? file.Value.Quarantine() : Result.Fail(file.Error);
        }

        /// <summary>계정 키는 정규화된 아이디(영문 소문자·숫자·밑줄)만 허용해 경로 조작을 막는다.</summary>
        private Result<ChecksummedFileStore> FileFor(string accountKey)
        {
            Result<string> normalized = AccountRules.NormalizeUsername(accountKey);
            if (!normalized.IsOk || normalized.Value != accountKey)
            {
                return Result<ChecksummedFileStore>.Fail(ErrorCode.InvalidArgument);
            }
            string path = System.IO.Path.Combine(_directory, "save_" + accountKey + ".dat");
            return Result<ChecksummedFileStore>.Ok(new ChecksummedFileStore(path));
        }
    }
}
