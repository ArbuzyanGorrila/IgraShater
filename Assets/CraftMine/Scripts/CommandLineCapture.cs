using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CraftMine
{
    // Opt-in diagnostic run uses a separate profile and never touches the player's save.
    public sealed class CommandLineCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureDiagnosticRun()
        {
            if (OutputDirectory() != null) Application.runInBackground = true;
        }
        public static string OutputDirectory()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length-1; i++) if (args[i] == "--craftmine-capture") return System.IO.Path.GetFullPath(args[i+1]);
            return null;
        }
        private IEnumerator Start()
        {
            string directory = OutputDirectory(); Directory.CreateDirectory(directory);
            Debug.Log("CRAFTMINE_RUNTIME_QA_STARTED");
            var game = GetComponent<GameController>();
            // A hidden Windows launch may not present its first swapchain until resized.
            Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.3f);
            Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("home-1600",directory);
            game.OpenTutorial();
            for (int page = 0; page < GameController.TutorialPageCount; page++)
            {
                yield return Capture("tutorial-page-"+page+"-1600",directory);
                if (page < GameController.TutorialPageCount-1) game.ChangeTutorialPage(1);
            }
            game.CloseTutorial();
            if (game.Screen != ScreenMode.Home) throw new Exception("Guide did not return to Home");
            game.StartRoom(0);
            if (game.Screen != ScreenMode.Tutorial) throw new Exception("First expedition did not show the introduction");
            game.PerformStep(Vector2Int.right); game.PerformMine();
            yield return Capture("tutorial-introduction-1600",directory);
            if (game.Model.State.moves != 0 || game.Elapsed != 0) throw new Exception("Gameplay or timer ran beneath the guide");
            game.CloseTutorial();
            if (!game.Profile.tutorialSeen || !game.Tutorial.Active) throw new Exception("Introduction preference or practice missing");
            yield return Capture("room-1600",directory);
            // Excavate ore ahead of the first-room route, then Undo. This exercises presentation and rollback.
            game.PerformMine(); while (game.Busy) yield return null;
            game.Undo();
            game.ToggleDanger();
            game.Show(ScreenMode.Paused); game.OpenTutorial();
            int pausedMoves = game.Model.State.moves; float pausedTime = game.Elapsed;
            yield return Capture("tutorial-from-pause-1600",directory);
            game.CloseTutorial();
            if (game.Screen != ScreenMode.Paused || game.Model.State.moves != pausedMoves || game.Elapsed != pausedTime)
                throw new Exception("Guide did not preserve a paused expedition");
            game.Show(ScreenMode.Playing);
            foreach (char c in game.Rooms[0].solution)
            {
                game.PerformStep(c == 'W' ? Vector2Int.up : c == 'S' ? Vector2Int.down : c == 'A' ? Vector2Int.left : Vector2Int.right);
                while (game.Busy) yield return null;
                if (game.Model.State.status != RunStatus.Playing && game.Model.State.status != RunStatus.Won)
                    throw new Exception("Runtime witness failed: " + game.Model.State.status);
            }
            if (!game.Banked || game.Model.State.status != RunStatus.Won) throw new Exception("Runtime victory was not saved");
            if (game.Tutorial.Active) throw new Exception("Practice remained active after victory");
            int bankedXP = game.Profile.totalExperience; game.BankVictory();
            if (game.Profile.totalExperience != bankedXP) throw new Exception("Victory banked twice");
            var loaded = new SaveService(System.IO.Path.Combine(directory,"qa-profile.json")).Load();
            if (loaded.totalExperience != bankedXP) throw new Exception("Runtime save did not round trip");
            if (!loaded.tutorialSeen) throw new Exception("Tutorial preference did not round trip");
            yield return Capture("results-1600",directory);
            game.Show(ScreenMode.Workshop); yield return Capture("workshop-1600",directory);
            game.Show(ScreenMode.Rooms); yield return Capture("rooms-1600",directory);
            Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.5f);
            game.StartRoom(0, true); yield return Capture("room-1280",directory);
            if (game.Screen != ScreenMode.Playing) throw new Exception("Introduction repeated after being acknowledged");
            for (int step = 1; step < 5; step++)
            {
                game.SkipTutorialStep(); yield return Capture("practice-step-"+step+"-1280",directory);
            }
            game.OpenTutorial(); yield return Capture("tutorial-1280",directory); game.CloseTutorial();
            Screen.SetResolution(1920,1080,false); yield return new WaitForSecondsRealtime(.5f);
            int finalRoom = Math.Min(2,game.Profile.unlockedRooms-1);
            game.StartRoom(finalRoom);
            game.OpenTutorial(); game.ChangeTutorialPage(3);
            yield return Capture("tutorial-1920",directory); game.CloseTutorial();
            for (int i = 0; i < 5; i++)
            { game.PerformStep(Vector2Int.right); while (game.Busy) yield return null; }
            yield return Capture("room-updated-1920",directory);
            game.Show(ScreenMode.Home); yield return Capture("home-return-1920",directory);
            game.OpenTutorial(); game.StartTraining();
            if (game.Screen != ScreenMode.Playing || !game.Tutorial.Active || game.RoomIndex != 0)
                throw new Exception("Practice could not be replayed from Home");
            game.OpenTutorial(); game.CloseTutorial(true);
            if (game.Tutorial.Active) throw new Exception("Practice could not be dismissed");
            Debug.Log("CRAFTMINE_RUNTIME_QA_PASSED: tutorial introduction, pages, pause, preference, replay, skip, gameplay, Undo, victory, single banking, save reload and screens at three resolutions.");
            File.WriteAllText(System.IO.Path.Combine(directory,"runtime-passed.txt"),"Runtime gameplay and save checks passed.");
            Application.Quit(0);
        }
        private IEnumerator Capture(string name,string directory)
        {
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            var game = GetComponent<GameController>();
            if (game.Screen == ScreenMode.Tutorial || (game.Screen == ScreenMode.Playing && game.Tutorial.Active))
            {
                Canvas.ForceUpdateCanvases();
                foreach (var text in FindObjectsOfType<UnityEngine.UI.Text>())
                    if (!string.IsNullOrEmpty(text.text) && text.preferredHeight > text.rectTransform.rect.height + 1)
                        throw new Exception("Tutorial text is clipped: " + text.text);
            }
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),texture.EncodeToPNG());
            Destroy(texture);
        }
    }
}
