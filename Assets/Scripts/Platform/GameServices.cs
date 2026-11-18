using System.IO;
using HalfAutomatic.Core;
using UnityEngine;

namespace HalfAutomatic.Platform
{
    /// <summary>
    /// 의존성 조립(Composition Root). 저장 위치는 Application.persistentDataPath 아래.
    /// 시작 시 계정 파일 무결성을 확인하고, 손상되었으면 격리 후 안내 문구를 남긴다.
    /// </summary>
    public sealed class GameServices
    {
        public GameRules Rules { get; }
        public LoginFlow Flow { get; }
        /// <summary>시작 시 계정 파일 점검 결과(UI 안내용). 정상이면 ErrorCode.None.</summary>
        public ErrorCode StartupIssue { get; }

        private GameServices(GameRules rules, LoginFlow flow, ErrorCode startupIssue)
        {
            Rules = rules;
            Flow = flow;
            StartupIssue = startupIssue;
        }

        public static GameServices Create()
        {
            string root = Application.persistentDataPath;
            var clock = new SystemClock();
            GameRules rules = GameRules.CreateDefault();

            var accountStore = new FileAccountStore(
                new ChecksummedFileStore(Path.Combine(root, "accounts.dat")),
                new JsonUtilitySerializer<AccountsData>());

            ErrorCode issue = ErrorCode.None;
            Result integrity = accountStore.CheckIntegrity();
            if (!integrity.IsOk)
            {
                issue = integrity.Error;
                if (integrity.Error == ErrorCode.DataCorrupt)
                {
                    Result moved = accountStore.Quarantine();
                    if (!moved.IsOk)
                    {
                        issue = moved.Error;
                    }
                }
                Debug.LogWarning($"[GameServices] account store issue: {issue}");
            }

            var accounts = new AccountService(accountStore, new PasswordHasher(), clock);
            var saves = new SaveRepository(Path.Combine(root, "saves"), new JsonUtilitySerializer<SaveData>(), rules);
            return new GameServices(rules, new LoginFlow(accounts, saves, rules, clock), issue);
        }
    }
}
