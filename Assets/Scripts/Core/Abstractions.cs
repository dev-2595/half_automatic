namespace HalfAutomatic.Core
{
    /// <summary>프로필 영속화 추상화. 파일/서버 등 구현체를 바꿔 끼울 수 있다.</summary>
    public interface IProfileStore
    {
        /// <summary>없으면 NoProfile, 손상되었으면 DataCorrupt, I/O 문제면 StorageFailure.</summary>
        Result<PlayerProfile> Load();
        Result Save(PlayerProfile profile);
        Result Delete();
        /// <summary>손상된 데이터를 덮어쓰지 않도록 따로 치워 둔다.</summary>
        Result Quarantine();
    }

    public interface IClock
    {
        long UnixSeconds { get; }
    }
}
