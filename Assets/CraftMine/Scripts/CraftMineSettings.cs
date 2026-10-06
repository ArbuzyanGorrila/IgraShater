using System;
using UnityEngine;

namespace CraftMine
{
    public enum CellType { Air, Dirt, Bedrock, Boulder, Copper, Iron, Gold, Diamond }
    public enum RunStatus { Playing, Won, Lost, Trapped }

    [CreateAssetMenu(menuName = "CraftMine/Balance")]
    public sealed class CraftMineSettings : ScriptableObject
    {
        public int maxLevel = 10;
        public int firstLevelExperience = 40;
        public int experienceStep = 20;
        public int[] miningExperience = { 1, 5, 8, 12, 20 };
        public float baseActionSeconds = .28f;
        public float[] pickaxeSpeed = { 1f, 1.15f, 1.3f, 1.45f, 1.6f };
        public string[] rewardNames = {
            "Деревянная кирка и медь", "Скорость копания +10%", "Открыта железная руда",
            "Один заряд защиты", "Открыта золотая руда", "Область копания 3 × 2",
            "Скорость копания ещё +10%", "Открыта алмазная руда", "Второй заряд защиты", "Область копания 3 × 3"
        };
        public int Level(int totalExperience)
        {
            int level = 1;
            while (level < maxLevel && totalExperience >= Threshold(level + 1)) level++;
            return level;
        }
        public int Threshold(int targetLevel)
        {
            int result = 0;
            for (int l = 1; l < targetLevel; l++) result += firstLevelExperience + experienceStep * (l - 1);
            return result;
        }
        public int Protection(int level) => level >= 9 ? 2 : level >= 4 ? 1 : 0;
        public int Radius(int level) => level >= 10 ? 3 : level >= 6 ? 2 : 1;
        public float ActionSeconds(int level, int pickaxe) => baseActionSeconds /
            ((level >= 7 ? 1.2f : level >= 2 ? 1.1f : 1f) * pickaxeSpeed[Mathf.Clamp(pickaxe, 0, 4)]);
        public static int OreIndex(CellType cell) => cell >= CellType.Copper ? (int)cell - (int)CellType.Copper : -1;
        public static readonly int[] OreLevels = { 1, 3, 5, 8 };
        public static readonly string[] OreNames = { "Медь", "Железо", "Золото", "Алмазы" };
        public static readonly string[] PickaxeNames = { "Деревянная", "Медная", "Железная", "Золотая", "Алмазная" };
    }

    [CreateAssetMenu(menuName = "CraftMine/Room")]
    public sealed class RoomDefinition : ScriptableObject
    {
        public string title;
        [TextArea] public string lesson;
        public bool strictTrail;
        public int par;
        public string[] rows;
        public string solution;
    }

    [CreateAssetMenu(menuName = "CraftMine/Recipe")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        public int pickaxeTier;
        public int requiredLevel;
        public int[] cost = new int[4];
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int version = 1;
        public int totalExperience;
        public int pickaxeTier;
        public int unlockedRooms = 1;
        public int[] inventory = new int[4];
        public int[] stars = new int[10];
        public int[] bestMoves = new int[10];
        public float[] bestSeconds = new float[10];
        public float volume = .6f;
    }
}
