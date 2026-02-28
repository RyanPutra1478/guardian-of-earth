using UnityEngine;
using System.Collections.Generic;

namespace NinuNinu.Systems
{
    public enum DialogueSide { Left, Right }

    [System.Serializable]
    public struct DialogueLine
    {
        public string characterName;
        public DialogueSide side;
        [TextArea(3, 10)]
        public string text;
    }

    [CreateAssetMenu(fileName = "NewDialogue", menuName = "NinuNinu/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        public List<DialogueLine> lines;
    }
}
