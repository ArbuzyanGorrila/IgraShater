using System;
using System.IO;
using UnityEngine;

namespace CraftMine
{
    public sealed class SaveService
    {
        public readonly string Path;
        public string Notice { get; private set; }
        public SaveService(string path = null) { Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, "craftmine-save.json"); }
        public PlayerProfile Load()
        {
            Notice = null;
            if (TryRead(Path, out var profile)) return profile;
            if (TryRead(Path + ".bak", out profile))
            { Notice = "Сохранение восстановлено из резервной копии."; return profile; }
            if (File.Exists(Path) || File.Exists(Path + ".bak"))
                Notice = "Не удалось прочитать сохранение. Создан новый профиль; старые файлы сохранены.";
            return new PlayerProfile();
        }
        private bool TryRead(string path, out PlayerProfile profile)
        {
            profile = null;
            try
            {
                if (!File.Exists(path)) return false;
                var p = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                if (p == null || p.version != 1 || p.inventory == null || p.inventory.Length != 4 ||
                    p.stars == null || p.stars.Length != 10 || p.bestMoves == null || p.bestMoves.Length != 10 ||
                    p.bestSeconds == null || p.bestSeconds.Length != 10 || p.totalExperience < 0 ||
                    p.pickaxeTier < 0 || p.pickaxeTier > 4 || p.unlockedRooms < 1 || p.unlockedRooms > 10 ||
                    float.IsNaN(p.volume) || float.IsInfinity(p.volume)) return false;
                foreach (int n in p.inventory) if (n < 0) return false;
                for (int i = 0; i < 10; i++) if (p.stars[i] < 0 || p.stars[i] > 3 || p.bestMoves[i] < 0 ||
                    p.bestSeconds[i] < 0 || float.IsNaN(p.bestSeconds[i]) || float.IsInfinity(p.bestSeconds[i])) return false;
                p.volume = Mathf.Clamp01(p.volume); profile = p; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { return false; }
        }
        public bool Save(PlayerProfile profile)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path + ".tmp", JsonUtility.ToJson(profile, true));
                if (TryRead(Path, out _))
                    File.Replace(Path + ".tmp", Path, Path + ".bak");
                else
                {
                    // Preserve an unreadable original rather than backing it up over a good backup.
                    if (File.Exists(Path)) File.Move(Path, Path + ".corrupt-" + DateTime.UtcNow.Ticks);
                    File.Move(Path + ".tmp", Path);
                }
                Notice = null; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { Notice = "Не удалось сохранить прогресс. Проверь доступ к папке сохранений и повтори."; Debug.LogWarning(e.Message); return false; }
        }
        public static PlayerProfile Copy(PlayerProfile profile) => JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
    }

    public static class Crafting
    {
        public static string Requirement(PlayerProfile profile, CraftMineSettings settings, RecipeDefinition recipe)
        {
            if (recipe.pickaxeTier != profile.pickaxeTier + 1) return "Сначала создай предыдущую кирку";
            if (settings.Level(profile.totalExperience) < recipe.requiredLevel) return "Нужен уровень героя " + recipe.requiredLevel;
            for (int i = 0; i < 4; i++)
                if (profile.inventory[i] < recipe.cost[i]) return "Не хватает: " + CraftMineSettings.OreNames[i] + " × " + (recipe.cost[i] - profile.inventory[i]);
            return null;
        }
        public static bool TryCraft(PlayerProfile profile, CraftMineSettings settings, RecipeDefinition recipe)
        {
            if (Requirement(profile, settings, recipe) != null) return false;
            for (int i = 0; i < 4; i++) profile.inventory[i] -= recipe.cost[i];
            profile.pickaxeTier = recipe.pickaxeTier; return true;
        }
    }
}
