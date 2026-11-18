using System;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class AccountRulesTests
    {
        [TestCase("abcd", "abcd")]
        [TestCase("Dimension_99", "dimension_99")]
        [TestCase("ABCDEFGHIJKLMNOP", "abcdefghijklmnop")]
        public void Normalizes_valid_usernames(string raw, string key)
        {
            Result<string> r = AccountRules.NormalizeUsername(raw);
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(key, r.Value);
        }

        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("abcdefghijklmnopq")]
        [TestCase("ab cd")]
        [TestCase("한글아이디")]
        [TestCase("../evil")]
        [TestCase("abcd!")]
        public void Rejects_invalid_usernames(string raw)
        {
            Assert.AreEqual(ErrorCode.InvalidUsername, AccountRules.NormalizeUsername(raw).Error);
        }

        [TestCase("password1", true)]
        [TestCase("Abcdefg9!", true)]
        [TestCase("short1", false)]
        [TestCase("onlyletters", false)]
        [TestCase("12345678", false)]
        [TestCase("has space1", false)]
        [TestCase(null, false)]
        public void Password_rules(string password, bool ok)
        {
            Assert.AreEqual(ok, AccountRules.ValidatePassword(password).IsOk);
        }

        [Test]
        public void Password_max_length()
        {
            Assert.IsTrue(AccountRules.ValidatePassword("a1" + new string('x', 62)).IsOk);
            Assert.IsFalse(AccountRules.ValidatePassword("a1" + new string('x', 63)).IsOk);
        }
    }

    public class PasswordHasherTests
    {
        [Test]
        public void Verifies_correct_password_only()
        {
            PasswordHasher h = Fixtures.FastHasher();
            PasswordHash stored = h.Hash("password1");
            Assert.IsTrue(h.Verify("password1", stored));
            Assert.IsFalse(h.Verify("password2", stored));
            Assert.IsFalse(h.Verify(null, stored));
            Assert.IsFalse(h.Verify("password1", null));
        }

        [Test]
        public void Same_password_gets_different_salt()
        {
            PasswordHasher h = Fixtures.FastHasher();
            PasswordHash a = h.Hash("password1");
            PasswordHash b = h.Hash("password1");
            CollectionAssert.AreNotEqual(a.Salt, b.Salt);
            CollectionAssert.AreNotEqual(a.Hash, b.Hash);
        }

        [Test]
        public void Stored_iterations_are_used_for_verification()
        {
            PasswordHash old = new PasswordHasher(1_000).Hash("password1");
            Assert.IsTrue(new PasswordHasher(2_000).Verify("password1", old), "raising the default must not break old accounts");
        }

        [Test]
        public void Default_iterations_follow_owasp()
        {
            Assert.AreEqual(600_000, PasswordHasher.DefaultIterations);
        }

        [Test]
        public void Rejects_invalid_parameters()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordHasher(10));
            Assert.Throws<ArgumentException>(() => new PasswordHash(new byte[4], new byte[32], 1_000));
            Assert.Throws<ArgumentException>(() => new PasswordHash(new byte[16], new byte[5], 1_000));
        }

        [Test]
        public void Exposed_arrays_are_copies()
        {
            PasswordHash stored = Fixtures.FastHasher().Hash("password1");
            stored.Hash[0] ^= 0xFF;
            Assert.IsTrue(Fixtures.FastHasher().Verify("password1", stored));
        }
    }

    public class AccountServiceTests
    {
        private InMemoryAccountStore _store;
        private FakeClock _clock;
        private AccountService _service;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemoryAccountStore();
            _clock = new FakeClock();
            _service = new AccountService(_store, Fixtures.FastHasher(), _clock);
        }

        [Test]
        public void Register_then_sign_in()
        {
            Result<AccountRecord> reg = _service.Register("Dimen_01", "password1");
            Assert.IsTrue(reg.IsOk);
            Assert.AreEqual("dimen_01", reg.Value.Key);
            Assert.AreEqual("Dimen_01", reg.Value.Username);
            Assert.AreEqual(1_000, reg.Value.CreatedAtUnix);

            Result<AccountRecord> signIn = _service.SignIn("dimen_01", "password1");
            Assert.IsTrue(signIn.IsOk, "username is case-insensitive");
        }

        [Test]
        public void Register_rejects_duplicates_and_bad_input()
        {
            _service.Register("player1", "password1");
            Assert.AreEqual(ErrorCode.UsernameTaken, _service.Register("PLAYER1", "password2").Error);
            Assert.AreEqual(ErrorCode.InvalidUsername, _service.Register("ab", "password1").Error);
            Assert.AreEqual(ErrorCode.InvalidPassword, _service.Register("player2", "short").Error);
            Assert.AreEqual(1, _store.SaveCount);
        }

        [Test]
        public void Register_propagates_store_errors()
        {
            _store.FindError = ErrorCode.DataCorrupt;
            Assert.AreEqual(ErrorCode.DataCorrupt, _service.Register("player1", "password1").Error);
        }

        [Test]
        public void Wrong_password_and_unknown_user_look_the_same()
        {
            _service.Register("player1", "password1");
            Assert.AreEqual(ErrorCode.WrongCredentials, _service.SignIn("player1", "password2").Error);
            Assert.AreEqual(ErrorCode.WrongCredentials, _service.SignIn("nobody1", "password1").Error);
            Assert.AreEqual(ErrorCode.WrongCredentials, _service.SignIn("!!", "password1").Error);
        }

        [Test]
        public void Locks_after_five_failures_and_unlocks_after_30_seconds()
        {
            _service.Register("player1", "password1");
            for (int i = 0; i < AccountService.MaxFailedAttempts; i++)
            {
                Assert.AreEqual(ErrorCode.WrongCredentials, _service.SignIn("player1", "nope1234").Error);
            }
            Assert.AreEqual(ErrorCode.TooManyAttempts, _service.SignIn("player1", "password1").Error,
                "even the right password is refused while locked");
            Assert.AreEqual(30, _service.LockRemainingSeconds("PLAYER1"));

            _clock.UnixSeconds += 29;
            Assert.AreEqual(ErrorCode.TooManyAttempts, _service.SignIn("player1", "password1").Error);
            _clock.UnixSeconds += 1;
            Assert.AreEqual(0, _service.LockRemainingSeconds("player1"));
            Assert.IsTrue(_service.SignIn("player1", "password1").IsOk);
        }

        [Test]
        public void Success_resets_failure_count()
        {
            _service.Register("player1", "password1");
            for (int i = 0; i < AccountService.MaxFailedAttempts - 1; i++) _service.SignIn("player1", "nope1234");
            Assert.IsTrue(_service.SignIn("player1", "password1").IsOk);
            for (int i = 0; i < AccountService.MaxFailedAttempts - 1; i++) _service.SignIn("player1", "nope1234");
            Assert.AreEqual(0, _service.LockRemainingSeconds("player1"));
        }

        [Test]
        public void Store_failure_is_not_counted_as_wrong_password()
        {
            _service.Register("player1", "password1");
            _store.FindError = ErrorCode.StorageFailure;
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(ErrorCode.StorageFailure, _service.SignIn("player1", "password1").Error);
            }
            _store.FindError = ErrorCode.None;
            Assert.IsTrue(_service.SignIn("player1", "password1").IsOk);
        }

        [Test]
        public void Change_password()
        {
            AccountRecord account = _service.Register("player1", "password1").Value;
            Assert.AreEqual(ErrorCode.PasswordUnchanged, _service.ChangePassword(account, "password1").Error);
            Assert.AreEqual(ErrorCode.InvalidPassword, _service.ChangePassword(account, "weak").Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _service.ChangePassword(null, "password2").Error);

            Assert.IsTrue(_service.ChangePassword(account, "password2").IsOk);
            Assert.AreEqual(ErrorCode.WrongCredentials, _service.SignIn("player1", "password1").Error);
            Assert.IsTrue(_service.SignIn("player1", "password2").IsOk);
        }

        [Test]
        public void Change_password_save_failure_keeps_old_password()
        {
            AccountRecord account = _service.Register("player1", "password1").Value;
            _store.SaveError = ErrorCode.StorageFailure;
            Assert.AreEqual(ErrorCode.StorageFailure, _service.ChangePassword(account, "password2").Error);
            _store.SaveError = ErrorCode.None;
            Assert.IsTrue(_service.SignIn("player1", "password1").IsOk);
        }
    }
}
