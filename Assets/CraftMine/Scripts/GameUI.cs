using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;

namespace CraftMine
{
    public sealed class GameUI : MonoBehaviour
    {
        private GameController game;
        private Canvas canvas;
        private RectTransform stage, content, confirmation;
        private CanvasGroup contentInput;
        private GameObject confirmationFocus;
        private ScreenMode displayedScreen;
        private int inputBlockedFrame = -1;
        private readonly Dictionary<ScreenMode, string> screenFocus = new Dictionary<ScreenMode, string>();
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Button> busyButtons = new List<Button>();
        private readonly List<Button> volumeButtons = new List<Button>();
        private Button defaultFocus, undoButton, dangerButton;
        private Text heartsCounter, movesCounter, timeCounter, undoCounter, volumeLabel;
        private enum ButtonStyle { Secondary, Primary, Destructive }
        private Font body, display;
        private GridView art;
        private Text xp, character, stats, notice;
        private readonly Text[] inventoryRows = new Text[4];
        private Image xpFill;
        private float nextRefresh;
        private readonly Color ink = new Color(.94f,.91f,.86f);
        private readonly Color secondary = new Color(.65f,.72f,.79f);
        private readonly Color amber = new Color(.96f,.73f,.39f);
        private readonly Color panel = new Color(.125f,.176f,.231f);
        private readonly Color buttonSurface = new Color(.22f,.30f,.39f);
        private readonly Color destructive = new Color(.40f,.18f,.18f);

