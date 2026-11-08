using System;
using System.Collections.Generic;
using System.Linq;
using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class TutorialRunnerTests
    {
        private static TutorialRunner TwoSteps() => new TutorialRunner(new List<TutorialStep>
        {
            new TutorialStep("a", "디멘션", "눌러.", new[] { "btn.a" }, new[] { "click:btn.a" }),
            new TutorialStep("b", "디멘션", "끝.", null, new[] { "dialogue:next" }),
        });

        [Test]
        public void Advances_only_on_matching_event()
        {
            TutorialRunner r = TwoSteps();
            Assert.AreEqual("a", r.Current.Id);
            Assert.IsFalse(r.Notify("click:btn.b"));
            Assert.IsFalse(r.Notify(null));
            Assert.AreEqual(0, r.CurrentIndex);
            Assert.IsTrue(r.Notify("click:btn.a"));
            Assert.AreEqual("b", r.Current.Id);
            Assert.IsTrue(r.Notify("dialogue:next"));
            Assert.IsTrue(r.IsComplete);
            Assert.IsNull(r.Current);
            Assert.IsFalse(r.Notify("dialogue:next"));
        }

        [Test]
        public void Blocks_input_outside_highlights()
        {
            TutorialRunner r = TwoSteps();
            Assert.IsTrue(r.IsInputAllowed("btn.a"));
            Assert.IsFalse(r.IsInputAllowed("btn.b"));
            Assert.IsFalse(r.IsInputAllowed(null));
            r.Notify("click:btn.a");
            Assert.IsTrue(r.IsInputAllowed("btn.b"), "no highlights → nothing blocked");
            r.Notify("dialogue:next");
            Assert.IsTrue(r.IsInputAllowed("anything"), "completed tutorial blocks nothing");
        }

        [Test]
        public void Restore_validates_range()
        {
            TutorialRunner r = TwoSteps();
            Assert.IsTrue(r.Restore(1).IsOk);
            Assert.AreEqual("b", r.Current.Id);
            Assert.IsTrue(r.Restore(2).IsOk);
            Assert.IsTrue(r.IsComplete);
            Assert.AreEqual(ErrorCode.InvalidTutorialState, r.Restore(3).Error);
            Assert.AreEqual(ErrorCode.InvalidTutorialState, r.Restore(-1).Error);
            Assert.IsTrue(r.IsComplete);
        }

        [Test]
        public void Rejects_invalid_scripts()
        {
            var s = new TutorialStep("x", "", "", null, new[] { "e" });
            Assert.Throws<ArgumentNullException>(() => new TutorialRunner(null));
            Assert.Throws<ArgumentException>(() => new TutorialRunner(new List<TutorialStep>()));
            Assert.Throws<ArgumentException>(() => new TutorialRunner(new List<TutorialStep> { s, s }));
            Assert.Throws<ArgumentException>(() => new TutorialStep("y", "", "", null, new string[0]));
            Assert.Throws<ArgumentException>(() => new TutorialStep("y", "", "", new[] { "" }, new[] { "e" }));
        }
    }

    public class TutorialScriptTests
    {
        [Test]
        public void Default_script_has_29_unique_steps()
        {
            IReadOnlyList<TutorialStep> steps = TutorialScript.CreateDefault();
            Assert.AreEqual(29, steps.Count);
            Assert.AreEqual("T1", steps[0].Id);
            Assert.AreEqual("T29", steps[28].Id);
            Assert.DoesNotThrow(() => new TutorialRunner(steps));
        }

        [Test]
        public void Can_be_played_to_completion()
        {
            var runner = new TutorialRunner(TutorialScript.CreateDefault());
            int guard = 0;
            while (!runner.IsComplete && guard++ < 100)
            {
                TutorialStep step = runner.Current;
                foreach (string target in step.Highlights)
                {
                    Assert.IsTrue(runner.IsInputAllowed(target), $"{step.Id} must allow its own highlight {target}");
                }
                Assert.IsTrue(runner.Notify(step.CompleteOn[0]), $"{step.Id} should complete on {step.CompleteOn[0]}");
            }
            Assert.IsTrue(runner.IsComplete);
        }

        [Test]
        public void Equip_steps_wait_for_the_right_weapon()
        {
            var runner = new TutorialRunner(TutorialScript.CreateDefault());
            runner.Notify(TutorialEvents.Click(UiTargets.TabEquipStats));
            Assert.AreEqual("T2", runner.Current.Id);
            Assert.IsFalse(runner.Notify(TutorialEvents.Equipped(WeaponCatalog.OldSwordId)));
            Assert.IsTrue(runner.Notify(TutorialEvents.Equipped(WeaponCatalog.BranchId)));
        }

        [Test]
        public void Job_dependent_line()
        {
            TutorialStep t22 = TutorialScript.CreateDefault().Single(s => s.Id == "T22");
            Assert.AreEqual("이제 근거리 사용 가능한 무기 나옴.", t22.LineFor(JobClass.Warrior));
            Assert.AreEqual("이제 힐 가능한 무기 나옴.", t22.LineFor(JobClass.Healer));
            Assert.AreEqual(string.Empty, t22.LineFor(JobClass.None));
        }

        [Test]
        public void Control_hints_follow_platform()
        {
            TutorialStep t6 = TutorialScript.CreateDefault().Single(s => s.Id == "T6");
            Assert.AreEqual("공격은 C! 한 대 때려 봐.", ControlHints.Keyboard.Apply(t6.Line));
            Assert.AreEqual("공격은 공격 버튼! 한 대 때려 봐.", ControlHints.Touch.Apply(t6.Line));
            Assert.AreEqual(string.Empty, ControlHints.Keyboard.Apply(null));
        }
    }
}
