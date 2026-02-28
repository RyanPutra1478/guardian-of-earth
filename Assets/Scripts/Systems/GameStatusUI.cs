using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    public enum GameStatusMode
    {
        GameOver,
        Victory,
        Pause
    }

    public class GameStatusUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public Text titleText;
        public GameObject restartButton;
        public GameObject mainMenuButton;
        public GameObject nextLevelButton;
        public GameObject continueButton;

        [Header("Settings")]
        public string gameOverTitle = "GAME OVER";
        public string victoryTitle = "STAGE CLEAR";
        public string pauseTitle = "PAUSED";

        [Header("Audio")]
        public AudioClip victorySFX;
        public AudioClip gameOverSFX;

        /// <summary>
        /// Shows the panel with specific configuration based on mode.
        /// </summary>
        public void Show(GameStatusMode mode)
        {
            gameObject.SetActive(true);

            // Set Title and Play SFX
            switch (mode)
            {
                case GameStatusMode.GameOver:
                    titleText.text = gameOverTitle;
                    if (gameOverSFX != null && AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFX(gameOverSFX);
                    break;
                case GameStatusMode.Victory:
                    titleText.text = victoryTitle;
                    if (victorySFX != null && AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFX(victorySFX);
                    break;
                case GameStatusMode.Pause:
                    titleText.text = pauseTitle;
                    break;
            }

            // Configure Button Visibility
            restartButton.SetActive(true); // Always show Restart
            mainMenuButton.SetActive(true); // Always show Main Menu

            // Mode Specific Buttons
            nextLevelButton.SetActive(mode == GameStatusMode.Victory);
            continueButton.SetActive(mode == GameStatusMode.Pause);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
