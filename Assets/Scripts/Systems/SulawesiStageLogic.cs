using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    public class SulawesiStageLogic : BaseStageLogic
    {
        [Header("Sulawesi UI References")]
        public Text trashCountText;
        public Text enemyCountText;
        public Text facilityCountText;

        [Header("Trash Spawning Area")]
        public GameObject trashPrefab;
        public Vector3 spawnAreaCenter;
        public Vector3 spawnAreaSize = new Vector3(20, 0, 20);
        public LayerMask groundLayer;

        [Header("Atmosphere Settings Override")]
        public Color sulawesiFogColor = new Color(0.6f, 0.7f, 0.8f); // Coastal/Blueish

        [Header("Contamination Settings")]
        public float baseContaminationIncrease = 0.2f;
        public float trashMultiplier = 0.1f;
        public float enemyMultiplier = 0.3f;

        private LevelManager m_Manager;

        public override void Initialize(LevelManager manager)
        {
            m_Manager = manager;
            
            // Apply theme-specific atmosphere
            fogColor = sulawesiFogColor;
            ApplyAtmosphere();

            SpawnInitialTrash();

            Debug.Log("[SULAWESI] Initialized Stage: Coastal Cleanup");
        }

        private void SpawnInitialTrash()
        {
            if (trashPrefab == null)
            {
                Debug.LogWarning("[SULAWESI] Missing Trash Prefab! No initial trash spawned.");
                return;
            }

            int spawnedCount = 0;
            int maxAttempts = initialTrashCount * 5;
            int attempts = 0;

            while (spawnedCount < initialTrashCount && attempts < maxAttempts)
            {
                attempts++;
                
                // Pick random XZ within the area
                float rx = Random.Range(spawnAreaCenter.x - spawnAreaSize.x * 0.5f, spawnAreaCenter.x + spawnAreaSize.x * 0.5f);
                float rz = Random.Range(spawnAreaCenter.z - spawnAreaSize.z * 0.5f, spawnAreaCenter.z + spawnAreaSize.z * 0.5f);
                
                Vector3 spawnPos = new Vector3(rx, spawnAreaCenter.y + 10f, rz);

                if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 20f, groundLayer))
                {
                    Vector3 finalPos = hit.point;

                    // Safe Spawn: Check if area is clear within 1.0f radius, ignoring ground
                    if (!Physics.CheckSphere(finalPos + Vector3.up * 0.5f, 0.8f, ~groundLayer)) 
                    {
                        GameObject trash = Instantiate(trashPrefab, finalPos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                        spawnedCount++;
                        
                        if (m_Manager != null)
                        {
                            m_Manager.RegisterTrash(trash);
                        }
                    }
                }
            }
            Debug.Log($"[SULAWESI] Spawned {spawnedCount} trash items after {attempts} attempts.");
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize spawn area in Inspector
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
        }

        public override void UpdateLogic(LevelManager manager)
        {
            m_Manager = manager;
            UpdateContamination();
        }

        private void UpdateContamination()
        {
            if (m_Manager == null) return;

            // Calculate Pollution increase
            float pollutionRate = baseContaminationIncrease 
                                + (m_Manager.GetTrashCount() * trashMultiplier)
                                + (m_Manager.GetEnemyCount() * enemyMultiplier);

            // In Sulawesi, contamination only goes UP unless user cleans it
            // (Standard behavior for cleanup stages)
            m_Manager.ChangeContamination(pollutionRate * Time.deltaTime);

            if (m_Manager.contamination >= m_Manager.maxContamination)
            {
                m_Manager.TriggerGameOver(false);
            }
        }

        public override void UpdateUI(LevelManager manager)
        {
            if (manager == null) return;

            // Update UI Counters
            if (trashCountText != null) 
                trashCountText.text = manager.GetTrashCount().ToString();
            
            if (enemyCountText != null) 
                enemyCountText.text = manager.GetEnemyCount().ToString();
            
            if (facilityCountText != null)
            {
                int total = manager.GetTotalFacilityCount();
                int broken = manager.GetBrokenFacilityCount();
                facilityCountText.text = $"{(total - broken)}/{total}";
            }
        }

        public override void OnFacilityRepaired(LevelManager manager, BreakableFacility facility, bool successfullyRepaired)
        {
            if (successfullyRepaired)
            {
                // Optional: Instant contamination reward for repairing facilities in Sulawesi
                manager.ChangeContamination(-5f);
                Debug.Log($"[SULAWESI] Facility repaired! Bonus recovery applied.");
            }
        }

        // Uses default IsVictoryConditionMet from BaseStageLogic (0 trash, 0 enemies, 0 broken)
    }
}
