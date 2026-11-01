using System;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAutomatic.UI
{
    /// <summary>시작 화면. 버튼 문구와 안내 메시지만 바꿀 수 있고, 흐름 판단은 하지 않는다.</summary>
    public sealed class TitleScreen
    {
        private readonly Text _startLabel;
        private readonly Text _notice;

        public RectTransform Root { get; }

        public TitleScreen(Transform canvas, Action onStartPressed)
        {
            if (onStartPressed == null)
            {
                throw new ArgumentNullException(nameof(onStartPressed));
            }
            Root = UiFactory.CreateScreenRoot("TitleScreen", canvas);

            UiFactory.CreateLabel(Root, "반자동 공장", 96, UiFactory.Accent);
            UiFactory.CreateLabel(Root, "기계는 알아서 돌지만, 고장은 직접 고쳐야 해요", 36, UiFactory.TextColor);

            Button start = UiFactory.CreateButton(Root, "시작하기", UiFactory.Accent);
            start.onClick.AddListener(() => onStartPressed());
            _startLabel = start.GetComponentInChildren<Text>();

            _notice = UiFactory.CreateLabel(Root, string.Empty, 32, UiFactory.ErrorColor);
        }

        public void SetStartText(string text)
        {
            _startLabel.text = text;
        }

        public void SetNotice(string message)
        {
            _notice.text = message ?? string.Empty;
        }
    }
}
