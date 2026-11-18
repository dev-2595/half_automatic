using System.IO;
using System.Linq;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class ChecksummedFileStoreTests
    {
        private TempDir _dir;
        private ChecksummedFileStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDir();
            _store = new ChecksummedFileStore(_dir.File("data.dat"));
        }

        [TearDown]
        public void TearDown() => _dir.Dispose();

        [Test]
        public void Missing_file_is_not_found()
        {
            Assert.AreEqual(ErrorCode.NotFound, _store.Read().Error);
        }

        [Test]
        public void Round_trip_including_unicode_and_newlines()
        {
            const string payload = "첫 줄\n둘째 줄 😀\n";
            Assert.IsTrue(_store.Write(payload).IsOk);
            Result<string> r = _store.Read();
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(payload, r.Value);
            Assert.IsFalse(_store.LastReadUsedBackup);
            Assert.IsFalse(File.Exists(_dir.File("data.dat.tmp")), "temp file must not remain");
        }

        [Test]
        public void Tampered_file_without_backup_is_corrupt()
        {
            _store.Write("coins=10");
            File.WriteAllText(_store.Path, File.ReadAllText(_store.Path).Replace("coins=10", "coins=99999"));
            Assert.AreEqual(ErrorCode.DataCorrupt, _store.Read().Error);
        }

        [Test]
        public void Garbage_file_is_corrupt()
        {
            File.WriteAllText(_store.Path, "not a save file");
            Assert.AreEqual(ErrorCode.DataCorrupt, _store.Read().Error);
        }

        [Test]
        public void Second_write_keeps_previous_as_backup_and_recovers_from_it()
        {
            _store.Write("v1");
            _store.Write("v2");
            Assert.AreEqual("v2", _store.Read().Value);

            File.WriteAllText(_store.Path, "broken");
            Result<string> r = _store.Read();
            Assert.IsTrue(r.IsOk);
            Assert.AreEqual("v1", r.Value);
            Assert.IsTrue(_store.LastReadUsedBackup);
        }

        [Test]
        public void Missing_main_but_valid_backup_recovers()
        {
            _store.Write("v1");
            _store.Write("v2");
            File.Delete(_store.Path);
            Assert.AreEqual("v1", _store.Read().Value);
        }

        [Test]
        public void Both_broken_is_corrupt_and_quarantine_moves_them()
        {
            _store.Write("v1");
            _store.Write("v2");
            File.WriteAllText(_store.Path, "x");
            File.WriteAllText(_store.BackupPath, "y");
            Assert.AreEqual(ErrorCode.DataCorrupt, _store.Read().Error);

            Assert.IsTrue(_store.Quarantine().IsOk);
            Assert.IsFalse(File.Exists(_store.Path));
            Assert.IsFalse(File.Exists(_store.BackupPath));
            Assert.IsTrue(File.Exists(_store.Path + ".corrupt"));
            Assert.AreEqual(ErrorCode.NotFound, _store.Read().Error);
            Assert.IsTrue(_store.Write("fresh").IsOk);
        }

        [Test]
        public void Write_into_unwritable_location_fails_cleanly()
        {
            File.WriteAllText(_dir.File("blocker"), "i am a file");
            var store = new ChecksummedFileStore(Path.Combine(_dir.File("blocker"), "data.dat"));
            Assert.AreEqual(ErrorCode.StorageFailure, store.Write("x").Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _store.Write(null).Error);
        }
    }

    public class SaveMapperTests
    {
        private static GameState RichState(GameRules rules)
        {
            GameState s = GameState.CreateNew(rules);
            s.SetNickname("환생자");
            s.SetJob(JobClass.Mage);
            s.Level.AddExp(500);
            s.Wallet.AddCoins(1_000_000);
            s.Wallet.AddScrolls(WeaponCategory.Magic, 50);
            s.Weapons.Add(WeaponCatalog.BranchId);
            WeaponInstance staff = s.Weapons.Add("old_staff").Value;
            s.Weapons.Add("old_staff");
            s.Weapons.Add("old_staff");
            new WeaponSynthesizer(rules.Config).AutoSynthesize(s.Weapons);
            s.Weapons.Equip(s.Weapons.Items.First(w => w.DefinitionId == "old_staff" && w.Stars == 2).Id);
            new WeaponLevelService(rules.Config, rules.Catalog).Apply(s.Weapons, s.Weapons.Equipped.Id, 3, s.Level.Level, s.Wallet);
            new StatLevelService(rules.Config).Apply(s.Stats, StatType.Attack, 2, s.Level.Level, s.Wallet);
            s.Tutorial.Restore(5);
            return s;
        }

        [Test]
        public void Round_trip_preserves_everything()
        {
            GameRules rules = GameRules.CreateDefault();
            GameState original = RichState(rules);
            SaveData data = SaveMapper.ToData(original, 12345);
            Assert.AreEqual(12345, data.savedAtUnix);

            Result<GameState> loaded = SaveMapper.FromData(data, rules);
            Assert.IsTrue(loaded.IsOk);
            GameState s = loaded.Value;
            Assert.AreEqual(original.Nickname, s.Nickname);
            Assert.AreEqual(JobClass.Mage, s.Job);
            Assert.AreEqual(original.Level.Level, s.Level.Level);
            Assert.AreEqual(original.Level.Exp, s.Level.Exp);
            Assert.AreEqual(original.Wallet.Coins, s.Wallet.Coins);
            CollectionAssert.AreEqual(original.Wallet.ScrollsSnapshot(), s.Wallet.ScrollsSnapshot());
            Assert.AreEqual(original.Weapons.Items.Count, s.Weapons.Items.Count);
            Assert.AreEqual(original.Weapons.Equipped.Id, s.Weapons.Equipped.Id);
            Assert.AreEqual(original.Weapons.Equipped.Level, s.Weapons.Equipped.Level);
            Assert.AreEqual(original.Weapons.NextId, s.Weapons.NextId);
            Assert.AreEqual(2, s.Stats.GetLevel(StatType.Attack));
            Assert.AreEqual(5, s.Tutorial.CurrentIndex);
            long newId = s.Weapons.Add(WeaponCatalog.OldSwordId).Value.Id;
            Assert.Greater(newId, original.Weapons.Items.Max(w => w.Id), "new ids continue after restored ones");
        }

        [Test]
        public void New_game_round_trip()
        {
            GameRules rules = GameRules.CreateDefault();
            Result<GameState> r = SaveMapper.FromData(SaveMapper.ToData(GameState.CreateNew(rules), 0), rules);
            Assert.IsTrue(r.IsOk);
            Assert.IsNull(r.Value.Nickname);
            Assert.IsNull(r.Value.Weapons.Equipped);
            Assert.AreEqual(1, r.Value.Level.Level);
        }

        [Test]
        public void Empty_nickname_is_treated_as_none()
        {
            GameRules rules = GameRules.CreateDefault();
            SaveData d = SaveMapper.ToData(GameState.CreateNew(rules), 0);
            d.nickname = "";
            Assert.IsNull(SaveMapper.FromData(d, rules).Value.Nickname);
        }

        private delegate void Breaker(SaveData d);

        [Test]
        public void Rejects_invalid_values()
        {
            GameRules rules = GameRules.CreateDefault();
            var breakers = new Breaker[]
            {
                d => d.version = 0,
                d => d.nickname = "디멘션",
                d => d.nickname = " 공백 ",
                d => d.job = 9,
                d => d.level = 0,
                d => d.exp = -1,
                d => d.coins = -5,
                d => d.scrolls = new long[2],
                d => d.scrolls = null,
                d => d.weapons = null,
                d => d.weapons = new WeaponData[] { null },
                d => d.weapons[0].definitionId = "nope",
                d => d.weapons[0].stars = 6,
                d => d.weapons[0].level = 0,
                d => d.weapons[0].id = d.nextWeaponId,
                d => d.weapons[1].id = d.weapons[0].id,
                d => d.equippedWeaponId = 999,
                d => d.nextWeaponId = 0,
                d => d.stats = new[] { 0, 0 },
                d => d.stats = new[] { 0, 999, 0 },
                d => d.tutorialIndex = 999,
            };
            for (int i = 0; i < breakers.Length; i++)
            {
                SaveData d = SaveMapper.ToData(RichState(rules), 0);
                breakers[i](d);
                Assert.AreEqual(ErrorCode.DataCorrupt, SaveMapper.FromData(d, rules).Error, $"breaker #{i}");
            }
            Assert.AreEqual(ErrorCode.DataCorrupt, SaveMapper.FromData(null, rules).Error);
        }

        [Test]
        public void Future_version_is_unsupported_not_corrupt()
        {
            GameRules rules = GameRules.CreateDefault();
            SaveData d = SaveMapper.ToData(GameState.CreateNew(rules), 0);
            d.version = SaveData.CurrentVersion + 1;
            Assert.AreEqual(ErrorCode.UnsupportedVersion, SaveMapper.FromData(d, rules).Error);
        }
    }

    public class SaveRepositoryTests
    {
        private TempDir _dir;
        private GameRules _rules;
        private SaveRepository _repo;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDir();
            _rules = GameRules.CreateDefault();
            _repo = new SaveRepository(_dir.Path, new XmlTestSerializer<SaveData>(), _rules);
        }

        [TearDown]
        public void TearDown() => _dir.Dispose();

        [Test]
        public void Save_and_load_through_real_file()
        {
            GameState s = GameState.CreateNew(_rules);
            s.SetNickname("환생자");
            s.Wallet.AddCoins(77);
            Assert.IsTrue(_repo.Save("player1", s, 10).IsOk);
            Assert.IsTrue(File.Exists(Path.Combine(_dir.Path, "save_player1.dat")));

            Result<GameState> loaded = _repo.Load("player1");
            Assert.IsTrue(loaded.IsOk);
            Assert.AreEqual("환생자", loaded.Value.Nickname);
            Assert.AreEqual(77, loaded.Value.Wallet.Coins);
        }

        [Test]
        public void Missing_save_is_not_found()
        {
            Assert.AreEqual(ErrorCode.NotFound, _repo.Load("player1").Error);
        }

        [TestCase("../evil")]
        [TestCase("Player1")] // 정규화되지 않은 키
        [TestCase(null)]
        public void Rejects_unsafe_keys(string key)
        {
            Assert.AreEqual(ErrorCode.InvalidArgument, _repo.Load(key).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _repo.Save(key, GameState.CreateNew(_rules), 0).Error);
        }

        [Test]
        public void Valid_checksum_but_bad_content_is_corrupt()
        {
            var file = new ChecksummedFileStore(Path.Combine(_dir.Path, "save_player1.dat"));
            file.Write("<not xml");
            Assert.AreEqual(ErrorCode.DataCorrupt, _repo.Load("player1").Error);
        }

        [Test]
        public void Recovers_previous_save_from_backup()
        {
            GameState s = GameState.CreateNew(_rules);
            s.Wallet.AddCoins(1);
            _repo.Save("player1", s, 0);
            s.Wallet.AddCoins(1);
            _repo.Save("player1", s, 0);
            File.WriteAllText(Path.Combine(_dir.Path, "save_player1.dat"), "garbage");

            Result<GameState> loaded = _repo.Load("player1");
            Assert.IsTrue(loaded.IsOk);
            Assert.AreEqual(1, loaded.Value.Wallet.Coins);
            Assert.IsTrue(_repo.LastLoadUsedBackup);
        }
    }

    public class FileAccountStoreTests
    {
        private TempDir _dir;
        private FileAccountStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDir();
            _store = new FileAccountStore(new ChecksummedFileStore(_dir.File("accounts.dat")),
                new XmlTestSerializer<AccountsData>());
        }

        [TearDown]
        public void TearDown() => _dir.Dispose();

        private static AccountRecord Record(string name) =>
            new AccountRecord(name, Fixtures.FastHasher().Hash("password1"), 5);

        [Test]
        public void Save_find_and_update()
        {
            Assert.IsTrue(_store.CheckIntegrity().IsOk, "no file yet is fine");
            Assert.AreEqual(ErrorCode.NotFound, _store.Find("player1").Error);
            Assert.IsTrue(_store.Save(Record("Player1")).IsOk);
            Assert.IsTrue(_store.Save(Record("player2")).IsOk);

            Result<AccountRecord> found = _store.Find("player1");
            Assert.IsTrue(found.IsOk);
            Assert.AreEqual("Player1", found.Value.Username);
            Assert.IsTrue(Fixtures.FastHasher().Verify("password1", found.Value.Password));
            Assert.IsTrue(_store.Find("player2").IsOk);
        }

        [Test]
        public void Works_end_to_end_with_account_service()
        {
            var service = new AccountService(_store, Fixtures.FastHasher(), new FakeClock());
            Assert.IsTrue(service.Register("player1", "password1").IsOk);
            var reopened = new FileAccountStore(new ChecksummedFileStore(_dir.File("accounts.dat")),
                new XmlTestSerializer<AccountsData>());
            var service2 = new AccountService(reopened, Fixtures.FastHasher(), new FakeClock());
            Assert.IsTrue(service2.SignIn("player1", "password1").IsOk);
            Assert.AreEqual(ErrorCode.UsernameTaken, service2.Register("PLAYER1", "password1").Error);
        }

        [Test]
        public void Corrupt_file_is_never_overwritten()
        {
            _store.Save(Record("player1"));
            File.WriteAllText(_dir.File("accounts.dat"), "x");
            File.WriteAllText(_dir.File("accounts.dat.bak"), "y");

            Assert.AreEqual(ErrorCode.DataCorrupt, _store.CheckIntegrity().Error);
            Assert.AreEqual(ErrorCode.DataCorrupt, _store.Find("player1").Error);
            Assert.AreEqual(ErrorCode.DataCorrupt, _store.Save(Record("player2")).Error);
            Assert.AreEqual("x", File.ReadAllText(_dir.File("accounts.dat")));

            Assert.IsTrue(_store.Quarantine().IsOk);
            Assert.IsTrue(_store.CheckIntegrity().IsOk);
            Assert.IsTrue(_store.Save(Record("player2")).IsOk);
        }

        [Test]
        public void Duplicate_or_invalid_entries_are_corrupt()
        {
            var file = new ChecksummedFileStore(_dir.File("accounts.dat"));
            var ser = new XmlTestSerializer<AccountsData>();
            var good = new AccountData
            {
                username = "player1", salt = System.Convert.ToBase64String(new byte[16]),
                hash = System.Convert.ToBase64String(new byte[32]), iterations = 1000, createdAtUnix = 0,
            };
            file.Write(ser.Serialize(new AccountsData { version = 1, accounts = new[] { good, good } }));
            Assert.AreEqual(ErrorCode.DataCorrupt, _store.CheckIntegrity().Error);

            var badSalt = new AccountData { username = "player1", salt = "%%%", hash = good.hash, iterations = 1000 };
            file.Write(ser.Serialize(new AccountsData { version = 1, accounts = new[] { badSalt } }));
            Assert.AreEqual(ErrorCode.DataCorrupt, _store.CheckIntegrity().Error);

            file.Write(ser.Serialize(new AccountsData { version = 99, accounts = new AccountData[0] }));
            Assert.AreEqual(ErrorCode.UnsupportedVersion, _store.CheckIntegrity().Error);
        }
    }
}
