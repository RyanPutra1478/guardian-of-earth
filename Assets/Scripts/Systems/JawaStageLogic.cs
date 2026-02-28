using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NinuNinu.Systems
{
    [System.Serializable]
    public class LaneData
    {
        public Transform spawnPoint;
        public Transform stopPoint;
        public Transform exitPoint;
        [HideInInspector] public VehicleController currentVehicle;

        public bool IsFree => currentVehicle == null;
    }

    public class JawaStageLogic : BaseStageLogic
    {
        [Header("Jawa UI References")]
        public Text servicedCountText;

        [Header("Vehicle Settings")]
        public GameObject[] carPrefabs;
        public List<LaneData> lanes = new List<LaneData>();
        public float spawnInterval = 5f;
        public int targetVehicles = 10;
        public float serviceBonus = 15f;

        [Header("Pollution Settings")]
        public float passivePollutionRate = 0.5f;
        public Color smogColor = new Color(0.3f, 0.3f, 0.25f);

        private float m_SpawnTimer;
        private int m_VehiclesServiced;
        private int m_VehiclesSpawned;
        private LevelManager m_Manager;

        public override void Initialize(LevelManager manager)
        {
            m_Manager = manager;
            initialTrashCount = 0;
            initialEnemyCount = 0; 

            ApplyAtmosphere();
            m_Manager.contamination = 20f; // Start with base pollution
            m_SpawnTimer = spawnInterval; // Spawn first car immediately
        }

        public override void UpdateLogic(LevelManager manager)
        {
            m_Manager = manager;
            
            HandleSpawning();
            ApplyPassivePollution();
            UpdateAtmosphere();

            // Game Over if pollution reaches 100%
            if (m_Manager.contamination >= m_Manager.maxContamination)
            {
                m_Manager.TriggerGameOver(false);
            }
        }

        public override void UpdateUI(LevelManager manager)
        {
            if (manager == null) return;

            if (servicedCountText != null) 
                servicedCountText.text = $"{m_VehiclesServiced} / {targetVehicles}";
        }

        public override void OnFacilityRepaired(LevelManager manager, BreakableFacility facility, bool successfullyRepaired)
        {
            if (facility.facilityType == FacilityType.Vehicle)
            {
                if (successfullyRepaired)
                {
                    m_VehiclesServiced++;
                    manager.ChangeContamination(-serviceBonus);
                    Debug.Log($"[JAWA] Vehicle serviced! Total: {m_VehiclesServiced}/{targetVehicles}");
                }
                else
                {
                    // Failed service - count it towards spawned but not towards victory
                    // Contamination penalty is already handled by VehicleController calling manager.ChangeContamination(10f)
                    Debug.Log($"[JAWA] Vehicle timed out. Total serviced remains: {m_VehiclesServiced}/{targetVehicles}");
                }
            }
        }

        public override bool IsVictoryConditionMet(LevelManager manager)
        {
            // Win only if target is reached AND no broken vehicles are left on the road
            return m_VehiclesServiced >= targetVehicles && manager.GetBrokenFacilityCount() == 0;
        }

        private void HandleSpawning()
        {
            if (m_Manager.IsGameOver || m_VehiclesSpawned >= targetVehicles) return;

            m_SpawnTimer += Time.deltaTime;
            if (m_SpawnTimer >= spawnInterval)
            {
                m_SpawnTimer = 0;
                TrySpawnVehicle();
            }
        }

        private void ApplyPassivePollution()
        {
            if (m_Manager.IsGameOver) return;
            m_Manager.contamination += passivePollutionRate * Time.deltaTime;
        }

        private void TrySpawnVehicle()
        {
            if (m_VehiclesSpawned >= targetVehicles) return;

            List<LaneData> freeLanes = lanes.FindAll(l => l.IsFree);
            if (freeLanes.Count == 0 || carPrefabs.Length == 0) return;

            LaneData selectedLane = freeLanes[Random.Range(0, freeLanes.Count)];
            GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];

            GameObject carObj = Instantiate(prefab, selectedLane.spawnPoint.position, selectedLane.spawnPoint.rotation);
            VehicleController vc = carObj.GetComponent<VehicleController>();
            
            if (vc != null)
            {
                vc.SetupLane(selectedLane, selectedLane.spawnPoint.position, selectedLane.stopPoint.position, selectedLane.exitPoint.position);
                selectedLane.currentVehicle = vc;
                m_VehiclesSpawned++;
            }
        }

        private void UpdateAtmosphere()
        {
            if (!useFog) return;

            float t = m_Manager.contamination / m_Manager.maxContamination;
            RenderSettings.fogColor = Color.Lerp(fogColor, smogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensity, fogDensity * 4f, t);
        }
    }
}
