namespace HalfAutomatic.Core
{
    /// <summary>튜토리얼이 가리키는 UI 대상 id. UI 계층은 같은 id로 버튼을 등록한다.</summary>
    public static class UiTargets
    {
        public const string TabEquipStats = "tab.equip_stats";
        public const string TabBattle = "tab.battle";
        public const string TabSettings = "tab.settings";

        public const string EquipButton = "equip.equip";
        public const string UnequipButton = "equip.unequip";
        public const string SynthesizeButton = "equip.synthesize";
        public const string WeaponLevelPanel = "equip.weapon_level";
        public const string StatsButton = "equip.stats";
        public const string DimensionFullShot = "equip.dimension";

        public const string ProfileFrame = "profile.frame";
        public const string ProfileTitle = "profile.title";
        public const string ProfileTitleChange = "profile.title_change";
        public const string ProfileWindowFrameTab = "profile_window.frame_tab";
        public const string WindowClose = "window.close";

        public const string JobWarrior = "settings.job.warrior";
        public const string JobMage = "settings.job.mage";
        public const string JobHealer = "settings.job.healer";

        public const string Loot = "battle.loot";
        public const string AutoBattle = "battle.auto";

        /// <summary>아이템 창의 특정 무기 칸.</summary>
        public static string InventoryItem(string weaponDefinitionId) => "equip.item:" + weaponDefinitionId;
    }

    /// <summary>튜토리얼 진행에 쓰는 게임 이벤트 id.</summary>
    public static class TutorialEvents
    {
        public const string DialogueNext = "dialogue:next";
        public const string CutsceneDone = "cutscene:done";
        public const string InputMove = "input:move";
        public const string InputAttack = "input:attack";
        public const string InputRun = "input:run";
        public const string InputDash = "input:dash";
        public const string MonsterKilled = "monster:killed";
        public const string LootCollected = "loot:collected";
        public const string JobSelected = "job:selected";

        public static string Click(string uiTarget) => "click:" + uiTarget;
        public static string Equipped(string weaponDefinitionId) => "equip:" + weaponDefinitionId;
    }
}
