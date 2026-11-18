using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAutomatic.UI
{
    /// <summary>화면 공통 기능: 루트, 오류 문구, 처리 중 버튼 잠금.</summary>
    public abstract class ScreenBase
    {
        private readonly List<Selectable> _interactables = new List<Selectable>();
        private Text _message;

        public RectTransform Root { get; }

        protected ScreenBase(Transform canvas, string name)
        {
            Root = UiFactory.CreateScreenRoot(name, canvas);
        }

        /// <summary>처리 중 잠글 버튼·입력창을 등록한다.</summary>
        protected T Track<T>(T selectable) where T : Selectable
        {
            _interactables.Add(selectable);
            return selectable;
        }

        /// <summary>오류/안내 문구 자리를 만든다(화면마다 한 번).</summary>
        protected void CreateMessageArea()
        {
            _message = UiFactory.CreateLabel(Root, string.Empty, 32, UiFactory.ErrorColor);
        }

        public void ShowMessage(string message)
        {
            if (_message != null)
            {
                _message.text = message ?? string.Empty;
            }
        }

        public void SetBusy(bool busy)
        {
            foreach (Selectable s in _interactables)
            {
                if (s != null)
                {
                    s.interactable = !busy;
                }
            }
        }

        /// <summary>화면이 다시 보일 때 호출된다.</summary>
        public virtual void OnShow()
        {
            ShowMessage(string.Empty);
            SetBusy(false);
        }
    }
}
