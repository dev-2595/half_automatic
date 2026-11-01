namespace HalfAutomatic.Core
{
    /// <summary>불변 플레이어 프로필. 생성은 항상 Create를 거쳐 검증된다.</summary>
    public sealed class PlayerProfile
    {
        public string Id { get; }
        public string Nickname { get; }
        public long CreatedAtUnix { get; }

        private PlayerProfile(string id, string nickname, long createdAtUnix)
        {
            Id = id;
            Nickname = nickname;
            CreatedAtUnix = createdAtUnix;
        }

        public static Result<PlayerProfile> Create(string id, string nickname, long createdAtUnix)
        {
            if (string.IsNullOrWhiteSpace(id) || createdAtUnix < 0)
            {
                return Result<PlayerProfile>.Fail(ErrorCode.InvalidProfileData);
            }
            Result<string> validated = NicknameRules.Validate(nickname);
            if (!validated.IsOk)
            {
                return Result<PlayerProfile>.Fail(validated.Error);
            }
            return Result<PlayerProfile>.Ok(new PlayerProfile(id, validated.Value, createdAtUnix));
        }
    }
}
