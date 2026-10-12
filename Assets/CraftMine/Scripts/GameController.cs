using System;
using System.Collections;
using UnityEngine;

namespace CraftMine
{
    public enum ScreenMode { Home, Rooms, Workshop, Playing, Paused, Results, Settings, Tutorial }

    public sealed class GameController : MonoBehaviour
    {
        public CraftMineSettings Balance { get; private set; }
        public RoomDefinition[] Rooms { get; private set; }
        public RecipeDefinition[] Recipes { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public GameModel Model { get; private set; }
        public ScreenMode Screen { get; private set; } = ScreenMode.Home;
        public int RoomIndex { get; private set; }
        public bool Busy { get; private set; }
        public bool Banked { get; private set; }
        public bool ShowDanger { get; private set; }
        public float Elapsed { get; private set; }
        public string Message { get; private set; }
        public int EarnedStars { get; private set; }
        public TutorialProgress Tutorial { get; } = new TutorialProgress();
        public int TutorialPage { get; private set; }
        public ScreenMode TutorialReturnScreen { get; private set; }
        public const int TutorialPageCount = 4;
        private bool tutorialIntroduction;
        private float messageUntil, nextAction;
        private SaveService saves;
        private GridView view;
        private GameUI ui;
        private AudioSource audioSource;
        private AudioClip digSound, heartSound, rockSound, levelSound;
        private Coroutine actionRoutine;

        private void Awake()
        {
            Balance = Resources.Load<CraftMineSettings>("Balance");
            Rooms = Resources.LoadAll<RoomDefinition>("Rooms");
            Array.Sort(Rooms, (a,b) => string.CompareOrdinal(a.name,b.name));
            Recipes = Resources.LoadAll<RecipeDefinition>("Recipes");
            Array.Sort(Recipes, (a,b) => a.pickaxeTier.CompareTo(b.pickaxeTier));
            if (Balance == null || Rooms.Length != 10 || Recipes.Length != 4)
                throw new InvalidOperationException("CraftMine content missing. Use Tools > CraftMine > Prepare project.");
            string captureDirectory = CommandLineCapture.OutputDirectory();
            saves = new SaveService(captureDirectory == null ? null : System.IO.Path.Combine(captureDirectory,"qa-profile.json"));
            Profile = saves.Load();
            view = gameObject.AddComponent<GridView>(); view.Initialize(this);
            ui = gameObject.AddComponent<GameUI>(); ui.Initialize(this);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            digSound = Tone(130, .08f, true); heartSound = Tone(740, .16f, false);
            rockSound = Tone(70, .2f, true); levelSound = Tone(1100, .28f, false);
            StartRoomPreview();
            if (saves.Notice != null) Notify(saves.Notice, 12);
            Application.targetFrameRate = 60;
            if (captureDirectory != null) gameObject.AddComponent<CommandLineCapture>();
        }
        private void Update()
        {
            if (Screen == ScreenMode.Playing && Model.State.status == RunStatus.Playing) Elapsed += Time.unscaledDeltaTime;
            if (Message != null && Time.unscaledTime > messageUntil) Message = null;
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (Screen == ScreenMode.Tutorial) CloseTutorial();
                else OpenTutorial();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Screen == ScreenMode.Tutorial) { CloseTutorial(); return; }
                if (Screen == ScreenMode.Playing && !Busy) Show(ScreenMode.Paused);
                else if (Screen == ScreenMode.Paused) Show(ScreenMode.Playing);
                else if (Screen != ScreenMode.Playing && Screen != ScreenMode.Results) Show(ScreenMode.Home);
            }
            if (Screen != ScreenMode.Playing || Busy) return;
            if (Input.GetKeyDown(KeyCode.R)) { StartRoom(RoomIndex); return; }
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace)) { Undo(); return; }
            if (Input.GetKeyDown(KeyCode.Space)) ToggleDanger();
            if (Model.State.status != RunStatus.Playing || Time.unscaledTime < nextAction) return;
            Vector2Int direction = Vector2Int.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction = Vector2Int.up;
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction = Vector2Int.right;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction = Vector2Int.down;
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction = Vector2Int.left;
            if (direction != Vector2Int.zero) PerformStep(direction);
            else if (Input.GetKey(KeyCode.E)) PerformMine();
        }
        public void PerformStep(Vector2Int direction)
        {
            if (Busy || Screen != ScreenMode.Playing) return;
            var result = Model.Step(direction); Tutorial.ObserveAction(result, true); Act(result);
        }
        public void PerformMine()
        {
            if (Busy || Screen != ScreenMode.Playing) return;
            var result = Model.Mine(); Tutorial.ObserveAction(result, false); Act(result);
        }
        public void ToggleDanger()
        {
            if (Busy || Screen != ScreenMode.Playing) return;
            ShowDanger = !ShowDanger; Tutorial.ObserveDanger(ShowDanger); view.RefreshOverlays();
        }
        public void OpenTutorial()
        {
            if (Busy || Screen == ScreenMode.Tutorial) return;
            TutorialReturnScreen = Screen; TutorialPage = 0; tutorialIntroduction = false;
            Show(ScreenMode.Tutorial);
        }
        public void ChangeTutorialPage(int offset)
        {
            if (Screen != ScreenMode.Tutorial) return;
            TutorialPage = Mathf.Clamp(TutorialPage + offset, 0, TutorialPageCount - 1); ui.Rebuild();
        }
        public void CloseTutorial(bool skipPractice = false)
        {
            if (Screen != ScreenMode.Tutorial) return;
            if (tutorialIntroduction)
            {
                var candidate = SaveService.Copy(Profile); candidate.tutorialSeen = true;
                if (saves.Save(candidate)) Profile = candidate;
                else Notify(saves.Notice, 12);
            }
            tutorialIntroduction = false;
            if (skipPractice) Tutorial.Stop();
            // Avoid a held movement key taking a step as the guide closes.
            nextAction = Time.unscaledTime + .25f;
            Show(TutorialReturnScreen);
        }
        public void StartTraining()
        {
            if (Screen != ScreenMode.Tutorial || TutorialReturnScreen != ScreenMode.Home) return;
            StartRoom(0, true);
        }
        public void SkipTutorialStep()
        {
            if (Screen != ScreenMode.Playing || Busy) return;
            if (Tutorial.Step < 4) Tutorial.SkipStep(); else Tutorial.Stop();
            ui.Rebuild();
        }
        public void Show(ScreenMode screen)
        {
            if (Busy) return;
            Screen = screen;
            if (screen == ScreenMode.Home || screen == ScreenMode.Rooms || screen == ScreenMode.Workshop || screen == ScreenMode.Settings)
                view.SetMenuAppearance(true);
            else view.SetMenuAppearance(false);
            ui.Rebuild();
        }
        private void StartRoomPreview()
        {
            Model = new GameModel(Rooms[0], Balance, Profile);
            view.LoadBoard(); view.SetMenuAppearance(true);
        }
        public void StartRoom(int index, bool training = false)
        {
            if (index < 0 || index >= Rooms.Length || index >= Profile.unlockedRooms) return;
            bool introduction = !Profile.tutorialSeen;
            bool practice = index == 0 && (introduction || training || Tutorial.Active);
            Tutorial.Stop(); if (practice) Tutorial.Begin();
            if (actionRoutine != null) StopCoroutine(actionRoutine);
            Busy = false; RoomIndex = index; Banked = false; ShowDanger = false; Elapsed = 0;
            Model = new GameModel(Rooms[index], Balance, Profile); Screen = ScreenMode.Playing;
            view.LoadBoard(); view.SetMenuAppearance(false); ui.Rebuild();
            Message = null; nextAction = Time.unscaledTime + .15f;
            Notify(Rooms[index].lesson, 8);
            if (introduction)
            {
                OpenTutorial(); tutorialIntroduction = true;
            }
        }
        public void Undo()
        {
            if (Busy || Banked) return;
            if (Model.Undo()) { Tutorial.ObserveUndo(); Screen = ScreenMode.Playing; view.LoadBoard(); ui.Rebuild(); Notify("Ход отменён. Опыт и добыча восстановлены."); }
            else Notify("Отмена недоступна. Осталось: " + Model.UndoRemaining);
        }
        private void Act(ActionResult result)
        {
            nextAction = Time.unscaledTime + .12f;
            if (!result.accepted) { if (result.message != null) Notify(result.message); view.RefreshOverlays(); return; }
            Busy = true;
            if (result.dug > 0) Play(digSound);
            actionRoutine = StartCoroutine(Animate(result));
        }
        private IEnumerator Animate(ActionResult result)
        {
            yield return view.Animate(result, Balance.ActionSeconds(Model.Level, Model.PickaxeTier));
            Busy = false; actionRoutine = null;
            if (result.hearts > 0) Play(heartSound);
            if (result.falls > 0) Play(rockSound);
            if (result.shields > 0) Notify("Защита спасла героя! Валун разбит.");
            if (result.levelsGained > 0)
            { Play(levelSound); Notify("Уровень " + Model.Level + ": " + Balance.rewardNames[Model.Level-1], 5); }
            if (Model.State.status != RunStatus.Playing)
            {
                Screen = ScreenMode.Results;
                if (Model.State.status == RunStatus.Won) { Tutorial.Stop(); BankVictory(); }
                ui.Rebuild();
            }
            else { ui.Refresh(); view.RefreshOverlays(); }
        }
        public void BankVictory()
        {
            if (Banked || Model.State.status != RunStatus.Won) return;
            var candidate = SaveService.Copy(Profile);
            candidate.totalExperience = Model.State.experience;
            for (int i = 0; i < 4; i++) candidate.inventory[i] += Model.State.loot[i];
            EarnedStars = Model.State.moves <= Model.Room.par ?
                (Model.UndoUsed == 0 && Model.State.protectionUsed == 0 ? 3 : 2) : 1;
            candidate.stars[RoomIndex] = Math.Max(candidate.stars[RoomIndex], EarnedStars);
            if (candidate.bestMoves[RoomIndex] == 0 || Model.State.moves < candidate.bestMoves[RoomIndex]) candidate.bestMoves[RoomIndex] = Model.State.moves;
            if (candidate.bestSeconds[RoomIndex] == 0 || Elapsed < candidate.bestSeconds[RoomIndex]) candidate.bestSeconds[RoomIndex] = Elapsed;
            candidate.unlockedRooms = Math.Max(candidate.unlockedRooms, Math.Min(10, RoomIndex+2));
            if (saves.Save(candidate)) { Profile = candidate; Banked = true; }
            else Notify(saves.Notice, 30);
        }
        public void Craft(int recipeIndex)
        {
            var recipe = Recipes[recipeIndex];
            string requirement = Crafting.Requirement(Profile, Balance, recipe);
            if (requirement != null) { Notify(requirement); return; }
            var candidate = SaveService.Copy(Profile);
            Crafting.TryCraft(candidate, Balance, recipe);
            if (!saves.Save(candidate)) { Notify(saves.Notice, 12); return; }
            Profile = candidate; ui.Rebuild(); Play(levelSound);
            Notify("Создана кирка: " + CraftMineSettings.PickaxeNames[Profile.pickaxeTier]);
        }
        public void SetVolume(float volume)
        {
            var candidate = SaveService.Copy(Profile); candidate.volume = volume;
            if (saves.Save(candidate)) Profile = candidate;
            else Notify(saves.Notice, 12);
        }
        public void Notify(string message, float seconds = 3)
        { Message = message; messageUntil = Time.unscaledTime + seconds; }
        private void Play(AudioClip clip) { audioSource.PlayOneShot(clip, Profile.volume); }
        private AudioClip Tone(float frequency, float seconds, bool noise)
        {
            int count = (int)(22050 * seconds); var samples = new float[count]; var random = new System.Random(17);
            for (int i = 0; i < count; i++)
            {
                float phase = i / 22050f * frequency;
                float wave = noise ? ((float)random.NextDouble()*2-1)*.6f + Mathf.Sin(phase * Mathf.PI*2)*.4f : Mathf.Sin(phase * Mathf.PI*2);
                samples[i] = wave * .2f * Mathf.Pow(1f - (float)i/count, 2);
            }
            var clip = AudioClip.Create("CraftMine effect", count, 1, 22050, false); clip.SetData(samples,0); return clip;
        }
        private void OnDestroy()
        {
            foreach (var clip in new[] { digSound, heartSound, rockSound, levelSound }) if (clip != null) Destroy(clip);
        }
    }
}
