using System;
using UnityEngine;
using UnityEngine.UI;
using HalfAutomatic.Core;

namespace HalfAutomatic.UI
{
    /// <summary>로그인 직후 "비밀번호 바꿀 거야?" 질문.</summary>
    public sealed class PasswordPromptScreen : ScreenBase
    {
        public PasswordPromptScreen(Transform canvas, Action onChange, Action onKeep) : base(canvas, "PasswordPrompt")
        {
            if (onChange == null || onKeep == null)
            {
                throw new ArgumentNullException(onChange == null ? nameof(onChange) : nameof(onKeep));
            }
            UiFactory.CreateSpeech(Root, "비밀번호 바꿀 거야? 빨리 정해!");
            CreateMessageArea();
            Track(UiFactory.CreateButton(Root, "바꾸기", UiFactory.Accent)).onClick.AddListener(() => onChange());
            Track(UiFactory.CreateSecondaryButton(Root, "안 바꾸기")).onClick.AddListener(() => onKeep());
        }
    }

    /// <summary>새 비밀번호 입력.</summary>
    public sealed class PasswordChangeScreen : ScreenBase
    {
        private readonly InputField _password;
        private readonly InputField _confirm;

        public PasswordChangeScreen(Transform canvas, Action<string, string> onSubmit, Action onCancel)
            : base(canvas, "PasswordChange")
        {
            if (onSubmit == null || onCancel == null)
            {
                throw new ArgumentNullException(onSubmit == null ? nameof(onSubmit) : nameof(onCancel));
            }
            UiFactory.CreateSpeech(Root, "새 비밀번호! 빨리!!");
            _password = Track(UiFactory.CreateInputField(Root, "새 비밀번호", AccountRules.PasswordMaxLength, password: true));
            _confirm = Track(UiFactory.CreateInputField(Root, "새 비밀번호 확인", AccountRules.PasswordMaxLength, password: true));
            CreateMessageArea();
            Track(UiFactory.CreateButton(Root, "변경", UiFactory.Accent))
                .onClick.AddListener(() => onSubmit(_password.text, _confirm.text));
            Track(UiFactory.CreateSecondaryButton(Root, "그냥 안 바꿀래")).onClick.AddListener(() => onCancel());
        }

        public override void OnShow()
        {
            base.OnShow();
            _password.text = string.Empty;
            _confirm.text = string.Empty;
        }
    }
}
