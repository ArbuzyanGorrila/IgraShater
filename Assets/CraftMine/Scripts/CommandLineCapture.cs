using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CraftMine
{
    // Opt-in diagnostic run uses a separate profile and never touches the player's save.
    public sealed class CommandLineCapture : MonoBehaviour
    {
        public static string OutputDirectory()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length-1; i++) if (args[i] == "--craftmine-capture") return System.IO.Path.GetFullPath(args[i+1]);
            return null;
        }
        private IEnumerator Start()
        {
            string directory = OutputDirectory(); Directory.CreateDirectory(directory);
            var game = GetComponent<GameController>();
            // A hidden Windows launch may not present its first swapchain until resized.
            Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.3f);
            Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("home-1600",directory);
            game.StartRoom(0); yield return Capture("room-1600",directory);
            // Excavate ore ahead of the first-room route, then Undo. This exercises presentation and rollback.
            game.PerformMine(); while (game.Busy) yield return null;
            game.Undo();
            foreach (char c in game.Rooms[0].solution)
            {
                game.PerformStep(c == 'W' ? Vector2Int.up : c == 'S' ? Vector2Int.down : c == 'A' ? Vector2Int.left : Vector2Int.right);
                while (game.Busy) yield return null;
                if (game.Model.State.status != RunStatus.Playing && game.Model.State.status != RunStatus.Won)
                    throw new Exception("Runtime witness failed: " + game.Model.State.status);
            }
            if (!game.Banked || game.Model.State.status != RunStatus.Won) throw new Exception("Runtime victory was not saved");
            int bankedXP = game.Profile.totalExperience; game.BankVictory();
            if (game.Profile.totalExperience != bankedXP) throw new Exception("Victory banked twice");
            var loaded = new SaveService(System.IO.Path.Combine(directory,"qa-profile.json")).Load();
            if (loaded.totalExperience != bankedXP) throw new Exception("Runtime save did not round trip");
            yield return Capture("results-1600",directory);
            game.Show(ScreenMode.Workshop); yield return Capture("workshop-1600",directory);
            game.Show(ScreenMode.Rooms); yield return Capture("rooms-1600",directory);
            Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.5f);
            game.StartRoom(0); yield return Capture("room-1280",directory);
            Screen.SetResolution(1920,1080,false); yield return new WaitForSecondsRealtime(.5f);
            int finalRoom = Math.Min(2,game.Profile.unlockedRooms-1);
            game.StartRoom(finalRoom);
            for (int i = 0; i < 5; i++)
            { game.PerformStep(Vector2Int.right); while (game.Busy) yield return null; }
            yield return Capture("room-updated-1920",directory);
            game.Show(ScreenMode.Home); yield return Capture("home-return-1920",directory);
            Debug.Log("CRAFTMINE_RUNTIME_QA_PASSED: gameplay, Undo, victory, single banking, save reload, screens at 1600x900, 1280x720 and 1920x1080.");
            File.WriteAllText(System.IO.Path.Combine(directory,"runtime-passed.txt"),"Runtime gameplay and save checks passed.");
            Application.Quit(0);
        }
        private IEnumerator Capture(string name,string directory)
        {
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),texture.EncodeToPNG());
            Destroy(texture);
        }
    }
}