        public void Initialize(GameController controller)
        {
            game = controller; art = GetComponent<GridView>();
#if UNITY_2023_1_OR_NEWER
            body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            body = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            display = Resources.Load<Font>("Fonts/PressStart2P");
            var obj = new GameObject("CraftMine interface",typeof(Canvas),typeof(GraphicRaycaster));
            canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            stage = new GameObject("Safe stage",typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(obj.transform,false); stage.anchorMin = stage.anchorMax = new Vector2(.5f,.5f);
            stage.sizeDelta = new Vector2(InterfaceLayout.Width,InterfaceLayout.Height);
            content = Rect("Screen content",0,0,InterfaceLayout.Width,InterfaceLayout.Height,stage);
            contentInput = content.gameObject.AddComponent<CanvasGroup>();
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("UI input",typeof(EventSystem),typeof(StandaloneInputModule));
            // WASD belongs to the board; menu navigation uses mouse, arrows and Tab.
            FindObjectOfType<EventSystem>().sendNavigationEvents = false;
            Rebuild();
        }
        private RectTransform Rect(string name, float x,float y,float w,float h,Transform parent = null)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent ?? content,false); rect.anchorMin = rect.anchorMax = new Vector2(0,1);
            rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h); return rect;
        }
        private Image Plate(float x,float y,float w,float h,Color color,Transform parent = null)
        { var img = Rect("Surface",x,y,w,h,parent).gameObject.AddComponent<Image>(); img.color = color; img.raycastTarget = false; return img; }
        private Text Label(string text,float x,float y,float w,float h,int size = 22,Color? color = null,bool title = false,Transform parent = null)
        {
            var label = Rect(text,x,y,w,h,parent).gameObject.AddComponent<Text>();
            label.font = title && display != null ? display : body; label.text = text; label.fontSize = size; label.color = color ?? ink;
            label.alignment = TextAnchor.UpperLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate; label.raycastTarget = false;
            return label;
        }
        private Button Button(string text,float x,float y,float w,Action action,bool enabled = true,float h = 50,
            ButtonStyle style = ButtonStyle.Secondary,Transform parent = null)
        {
            var rect = Rect(text,x,y,w,h,parent); var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            SetStyle(button,style); button.interactable = enabled;
            var focus = rect.gameObject.AddComponent<Outline>(); focus.effectColor = ink;
            focus.effectDistance = new Vector2(3,-3); focus.enabled = false;
            var label = Rect("Caption",12,4,w-24,h-8,rect).gameObject.AddComponent<Text>();
            label.font = body; label.fontSize = 22; label.text = text; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            button.onClick.AddListener(() =>
            {
                if (!button.IsInteractable()) return;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
                inputBlockedFrame = Time.frameCount;
                action();
            });
            buttons.Add(button);
            if (defaultFocus == null && enabled && style == ButtonStyle.Primary) defaultFocus = button;
            UpdateButton(button); return button;
        }
        private void SetStyle(Button button,ButtonStyle style)
        {
            Color surface = style == ButtonStyle.Primary ? amber : style == ButtonStyle.Destructive ? destructive : buttonSurface;
            var colors = button.colors; colors.normalColor = surface;
            colors.highlightedColor = Color.Lerp(surface,Color.white,.18f);
            colors.selectedColor = Color.Lerp(surface,Color.white,.24f);
            colors.pressedColor = Color.Lerp(surface,Color.black,.25f);
            colors.disabledColor = new Color(.15f,.19f,.24f); colors.fadeDuration = .08f; button.colors = colors;
        }
        private void UpdateButton(Button button)
        {
            bool enabled = button.IsInteractable();
            button.GetComponentInChildren<Text>().color = !enabled ? secondary :
                button.colors.normalColor == amber ? new Color(.08f,.12f,.17f) : ink;
            button.GetComponent<Outline>().enabled = enabled && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == button.gameObject;
        }
        private void Icon(int index,float x,float y,float size,Color? tint = null)
        { var image = Rect("Icon",x,y,size,size).gameObject.AddComponent<Image>(); image.sprite = art.Icon(index); image.color = tint ?? Color.white; image.raycastTarget = false; }
        public void Rebuild()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.transform.IsChildOf(content))
                screenFocus[displayedScreen] = eventSystem.currentSelectedGameObject.name;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            for (int i = content.childCount-1; i >= 0; i--)
            { var child = content.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            buttons.Clear(); busyButtons.Clear(); volumeButtons.Clear(); defaultFocus = undoButton = dangerButton = null;
            heartsCounter = movesCounter = timeCounter = undoCounter = volumeLabel = null;
            xp = character = stats = notice = null; xpFill = null;
            Array.Clear(inventoryRows,0,inventoryRows.Length);
            displayedScreen = game.Screen; inputBlockedFrame = Time.frameCount;
            canvas.scaleFactor = InterfaceLayout.Scale(Screen.width,Screen.height);
            Plate(1100,40,460,765,panel);
            if (game.Screen == ScreenMode.Home) Home();
            else
            {
                Label("CRAFTMINE",48,46,1000,50,28,amber,true);
                string heading = game.Screen == ScreenMode.Rooms ? "Карта шахты" : game.Screen == ScreenMode.Workshop ? "Мастерская" :
                    game.Screen == ScreenMode.Settings ? "Звук и управление" :
                    "Забой " + (game.RoomIndex+1) + "  /  " + game.Model.Room.title;
                Label(heading,48,106,1000,45,28);
                if (game.Screen == ScreenMode.Rooms) Rooms();
                else if (game.Screen == ScreenMode.Workshop) Workshop();
                else if (game.Screen == ScreenMode.Settings) Settings();
                else if (game.Screen == ScreenMode.Results) Results();
                else if (game.Screen == ScreenMode.Paused) Pause();
                else Playing();
            }
            notice = Label("",48,817,1504,50,21,amber);
            Label("Tab / Shift+Tab — выбор кнопки   Enter — нажать   Esc — назад / пауза",48,869,1504,25,17,secondary);
            Refresh();
            if (game.Screen != ScreenMode.Playing && eventSystem != null)
            {
                Button selected = null;
                if (screenFocus.TryGetValue(game.Screen,out var name))
                    selected = buttons.Find(b => b.name == name && b.IsInteractable());
                if (selected == null) selected = defaultFocus ?? buttons.Find(b => b.IsInteractable());
                if (selected != null) eventSystem.SetSelectedGameObject(selected.gameObject);
            }
        }
        private void Home()
        {
            Label("CRAFTMINE",48,42,1000,74,48,amber,true);
            Label("Прокладывай путь. Собирай сердечки.\nСтановись сильнее с каждым раскопанным блоком.",48,120,1000,70,25);
            Label("Твоя следующая экспедиция",1130,75,400,55,25);
            CharacterPanel(145,false);
            bool completed = Array.TrueForAll(game.Profile.stars,n => n > 0);
            int next = game.Profile.unlockedRooms-1;
            Label(game.Rooms[next].title,1130,500,400,52,22,secondary);
            Button((completed ? "Повторить забой " : "Начать забой ") + (next+1),1130,565,400,
                () => game.StartRoom(next),style:ButtonStyle.Primary);
            Button("Карта шахты",1130,625,400,() => game.Show(ScreenMode.Rooms));
            Button("Мастерская",1130,685,400,() => game.Show(ScreenMode.Workshop));
            Button("Звук и управление",1130,745,400,() => game.Show(ScreenMode.Settings));
            Label("Собери все сердечки одним маршрутом.\nКамни падают, когда теряют опору. Добыча сохраняется после победы.",48,752,1000,56,22,secondary);
        }
        private void CharacterPanel(float y,bool attempt)
        {
            character = Label("",1130,y,400,40,27,amber);
            xp = Label("",1130,y+44,400,30,20,secondary);
            Plate(1130,y+80,400,8,new Color(.06f,.1f,.15f)); xpFill = Plate(1130,y+80,400,8,amber);
            stats = Label("",1130,y+102,400,64,22);
            Label(attempt ? "Добыча этой попытки" : "Ресурсы в запасе",1130,y+176,400,30,23);
            for (int i = 0; i < 4; i++)
            {
                Icon(i == 3 ? 67 : 7,1130,y+213+i*32,26,GridView.OreColors[i]);
                inventoryRows[i] = Label("",1172,y+214+i*32,355,30,22);
            }
        }
        private void Playing()
        {
            heartsCounter = Label("",48,158,280,34,23,amber);
            movesCounter = Label("",338,158,210,34,23);
            timeCounter = Label("",558,158,225,34,23);
            undoCounter = Label("",793,158,267,34,23);
            CharacterPanel(74,true);
            Label("Опыт и добыча — после победы",1130,424,400,30,19,secondary);
            undoButton = Button("Отменить ход · Z",1130,466,400,game.Undo,game.CanUndo);
            dangerButton = Button("",1130,526,400,game.ToggleDanger);
            busyButtons.Add(dangerButton);
            busyButtons.Add(Button("Пауза · Esc",1130,586,400,() => game.Show(ScreenMode.Paused)));
            busyButtons.Add(Button("Перезапустить · R",1130,646,400,game.RequestRestart,style:ButtonStyle.Destructive));
            Label(StarConditions(),1130,710,400,94,19,secondary);
            Label("Цель: собрать все сердечки. " + TrailRule(),48,752,1012,27,21,amber);
            Label("WASD / стрелки — шаг   E — копать перед собой   Space — линии падения",48,782,1012,27,20,secondary);
        }
        private string TrailRule() => game.Model.Room.strictTrail ? "Не пересекай свой след." : "След показывает маршрут.";
        private string StarConditions() => "1 звезда — собрать все сердечки.\n2 звезды — не больше " + game.Model.Room.par +
            " ходов.\n3 звезды — дополнительно без\nотмен и расхода защиты.";
        private void Rooms()
        {
            Plate(40,166,1020,638,panel);
            Label("Выбери маршрут. Звёзды и рекорды сохраняются.",70,194,950,44,26);
            for (int i = 0; i < game.Rooms.Length; i++)
            {
                int index = i; float x = 70+i%5*194,y = 254+i/5*246;
                bool open = i < game.Profile.unlockedRooms, passed = game.Profile.stars[i] > 0;
                Button((open ? "Забой " : "Закрыт · ") + (i+1),x,y,174,() => game.StartRoom(index),open,52,
                    open && !passed && i == game.Profile.unlockedRooms-1 ? ButtonStyle.Primary : ButtonStyle.Secondary);
                Label(game.Rooms[i].title,x,y+64,174,64,21);
                Label(!open ? "Пройди забой " + i : passed ? "Пройден · " + game.Profile.stars[i] + " / 3" : "Доступен",x,y+134,174,26,18,passed ? amber : secondary);
                Label(passed ? "Рекорд: " + game.Profile.bestMoves[i] + " ходов" : open ? "Цель: ≤ " + game.Rooms[i].par + " ходов" : "",x,y+166,174,48,18,secondary);
            }
            CharacterPanel(80,false);
            Button("Мастерская",1130,590,400,() => game.Show(ScreenMode.Workshop));
            Button("Главное меню",1130,666,400,game.RequestHome);
        }
        private void Workshop()
        {
            Plate(40,166,1020,638,panel);
            Label("Следующее улучшение сразу экипирует новую кирку.",70,191,950,46,26);
            for (int i = 0; i < game.Recipes.Length; i++)
            {
                int index = i; var r = game.Recipes[i]; float y = 248+i*132;
                bool owned = game.Profile.pickaxeTier >= r.pickaxeTier, next = r.pickaxeTier == game.Profile.pickaxeTier+1;
                Label(CraftMineSettings.PickaxeNames[r.pickaxeTier] + " кирка" + (next ? " · следующая" : ""),70,y,650,34,25,next ? amber : ink);
                var costs = new List<string>(); var requirements = new List<string>();
                if (!owned && !next) requirements.Add("Сначала создай предыдущую кирку");
                if (!owned && game.Balance.Level(game.Profile.totalExperience) < r.requiredLevel)
                    requirements.Add("Нужен уровень " + r.requiredLevel);
                for (int j = 0; j < 4; j++) if (r.cost[j] > 0)
                {
                    costs.Add(CraftMineSettings.OreNames[j] + " " + game.Profile.inventory[j] + " / " + r.cost[j]);
                    if (!owned && game.Profile.inventory[j] < r.cost[j])
                        requirements.Add(CraftMineSettings.OreNames[j] + ": не хватает " + (r.cost[j]-game.Profile.inventory[j]));
                }
                Label("Есть / нужно: " + string.Join(" · ",costs),70,y+39,650,30,20);
                Label(owned ? game.Profile.pickaxeTier == r.pickaxeTier ? "Экипирована" : "Уже создана" :
                    requirements.Count == 0 ? "Можно создать" : string.Join(" · ",requirements),70,y+73,650,50,18,owned ? secondary : amber);
                Button(owned ? "Создана" : "Создать",760,y+10,266,() => game.Craft(index),
                    !owned && Crafting.Requirement(game.Profile,game.Balance,r) == null,style:next ? ButtonStyle.Primary : ButtonStyle.Secondary);
            }
            CharacterPanel(80,false);
            Button("Карта шахты",1130,590,400,() => game.Show(ScreenMode.Rooms));
            Button("Главное меню",1130,666,400,game.RequestHome);
        }
        private void Pause()
        {
            CharacterPanel(80,true);
            Label("Экспедиция на паузе",1130,438,400,40,26,amber);
            Button("Продолжить",1130,488,400,() => game.Show(ScreenMode.Playing),style:ButtonStyle.Primary);
            Button("Звук и управление",1130,548,400,() => game.Show(ScreenMode.Settings));
            Button("Перезапустить",1130,608,400,game.RequestRestart,style:ButtonStyle.Destructive);
            Button("Главное меню",1130,668,400,game.RequestHome);
            Label("При выходе опыт и добыча\nэтой попытки потеряются.",1130,735,400,58,19,secondary);
            Label("Время остановлено. " + TrailRule() + "\nEsc — продолжить экспедицию.",48,752,1000,56,22,secondary);
        }
        private void Results()
        {
            bool won = game.Model.State.status == RunStatus.Won;
            CharacterPanel(80,true);
            Label(won ? "Забой пройден!" : game.Model.State.status == RunStatus.Lost ? "Обвал!" : "Нет доступных ходов",1130,440,400,40,29,amber);
            Label(won ? "Звёзды: " + game.EarnedStars + " / 3\nОпыт: +" + game.AttemptExperience + " XP\n" +
                (game.Banked ? "Опыт и добыча сохранены" : "Награды ещё не сохранены") :
                "Опыт и добыча не сохранены.\n" + (game.CanUndo ? "Отмени ход и продолжи попытку." : "Начни новую попытку."),1130,488,400,85,21);
            if (won && !game.Banked)
                Button("Повторить сохранение",1130,586,400,() => { game.BankVictory(); Rebuild(); },style:ButtonStyle.Primary);
            else if (won && game.RoomIndex < game.Rooms.Length-1)
                Button("Следующий забой",1130,586,400,() => game.StartRoom(game.RoomIndex+1),style:ButtonStyle.Primary);
            else if (won) Button("Снова пройти забой",1130,586,400,game.RequestRestart,style:ButtonStyle.Primary);
            else if (game.CanUndo) undoButton = Button("Отменить ход",1130,586,400,game.Undo,style:ButtonStyle.Primary);
            else Button("Попробовать снова",1130,586,400,game.RequestRestart,style:ButtonStyle.Primary);
            if (won) Button("Мастерская",1130,646,400,() => game.Show(ScreenMode.Workshop),game.Banked);
            else if (game.CanUndo) Button("Попробовать снова",1130,646,400,game.RequestRestart,style:ButtonStyle.Destructive);
            Button("Главное меню",1130,706,400,game.RequestHome,!won || game.Banked);
            string reason = !won ? "За победу — одна звезда. Добыча сохраняется только после победы." :
                game.EarnedStars == 1 ? "1 звезда: превышена цель " + game.Model.Room.par + " ходов. Для 2 звёзд — уложись в цель." :
                game.EarnedStars == 2 ? "2 звезды: отмены " + game.Model.UndoUsed + ", расход защиты " + game.Model.State.protectionUsed +
                    ". Для 3 звёзд — без них." : "Три звезды: уложился в цель, без отмен и расхода защиты.";
            Label("Ходы: " + game.Model.State.moves + " / цель " + game.Model.Room.par + "   ·   Время: " + FormatTime(game.Elapsed),48,752,1012,28,23);
            Label(reason,48,785,1012,28,20,secondary);
        }
        private void Settings()
        {
            Plate(40,166,1020,638,panel);
            Label("Громкость",75,208,600,45,28);
            volumeLabel = Label("",75,264,900,34,22,secondary);
            for (int i = 0; i < 5; i++)
            {
                float volume = i*.25f;
                var button = Button(i == 0 ? "Выкл." : (i*25) + "%",75+i*190,318,166,() => game.SetVolume(volume),h:64);
                button.GetComponentInChildren<Text>().fontSize = 20; volumeButtons.Add(button);
            }
            Label("Управление",75,420,850,45,28,amber);
            Label("WASD / стрелки — шаг и направление копания\nE — копать область перед собой\nSpace — включить / выключить линии падения\nZ / Backspace — отменить ход (три раза за попытку)\nR — перезапустить с подтверждением после первого хода\nEsc — пауза / назад",75,480,930,208,23);
            Label("Добыча и опыт сохраняются после победы.\nШаг в недоступную клетку только поворачивает героя.",75,720,930,60,21,secondary);
            CharacterPanel(80,game.AttemptVisible);
            Button(game.SettingsReturnScreen == ScreenMode.Paused ? "Вернуться к паузе" : "Главное меню",1130,666,400,
                () => game.Show(game.SettingsReturnScreen),style:ButtonStyle.Primary);
        }
        public void OpenConfirmation()
        {
            if (confirmation != null) return;
            confirmationFocus = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            contentInput.interactable = false; contentInput.blocksRaycasts = false;
            confirmation = Rect("Confirm discard",0,0,InterfaceLayout.Width,InterfaceLayout.Height,stage);
            var backdrop = confirmation.gameObject.AddComponent<Image>(); backdrop.color = new Color(0,0,0,.78f);
            Plate(420,245,760,390,panel,confirmation);
            Label(game.PendingDiscard == DiscardAction.Restart ? "Начать забой заново?" : "Выйти в главное меню?",452,275,696,48,30,amber,parent:confirmation);
            var loot = new List<string>();
            for (int i = 0; i < 4; i++) if (game.Model.State.loot[i] > 0)
                loot.Add(CraftMineSettings.OreNames[i] + " × " + game.Model.State.loot[i]);
            Label("Прогресс этой попытки потеряется.\nОпыт: +" + game.AttemptExperience + " XP · Ходы: " + game.Model.State.moves +
                "\nДобыча: " + (loot.Count == 0 ? "нет" : string.Join(" · ",loot)),452,342,696,116,23,parent:confirmation);
            Label("Ранее сохранённые ресурсы и уровни останутся.",452,468,696,34,20,secondary,parent:confirmation);
            var cancel = Button("Остаться · Esc",452,540,330,game.CancelDiscard,style:ButtonStyle.Primary,parent:confirmation);
            Button(game.PendingDiscard == DiscardAction.Restart ? "Перезапустить" : "Выйти",818,540,330,game.ConfirmDiscard,
                style:ButtonStyle.Destructive,parent:confirmation);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
            inputBlockedFrame = Time.frameCount; Refresh();
        }
        public void CloseConfirmation()
        {
            if (confirmation == null) return;
            buttons.RemoveAll(b => b.transform.IsChildOf(confirmation));
            confirmation.gameObject.SetActive(false); Destroy(confirmation.gameObject); confirmation = null;
            contentInput.interactable = true; contentInput.blocksRaycasts = true;
            Refresh();
            if (EventSystem.current != null)
            {
                var previous = confirmationFocus == null ? null : confirmationFocus.GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(previous != null && previous.IsInteractable() ? previous.gameObject : null);
            }
            confirmationFocus = null; inputBlockedFrame = Time.frameCount;
        }
        public void Refresh()
        {
            if (game == null || game.Balance == null || game.Profile == null) return;
            bool attempt = game.AttemptVisible;
            if (attempt && game.Model == null) return;
            int totalXP = attempt ? game.Model.State.experience : game.Profile.totalExperience;
            int level = game.Balance.Level(totalXP), current = totalXP-game.Balance.Threshold(level);
            int required = game.Balance.Threshold(level+1)-game.Balance.Threshold(level);
            if (character != null) character.text = "Уровень персонажа " + level;
            if (xp != null) xp.text = level == game.Balance.maxLevel ? "Максимальный уровень / " + totalXP + " XP" : current + " / " + required + " XP до уровня " + (level+1);
            if (xpFill != null) xpFill.rectTransform.sizeDelta = new Vector2(400*(level == game.Balance.maxLevel ? 1f : (float)current/required),8);
            if (stats != null)
            {
                int radius = game.Balance.Radius(level);
                stats.text = "Кирка: " + CraftMineSettings.PickaxeNames[attempt ? game.Model.PickaxeTier : game.Profile.pickaxeTier] + "\n" +
                    "Удар: " + (radius == 1 ? "1 клетка" : "3 × " + radius) + " / Защита: " + (attempt ? game.Model.State.charges : game.Balance.Protection(level));
            }
            for (int i = 0; i < 4; i++) if (inventoryRows[i] != null)
                inventoryRows[i].text = CraftMineSettings.OreNames[i] + "   " + (attempt ? game.Model.State.loot[i] : game.Profile.inventory[i]);
            if (heartsCounter != null) heartsCounter.text = "Сердечки " + game.Model.State.collected + " / " + game.Model.State.heartCount;
            if (movesCounter != null) movesCounter.text = "Ходы " + game.Model.State.moves;
            if (timeCounter != null) timeCounter.text = "Время " + FormatTime(game.Elapsed);
            if (undoCounter != null) undoCounter.text = "Отмены " + game.Model.UndoRemaining + " / 3";
            if (notice != null) notice.text = game.Message ?? (attempt ? game.Model.ReachabilityWarning : "");
            foreach (var button in busyButtons) button.interactable = !game.Busy;
            if (undoButton != null) undoButton.interactable = game.CanUndo;
            if (dangerButton != null)
            {
                dangerButton.GetComponentInChildren<Text>().text = "Линии падения: " + (game.ShowDanger ? "вкл." : "выкл.") + " · Space";
                dangerButton.GetComponentInChildren<Text>().fontSize = 20;
                SetStyle(dangerButton,game.ShowDanger ? ButtonStyle.Primary : ButtonStyle.Secondary);
            }
            if (volumeLabel != null) volumeLabel.text = "Текущая: " + Mathf.RoundToInt(game.Profile.volume*100) + "% · применяется сразу";
            for (int i = 0; i < volumeButtons.Count; i++)
            {
                bool selected = Mathf.Abs(game.Profile.volume-i*.25f) < .001f;
                volumeButtons[i].GetComponentInChildren<Text>().text = (i == 0 ? "Выкл." : (i*25) + "%") + (selected ? "\nВыбрано" : "");
                SetStyle(volumeButtons[i],selected ? ButtonStyle.Primary : ButtonStyle.Secondary);
            }
            foreach (var button in buttons) UpdateButton(button);
        }
        private static string FormatTime(float seconds) => ((int)seconds/60).ToString("00") + ":" + ((int)seconds%60).ToString("00");
        private void Update()
        {
            if (canvas == null || stage == null || game == null) return;
            canvas.scaleFactor = InterfaceLayout.Scale(Screen.width,Screen.height);
            if (Time.unscaledTime > nextRefresh) { nextRefresh = Time.unscaledTime+.1f; Refresh(); }
            foreach (var button in buttons) UpdateButton(button);
            var eventSystem = EventSystem.current;
            if (eventSystem == null || inputBlockedFrame == Time.frameCount || Input.GetKeyDown(KeyCode.Escape)) return;
            bool menu = game.Screen != ScreenMode.Playing || game.ConfirmationOpen;
            if (!Input.GetKeyDown(KeyCode.Tab) && !Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter) &&
                !(menu && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                    Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))) return;
            var valid = buttons.FindAll(b => b != null && b.isActiveAndEnabled && b.IsInteractable());
            if (valid.Count == 0) return;
            int selected = valid.FindIndex(b => b.gameObject == eventSystem.currentSelectedGameObject);
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool backwards = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                int next = selected < 0 ? (backwards ? valid.Count-1 : 0) : (selected+(backwards ? valid.Count-1 : 1))%valid.Count;
                eventSystem.SetSelectedGameObject(valid[next].gameObject);
            }
            else if (menu)
            {
                Vector2 direction = Input.GetKeyDown(KeyCode.UpArrow) ? Vector2.up :
                    Input.GetKeyDown(KeyCode.DownArrow) ? Vector2.down :
                    Input.GetKeyDown(KeyCode.LeftArrow) ? Vector2.left :
                    Input.GetKeyDown(KeyCode.RightArrow) ? Vector2.right : Vector2.zero;
                if (direction != Vector2.zero)
                {
                    Button next = selected < 0 ? valid[0] : Neighbour(valid[selected],valid,direction);
                    eventSystem.SetSelectedGameObject(next.gameObject);
                }
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                var button = eventSystem.currentSelectedGameObject == null ? null : eventSystem.currentSelectedGameObject.GetComponent<Button>();
                if (button != null && valid.Contains(button)) button.onClick.Invoke();
            }
        }
        private Button Neighbour(Button current,List<Button> valid,Vector2 direction)
        {
            var origin = ((RectTransform)current.transform).position;
            Button best = current; float bestScore = float.MaxValue;
            foreach (var button in valid)
            {
                if (button == current) continue;
                Vector2 delta = ((RectTransform)button.transform).position-origin;
                float forward = Vector2.Dot(delta,direction);
                if (forward <= 1f) continue;
                float sideways = Mathf.Abs(Vector2.Dot(delta,new Vector2(-direction.y,direction.x)));
                float score = forward+sideways*4;
                if (score < bestScore) { bestScore = score; best = button; }
            }
            return best;
        }
        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
