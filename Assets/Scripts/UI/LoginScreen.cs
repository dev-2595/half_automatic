using System;
using UnityEngine;
using UnityEngine.UI;
using HalfAutomatic.Core;

namespace HalfAutomatic.UI
{
    /// <summary>게스트 닉네임 입력 화면. 검증과 생성은 AuthService에 위임한다.</summary>
    public sealed class LoginScreen
    {
        private readonly AuthService _auth;
        private readonly InputField _input;
        private readonly Text _error;
        private readonly Action<PlayerProfile> _onSuccess;

        public RectTransform Root { get; }

        public LoginScreen(Transform canvas, AuthService auth, Action<PlayerProfile> onSuccess, Action onBack)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _onSuccess = onSuccess ?? throw new ArgumentNullException(nameof(onSuccess));

            Root = UiFactory.CreateScreenRoot("LoginScreen", canvas);

            UiFactory.CreateLabel(Root, "게스트로 시작", 72, UiFactory.Accent);
            UiFactory.CreateLabel(Root,
                $"사용할 닉네임을 정해 주세요 ({NicknameRules.MinLength}~{NicknameRules.MaxLength}자)",
                34, UiFactory.TextColor);

            _input = UiFactory.CreateInputField(Root, "닉네임", NicknameRules.MaxLength);
            _error = UiFactory.CreateLabel(Root, string.Empty, 32, UiFactory.ErrorColor);

            Button confirm = UiFactory.CreateButton(Root, "확인", UiFactory.Accent);
            confirm.onClick.AddListener(OnConfirm);

            Button back = UiFactory.CreateButton(Root, "뒤로", UiFactory.Panel);
            back.GetComponentInChildren<Text>().color = UiFactory.TextColor;
            back.onClick.AddListener(() => onBack());
        }

        /// <summary>화면이 다시 보일 때 이전 입력과 오류를 지운다.</summary>
        public void Reset()
        {
            _input.text = string.Empty;
            _error.text = string.Empty;
        }

        private void OnConfirm()
        {
            Result<PlayerProfile> result = _auth.CreateGuest(_input.text);
            if (!result.IsOk)
            {
                _error.text = ErrorMessages.For(result.Error);
                return;
            }
            _error.text = string.Empty;
            _onSuccess(result.Value);
        }
    }
}
