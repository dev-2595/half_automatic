namespace HalfAutomatic.Core
{
    /// <summary>
    /// 아이디·비밀번호 규칙.
    /// ⚠️ World Of God 계정과 공유하려면 두 게임의 규칙이 같아야 한다. WoG 규칙 확인 전 임시값.
    /// </summary>
    public static class AccountRules
    {
        public const int UsernameMinLength = 4;
        public const int UsernameMaxLength = 16;
        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 64;

        /// <summary>
        /// 아이디: 영문·숫자·밑줄, 4~16자. 대소문자는 구분하지 않는다(같은 계정으로 취급).
        /// 성공 시 저장 키로 쓰는 소문자 아이디를 돌려준다(파일 이름에 그대로 써도 안전한 문자만 남는다).
        /// </summary>
        public static Result<string> NormalizeUsername(string raw)
        {
            if (raw == null || raw.Length < UsernameMinLength || raw.Length > UsernameMaxLength)
            {
                return Result<string>.Fail(ErrorCode.InvalidUsername);
            }
            foreach (char c in raw)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                {
                    return Result<string>.Fail(ErrorCode.InvalidUsername);
                }
            }
            return Result<string>.Ok(raw.ToLowerInvariant());
        }

        /// <summary>비밀번호: 8~64자, 영문과 숫자를 각각 하나 이상 포함. 공백·제어문자 불가.</summary>
        public static Result ValidatePassword(string password)
        {
            if (password == null || password.Length < PasswordMinLength || password.Length > PasswordMaxLength)
            {
                return Result.Fail(ErrorCode.InvalidPassword);
            }
            bool hasLetter = false;
            bool hasDigit = false;
            foreach (char c in password)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    return Result.Fail(ErrorCode.InvalidPassword);
                }
                hasLetter |= (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                hasDigit |= c >= '0' && c <= '9';
            }
            return hasLetter && hasDigit ? Result.Ok() : Result.Fail(ErrorCode.InvalidPassword);
        }
    }
}
