using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    public class ForestStageLogic : BaseStageLogic
    {
        [Header("Forest Health Settings")]
        public float replantRecoveryBonus = 5f;

        [Header("Visuals")]
        public Color healthyForestColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        public Color deadForestColor = new Color(0.6f, 0.4f, 0.2f, 1f);

        [Header("UI References (Kalimantan Specific)")]
        public Text enemyCountText;
        public Text treeCountText;

        private LevelManager m_Manager;

        public override void Initialize(LevelManager manager)
        {
            m_Manager = manager;
            
            // Set Kalimantan Defaults if not configured in Inspector
            initialTrashCount = 0; // Forest stage has No Trash items (only Stumps as broken trees)
            if (fogColor == Color.gray) fogColor = new Color(0.4f, 0.35f, 0.3f); // Dusty brownish
            
            ApplyAtmosphere();
            
            // Set initial state
            m_Manager.contamination = 0f; // Start with healthy forest
        }

        public override void UpdateLogic(LevelManager manager)
        {
            m_Manager = manager;
            UpdateForestHealth();
        }

        public override void UpdateUI(LevelManager manager)
        {
            if (manager == null) return;

            // Update Enemy Count
            if (enemyCountText != null) enemyCountText.text = manager.GetEnemyCount().ToString();

            // Update Tree Count (Target: Number of trees to repair)
            if (treeCountText != null)
            {
                int brokenTrees = manager.GetBrokenCountByType(FacilityType.Tree);
                treeCountText.text = brokenTrees.ToString();
            }
        }


        public override void OnFacilityRepaired(LevelManager manager, BreakableFacility facility, bool successfullyRepaired)
        {
            if (facility.facilityType == FacilityType.Tree)
            {
                // Instant Recovery Bonus
                manager.ChangeContamination(-replantRecoveryBonus);
                Debug.Log($"[FOREST] Tree replanted! Instant recovery bonus: -{replantRecoveryBonus}");
            }
        }

        private void UpdateForestHealth()
        {
            if (m_Manager == null) return;

            // 1. Get counts for balance
            int totalTrees = m_Manager.GetTotalCountByType(FacilityType.Tree);
            int brokenTrees = m_Manager.GetBrokenCountByType(FacilityType.Tree);
            int healthyTrees = totalTrees - brokenTrees;

            // 2. Balancing factor: 
            // - Each broken tree causes degradation (e.g., 1.5 per second)
            // - Each healthy tree provides natural recovery (e.g., 0.2 per second)
            float degradation = brokenTrees * 1.5f;
            float recovery = healthyTrees * 0.2f;

            float netRate = degradation - recovery;

            m_Manager.contamination += netRate * Time.deltaTime;
            m_Manager.contamination = Mathf.Clamp(m_Manager.contamination, 0, m_Manager.maxContamination);

            // Dynamic Atmosphere (Fog)
            UpdateDynamicFog();

            if (m_Manager.contamination >= m_Manager.maxContamination)
            {
                m_Manager.TriggerGameOver(false);
            }
        }

        private void UpdateDynamicFog()
        {
            if (!useFog) return;

            float t = m_Manager.contamination / m_Manager.maxContamination;
            
            // Interpolate fog color and density
            RenderSettings.fogColor = Color.Lerp(fogColor, deadForestColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensity, fogDensity * 5f, t); // Fog gets thicker as forest dies
        }
    }
}
