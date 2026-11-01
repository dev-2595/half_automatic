using UnityEngine;
using UnityEngine.UI;
using HalfAutomatic.Core;

namespace HalfAutomatic.UI
{
    /// <summary>로그인 후 진입하는 임시 홈 화면. 이후 공장 화면으로 교체될 자리.</summary>
    public sealed class HomeScreen
    {
        private readonly Text _greeting;

        public RectTransform Root { get; }

        public HomeScreen(Transform canvas)
        {
            Root = UiFactory.CreateScreenRoot("HomeScreen", canvas);
            _greeting = UiFactory.CreateLabel(Root, string.Empty, 60, UiFactory.Accent);
            UiFactory.CreateLabel(Root, "공장 화면은 다음 단계에서 만들어요", 36, UiFactory.TextColor);
        }

        public void Show(PlayerProfile profile)
        {
            _greeting.text = $"{profile.Nickname}님, 환영합니다!";
        }
    }
}
