using System;
using UnityEngine;
using UnityEngine.UI;
using HalfAutomatic.Core;

namespace HalfAutomatic.UI
{
    /// <summary>아이디·비밀번호 입력(기획서 §4-1). 실제 처리는 콜백으로 넘긴다.</summary>
    public sealed class AccountScreen : ScreenBase
    {
        private readonly InputField _username;
        private readonly InputField _password;

        public AccountScreen(Transform canvas, Action<string, string> onSignIn, Action<string, string> onRegister,
            Action onBack) : base(canvas, "AccountScreen")
        {
            if (onSignIn == null || onRegister == null || onBack == null)
            {
                throw new ArgumentNullException(onSignIn == null ? nameof(onSignIn)
                    : onRegister == null ? nameof(onRegister) : nameof(onBack));
            }

            UiFactory.CreateSpeech(Root, "빨리해! 나 곧 있으면 죽을 것 같다고 빨리!!!");
            UiFactory.CreateLabel(Root, "World Of God 아이디로 로그인", 34, UiFactory.TextColor);

            _username = Track(UiFactory.CreateInputField(Root, "아이디", AccountRules.UsernameMaxLength));
            _password = Track(UiFactory.CreateInputField(Root, "비밀번호", AccountRules.PasswordMaxLength, password: true));
            CreateMessageArea();

            Track(UiFactory.CreateButton(Root, "로그인", UiFactory.Accent))
                .onClick.AddListener(() => onSignIn(_username.text, _password.text));
            Track(UiFactory.CreateSecondaryButton(Root, "처음이야 (가입하기)"))
                .onClick.AddListener(() => onRegister(_username.text, _password.text));
            Track(UiFactory.CreateSecondaryButton(Root, "뒤로"))
                .onClick.AddListener(() => onBack());
        }

        public override void OnShow()
        {
            base.OnShow();
            _password.text = string.Empty; // 비밀번호는 화면에 남기지 않는다.
        }

        public void ClearPassword()
        {
            _password.text = string.Empty;
        }
    }
}
