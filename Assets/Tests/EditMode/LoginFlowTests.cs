using System.IO;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class LoginFlowTests
    {
        private TempDir _dir;
        private GameRules _rules;
        private FakeClock _clock;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDir();
            _rules = GameRules.CreateDefault();
            _clock = new FakeClock();
        }

        [TearDown]
        public void TearDown() => _dir.Dispose();

        private LoginFlow NewFlow(string saveDir = null)
        {
            var accounts = new FileAccountStore(new ChecksummedFileStore(_dir.File("accounts.dat")),
                new XmlTestSerializer<AccountsData>());
            var service = new AccountService(accounts, Fixtures.FastHasher(), _clock);
            var saves = new SaveRepository(saveDir ?? _dir.Path, new XmlTestSerializer<SaveData>(), _rules);
            return new LoginFlow(service, saves, _rules, _clock);
        }

        private LoginFlow RegisteredWithNickname()
        {
            LoginFlow flow = NewFlow();
            Assert.IsTrue(flow.Register("player1", "password1").IsOk);
            Assert.IsTrue(flow.SetNickname("환생자").IsOk);
            return flow;
        }

        [Test]
        public void Register_goes_straight_to_nickname_then_game()
        {
            LoginFlow flow = NewFlow();
            Assert.IsTrue(flow.Register("player1", "password1").IsOk);
            Assert.AreEqual(LoginStep.Nickname, flow.Step);
            Assert.AreEqual(ErrorCode.NicknameTooObvious, flow.SetNickname("디멘션").Error);
            Assert.AreEqual(LoginStep.Nickname, flow.Step);
            Assert.IsTrue(flow.SetNickname("환생자").IsOk);
            Assert.AreEqual(LoginStep.InGame, flow.Step);
            Assert.AreEqual("환생자", flow.State.Nickname);
        }

        [Test]
        public void Returning_player_is_asked_about_password_then_enters_game()
        {
            RegisteredWithNickname().SignOut();
            LoginFlow flow = NewFlow();
            Assert.IsTrue(flow.SignIn("player1", "password1").IsOk);
            Assert.AreEqual(LoginStep.PasswordChangePrompt, flow.Step);
            Assert.AreEqual("환생자", flow.State.Nickname, "progress was loaded");
            Assert.IsTrue(flow.DeclinePasswordChange().IsOk);
            Assert.AreEqual(LoginStep.InGame, flow.Step);
        }

        [Test]
        public void Player_without_nickname_goes_to_nickname_after_prompt()
        {
            NewFlow().Register("player1", "password1"); // 닉네임을 정하지 않고 종료
            LoginFlow flow = NewFlow();
            flow.SignIn("player1", "password1");
            flow.DeclinePasswordChange();
            Assert.AreEqual(LoginStep.Nickname, flow.Step);
        }

        [Test]
        public void Change_password_flow()
        {
            RegisteredWithNickname().SignOut();
            LoginFlow flow = NewFlow();
            flow.SignIn("player1", "password1");
            Assert.IsTrue(flow.BeginPasswordChange().IsOk);
            Assert.AreEqual(ErrorCode.PasswordMismatch, flow.ChangePassword("password2", "password3").Error);
            Assert.AreEqual(ErrorCode.PasswordUnchanged, flow.ChangePassword("password1", "password1").Error);
            Assert.AreEqual(LoginStep.PasswordChange, flow.Step);
            Assert.IsTrue(flow.ChangePassword("password2", "password2").IsOk);
            Assert.AreEqual(LoginStep.InGame, flow.Step);

            LoginFlow again = NewFlow();
            Assert.AreEqual(ErrorCode.WrongCredentials, again.SignIn("player1", "password1").Error);
            Assert.IsTrue(again.SignIn("player1", "password2").IsOk);
        }

        [Test]
        public void Can_cancel_password_change()
        {
            RegisteredWithNickname().SignOut();
            LoginFlow flow = NewFlow();
            flow.SignIn("player1", "password1");
            flow.BeginPasswordChange();
            Assert.IsTrue(flow.DeclinePasswordChange().IsOk);
            Assert.AreEqual(LoginStep.InGame, flow.Step);
        }

        [Test]
        public void Progress_is_saved_and_restored()
        {
            LoginFlow flow = RegisteredWithNickname();
            flow.State.Wallet.AddCoins(123);
            flow.State.SetJob(JobClass.Healer);
            Assert.IsTrue(flow.Save().IsOk);

            LoginFlow next = NewFlow();
            next.SignIn("player1", "password1");
            Assert.AreEqual(123, next.State.Wallet.Coins);
            Assert.AreEqual(JobClass.Healer, next.State.Job);
        }

        [Test]
        public void Corrupt_save_is_quarantined_and_reset()
        {
            RegisteredWithNickname().SignOut();
            string savePath = Path.Combine(_dir.Path, "save_player1.dat");
            File.WriteAllText(savePath, "broken");
            File.WriteAllText(savePath + ".bak", "broken too");

            LoginFlow flow = NewFlow();
            Assert.IsTrue(flow.SignIn("player1", "password1").IsOk);
            Assert.IsTrue(flow.SaveWasReset);
            Assert.IsNull(flow.State.Nickname);
            Assert.IsTrue(File.Exists(savePath + ".corrupt"), "broken save kept for inspection");
        }

        [Test]
        public void Future_version_save_blocks_sign_in_to_protect_data()
        {
            LoginFlow first = RegisteredWithNickname();
            first.SignOut();
            var ser = new XmlTestSerializer<SaveData>();
            SaveData d = SaveMapper.ToData(GameState.CreateNew(_rules), 0);
            d.version = SaveData.CurrentVersion + 1;
            new ChecksummedFileStore(Path.Combine(_dir.Path, "save_player1.dat")).Write(ser.Serialize(d));

            LoginFlow flow = NewFlow();
            Assert.AreEqual(ErrorCode.UnsupportedVersion, flow.SignIn("player1", "password1").Error);
            Assert.AreEqual(LoginStep.Login, flow.Step);
            Assert.IsNull(flow.State);
        }

        [Test]
        public void Nickname_save_failure_reverts_name_and_step()
        {
            string saveDir = _dir.File("saves");
            LoginFlow flow = NewFlow(saveDir);
            Assert.IsTrue(flow.Register("player1", "password1").IsOk);

            // 저장 폴더를 같은 이름의 파일로 바꿔 이후 저장이 실패하게 만든다.
            Directory.Delete(saveDir, true);
            File.WriteAllText(saveDir, "file, not a folder");

            Assert.AreEqual(ErrorCode.StorageFailure, flow.SetNickname("환생자").Error);
            Assert.IsNull(flow.State.Nickname, "name must be reverted");
            Assert.AreEqual(LoginStep.Nickname, flow.Step, "step must not advance");
        }

        [Test]
        public void Register_fails_when_first_save_fails()
        {
            File.WriteAllText(_dir.File("blocked"), "file, not a folder");
            LoginFlow flow = NewFlow(saveDir: _dir.File("blocked"));
            Assert.AreEqual(ErrorCode.StorageFailure, flow.Register("player1", "password1").Error);
            Assert.AreEqual(LoginStep.Login, flow.Step);
            Assert.IsNull(flow.Account);
        }

        [Test]
        public void Sign_out_is_refused_when_save_fails()
        {
            string saveDir = _dir.File("saves");
            LoginFlow flow = NewFlow(saveDir);
            flow.Register("player1", "password1");
            flow.SetNickname("환생자");
            Directory.Delete(saveDir, true);
            File.WriteAllText(saveDir, "file, not a folder");

            Assert.AreEqual(ErrorCode.StorageFailure, flow.SignOut().Error);
            Assert.AreEqual(LoginStep.InGame, flow.Step, "stay signed in so progress is not lost");
            Assert.IsNotNull(flow.State);
        }

        [Test]
        public void Wrong_step_calls_are_rejected()
        {
            LoginFlow flow = NewFlow();
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.SetNickname("환생자").Error);
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.BeginPasswordChange().Error);
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.DeclinePasswordChange().Error);
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.ChangePassword("a", "a").Error);
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.Save().Error);
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.SignOut().Error);

            flow.Register("player1", "password1");
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.SignIn("player1", "password1").Error);
            Assert.AreEqual(ErrorCode.InvalidOperation, flow.Register("player2", "password1").Error);
        }

        [Test]
        public void Wrong_password_keeps_login_step()
        {
            RegisteredWithNickname().SignOut();
            LoginFlow flow = NewFlow();
            Assert.AreEqual(ErrorCode.WrongCredentials, flow.SignIn("player1", "nope1234").Error);
            Assert.AreEqual(LoginStep.Login, flow.Step);
            Assert.IsNull(flow.Account);
        }
    }

    public class GameStateTests
    {
        [Test]
        public void Job_cannot_be_reset_to_none()
        {
            GameState s = GameState.CreateNew(GameRules.CreateDefault());
            Assert.AreEqual(JobClass.None, s.Job);
            Assert.IsTrue(s.SetJob(JobClass.Warrior).IsOk);
            Assert.AreEqual(ErrorCode.InvalidArgument, s.SetJob(JobClass.None).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, s.SetJob((JobClass)77).Error);
            Assert.AreEqual(JobClass.Warrior, s.Job);
        }

        [Test]
        public void Nickname_validation()
        {
            GameState s = GameState.CreateNew(GameRules.CreateDefault());
            Assert.IsFalse(s.HasNickname);
            Assert.AreEqual(ErrorCode.NicknameTooShort, s.SetNickname("a").Error);
            Assert.IsFalse(s.HasNickname);
            Assert.IsTrue(s.SetNickname(" 환생자 ").IsOk);
            Assert.AreEqual("환생자", s.Nickname);
        }
    }
}
