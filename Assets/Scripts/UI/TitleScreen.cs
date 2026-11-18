using System;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAutomatic.UI
{
    /// <summary>
    /// 임시 타이틀 화면. 기획서 §3의 일러스트(절대신 추격 등)는 그림이 준비되면 교체한다.
    /// </summary>
    public sealed class TitleScreen : ScreenBase
    {
        public TitleScreen(Transform canvas, Action onStartPressed) : base(canvas, "TitleScreen")
        {
            if (onStartPressed == null)
            {
                throw new ArgumentNullException(nameof(onStartPressed));
            }
            UiFactory.CreateLabel(Root, "God(?)", 120, UiFactory.Accent);
            UiFactory.CreateLabel(Root, "(타이틀 일러스트 자리: 절대신에게 쫓기는 디멘션)", 30, UiFactory.TextColor);

            Button start = Track(UiFactory.CreateButton(Root, "시작", UiFactory.Accent));
            start.onClick.AddListener(() => onStartPressed());

            CreateMessageArea();
        }
    }
}
