namespace HalfAutomatic.Core
{
    /// <summary>닉네임 검증 규칙만 담당한다.</summary>
    public static class NicknameRules
    {
        public const int MinLength = 2;
        public const int MaxLength = 12;

        /// <summary>환생한 정체가 바로 들키는 이름(개그 요소: "그럼 바로 들키잖아!!").</summary>
        private static readonly string[] ObviousNames = { "디멘션", "dimension" };

        /// <summary>
        /// 앞뒤 공백을 제거하고 검증한다. 허용: 글자(한글 포함)·숫자·밑줄. 공백/특수문자/이모지는 불가.
        /// </summary>
        public static Result<string> Validate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Result<string>.Fail(ErrorCode.NicknameEmpty);
            }

            string nickname = raw.Trim();
            if (nickname.Length < MinLength)
            {
                return Result<string>.Fail(ErrorCode.NicknameTooShort);
            }
            if (nickname.Length > MaxLength)
            {
                return Result<string>.Fail(ErrorCode.NicknameTooLong);
            }
            foreach (char c in nickname)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return Result<string>.Fail(ErrorCode.NicknameInvalidChar);
                }
            }
            if (IsTooObvious(nickname))
            {
                return Result<string>.Fail(ErrorCode.NicknameTooObvious);
            }
            return Result<string>.Ok(nickname);
        }

        private static bool IsTooObvious(string nickname)
        {
            foreach (string name in ObviousNames)
            {
                if (nickname.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
