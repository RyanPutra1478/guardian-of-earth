using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Attached to an individual island button to trigger the stage selection popup.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class IslandButtonController : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The data for this specific island.")]
        public IslandData islandData;
        
        [Tooltip("Reference to the StageSelectionPopup manager in the scene.")]
        public StageSelectionPopup popupManager;

        private void Awake()
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(OnIslandClicked);
            }
        }

        private void OnIslandClicked()
        {
            if (popupManager != null)
            {
                if (islandData != null)
                {
                    popupManager.Open(islandData);
                }
                else
                {
                    // Show coming soon if data is missing
                    popupManager.OpenComingSoon(gameObject.name);
                }
            }
            else
            {
                Debug.LogWarning($"IslandButtonController on {gameObject.name}: PopupManager reference is missing!");
            }
        }
    }
}
