using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NinuNinu.Systems
{
    public class RiverStageLogic : BaseStageLogic
    {
        [Header("River Logic Settings")]
        public float baseContaminationIncrease = 0.5f;
        public float trashMultiplier = 0.1f;
        public float pipeMultiplier = 0.5f;
        public float filterMultiplier = 2.0f;
        public float filterCleaningMultiplier = 1.5f;

        [Header("Water Visuals")]
        public Material waterMaterial;
        public Color cleanColor = new Color(0f, 0.5f, 1f, 0.5f);
        public Color dirtyColor = new Color(0.3f, 0.2f, 0.1f, 0.8f);
        public float restorationBonusPerClean = 10f;
        public string colorPropertyName = "_BaseColor";

        [Header("UI References (Aceh Specific)")]
        public Text trashCountText;
        public Text enemyCountText;
        public Text facilityCountText;


        private LevelManager m_Manager;

        public override void Initialize(LevelManager manager)
        {
            m_Manager = manager;
            ApplyAtmosphere();
            
            // Instantiate material locally for isolation
            if (waterMaterial != null)
            {
                waterMaterial = new Material(waterMaterial);
                ApplyMaterialToScene();
            }
        }

        private void ApplyMaterialToScene()
        {
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer == -1)
            {
                Debug.LogWarning("[RIVER] 'Water' layer not found in project settings!");
                return;
            }

            // Find all water objects and apply the new instance
            MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
            foreach (var renderer in allRenderers)
            {
                if (renderer.gameObject.layer == waterLayer)
                {
                    renderer.material = waterMaterial;
                }
            }
            Debug.Log("[RIVER] Applied isolated material to water objects on layer 'Water'.");
        }

        public override void UpdateLogic(LevelManager manager)
        {
            m_Manager = manager;
            UpdateContamination();
        }

        public override void UpdateUI(LevelManager manager)
        {
            if (manager == null) return;

            // Update River UI
            if (trashCountText != null) trashCountText.text = manager.GetTrashCount().ToString();
            if (enemyCountText != null) enemyCountText.text = manager.GetEnemyCount().ToString();
            
            if (facilityCountText != null)
            {
                int total = manager.GetTotalFacilityCount();
                int broken = manager.GetBrokenFacilityCount();
                facilityCountText.text = $"{(total - broken)}/{total}";
            }
        }


        private void UpdateContamination()
        {
            if (m_Manager == null) return;

            // Hitung jumlah fasilitas rusak berdasarkan tipe
            int brokenPipes = 0;
            int brokenFilters = 0;

            foreach (var f in m_Manager.GetBrokenFacilities())
            {
                if (f.facilityName.ToLower().Contains("filter")) brokenFilters++;
                else brokenPipes++;
            }

            // 1. Hitung Polusi (Menambah Kontaminasi)
            float pollutionRate = baseContaminationIncrease 
                                + (m_Manager.GetTrashCount() * trashMultiplier)
                                + (brokenPipes * pipeMultiplier);
            
            // 2. Hitung Recovery (Mengurangi Kontaminasi)
            int workingFilters = m_Manager.GetTotalFilters() - brokenFilters;
            float cleaningRate = workingFilters * filterCleaningMultiplier;

            // 3. Laju Bersih (Net Change)
            float netRate = pollutionRate - cleaningRate;

            float currentContamination = m_Manager.contamination;
            currentContamination += netRate * Time.deltaTime;
            currentContamination = Mathf.Clamp(currentContamination, 0, m_Manager.maxContamination);
            m_Manager.contamination = currentContamination;

            UpdateWaterColor();

            if (m_Manager.contamination >= m_Manager.maxContamination)
            {
                m_Manager.TriggerGameOver(false);
            }
        }

        private void UpdateWaterColor()
        {
            if (waterMaterial != null)
            {
                float t = m_Manager.contamination / m_Manager.maxContamination;
                Color currentColor = Color.Lerp(cleanColor, dirtyColor, t);
                waterMaterial.SetColor(colorPropertyName, currentColor);
            }
        }
    }
}
