using UnityEngine;
using UnityEngine.Events;

namespace NinuNinu.Systems
{
    public class InteractableItem : MonoBehaviour
    {
        [Header("Settings")]
        public string itemName = "Trash";
        public string promptAction = "Interact"; // Teks yang muncul di UI (misal: "Tangkap", "Perbaiki")
        public float interactionDuration = 0.5f; // Waktu yang dibutuhkan player untuk animasi aksi
        public UnityEvent onInteract;

        [Header("Collect Settings")]
        public bool destroyOnInteract = true;
        public GameObject collectEffect;

        public void Interact()
        {
            Debug.Log($"Interacting with {itemName}");
            onInteract?.Invoke();

            if (collectEffect != null)
            {
                Instantiate(collectEffect, transform.position, Quaternion.identity);
            }

            // Integrasi LevelManager: Jika ini adalah sampah, lapor agar kontaminasi turun
            if (LevelManager.Instance != null && itemName.ToLower().Contains("trash"))
            {
                LevelManager.Instance.UnregisterTrash(gameObject);
            }

            if (destroyOnInteract)
            {
                Destroy(gameObject);
            }
        }
    }
}
