using UnityEngine;
using UnityEngine.SceneManagement;

namespace NinuNinu.Systems
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Settings")]
        [Tooltip("Nama scene untuk pemilihan level (Map Indo)")]
        public string playSceneName = "StageSelection";

        /// <summary>
        /// Dipanggil saat tombol Play diklik.
        /// </summary>
        public void PlayGame()
        {
            Debug.Log("[MainMenu] Memuat Map Indo...");
            SceneManager.LoadScene(playSceneName);
        }

        /// <summary>
        /// Dipanggil saat tombol Quit diklik.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[MainMenu] Menutup Game...");
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        /// <summary>
        /// Dipanggil saat tombol Back di Stage Selection diklik.
        /// </summary>
        public void BackToHome()
        {
            Debug.Log("[MainMenu] Kembali ke Home...");
            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// Placeholder untuk fitur Settings di masa mendatang.
        /// </summary>
        public void OpenSettings()
        {
            Debug.Log("[MainMenu] Fitur Settings akan segera hadir!");
        }
    }
}
