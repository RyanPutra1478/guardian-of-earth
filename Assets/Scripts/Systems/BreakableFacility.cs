using UnityEngine;
using UnityEngine.Events;

namespace NinuNinu.Systems
{
    public enum FacilityType { Pipe, Filter, Tree, Vehicle, BirdCage }

    public class BreakableFacility : MonoBehaviour
    {
        [Header("Facility Settings")]
        public string facilityName = "Water Pipe";
        public FacilityType facilityType = FacilityType.Pipe;
        public bool isBroken = false;
        public float contaminationIncreasePerSecond = 2f;
        public float repairDuration = 3f;

        [Header("Visual Effects")]
        public GameObject brokenStatusUI; // UI Image/Icon yang muncul di atas objek
        public GameObject repairParticle;

        [Header("Events")]
        public UnityEvent onFacilityBroken;
        public UnityEvent onFacilityRepaired;

        private InteractableItem interactable;

        private void OnValidate()
        {
            // Update UI/State di editor saat variabel diubah manual
            UpdateState();

            // Sync with LevelManager if manual change happens during Play Mode
            if (Application.isPlaying && LevelManager.Instance != null)
            {
                if (isBroken) LevelManager.Instance.RegisterBrokenFacility(this);
                else LevelManager.Instance.UnregisterBrokenFacility(this);
            }
        }

        private void Start()
        {
            interactable = GetComponent<InteractableItem>();
            UpdateState();

            // Skenario awal: Jika objek ditaruh di map dalam keadaan rusak
            if (isBroken && LevelManager.Instance != null)
            {
                LevelManager.Instance.RegisterBrokenFacility(this);
            }
        }

        private void Update()
        {
            // Update kontaminasi sekarang ditangani secara terpusat oleh LevelManager
        }

        // Dipanggil oleh musuh atau trigger lingkungan
        public void BreakFacility()
        {
            if (isBroken) return;
            
            isBroken = true;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.RegisterBrokenFacility(this);
            }

            onFacilityBroken?.Invoke();
            UpdateState();
            Debug.Log($"{facilityName} has been broken!");
        }

        // Dipanggil lewat InteractableItem (Event onInteract)
        public void RepairFacility()
        {
            if (!isBroken) return;

            isBroken = false;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.UnregisterBrokenFacility(this);
            }

            onFacilityRepaired?.Invoke();
            UpdateState();

            if (repairParticle != null)
            {
                Instantiate(repairParticle, transform.position, Quaternion.identity);
            }

            Debug.Log($"{facilityName} has been repaired!");
        }

        private void UpdateState()
        {
            if (brokenStatusUI != null) brokenStatusUI.SetActive(isBroken);

            // Update InteractableItem data
            if (interactable != null)
            {
                interactable.interactionDuration = isBroken ? repairDuration : 0.1f;
                // Custom prompt based on type
                string actionPrefix = (facilityType == FacilityType.Tree) ? "Replant " : "Repair ";
                interactable.promptAction = isBroken ? actionPrefix + facilityName : "";
                // Hanya izinkan interaksi jika sedang rusak
                interactable.destroyOnInteract = false; 
            }
        }
    }
}
