using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using HalfAutomatic.Core;
using HalfAutomatic.Platform;

namespace HalfAutomatic.UI
{
    /// <summary>
    /// 앱 진입점. 화면을 만들고, LoginFlow의 단계에 맞춰 화면을 전환하고, 자동 저장을 담당한다.
    /// 씬 파일 없이 어떤 씬에서 재생해도 동작하도록 런타임에 스스로 생성된다.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        private const float AutosaveIntervalSeconds = 10f;
        private const float NicknameAcceptedDelaySeconds = 2f;

        private GameServices _services;
        private TitleScreen _title;
        private AccountScreen _account;
        private PasswordPromptScreen _prompt;
        private PasswordChangeScreen _change;
        private NicknameScreen _nickname;
        private HomeScreen _home;
        private ScreenBase _current;

        private bool _busy;
        private float _autosaveLeft = AutosaveIntervalSeconds;

        private LoginFlow Flow => _services.Flow;

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
            _services = GameServices.Create();

            Canvas canvas = UiFactory.CreateCanvas(transform);
            Transform c = canvas.transform;
            _title = new TitleScreen(c, () => Show(_account));
            _account = new AccountScreen(c, OnSignIn, OnRegister, () => Show(_title));
            _prompt = new PasswordPromptScreen(c, OnBeginPasswordChange, OnKeepPassword);
            _change = new PasswordChangeScreen(c, OnSubmitPassword, OnKeepPassword);
            _nickname = new NicknameScreen(c, OnSubmitNickname);
            _home = new HomeScreen(c, OnSignOut);

            foreach (ScreenBase screen in new ScreenBase[] { _title, _account, _prompt, _change, _nickname, _home })
            {
                screen.Root.gameObject.SetActive(false);
            }

            Show(_title);
            if (_services.StartupIssue != ErrorCode.None)
            {
                _title.ShowMessage(ErrorMessages.For(_services.StartupIssue));
            }
        }

        private void Update()
        {
            if (_services == null || _busy || Flow.Step != LoginStep.InGame)
            {
                return;
            }
            _autosaveLeft -= Time.unscaledDeltaTime;
            if (_autosaveLeft <= 0f)
            {
                _autosaveLeft = AutosaveIntervalSeconds;
                SaveNow();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        // ------------------------------------------------------------------ 입력 처리

        private void OnSignIn(string username, string password)
        {
            RunInBackground(_account, () => Flow.SignIn(username, password), AfterAuthenticated);
        }

        private void OnRegister(string username, string password)
        {
            RunInBackground(_account, () => Flow.Register(username, password), AfterAuthenticated);
        }

        private void OnBeginPasswordChange()
        {
            ShowResultOrAdvance(_prompt, Flow.BeginPasswordChange());
        }

        private void OnKeepPassword()
        {
            ShowResultOrAdvance(_current, Flow.DeclinePasswordChange());
        }

        private void OnSubmitPassword(string password, string confirm)
        {
            RunInBackground(_change, () => Flow.ChangePassword(password, confirm), ShowCurrentStep);
        }

        private void OnSubmitNickname(string nickname)
        {
            if (_busy)
            {
                return;
            }
            Result result = Flow.SetNickname(nickname);
            if (!result.IsOk)
            {
                _nickname.ShowMessage(ErrorMessages.For(result.Error));
                return;
            }
            StartCoroutine(ShowNicknameAcceptedThenContinue());
        }

        private void OnSignOut()
        {
            Result result = Flow.SignOut();
            if (!result.IsOk)
            {
                _home.ShowMessage(ErrorMessages.For(result.Error));
                return;
            }
            Show(_account);
        }

        // ------------------------------------------------------------------ 흐름

        private void AfterAuthenticated()
        {
            _account.ClearPassword();
            ShowCurrentStep();
            if (Flow.SaveWasReset)
            {
                _current.ShowMessage(ErrorMessages.For(ErrorCode.DataCorrupt));
            }
            else if (Flow.SaveRestoredFromBackup)
            {
                _current.ShowMessage("저장 데이터가 손상되어 직전 백업으로 복구했어요.");
            }
        }

        private void ShowCurrentStep()
        {
            switch (Flow.Step)
            {
                case LoginStep.Login:
                    Show(_account);
                    break;
                case LoginStep.PasswordChangePrompt:
                    Show(_prompt);
                    break;
                case LoginStep.PasswordChange:
                    Show(_change);
                    break;
                case LoginStep.Nickname:
                    Show(_nickname);
                    break;
                case LoginStep.InGame:
                    _home.Refresh(Flow.State);
                    _autosaveLeft = AutosaveIntervalSeconds;
                    Show(_home);
                    break;
                default:
                    throw new InvalidOperationException("unknown login step: " + Flow.Step);
            }
        }

        private void ShowResultOrAdvance(ScreenBase screen, Result result)
        {
            if (!result.IsOk)
            {
                screen.ShowMessage(ErrorMessages.For(result.Error));
                return;
            }
            ShowCurrentStep();
        }

        private IEnumerator ShowNicknameAcceptedThenContinue()
        {
            _busy = true;
            _nickname.ShowAccepted();
            yield return new WaitForSecondsRealtime(NicknameAcceptedDelaySeconds);
            _busy = false;
            ShowCurrentStep();
        }

        private void SaveNow()
        {
            if (_services == null || _busy || Flow.Step != LoginStep.InGame)
            {
                return;
            }
            Result result = Flow.Save();
            if (!result.IsOk)
            {
                Debug.LogWarning($"[AppRoot] save failed: {result.Error}");
                _home.ShowMessage(ErrorMessages.For(result.Error));
            }
        }

        /// <summary>
        /// 비밀번호 해시처럼 오래 걸리는 작업을 백그라운드에서 실행한다. 그동안 화면 입력을 잠근다.
        /// await 이후는 Unity 메인 스레드에서 이어진다.
        /// </summary>
        private async void RunInBackground(ScreenBase screen, Func<Result> work, Action onSuccess)
        {
            if (_busy)
            {
                return;
            }
            _busy = true;
            screen.SetBusy(true);
            screen.ShowMessage("확인 중...");

            Result result;
            bool crashed = false;
            try
            {
                result = await Task.Run(work);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = Result.Fail(ErrorCode.InvalidOperation);
                crashed = true;
            }
            finally
            {
                _busy = false;
            }

            if (this == null)
            {
                return; // 처리 중 앱이 종료됨
            }
            screen.SetBusy(false);
            if (!result.IsOk)
            {
                screen.ShowMessage(crashed ? "알 수 없는 오류가 발생했어요." : ErrorMessages.For(result.Error));
                return;
            }
            screen.ShowMessage(string.Empty);
            onSuccess();
        }

        private void Show(ScreenBase target)
        {
            if (_current != null)
            {
                _current.Root.gameObject.SetActive(false);
            }
            _current = target;
            _current.Root.gameObject.SetActive(true);
            _current.OnShow();
        }
    }
}
