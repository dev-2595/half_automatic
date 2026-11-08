namespace HalfAutomatic.Core
{
    /// <summary>실패 사유. 사용자에게 보일 문구는 UI 계층에서 매핑한다.</summary>
    public enum ErrorCode
    {
        None = 0,
        NicknameEmpty,
        NicknameTooShort,
        NicknameTooLong,
        NicknameInvalidChar,
        InvalidProfileData,
        NoProfile,
        AlreadySignedIn,
        DataCorrupt,
        StorageFailure,

        // 성장 / 경제
        InvalidArgument,
        Overflow,
        InsufficientCoins,
        InsufficientScrolls,
        LevelCapReached,

        // 무기
        UnknownWeapon,
        WeaponNotFound,
        NothingEquipped,
        AlreadyEquipped,

        // 튜토리얼
        InvalidTutorialState,
    }

    /// <summary>값이 없는 연산의 결과.</summary>
    public readonly struct Result
    {
        public bool IsOk { get; }
        public ErrorCode Error { get; }

        private Result(bool isOk, ErrorCode error)
        {
            IsOk = isOk;
            Error = error;
        }

        public static Result Ok() => new Result(true, ErrorCode.None);
        public static Result Fail(ErrorCode error) => new Result(false, error);
    }

    /// <summary>값이 있는 연산의 결과. 실패 시 Value는 default.</summary>
    public readonly struct Result<T>
    {
        public bool IsOk { get; }
        public T Value { get; }
        public ErrorCode Error { get; }

        private Result(bool isOk, T value, ErrorCode error)
        {
            IsOk = isOk;
            Value = value;
            Error = error;
        }

        public static Result<T> Ok(T value) => new Result<T>(true, value, ErrorCode.None);
        public static Result<T> Fail(ErrorCode error) => new Result<T>(false, default, error);
    }
}
