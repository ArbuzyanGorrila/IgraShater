namespace CraftMine
{
    // Observe successful actions in any order; optional tools never block an expedition.
    public sealed class TutorialProgress
    {
        public bool Active { get; private set; }
        private readonly bool[] practiced = new bool[4];
        public int Step
        {
            get
            {
                for (int i = 0; i < practiced.Length; i++) if (!practiced[i]) return i;
                return 4;
            }
        }
        public string Title => Titles[Step];
        public string Hint => Hints[Step];
        private static readonly string[] Titles = {
            "WASD / стрелки — сделай первый шаг",
            "E — попробуй удар киркой",
            "Space — посмотри линии падения",
            "Z / Backspace — попробуй отменить ход",
            "Собери все сердечки и заверши забой"
        };
        private static readonly string[] Hints = {
            "Шаг сам копает землю. Удерживай клавишу, чтобы идти дальше.",
            "Повернись к земле или меди и нажми E. Область удара отмечена жёлтым.",
            "Нажми Space. Красные клетки — возможные пути падения валунов.",
            "Нажми Z или пропусти шаг, чтобы сберечь отмены и шанс на 3 звезды.",
            "Пройди по всем сердечкам. Победа сохранит опыт и добычу."
        };
        public void Begin()
        {
            for (int i = 0; i < practiced.Length; i++) practiced[i] = false;
            Active = true;
        }
        public void Stop() { Active = false; }
        public void ObserveAction(ActionResult result, bool movement)
        {
            if (Active && result.accepted) practiced[movement ? 0 : 1] = true;
        }
        public void ObserveDanger(bool visible) { if (Active && visible) practiced[2] = true; }
        public void ObserveUndo() { if (Active) practiced[3] = true; }
        public void SkipStep() { if (Active && Step < practiced.Length) practiced[Step] = true; }
    }
}
