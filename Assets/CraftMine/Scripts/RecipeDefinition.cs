using UnityEngine;

namespace CraftMine
{
    [CreateAssetMenu(menuName = "CraftMine/Recipe")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        public int pickaxeTier;
        public int requiredLevel;
        public int[] cost = new int[4];
    }
}
