using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    public class PapuaStageLogic : BaseStageLogic
    {
        [Header("Papua UI References")]
        public Text birdCountText;
        public Text enemyCountText;
        public Text facilityCountText;

        [Header("Trap Spawning Area")]
        public GameObject cagePrefab;
        public Vector3 spawnAreaCenter;
        public Vector3 spawnAreaSize = new Vector3(20, 0, 20);
        public LayerMask groundLayer;

        [Header("Balance Settings")]
        public float degradationPerTrap = 1.0f; // Contamination increase per second per trap
        public float rescueRecoveryBonus = 10f; // Instant recovery when trap is cleared

        [Header("Atmosphere Settings Override")]
        public Color papuaFogColor = new Color(0.4f, 0.5f, 0.3f); // Misty Jungle

        private LevelManager m_Manager;

        public override void Initialize(LevelManager manager)
        {
            m_Manager = manager;
            
            // Apply theme-specific atmosphere
            fogColor = papuaFogColor;
            ApplyAtmosphere();

            SpawnInitialCages();

            Debug.Log("[PAPUA] Initialized Stage: Bird Rescue & Habitat Protection");
        }

        private void SpawnInitialCages()
        {
            if (cagePrefab == null)
            {
                Debug.LogWarning("[PAPUA] Missing Cage Prefab! No initial cages spawned.");
                return;
            }

            int spawnedCount = 0;
            int maxAttempts = initialTrashCount * 5; 
            int attempts = 0;

            while (spawnedCount < initialTrashCount && attempts < maxAttempts)
            {
                attempts++;
                
                float rx = Random.Range(spawnAreaCenter.x - spawnAreaSize.x * 0.5f, spawnAreaCenter.x + spawnAreaSize.x * 0.5f);
                float rz = Random.Range(spawnAreaCenter.z - spawnAreaSize.z * 0.5f, spawnAreaCenter.z + spawnAreaSize.z * 0.5f);
                
                // Start higher to ensure we hit even tall terrain
                Vector3 rayStart = new Vector3(rx, spawnAreaCenter.y + 50f, rz);

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, groundLayer))
                {
                    Vector3 finalPos = hit.point;

                    // Safe Spawn: Check overlap but IGNORE the ground layer
                    // We use ~groundLayer to only detect other objects (trees, other cages, etc.)
                    if (!Physics.CheckSphere(finalPos + Vector3.up * 0.5f, 0.8f, ~groundLayer)) 
                    {
                        GameObject cage = Instantiate(cagePrefab, finalPos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                        spawnedCount++;
                    }
                }
            }
            Debug.Log($"[PAPUA] Spawned {spawnedCount}/{initialTrashCount} cages in the area (after {attempts} attempts).");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f); // Orange for traps
            Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
        }

        public override void UpdateLogic(LevelManager manager)
        {
            m_Manager = manager;
            UpdateHabitatHealth();
        }

        private void UpdateHabitatHealth()
        {
            if (m_Manager == null) return;

            // Health is influenced by how many traps are active
            int activeTraps = m_Manager.GetBrokenFacilityCount();
            float netRate = activeTraps * degradationPerTrap;

            m_Manager.contamination += netRate * Time.deltaTime;
            m_Manager.contamination = Mathf.Clamp(m_Manager.contamination, 0, m_Manager.maxContamination);

            if (m_Manager.contamination >= m_Manager.maxContamination)
            {
                m_Manager.TriggerGameOver(false);
            }
        }

        public override void UpdateUI(LevelManager manager)
        {
            if (manager == null) return;

            // Update UI Counters
            if (birdCountText != null) 
            {
                int total = manager.GetTotalFacilityCount();
                int broken = manager.GetBrokenFacilityCount();
                int rescued = total - broken;
                birdCountText.text = $"{rescued}/{total}";
            }
            
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
                // Instant Recovery Bonus for destroying a trap or freeing a bird
                manager.ChangeContamination(-rescueRecoveryBonus);
                Debug.Log($"[PAPUA] Trap cleared! Habitat restored by {rescueRecoveryBonus}.");

                // Papua specific: Cages disappear after being released
                if (facility.facilityType == FacilityType.BirdCage)
                {
                    // Delay destruction slightly to allow particle/sound to play
                    Destroy(facility.gameObject, 0.2f);
                }
            }
        }

        // Uses default IsVictoryConditionMet (0 broken facilities, 0 enemies)
    }
}
