namespace HalfAutomatic.Core
{
    /// <summary>플레이어 직업. 무기/강화의 서 드랍 종류를 정한다. None = 튜토리얼에서 아직 고르지 않음.</summary>
    public enum JobClass
    {
        None = 0,
        Warrior,
        Mage,
        Healer,
    }

    /// <summary>무기 계열. 장착한 무기 계열이 쓸 수 있는 스킬을 정하고, 강화의 서 종류와 1:1로 대응한다.</summary>
    public enum WeaponCategory
    {
        Melee = 0, // 검, 도끼 등 → 근거리 강화의 서
        Magic = 1, // 지팡이 등   → 마법 강화의 서
        Heal = 2,  // 성배, 리라 등 → 치유 강화의 서
    }

    public static class JobRules
    {
        public const int CategoryCount = 3;

        /// <summary>직업에 대응하는 무기 계열. None이면 null.</summary>
        public static WeaponCategory? CategoryOf(JobClass job)
        {
            switch (job)
            {
                case JobClass.Warrior: return WeaponCategory.Melee;
                case JobClass.Mage: return WeaponCategory.Magic;
                case JobClass.Healer: return WeaponCategory.Heal;
                default: return null;
            }
        }

        public static bool IsDefined(WeaponCategory category)
        {
            return category == WeaponCategory.Melee || category == WeaponCategory.Magic
                || category == WeaponCategory.Heal;
        }

        public static bool IsDefined(JobClass job)
        {
            return job == JobClass.None || job == JobClass.Warrior || job == JobClass.Mage
                || job == JobClass.Healer;
        }
    }
}
