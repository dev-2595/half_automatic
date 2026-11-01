using System;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class AuthServiceTests
    {
        private sealed class FakeClock : IClock
        {
            public long UnixSeconds { get; set; } = 1000;
        }

        private sealed class FakeStore : IProfileStore
        {
            public PlayerProfile Stored;
            public ErrorCode LoadError = ErrorCode.None;
            public ErrorCode SaveError = ErrorCode.None;
            public ErrorCode DeleteError = ErrorCode.None;
            public int QuarantineCalls;

            public Result<PlayerProfile> Load()
            {
                if (LoadError != ErrorCode.None) return Result<PlayerProfile>.Fail(LoadError);
                return Stored == null
                    ? Result<PlayerProfile>.Fail(ErrorCode.NoProfile)
                    : Result<PlayerProfile>.Ok(Stored);
            }

            public Result Save(PlayerProfile profile)
            {
                if (SaveError != ErrorCode.None) return Result.Fail(SaveError);
                Stored = profile;
                return Result.Ok();
            }

            public Result Delete()
            {
                if (DeleteError != ErrorCode.None) return Result.Fail(DeleteError);
                Stored = null;
                return Result.Ok();
            }

            public Result Quarantine()
            {
                QuarantineCalls++;
                return Result.Ok();
            }
        }

        private FakeStore _store;
        private AuthService _auth;

        [SetUp]
        public void SetUp()
        {
            _store = new FakeStore();
            _auth = new AuthService(_store, new FakeClock(), () => "id-1");
        }

        [Test]
        public void Constructor_rejects_nulls()
        {
            Assert.Throws<ArgumentNullException>(() => new AuthService(null, new FakeClock(), () => "x"));
            Assert.Throws<ArgumentNullException>(() => new AuthService(_store, null, () => "x"));
            Assert.Throws<ArgumentNullException>(() => new AuthService(_store, new FakeClock(), null));
        }

        [Test]
        public void TryRestore_without_profile_reports_NoProfile()
        {
            Result<PlayerProfile> r = _auth.TryRestore();
            Assert.IsFalse(r.IsOk);
            Assert.AreEqual(ErrorCode.NoProfile, r.Error);
            Assert.IsFalse(_auth.IsSignedIn);
            Assert.AreEqual(0, _store.QuarantineCalls);
        }

        [Test]
        public void TryRestore_quarantines_corrupt_data()
        {
            _store.LoadError = ErrorCode.DataCorrupt;
            Result<PlayerProfile> r = _auth.TryRestore();
            Assert.AreEqual(ErrorCode.DataCorrupt, r.Error);
            Assert.AreEqual(1, _store.QuarantineCalls);
            Assert.IsFalse(_auth.IsSignedIn);
        }

        [Test]
        public void CreateGuest_saves_and_signs_in()
        {
            Result<PlayerProfile> r = _auth.CreateGuest("  홍길동 ");
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual("홍길동", r.Value.Nickname);
            Assert.AreEqual("id-1", r.Value.Id);
            Assert.AreEqual(1000, r.Value.CreatedAtUnix);
            Assert.AreSame(r.Value, _store.Stored);
            Assert.AreSame(r.Value, _auth.Current);
        }

        [Test]
        public void CreateGuest_with_invalid_nickname_changes_nothing()
        {
            Result<PlayerProfile> r = _auth.CreateGuest("a");
            Assert.AreEqual(ErrorCode.NicknameTooShort, r.Error);
            Assert.IsNull(_store.Stored);
            Assert.IsFalse(_auth.IsSignedIn);
        }

        [Test]
        public void CreateGuest_with_empty_id_is_rejected()
        {
            var auth = new AuthService(_store, new FakeClock(), () => " ");
            Result<PlayerProfile> r = auth.CreateGuest("홍길동");
            Assert.AreEqual(ErrorCode.InvalidProfileData, r.Error);
            Assert.IsNull(_store.Stored);
        }

        [Test]
        public void CreateGuest_save_failure_does_not_sign_in()
        {
            _store.SaveError = ErrorCode.StorageFailure;
            Result<PlayerProfile> r = _auth.CreateGuest("홍길동");
            Assert.AreEqual(ErrorCode.StorageFailure, r.Error);
            Assert.IsFalse(_auth.IsSignedIn);
        }

        [Test]
        public void CreateGuest_twice_is_rejected()
        {
            Assert.IsTrue(_auth.CreateGuest("홍길동").IsOk);
            Assert.AreEqual(ErrorCode.AlreadySignedIn, _auth.CreateGuest("김철수").Error);
            Assert.AreEqual("홍길동", _auth.Current.Nickname);
        }

        [Test]
        public void SignOut_with_delete_clears_store_and_state()
        {
            _auth.CreateGuest("홍길동");
            Assert.IsTrue(_auth.SignOut(true).IsOk);
            Assert.IsFalse(_auth.IsSignedIn);
            Assert.IsNull(_store.Stored);
        }

        [Test]
        public void SignOut_delete_failure_keeps_signed_in()
        {
            _auth.CreateGuest("홍길동");
            _store.DeleteError = ErrorCode.StorageFailure;
            Assert.AreEqual(ErrorCode.StorageFailure, _auth.SignOut(true).Error);
            Assert.IsTrue(_auth.IsSignedIn);
            Assert.IsNotNull(_store.Stored);
        }

        [Test]
        public void SignOut_without_delete_keeps_stored_profile()
        {
            _auth.CreateGuest("홍길동");
            Assert.IsTrue(_auth.SignOut(false).IsOk);
            Assert.IsFalse(_auth.IsSignedIn);
            Assert.IsNotNull(_store.Stored);
        }
    }
}
