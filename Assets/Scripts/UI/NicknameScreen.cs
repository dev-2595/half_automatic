using System;
using UnityEngine;
using UnityEngine.UI;
using HalfAutomatic.Core;

namespace HalfAutomatic.UI
{
    /// <summary>환생 후 이름 정하기(기획서 §4-2).</summary>
    public sealed class NicknameScreen : ScreenBase
    {
        public const string AcceptedLine = "뭐, 내 이름이라기에는 구리지만 어쩔 수 없지.";

        private readonly InputField _input;
        private readonly Text _speech;

        public NicknameScreen(Transform canvas, Action<string> onSubmit) : base(canvas, "NicknameScreen")
        {
            if (onSubmit == null)
            {
                throw new ArgumentNullException(nameof(onSubmit));
            }
            _speech = UiFactory.CreateSpeech(Root, string.Empty);
            UiFactory.CreateLabel(Root,
                $"이름 ({NicknameRules.MinLength}~{NicknameRules.MaxLength}자, 글자·숫자·밑줄)", 32, UiFactory.TextColor);
            _input = Track(UiFactory.CreateInputField(Root, "이름", NicknameRules.MaxLength));
            CreateMessageArea();
            Track(UiFactory.CreateButton(Root, "확인", UiFactory.Accent)).onClick.AddListener(() => onSubmit(_input.text));
        }

        public override void OnShow()
        {
            base.OnShow();
            _input.text = string.Empty;
            _speech.text = "내가 환생했을 때 이름은 뭐야? 최대한 안 들키도록 다르게 지어 줘. 빨리!!!";
        }

        /// <summary>이름이 정해진 직후 잠깐 보여 줄 대사.</summary>
        public void ShowAccepted()
        {
            _speech.text = AcceptedLine;
            ShowMessage(string.Empty);
            SetBusy(true);
        }
    }
}
