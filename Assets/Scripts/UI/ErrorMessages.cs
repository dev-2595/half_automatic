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
                case ErrorCode.None:
                case ErrorCode.NotFound:
                    return string.Empty;

                // 닉네임
                case ErrorCode.NicknameEmpty:
                    return "이름을 입력하라고! 빨리!";
                case ErrorCode.NicknameTooShort:
                    return $"너무 짧아! {NicknameRules.MinLength}자 이상!";
                case ErrorCode.NicknameTooLong:
                    return $"너무 길어! {NicknameRules.MaxLength}자 이하!";
                case ErrorCode.NicknameInvalidChar:
                    return "글자, 숫자, 밑줄(_)만 써!";
                case ErrorCode.NicknameTooObvious:
                    return "그럼 바로 들키잖아!!";

                // 계정
                case ErrorCode.InvalidUsername:
                    return $"아이디는 영문·숫자·밑줄(_) {AccountRules.UsernameMinLength}~{AccountRules.UsernameMaxLength}자야.";
                case ErrorCode.InvalidPassword:
                    return $"비밀번호는 {AccountRules.PasswordMinLength}~{AccountRules.PasswordMaxLength}자, 영문과 숫자를 섞고 공백은 빼.";
                case ErrorCode.UsernameTaken:
                    return "이미 있는 아이디야. 다른 걸로!";
                case ErrorCode.WrongCredentials:
                    return "틀렸잖아!! 진짜 죽는다고!!";
                case ErrorCode.TooManyAttempts:
                    return "너무 많이 틀렸어. 잠깐 숨 좀 돌리고 다시 해.";
                case ErrorCode.PasswordUnchanged:
                    return "그거 원래 비밀번호잖아.";
                case ErrorCode.PasswordMismatch:
                    return "두 비밀번호가 달라!";

                // 저장
                case ErrorCode.DataCorrupt:
                    return "저장 데이터가 손상되어 새로 시작해요. (기존 파일은 .corrupt로 보관했어요)";
                case ErrorCode.StorageFailure:
                    return "저장 공간에 접근하지 못했어요. 잠시 후 다시 시도해 주세요.";
                case ErrorCode.UnsupportedVersion:
                    return "더 새로운 버전에서 저장된 데이터예요. 게임을 업데이트해 주세요.";

                // 성장 / 무기
                case ErrorCode.InsufficientCoins:
                    return "코인이 부족해!";
                case ErrorCode.InsufficientScrolls:
                    return "강화의 서가 부족해!";
                case ErrorCode.LevelCapReached:
                    return "지금은 더 못 올려. 레벨부터 올려!";
                case ErrorCode.Overflow:
                    return "숫자가 너무 커서 처리할 수 없어요.";
                case ErrorCode.UnknownWeapon:
                case ErrorCode.WeaponNotFound:
                    return "그런 무기는 없는데?";
                case ErrorCode.NothingEquipped:
                    return "장착한 무기가 없어.";
                case ErrorCode.AlreadyEquipped:
                    return "이미 장착했잖아.";

                case ErrorCode.InvalidTutorialState:
                    return "튜토리얼 진행 정보가 이상해요.";
                case ErrorCode.InvalidOperation:
                case ErrorCode.InvalidArgument:
                    return "잘못된 요청이에요.";
                default:
                    return "알 수 없는 오류가 발생했어요.";
            }
        }
    }
}
