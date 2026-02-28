using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Controls the stage selection popup UI.
    /// Updates its content based on the provided IslandData.
    /// </summary>
    public class StageSelectionPopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Button[] stageButtons;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject contentPanel;
        
        [Tooltip("The panel containing the stage buttons.")]
        [SerializeField] private GameObject selectionPanel;
        
        [Tooltip("The panel containing the 'Coming Soon' message.")]
        [SerializeField] private GameObject comingSoonPanel;

        private IslandData currentData;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        private void Start()
        {
            // Ensure closed at start
            if (contentPanel != null) contentPanel.SetActive(false);
        }

        public void Open(IslandData data)
        {
            if (data == null) return;

            currentData = data;
            if (titleText != null) titleText.text = data.islandName;
            
            if (contentPanel != null) contentPanel.SetActive(true);
            
            // Switch to selection view
            if (selectionPanel != null) selectionPanel.SetActive(true);
            if (comingSoonPanel != null) comingSoonPanel.SetActive(false);

            // Configure each button based on the data
            for (int i = 0; i < stageButtons.Length; i++)
            {
                if (stageButtons[i] == null) continue;

                int stageIndex = i; // Local copy for closure
                stageButtons[i].onClick.RemoveAllListeners();
                
                // Check if scene name exists for this stage
                if (i < data.stageScenes.Count && !string.IsNullOrEmpty(data.stageScenes[i]))
                {
                    stageButtons[i].interactable = true;
                    stageButtons[i].onClick.AddListener(() => LoadStage(stageIndex));
                }
                else
                {
                    // Disable button if no scene is assigned
                    stageButtons[i].interactable = false;
                }
            }
        }

        public void OpenComingSoon(string itemName)
        {
            if (titleText != null) titleText.text = itemName;
            
            if (contentPanel != null) contentPanel.SetActive(true);
            
            // Switch to coming soon view
            if (selectionPanel != null) selectionPanel.SetActive(false);
            if (comingSoonPanel != null) comingSoonPanel.SetActive(true);
        }

        public void Close()
        {
            if (contentPanel != null) contentPanel.SetActive(false);
        }

        private void LoadStage(int index)
        {
            if (currentData == null) return;
            
            string sceneName = currentData.stageScenes[index];
            Debug.Log($"[UI] Loading {currentData.islandName} Stage {index + 1}: {sceneName}");
            
            // Optional: Add loading screen trigger here
            SceneManager.LoadScene(sceneName);
        }
    }
}
