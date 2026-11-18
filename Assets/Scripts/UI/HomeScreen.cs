using System;
using UnityEngine;
using UnityEngine.UI;
using HalfAutomatic.Core;

namespace HalfAutomatic.UI
{
    /// <summary>로그인 후 임시 홈 화면. 다음 단계에서 2D 게임 화면과 하단 탭으로 교체된다.</summary>
    public sealed class HomeScreen : ScreenBase
    {
        private readonly Text _greeting;
        private readonly Text _summary;

        public HomeScreen(Transform canvas, Action onSignOut) : base(canvas, "HomeScreen")
        {
            if (onSignOut == null)
            {
                throw new ArgumentNullException(nameof(onSignOut));
            }
            _greeting = UiFactory.CreateLabel(Root, string.Empty, 56, UiFactory.Accent);
            _summary = UiFactory.CreateLabel(Root, string.Empty, 36, UiFactory.TextColor);
            UiFactory.CreateLabel(Root, "(게임 화면은 다음 단계에서 만들어요)", 30, UiFactory.TextColor);
            CreateMessageArea();
            Track(UiFactory.CreateSecondaryButton(Root, "로그아웃")).onClick.AddListener(() => onSignOut());
        }

        public void Refresh(GameState state)
        {
            if (state == null)
            {
                return;
            }
            _greeting.text = $"으으으.... 성공이다! 나는 이제 {state.Nickname}!";
            _summary.text = $"LV. {state.Level.Level}   코인 {state.Wallet.Coins}";
        }
    }
}
