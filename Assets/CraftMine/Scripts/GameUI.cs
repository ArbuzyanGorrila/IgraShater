using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftMine
{
    public sealed class GameUI : MonoBehaviour
    {
        private GameController game;
        private Canvas canvas;
        private RectTransform stage;
        private Font body, display;
        private GridView art;
        private Text counters, xp, character, stats, notice, status, inventory;
        private Text tutorialTask, tutorialHint, tutorialSkip;
        private Image xpFill;
        private float nextRefresh;
        private readonly Color ink = new Color(.94f,.91f,.86f);
        private readonly Color secondary = new Color(.65f,.72f,.79f);
        private readonly Color amber = new Color(.96f,.73f,.39f);
        private readonly Color panel = new Color(.125f,.176f,.231f);

        public void Initialize(GameController controller)
        {
            game = controller; art = GetComponent<GridView>();
#if UNITY_2022_2_OR_NEWER
            body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            body = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            display = Resources.Load<Font>("Fonts/PressStart2P");
            var obj = new GameObject("CraftMine interface",typeof(Canvas),typeof(GraphicRaycaster));
            canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            stage = new GameObject("Safe stage",typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(obj.transform,false); stage.anchorMin = stage.anchorMax = new Vector2(.5f,.5f);
            stage.sizeDelta = new Vector2(1600,900);
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("UI input",typeof(EventSystem),typeof(StandaloneInputModule));
            // WASD belongs to the board; menu navigation uses mouse, arrows and Tab.
            FindObjectOfType<EventSystem>().sendNavigationEvents = false;
            Rebuild();
        }
        private RectTransform Rect(string name, float x,float y,float w,float h,Transform parent = null)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent ?? stage,false); rect.anchorMin = rect.anchorMax = new Vector2(0,1);
            rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h); return rect;
        }
        private Image Plate(float x,float y,float w,float h,Color color)
        { var img = Rect("Surface",x,y,w,h).gameObject.AddComponent<Image>(); img.color = color; img.raycastTarget = false; return img; }
        private Text Label(string text,float x,float y,float w,float h,int size = 22,Color? color = null,bool title = false)
        {
            var label = Rect(text,x,y,w,h).gameObject.AddComponent<Text>();
            label.font = title ? display : body; label.text = text; label.fontSize = size; label.color = color ?? ink;
            label.alignment = TextAnchor.UpperLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate; label.raycastTarget = false;
            return label;
        }
        private Button Button(string text,float x,float y,float w,Action action,bool enabled = true,float h = 54)
        {
            var rect = Rect(text,x,y,w,h); var image = rect.gameObject.AddComponent<Image>();
            image.color = amber; var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.08f,1.08f,1.08f);
            colors.pressedColor = new Color(.82f,.82f,.82f); colors.selectedColor = new Color(1.1f,1.1f,1.1f);
            colors.disabledColor = new Color(.38f,.43f,.48f); button.colors = colors; button.interactable = enabled;
            var label = Rect("Caption",12,6,w-24,h-12,rect).gameObject.AddComponent<Text>();
            label.font = body; label.fontSize = 22; label.text = text; label.alignment = TextAnchor.MiddleCenter;
            label.color = enabled ? new Color(.08f,.12f,.17f) : ink; label.raycastTarget = false;
            button.onClick.AddListener(() => action()); return button;
        }
        private void Icon(int index,float x,float y,float size,Color? tint = null)
        { var image = Rect("Icon",x,y,size,size).gameObject.AddComponent<Image>(); image.sprite = art.Icon(index); image.color = tint ?? Color.white; image.raycastTarget = false; }
        public void Rebuild()
        {
            for (int i = stage.childCount-1; i >= 0; i--)
            { var child = stage.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            counters = xp = character = stats = notice = status = inventory = null; xpFill = null;
            tutorialTask = tutorialHint = tutorialSkip = null;
            Plate(1100,40,460,765,panel);
            if (game.Screen == ScreenMode.Home) Home();
            else
            {
                Label("CRAFTMINE",48,46,1000,50,28,amber,true);
                string heading = game.Screen == ScreenMode.Rooms ? "Карта шахты" : game.Screen == ScreenMode.Workshop ? "Мастерская" :
                    game.Screen == ScreenMode.Tutorial ? "Как играть" :
                    game.Screen == ScreenMode.Settings ? "Звук и управление" :
                    "Забой " + (game.RoomIndex+1) + "  /  " + game.Model.Room.title;
                Label(heading,48,106,1000,45,28);
                if (game.Screen == ScreenMode.Rooms) Rooms();
                else if (game.Screen == ScreenMode.Workshop) Workshop();
                else if (game.Screen == ScreenMode.Settings) Settings();
                else if (game.Screen == ScreenMode.Tutorial) Tutorial();
                else if (game.Screen == ScreenMode.Results) Results();
                else if (game.Screen == ScreenMode.Paused) Pause();
                else Playing();
            }
            notice = Label("",48,819,1504,66,21,amber);
            Refresh();
        }
        private void Home()
        {
            Label("CRAFTMINE",48,42,1000,74,48,amber,true);
            Label("Прокладывай путь. Собирай сердечки.\nСтановись сильнее с каждым раскопанным блоком.",48,120,1000,70,25);
            Label("Твоя следующая экспедиция",1130,75,400,55,25);
            CharacterPanel(145,false);
            Button("Начать забой " + game.Profile.unlockedRooms,1130,565,400,() => game.StartRoom(game.Profile.unlockedRooms-1),true,44);
            Button("Карта шахты",1130,614,400,() => game.Show(ScreenMode.Rooms),true,44);
            Button("Мастерская",1130,663,400,() => game.Show(ScreenMode.Workshop),true,44);
            Button("Как играть  ·  F1",1130,712,400,game.OpenTutorial,true,44);
            Button("Звук и управление",1130,761,400,() => game.Show(ScreenMode.Settings),true,44);
            Label("Собери все сердечки одним маршрутом.\nКамни падают, когда теряют опору.",48,762,1000,58,22,secondary);
        }
        private void CharacterPanel(float y,bool attempt)
        {
            character = Label("",1130,y,400,42,28,amber);
            xp = Label("",1130,y+49,400,32,20,secondary);
            Plate(1130,y+88,400,8,new Color(.06f,.1f,.15f)); xpFill = Plate(1130,y+88,400,8,amber);
            stats = Label("",1130,y+112,400,95,22);
            Label(attempt ? "Добыча этой попытки" : "Ресурсы в запасе",1130,y+215,400,32,23);
            for (int i = 0; i < 4; i++) Icon(i == 3 ? 67 : 7,1130,y+255+i*35,26,GridView.OreColors[i]);
            inventory = Label("",1172,y+256,355,150,22);
        }
        private void Playing()
        {
            counters = Label("",48,155,1000,38,23,amber);
            CharacterPanel(74,true);
            Label("Сохраняется после победы",1130,498,400,30,19,secondary);
            Button("Отменить ход  ·  Z",1130,540,400,game.Undo,game.Model.UndoRemaining > 0);
            Button("Перезапустить  ·  R",1130,606,400,() => game.StartRoom(game.RoomIndex));
            Button("Пауза  ·  Esc",1130,672,400,() => game.Show(ScreenMode.Paused));
            if (game.Tutorial.Active)
            {
                Plate(40,742,1020,66,panel);
                tutorialTask = Label("",60,748,750,28,21,amber);
                tutorialHint = Label("",60,779,750,25,18);
                var skip = Button("Пропустить шаг",828,754,214,game.SkipTutorialStep,true,44);
                tutorialSkip = skip.GetComponentInChildren<UnityEngine.UI.Text>();
                tutorialSkip.fontSize = 18;
            }
            else Label("WASD / стрелки — шаг   E — удар перед собой\nSpace — линии падения   Z — отмена   R — заново",48,765,1000,64,22,secondary);
            Button("Как играть  ·  F1",1130,733,400,game.OpenTutorial,true,44);
            status = Label("",1130,782,400,25,18,amber);
        }
        private void Rooms()
        {
            Plate(40,166,1020,638,panel);
            Label("Десять маршрутов к сердцу шахты",70,195,950,44,28);
            for (int i = 0; i < game.Rooms.Length; i++)
            {
                int index = i; float x = 70+i%5*194,y = 270+i/5*218;
                bool open = i < game.Profile.unlockedRooms;
                Button("Забой " + (i+1),x,y,174,() => game.StartRoom(index),open,68);
                Label(game.Rooms[i].title,x,y+84,174,70,22);
                Label(open ? (game.Profile.stars[i] == 0 ? "Ещё не пройден" : "Звёзды: " + game.Profile.stars[i] + " / 3") : "Пройди предыдущий",x,y+156,174,48,18,secondary);
            }
            CharacterPanel(80,false);
            Button("Мастерская",1130,590,400,() => game.Show(ScreenMode.Workshop));
            Button("Главное меню",1130,666,400,() => game.Show(ScreenMode.Home));
        }
        private void Workshop()
        {
            Plate(40,166,1020,638,panel);
            Label("Улучшай кирку перед экспедицией",70,191,950,46,28);
            for (int i = 0; i < game.Recipes.Length; i++)
            {
                int index = i; var r = game.Recipes[i]; float y = 255+i*126;
                Label(CraftMineSettings.PickaxeNames[r.pickaxeTier] + " кирка",70,y,510,34,26,amber);
                var costs = new List<string>();
                for (int j = 0; j < 4; j++) if (r.cost[j] > 0) costs.Add(CraftMineSettings.OreNames[j] + " × " + r.cost[j]);
                Label(string.Join("  /  ",costs),70,y+40,570,30,21);
                string reason = Crafting.Requirement(game.Profile,game.Balance,r);
                bool owned = game.Profile.pickaxeTier >= r.pickaxeTier;
                Label(owned ? "Уже создана" : reason ?? "Ресурсов достаточно",70,y+73,590,31,18,owned ? secondary : amber);
                Button(owned ? "Создана" : "Создать",760,y+10,266,() => game.Craft(index),!owned && reason == null);
            }
            CharacterPanel(80,false);
            Button("Карта шахты",1130,590,400,() => game.Show(ScreenMode.Rooms));
            Button("Главное меню",1130,666,400,() => game.Show(ScreenMode.Home));
        }
        private void Pause()
        {
            CharacterPanel(80,true);
            Label("Экспедиция на паузе",1130,520,400,42,26,amber);
            Button("Продолжить",1130,563,400,() => game.Show(ScreenMode.Playing));
            Button("Как играть  ·  F1",1130,623,400,game.OpenTutorial);
            Button("Перезапустить",1130,683,400,() => game.StartRoom(game.RoomIndex));
            Button("В меню: добыча не сохранится",1130,743,400,() => game.Show(ScreenMode.Home));
            Label("Время остановлено. Можно спокойно осмотреть шахту.",48,765,1000,60,22,secondary);
        }
        private void Results()
        {
            bool won = game.Model.State.status == RunStatus.Won;
            CharacterPanel(80,true);
            Label(won ? "Забой пройден!" : game.Model.State.status == RunStatus.Lost ? "Обвал!" : "Нет доступных ходов",1130,516,400,45,29,amber);
            Label(won ? "Звёзды: " + game.EarnedStars + " / 3\n" + (game.Banked ? "Опыт и добыча сохранены" : "Награды ожидают сохранения") :
                "Добыча этой попытки не сохранена.\nМожно отменить последний ход.",1130,566,400,69,21);
            if (won && !game.Banked) Button("Повторить сохранение",1130,646,400,() => { game.BankVictory(); Rebuild(); });
            else if (won && game.RoomIndex < 9) Button("Следующий забой",1130,646,400,() => game.StartRoom(game.RoomIndex+1));
            else if (!won) Button("Отменить ход",1130,646,400,game.Undo,game.Model.UndoRemaining > 0);
            else Button("Снова пройти забой",1130,646,400,() => game.StartRoom(game.RoomIndex));
            Button(won ? "Мастерская" : "Попробовать снова",1130,714,400,() => { if (won) game.Show(ScreenMode.Workshop); else game.StartRoom(game.RoomIndex); },!won || game.Banked);
            Button("Главное меню",48,762,270,() => game.Show(ScreenMode.Home),!won || game.Banked);
            Label("Ходов: " + game.Model.State.moves + "   /   Время: " + FormatTime(game.Elapsed),350,771,700,45,24,secondary);
        }
        private void Settings()
        {
            Plate(40,166,1020,638,panel);
            Label("Громкость",75,208,600,45,28);
            Label("Текущая: " + Mathf.RoundToInt(game.Profile.volume*100) + "%",75,264,700,34,22,secondary);
            for (int i = 0; i < 5; i++)
            { float volume = i*.25f; Button(i == 0 ? "Выкл." : (i*25) + "%",75+i*190,318,166,() => { game.SetVolume(volume); Rebuild(); }); }
            Label("Управление",75,426,850,45,28,amber);
            Label("WASD / стрелки — шаг и направление копания\nE — копать область перед собой\nSpace — показать возможные линии падения\nZ / Backspace — отменить ход (три раза за попытку)\nR — начать комнату заново\nEsc — пауза",75,492,900,258,24);
            CharacterPanel(80,false);
            Button("Главное меню",1130,666,400,() => game.Show(ScreenMode.Home));
        }
        private void GuideRow(string key, string explanation, float y)
        {
            Plate(75,y,196,62,new Color(.08f,.12f,.17f));
            var badge = Label(key,85,y+10,176,44,24,amber);
            badge.alignment = TextAnchor.MiddleCenter;
            Label(explanation,300,y+2,720,76,23);
        }
        private void Tutorial()
        {
            Plate(40,166,1020,638,panel);
            string[] chapters = { "Первые шаги", "Кирка и обвалы", "План и страховка", "Добыча и прогресс" };
            Label(chapters[game.TutorialPage],75,203,950,48,32,amber);
            if (game.TutorialPage == 0)
            {
                Icon(44,75,266,40);
                Label("Цель — собрать ВСЕ сердечки в забое.\nСердечко собирается, когда герой проходит по его клетке.",137,263,875,75,25);
                GuideRow("W A S D", "W — вверх, A — влево, S — вниз, D — вправо.\nМожно использовать стрелки. Удержание повторяет шаги.",355);
                GuideRow("Шаг", "Доступная земля и руда копаются при движении.\nКаждый успешный шаг считается ходом.",454);
                GuideRow("Поворот", "Шаг в стену или валун только поворачивает героя.\nХод не тратится. Повернись так перед ударом киркой.",553);
                GuideRow("Сердечки", "Не обязательно добывать всю руду.\nЗабой завершится, когда собраны все сердечки.",652);
            }
            else if (game.TutorialPage == 1)
            {
                Label("Жёлтая рамка — область следующего удара.\nКрасные клетки — возможные линии падения камней.",75,263,940,75,25);
                GuideRow("E", "Удар киркой перед героем без перемещения.\nКопает только доступную землю и руду; стены и валуны целы.",355);
                GuideRow("Space", "Включить или выключить линии падения.\nЭто подсказка: после копания проверь опору камней.",454);
                GuideRow("Валуны", "После успешного хода валуны падают без опоры\nи могут скатиться с другого валуна. Не стой под ними.",553);
                GuideRow("Защита", "Заряд защиты разбивает попавший в героя валун.\nБез заряда обвал завершает попытку поражением.",652);
            }
            else if (game.TutorialPage == 2)
            {
                Label("Сначала наметь маршрут к сердечкам.\nМожно открыть эту справку в любой момент: игра подождёт.",75,263,940,75,25);
                GuideRow("Z / Backspace", "Отменить успешный ход: вернуть камни, добычу и опыт.\nВсего три отмены за попытку; они не пополняются ходами.",355);
                GuideRow("R", "Начать текущий забой заново.\nНесохранённые опыт и добыча этой попытки пропадут.",454);
                GuideRow("Esc / F1", "Esc — пауза. F1 — справка; время в ней остановлено.\nEsc или F1 в справке вернут тебя на предыдущий экран.",553);
                GuideRow("След", "В испытаниях нельзя возвращаться на свой след.\nОб этом написано справа внизу. След не держит камни.",652);
            }
            else
            {
                Label("Уровень героя и номер забоя — разные вещи.\nКопание даёт опыт, а рудные блоки — ещё и ресурсы для кирок.",75,263,940,75,25);
                GuideRow("Руда", "Медь доступна сразу. Железо: уровень 3 + медная кирка;\nзолото: 5 + железная; алмазы: 8 + золотая.",355);
                GuideRow("Мастерская", "Между попытками создавай кирки из сохранённой руды.\nУровни открывают скорость, защиту и более широкие удары.",454);
                GuideRow("Победа", "Опыт и добыча сохраняются только после победы.\nПри поражении, перезапуске или выходе они теряются.",553);
                GuideRow("Звёзды", "1 — победа; 2 — уложиться в заданное число ходов;\n3 — дополнительно без отмен и расхода защиты.",652);
            }
            Label("СПРАВОЧНИК ШАХТЁРА",1130,80,400,44,24,amber);
            Label("Страница " + (game.TutorialPage+1) + " / " + GameController.TutorialPageCount,1130,134,400,36,23,secondary);
            for (int i = 0; i < chapters.Length; i++)
                Label((i+1) + "   " + chapters[i],1130,206+i*53,400,38,23,i == game.TutorialPage ? amber : secondary);
            Label("Кнопки меню: мышь\nTab — выбрать, Enter — нажать.\nF1 / Esc — закрыть справку.",1130,435,400,95,22,secondary);
            bool last = game.TutorialPage == GameController.TutorialPageCount-1;
            Button(last ? (game.TutorialReturnScreen == ScreenMode.Playing ? "Понятно — в шахту!" : "Понятно — вернуться") : "Дальше",1130,554,400,
                () => { if (last) game.CloseTutorial(); else game.ChangeTutorialPage(1); });
            Button("Назад",1130,617,400,() => game.ChangeTutorialPage(-1),game.TutorialPage > 0);
            Button(game.Tutorial.Active ? "Играть без подсказок" : "Закрыть справку",1130,680,400,() => game.CloseTutorial(true));
            if (game.TutorialReturnScreen == ScreenMode.Home)
                Button("Обучение в первом забое",1130,743,400,game.StartTraining);
        }
        public void Refresh()
        {
            if (game == null || game.Balance == null || game.Profile == null) return;
            bool attempt = game.Screen == ScreenMode.Playing || game.Screen == ScreenMode.Paused || game.Screen == ScreenMode.Results;
            if (attempt && game.Model == null) return;
            int totalXP = attempt ? game.Model.State.experience : game.Profile.totalExperience;
            int level = game.Balance.Level(totalXP), current = totalXP-game.Balance.Threshold(level);
            int required = game.Balance.Threshold(level+1)-game.Balance.Threshold(level);
            if (character != null) character.text = "Уровень персонажа " + level;
            if (xp != null) xp.text = level == game.Balance.maxLevel ? "Максимальный уровень  /  " + totalXP + " XP" : current + " / " + required + " XP до уровня " + (level+1);
            if (xpFill != null) xpFill.rectTransform.sizeDelta = new Vector2(400*(level == game.Balance.maxLevel ? 1f : (float)current/required),8);
            if (stats != null)
            {
                int radius = game.Balance.Radius(level);
                stats.text = "Кирка: " + CraftMineSettings.PickaxeNames[game.Profile.pickaxeTier] + "\n" +
                    "Удар: " + (radius == 1 ? "1 клетка" : "3 × " + radius) + "   /   Защита: " + (attempt ? game.Model.State.charges : game.Balance.Protection(level));
            }
            if (inventory != null)
            {
                var rows = new List<string>();
                for (int i = 0; i < 4; i++) rows.Add(CraftMineSettings.OreNames[i] + "   " + (attempt ? game.Model.State.loot[i] : game.Profile.inventory[i]));
                inventory.text = string.Join("\n",rows); inventory.lineSpacing = 1.37f;
            }
            if (counters != null) counters.text = "Сердечки " + game.Model.State.collected + " / " + game.Model.State.heartCount +
                "      Ходы " + game.Model.State.moves + "      " + FormatTime(game.Elapsed) + "      Отмена " + game.Model.UndoRemaining + "/3";
            if (notice != null) notice.text = game.Screen == ScreenMode.Tutorial ? "" : game.Message ?? (attempt ? game.Model.ReachabilityWarning : "");
            if (status != null) status.text = game.Model.Room.strictTrail ? "Испытание: не пересекай след" : "След показывает твой маршрут";
            if (tutorialTask != null) tutorialTask.text = "Шаг " + (game.Tutorial.Step+1) + " / 5  ·  " + game.Tutorial.Title;
            if (tutorialHint != null) tutorialHint.text = game.Tutorial.Hint;
            if (tutorialSkip != null) tutorialSkip.text = game.Tutorial.Step < 4 ? "Пропустить шаг" : "Скрыть подсказки";
        }
        private static string FormatTime(float seconds) => ((int)seconds/60).ToString("00") + ":" + ((int)seconds%60).ToString("00");
        private void Update()
        {
            if (canvas == null || stage == null || game == null) return;
            canvas.scaleFactor = Mathf.Min(Screen.width/1600f,Screen.height/900f);
            if (Time.unscaledTime > nextRefresh) { nextRefresh = Time.unscaledTime+.1f; Refresh(); }
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var buttons = stage.GetComponentsInChildren<Button>();
                var valid = new List<Button>(); foreach (var b in buttons) if (b.interactable) valid.Add(b);
                if (valid.Count > 0)
                {
                    int selected = valid.FindIndex(b => b.gameObject == eventSystem.currentSelectedGameObject);
                    eventSystem.SetSelectedGameObject(valid[(selected+1)%valid.Count].gameObject);
                }
            }
            if (Input.GetKeyDown(KeyCode.Return) && eventSystem.currentSelectedGameObject != null)
            { var b = eventSystem.currentSelectedGameObject.GetComponent<Button>(); if (b != null && b.interactable) b.onClick.Invoke(); }
        }
        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
