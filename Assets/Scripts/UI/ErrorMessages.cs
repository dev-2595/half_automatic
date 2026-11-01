using HalfAutomatic.Core;

namespace HalfAutomatic.UI
{
    /// <summary>ErrorCode → 사용자에게 보일 한국어 문구. 표시 문구를 코어와 분리해 둔다.</summary>
    public static class ErrorMessages
    {
        public static string For(ErrorCode code)
        {
            switch (code)
            {
                case ErrorCode.NicknameEmpty:
                    return "닉네임을 입력해 주세요.";
                case ErrorCode.NicknameTooShort:
                    return $"닉네임은 {NicknameRules.MinLength}자 이상이어야 해요.";
                case ErrorCode.NicknameTooLong:
                    return $"닉네임은 {NicknameRules.MaxLength}자 이하여야 해요.";
                case ErrorCode.NicknameInvalidChar:
                    return "닉네임에는 글자, 숫자, 밑줄(_)만 쓸 수 있어요.";
                case ErrorCode.AlreadySignedIn:
                    return "이미 로그인되어 있어요.";
                case ErrorCode.DataCorrupt:
                    return "저장된 프로필이 손상되어 새로 시작해요. (기존 파일은 .corrupt로 보관했어요)";
                case ErrorCode.StorageFailure:
                    return "저장 공간에 접근하지 못했어요. 잠시 후 다시 시도해 주세요.";
                case ErrorCode.NoProfile:
                case ErrorCode.None:
                    return string.Empty;
                default:
                    return "알 수 없는 오류가 발생했어요.";
            }
        }
    }
}
