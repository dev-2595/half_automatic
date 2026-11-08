using System.Collections.Generic;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 기본 튜토리얼(기획서 docs/design/01_opening_and_tutorial.md §6, T1~T29).
    /// 대사 중 "(초안)" 표시가 붙은 단계는 원문에 없던 문장을 임시로 채운 것이다.
    /// </summary>
    public static class TutorialScript
    {
        private const string Dimension = "디멘션";

        public static IReadOnlyList<TutorialStep> CreateDefault()
        {
            string branchItem = UiTargets.InventoryItem(WeaponCatalog.BranchId);
            string swordItem = UiTargets.InventoryItem(WeaponCatalog.OldSwordId);

            return new List<TutorialStep>
            {
                Step("T1", "저기 장비 및 스탯 창 클릭하라고. 빨리!",
                    new[] { UiTargets.TabEquipStats }, TutorialEvents.Click(UiTargets.TabEquipStats)),
                Step("T2", "아 이딴 게 무기라니 내키지는 않지만, 어쨌든 이 나뭇가지를 장착해.",
                    new[] { branchItem, UiTargets.EquipButton }, TutorialEvents.Equipped(WeaponCatalog.BranchId)),
                Step("T3", "이제 저 버르장머리 없는 몬스터를 없애야 해.",
                    new[] { UiTargets.TabBattle }, TutorialEvents.Click(UiTargets.TabBattle)),
                Step("T4", "이제부터는 너와 내가 텔레파시를 주고받으며 저걸 공격해야 하는데, 일단 너의 의견을 전적으로 따르도록 하지. 나는 컨트롤이 구려서.",
                    null, TutorialEvents.DialogueNext),
                // T5~T8 대사는 (초안)
                Step("T5", "이동은 {move}! 움직여 봐.", null, TutorialEvents.InputMove),
                Step("T6", "공격은 {attack}! 한 대 때려 봐.", null, TutorialEvents.InputAttack),
                Step("T7", "빨리 달리려면 {run}!", null, TutorialEvents.InputRun),
                Step("T8", "대쉬는 {dash}! 휙!", null, TutorialEvents.InputDash),
                Step("T9", string.Empty, null, TutorialEvents.MonsterKilled),
                Step("T10", "오오오, 이것이 바로 프로필 창이로군. 역시 개발자, 대충 만들었군.",
                    null, TutorialEvents.CutsceneDone),
                Step("T11", "음, 이게 바로 우리를 표현하는 창인가 보군. 아주아주 심플하고 만들기도 간단해서 개발자의 사악함이 돋보이는군. 이 몸의 잘생긴 얼굴이 시작부터 열려 있다니, 이거 이거 밸런스가 개발자 따라서 끔찍하군.",
                    new[] { UiTargets.ProfileFrame }, TutorialEvents.Click(UiTargets.ProfileFrame)),
                Step("T12", "이거 눌러.",
                    new[] { UiTargets.ProfileWindowFrameTab }, TutorialEvents.Click(UiTargets.ProfileWindowFrameTab)),
                Step("T13", "음.. 이거 나를 얼른 꾸며 주라고, 왜 이렇게 텅 비었어.",
                    new[] { UiTargets.WindowClose }, TutorialEvents.Click(UiTargets.WindowClose)),
                new TutorialStep("T14", Dimension, string.Empty,
                    new[] { UiTargets.ProfileTitle, UiTargets.ProfileTitleChange },
                    new[] { TutorialEvents.Click(UiTargets.ProfileTitle), TutorialEvents.Click(UiTargets.ProfileTitleChange) }),
                Step("T15", "나를 빛나게 해 줄 명예로운 칭호들이 없다고? 매우 불만족스럽군. 나중에 해 보지 뭐.",
                    new[] { UiTargets.WindowClose }, TutorialEvents.Click(UiTargets.WindowClose)),
                Step("T16", string.Empty, new[] { UiTargets.Loot }, TutorialEvents.LootCollected),
                Step("T17", string.Empty,
                    new[] { UiTargets.TabEquipStats }, TutorialEvents.Click(UiTargets.TabEquipStats)),
                Step("T18", "나뭇가지 빼고 그 낡은 검 장착해.",
                    new[] { UiTargets.UnequipButton, swordItem, UiTargets.EquipButton },
                    TutorialEvents.Equipped(WeaponCatalog.OldSwordId)),
                Step("T19", "검을 장착하면 쓸 수 있는 스킬, 지팡이를 장착하면 쓸 수 있는 스킬, 성배랑 리라를 장착하면 쓸 수 있는 스킬이 따로 있어. 검·도끼 같은 건 전사, 지팡이 같은 건 마법, 성배·리라 같은 건 힐이야.",
                    null, TutorialEvents.DialogueNext),
                Step("T20", "설정 창 눌러.",
                    new[] { UiTargets.TabSettings }, TutorialEvents.Click(UiTargets.TabSettings)),
                Step("T21", "마법 쓰고 싶으면 마법사, 근거리 하고 싶으면 전사, 힐하고 싶으면 힐러 체크해. 나는 상관없음, 네 마음대로 하던가.",
                    new[] { UiTargets.JobWarrior, UiTargets.JobMage, UiTargets.JobHealer }, TutorialEvents.JobSelected),
                new TutorialStep("T22", Dimension, string.Empty, null, new[] { TutorialEvents.DialogueNext },
                    new Dictionary<JobClass, string>
                    {
                        { JobClass.Warrior, "이제 근거리 사용 가능한 무기 나옴." },
                        { JobClass.Mage, "이제 몬스터나 무기 획득할 때 마법사용 가능한 무기 나옴." },
                        { JobClass.Healer, "이제 힐 가능한 무기 나옴." },
                    }),
                Step("T23", "다시 장비 및 스탯 창 눌러.",
                    new[] { UiTargets.TabEquipStats }, TutorialEvents.Click(UiTargets.TabEquipStats)),
                // T24~T27 대사는 (초안)
                Step("T24", "이거 누르면 같은 무기끼리 자동으로 합성돼. 최대 5성까지.",
                    new[] { UiTargets.SynthesizeButton }, TutorialEvents.DialogueNext),
                Step("T25", "코인이랑 강화의 서로 무기 레벨을 올릴 수 있어. 자동 레벨업으로 숫자 정해서 한 번에 올려도 되고.",
                    new[] { UiTargets.WeaponLevelPanel }, TutorialEvents.DialogueNext),
                Step("T26", "스탯 버튼 누르면 체력, 공격력, 쿨타임을 올릴 수 있어.",
                    new[] { UiTargets.StatsButton }, TutorialEvents.Click(UiTargets.StatsButton)),
                Step("T27", "나 눌러 보든가.",
                    new[] { UiTargets.DimensionFullShot }, TutorialEvents.Click(UiTargets.DimensionFullShot)),
                Step("T28", "이제 네가 싸우기 귀찮으면, 이 몸의 실력을 볼 차례다! 푹 쉬라구!",
                    null, TutorialEvents.DialogueNext),
                // T29 대사는 (초안)
                Step("T29", "자, 이제 진짜 모험이다!", null, TutorialEvents.DialogueNext),
            };
        }

        private static TutorialStep Step(string id, string line, string[] highlights, string completeOn)
        {
            return new TutorialStep(id, Dimension, line, highlights, new[] { completeOn });
        }
    }
}
