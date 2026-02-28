using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace NinuNinu.Systems
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("UI References")]
        public GameObject dialoguePanel; // The Main Panel object
        public GameObject dialogueCanvas; // The Root Canvas/Parent (Optional)
        public Text dialogueText;
        public GameObject nextIndicator;

        [Header("Name Tag References")]
        public GameObject nameTagLeft;
        public Text nameTagTextLeft;
        public GameObject nameTagRight;
        public Text nameTagTextRight;

        [Header("Settings")]
        public float typingSpeed = 0.05f;
        public AudioClip typingSFX;

        private Queue<DialogueLine> m_Lines = new Queue<DialogueLine>();
        private bool m_IsTyping = false;
        private string m_CurrentFullText = "";
        private System.Action m_OnCompleteCallback;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            
            // Initial hide
            HideAllUI();
        }

        public void StartDialogue(DialogueData data, System.Action onComplete = null)
        {
            if (data == null || data.lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            m_OnCompleteCallback = onComplete;
            m_Lines.Clear();

            foreach (var line in data.lines)
            {
                m_Lines.Enqueue(line);
            }

            if (dialogueCanvas != null) dialogueCanvas.SetActive(true);
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            
            DisplayNextLine();
        }

        public void DisplayNextLine()
        {
            if (m_IsTyping)
            {
                // Skip typing and show full text
                StopAllCoroutines();
                dialogueText.text = m_CurrentFullText;
                m_IsTyping = false;
                if (nextIndicator != null) nextIndicator.SetActive(true);
                return;
            }

            if (m_Lines.Count == 0)
            {
                EndDialogue();
                return;
            }

            DialogueLine line = m_Lines.Dequeue();
            m_CurrentFullText = line.text;
            
            // Handle Name Tag placement and Activation
            if (line.side == DialogueSide.Left)
            {
                if (nameTagLeft != null) nameTagLeft.SetActive(true);
                if (nameTagTextLeft != null) nameTagTextLeft.text = line.characterName;
                
                if (nameTagRight != null) nameTagRight.SetActive(false);
            }
            else
            {
                if (nameTagRight != null) nameTagRight.SetActive(true);
                if (nameTagTextRight != null) nameTagTextRight.text = line.characterName;
                
                if (nameTagLeft != null) nameTagLeft.SetActive(false);
            }

            StartCoroutine(TypeSentence(line.text));
        }

        IEnumerator TypeSentence(string sentence)
        {
            dialogueText.text = "";
            m_IsTyping = true;
            if (nextIndicator != null) nextIndicator.SetActive(false);

            int charCount = 0;
            foreach (char letter in sentence.ToCharArray())
            {
                dialogueText.text += letter;
                
                // Optimized: Play sound every 2 characters and skip spaces
                if (typingSFX != null && AudioManager.Instance != null && !char.IsWhiteSpace(letter))
                {
                    charCount++;
                    if (charCount % 2 == 0)
                    {
                        AudioManager.Instance.PlaySFX(typingSFX, 0.5f);
                    }
                }

                yield return new WaitForSeconds(typingSpeed);
            }

            m_IsTyping = false;
            if (nextIndicator != null) nextIndicator.SetActive(true);
        }

        private void EndDialogue()
        {
            Debug.Log("[DialogueManager] Ending Dialogue and Hiding UI.");
            HideAllUI();
            m_OnCompleteCallback?.Invoke();
        }

        private void HideAllUI()
        {
            if (dialogueCanvas != null) dialogueCanvas.SetActive(false);
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (nameTagLeft != null) nameTagLeft.SetActive(false);
            if (nameTagRight != null) nameTagRight.SetActive(false);
            if (nextIndicator != null) nextIndicator.SetActive(false);
            
            m_IsTyping = false;
        }

        private void Update()
        {
            // Input to advance dialogue (Space, Enter, or Mouse Click)
            if (dialoguePanel != null && dialoguePanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    DisplayNextLine();
                }
            }
        }
    }
}
