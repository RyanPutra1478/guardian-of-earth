using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Helper component to automatically play click SFX when a UI Button is clicked.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonAudio : MonoBehaviour
    {
        private void Start()
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(PlayClickSound);
            }
        }

        private void PlayClickSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayClick();
            }
        }
    }
}
