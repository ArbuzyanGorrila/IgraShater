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
            Require(!game.CanUndo && !game.Model.CanUndo,"Undo is available before the first action");
            game.ToggleDanger(); Require(game.ShowDanger,"Danger toggle did not enable");
            yield return Capture("danger-1600",directory);
            game.ToggleDanger(); Require(!game.ShowDanger,"Danger toggle did not disable");
            // Excavate ore ahead of the first-room route, then Undo. This exercises presentation and rollback.
            game.PerformMine();
            Require(game.Busy && !game.CanUndo,"Actions were not blocked during animation");
            Require(!FindButton("Перезапустить · R").IsInteractable(),"Restart is available during animation");
            while (game.Busy) yield return null;
            Require(game.CanUndo,"Undo did not become available after mining");
            var before = game.Model.State; float elapsed = game.Elapsed;
            game.RequestRestart(); Require(game.ConfirmationOpen,"Restart did not request confirmation");
            Require(FindButton("Остаться · Esc").gameObject == UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                "Discard confirmation did not focus the safe action");
            Require(!FindButton("Отменить ход · Z").IsInteractable(),"Modal did not block underlying buttons");
            game.PerformStep(Vector2Int.right); game.PerformMine(); game.ToggleDanger(); game.Undo();
            yield return new WaitForSecondsRealtime(.2f);
            Require(game.Elapsed == elapsed && ReferenceEquals(before,game.Model.State) && !game.ShowDanger,
                "Discard confirmation changed the attempt or timer");
            yield return Capture("confirm-restart-1600",directory);
            game.HandleEscape(); Require(!game.ConfirmationOpen && ReferenceEquals(before,game.Model.State),"Esc did not cancel discard");
            Require(FindButton("Отменить ход · Z").IsInteractable(),"Undo did not recover after closing modal");
            game.Show(ScreenMode.Paused); elapsed = game.Elapsed;
            yield return Capture("pause-1600",directory);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(FindButton("Звук и управление").gameObject);
            game.Show(ScreenMode.Settings); game.SetVolume(.25f);
            yield return Capture("settings-paused-1600",directory);
            Require(game.Elapsed == elapsed && Mathf.Abs(game.Profile.volume-.25f) < .001f,"Paused settings changed time or failed to save volume");
            game.HandleEscape(); Require(game.Screen == ScreenMode.Paused,"Settings did not return to pause");
            Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == FindButton("Звук и управление").gameObject,
                "Settings did not restore pause focus");
            game.RequestHome(); Require(game.ConfirmationOpen,"Leaving pause did not request confirmation");
            yield return Capture("confirm-home-1600",directory);
            game.HandleEscape(); Require(game.Screen == ScreenMode.Paused && ReferenceEquals(before,game.Model.State),"Cancel did not preserve paused attempt");
            Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == FindButton("Звук и управление").gameObject,
                "Modal did not restore the previous focus");
            game.Show(ScreenMode.Playing);
            game.Undo();
            Require(!game.CanUndo && game.Model.State.moves == 0,"Undo did not clear history");
            game.RequestRestart(); Require(!game.ConfirmationOpen && game.Model.State.moves == 0,"Empty attempt required confirmation");
            game.PerformMine(); while (game.Busy) yield return null;
            game.RequestRestart(); game.ConfirmDiscard();
            Require(game.Model.State.moves == 0 && game.Model.UndoRemaining == 3 && game.AttemptExperience == 0,"Confirmed restart did not reset attempt");
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
            var resolutions = new[] { new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080),
                new Vector2Int(1600,1000),new Vector2Int(2560,1080) };
            foreach (var resolution in resolutions)
            {
                Screen.SetResolution(resolution.x,resolution.y,false); yield return new WaitForSecondsRealtime(.5f);
                yield return Capture("results-"+resolution.x+"x"+resolution.y,directory);
            }
            foreach (var resolution in resolutions)
            {
                Screen.SetResolution(resolution.x,resolution.y,false); yield return new WaitForSecondsRealtime(.5f);
                string size = resolution.x+"x"+resolution.y;
                game.RequestHome(); if (game.ConfirmationOpen) game.ConfirmDiscard();
                yield return Capture("home-"+size,directory);
                game.Show(ScreenMode.Rooms); yield return Capture("rooms-"+size,directory);
                game.Show(ScreenMode.Workshop); yield return Capture("workshop-"+size,directory);
                game.Show(ScreenMode.Settings); yield return Capture("settings-"+size,directory);
                game.HandleEscape(); Require(game.Screen == ScreenMode.Home,"Menu settings did not return home");
                game.StartRoom(0); yield return Capture("room-"+size,directory);
                game.PerformMine(); while (game.Busy) yield return null;
                game.RequestRestart(); yield return Capture("confirmation-"+size,directory); game.CancelDiscard();
                game.Show(ScreenMode.Paused); yield return Capture("pause-"+size,directory);
                game.Show(ScreenMode.Settings); yield return Capture("settings-paused-"+size,directory);
                game.HandleEscape();
            }
            // Confirm leaving an active attempt, then check its rewards never entered the profile.
            int savedXP = game.Profile.totalExperience;
            game.RequestHome(); Require(game.ConfirmationOpen,"Active attempt exited without confirmation"); game.ConfirmDiscard();
            Require(game.Screen == ScreenMode.Home && game.Profile.totalExperience == savedXP,"Discard changed saved rewards");
            yield return Capture("home-return-wide",directory);
            Debug.Log("CRAFTMINE_RUNTIME_QA_PASSED: gameplay, Undo, danger, confirmation, paused settings, victory, single banking, save reload and five screen sizes.");
            File.WriteAllText(System.IO.Path.Combine(directory,"runtime-passed.txt"),"Runtime gameplay, UX transitions and save checks passed. Screenshot layout still requires human review.");
            Application.Quit(0);
        }
        private static void Require(bool condition,string message)
        { if (!condition) throw new Exception("Runtime UX check failed: " + message); }
        private static UnityEngine.UI.Button FindButton(string name)
        {
            foreach (var button in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>())
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            throw new Exception("Runtime UX button missing: " + name);
        }
        private IEnumerator Capture(string name,string directory)
        {
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),texture.EncodeToPNG());
            Destroy(texture);
            foreach (var text in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>())
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                Require(text.font != null && text.rectTransform.rect.width > 0 && text.rectTransform.rect.height > 0,
                    "Invalid text bounds or font: " + text.text);
                Require(text.preferredHeight <= text.rectTransform.rect.height+1,
                    "Text would be truncated in " + name + ": " + text.text);
            }
        }
    }
}
