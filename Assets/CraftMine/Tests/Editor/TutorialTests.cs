using System;
using System.IO;
using CraftMine;
using NUnit.Framework;
using UnityEngine;

namespace CraftMineTests
{
    public sealed class TutorialTests
    {
        [Test] public void RejectedMovementAndEmptyMiningDoNotAdvancePractice()
        {
            var tutorial = new TutorialProgress(); tutorial.Begin();
            tutorial.ObserveAction(new ActionResult(), true);
            tutorial.ObserveAction(new ActionResult(), false);
            Assert.AreEqual(0, tutorial.Step);
            tutorial.ObserveAction(new ActionResult { accepted = true }, true);
            Assert.AreEqual(1, tutorial.Step);
        }
        [Test] public void PracticeRemembersToolsUsedBeforeTheirPrompt()
        {
            var tutorial = new TutorialProgress(); tutorial.Begin();
            tutorial.ObserveAction(new ActionResult { accepted = true }, false);
            tutorial.ObserveDanger(true); tutorial.ObserveUndo();
            Assert.AreEqual(0, tutorial.Step);
            tutorial.ObserveAction(new ActionResult { accepted = true }, true);
            Assert.AreEqual(4, tutorial.Step);
            tutorial.Begin(); Assert.AreEqual(0, tutorial.Step);
        }
        [Test] public void OptionalPracticeCanBeSkippedWithoutChangingTheGame()
        {
            var tutorial = new TutorialProgress(); tutorial.Begin();
            for (int i = 0; i < 8; i++) tutorial.SkipStep();
            Assert.AreEqual(4, tutorial.Step); Assert.IsTrue(tutorial.Active);
            tutorial.Stop(); Assert.IsFalse(tutorial.Active);
        }
        [Test] public void OldSaveKeepsProgressAndNewTutorialPreferenceRoundTrips()
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "tutorial-save-" + Guid.NewGuid());
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "profile.json");
            try
            {
                var original = new PlayerProfile { totalExperience = 140, unlockedRooms = 3, inventory = new[] {8, 4, 0, 0} };
                string oldJson = JsonUtility.ToJson(original).Replace(",\"tutorialSeen\":false", "");
                File.WriteAllText(path, oldJson);
                var saves = new SaveService(path); var loaded = saves.Load();
                Assert.AreEqual(140, loaded.totalExperience); Assert.AreEqual(3, loaded.unlockedRooms);
                Assert.AreEqual(8, loaded.inventory[0]); Assert.IsFalse(loaded.tutorialSeen);
                loaded.tutorialSeen = true;
                var copy = SaveService.Copy(loaded); Assert.IsTrue(copy.tutorialSeen);
                Assert.IsTrue(saves.Save(copy)); Assert.IsTrue(saves.Load().tutorialSeen);
                Assert.AreEqual(140, saves.Load().totalExperience);
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
