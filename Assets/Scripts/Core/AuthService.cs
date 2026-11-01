using System;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 로그인 상태 관리. 현재는 게스트(로컬 프로필)만 지원하지만,
    /// 저장소를 IProfileStore로 추상화해 두어 서버 로그인으로 확장할 수 있다.
    /// 실패 시 상태를 바꾸지 않는다(저장 성공 후에만 Current 갱신).
    /// </summary>
    public sealed class AuthService
    {
        private readonly IProfileStore _store;
        private readonly IClock _clock;
        private readonly Func<string> _idFactory;

        public PlayerProfile Current { get; private set; }
        public bool IsSignedIn => Current != null;

        public AuthService(IProfileStore store, IClock clock, Func<string> idFactory)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _idFactory = idFactory ?? throw new ArgumentNullException(nameof(idFactory));
        }

        /// <summary>저장된 프로필로 자동 로그인을 시도한다. 손상 시 격리 후 DataCorrupt를 돌려준다.</summary>
        public Result<PlayerProfile> TryRestore()
        {
            Result<PlayerProfile> loaded = _store.Load();
            if (loaded.IsOk)
            {
                Current = loaded.Value;
                return loaded;
            }
            if (loaded.Error == ErrorCode.DataCorrupt)
            {
                _store.Quarantine();
            }
            return loaded;
        }

        public Result<PlayerProfile> CreateGuest(string rawNickname)
        {
            if (IsSignedIn)
            {
                return Result<PlayerProfile>.Fail(ErrorCode.AlreadySignedIn);
            }

            Result<string> nickname = NicknameRules.Validate(rawNickname);
            if (!nickname.IsOk)
            {
                return Result<PlayerProfile>.Fail(nickname.Error);
            }

            Result<PlayerProfile> created =
                PlayerProfile.Create(_idFactory(), nickname.Value, _clock.UnixSeconds);
            if (!created.IsOk)
            {
                return created;
            }

            Result saved = _store.Save(created.Value);
            if (!saved.IsOk)
            {
                return Result<PlayerProfile>.Fail(saved.Error);
            }

            Current = created.Value;
            return created;
        }

        /// <summary>로그아웃. deleteData가 true면 저장된 프로필도 지우며, 지우기에 실패하면 로그인 상태를 유지한다.</summary>
        public Result SignOut(bool deleteData)
        {
            if (deleteData)
            {
                Result deleted = _store.Delete();
                if (!deleted.IsOk)
                {
                    return deleted;
                }
            }
            Current = null;
            return Result.Ok();
        }
    }
}
