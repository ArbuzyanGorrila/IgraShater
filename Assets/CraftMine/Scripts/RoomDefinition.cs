using UnityEngine;

namespace CraftMine
{
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
}
