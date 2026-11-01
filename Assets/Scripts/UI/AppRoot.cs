using UnityEngine;
using HalfAutomatic.Core;
using HalfAutomatic.Platform;

namespace HalfAutomatic.UI
{
    /// <summary>
    /// 앱 진입점. 의존성을 조립하고 화면 전환만 담당한다.
    /// 씬 파일 없이 어떤 씬에서 재생해도 동작하도록 런타임에 스스로 생성된다.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        private AuthService _auth;
        private TitleScreen _title;
        private LoginScreen _login;
        private HomeScreen _home;
        private RectTransform _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<AppRoot>() != null)
            {
                return;
            }
            var go = new GameObject("AppRoot");
            DontDestroyOnLoad(go);
            go.AddComponent<AppRoot>();
        }

        private void Start()
        {
            _auth = new AuthService(FileProfileStore.CreateDefault(), new SystemClock(),
                () => System.Guid.NewGuid().ToString("N"));

            Canvas canvas = UiFactory.CreateCanvas(transform);
            _title = new TitleScreen(canvas.transform, OnStartPressed);
            _login = new LoginScreen(canvas.transform, _auth, OnLoggedIn, () => Show(_title.Root));
            _home = new HomeScreen(canvas.transform);

            // Show()는 직전 화면만 끄므로, 시작 시에는 나머지 화면을 직접 꺼 둔다.
            _login.Root.gameObject.SetActive(false);
            _home.Root.gameObject.SetActive(false);

            PrepareTitle();
            Show(_title.Root);
        }

        private void PrepareTitle()
        {
            Result<PlayerProfile> restored = _auth.TryRestore();
            if (restored.IsOk)
            {
                _title.SetStartText($"이어하기 ({restored.Value.Nickname})");
                _title.SetNotice(string.Empty);
                return;
            }

            _title.SetStartText("시작하기");
            // 새 게임(NoProfile)은 오류가 아니므로 문구가 비어 있다.
            _title.SetNotice(ErrorMessages.For(restored.Error));
        }

        private void OnStartPressed()
        {
            if (_auth.IsSignedIn)
            {
                OnLoggedIn(_auth.Current);
                return;
            }
            _login.Reset();
            Show(_login.Root);
        }

        private void OnLoggedIn(PlayerProfile profile)
        {
            _home.Show(profile);
            Show(_home.Root);
        }

        private void Show(RectTransform target)
        {
            if (_current != null)
            {
                _current.gameObject.SetActive(false);
            }
            _current = target;
            _current.gameObject.SetActive(true);
        }
    }
}
