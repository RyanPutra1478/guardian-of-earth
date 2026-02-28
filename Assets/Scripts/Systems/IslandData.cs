using UnityEngine;
using System.Collections.Generic;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Data container for island-specific information and its corresponding stages.
    /// Create these assets via Right Click > Create > NinuNinu > Island Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewIslandData", menuName = "NinuNinu/Island Data")]
    public class IslandData : ScriptableObject
    {
        public string islandName;
        
        [TextArea(2, 5)]
        public string description;

        [Tooltip("List of 5 scene names for stages 1 to 5.")]
        public List<string> stageScenes = new List<string>(5);
        
        public Sprite islandIcon;
    }
}
