using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace NinuNinu.Systems
{

    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Stage Settings")]
        public BaseStageLogic activeStageLogic;
        public float timeLimit = 180f; // 3 menit
        
        [Header("Next Level Settings (Optional)")]
        public IslandData currentIslandData;
        public int currentStageIndex = 0; // 0-indexed (Stage 1 = 0)
        
        [Header("Spawn Settings")]
        public GameObject playerPrefab; // Prefab player jika ingin diinstantiate
        public Transform playerSpawnPoint;
        
        [Header("Enemy Settings")]
        public GameObject enemyPrefab;
        public List<Transform> enemySpawnPoints = new List<Transform>(); // Titik spesifik untuk musuh

        [Header("Trash Settings")]
        public GameObject trashPrefab;

        [Header("Contamination Logic (Global State)")]
        [Range(0, 100)]
        public float contamination = 20f;
        public float maxContamination = 100f;


        [Header("Recovery Settings (Global)")]
        private int totalFilters = 0; // Otomatis dihitung di Start

        [Header("UI References")]
        public Slider contaminationSlider;
        public Text timerText;
        
        [Header("Global UI References")]
        public GameObject interactionUI;
        public Text interactionText;
        public GameStatusUI statusUI;

        [Header("Victory Settings")]
        public float victoryDelay = 2.0f; // Jeda sebelum panel menang muncul
        public string mainMenuSceneName = "MainMenu";

        private List<GameObject> activeTrash = new List<GameObject>();
        private List<BreakableFacility> brokenFacilities = new List<BreakableFacility>();
        private List<GameObject> activeEnemies = new List<GameObject>(); // Tracking musuh
        private int totalFacilities = 0;
        private GameObject playerInstance;
        private bool isGameOver = false;
        private bool isPaused = false;
        private bool isDialogueActive = false;
        public bool IsGameOver => isGameOver;
        public bool IsPaused => isPaused;
        public bool IsDialogueActive => isDialogueActive;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // Pastikan timeScale reset jika baru restart
            Time.timeScale = 1f;
            isPaused = false;
            
            if (statusUI != null) statusUI.Hide();
        }

        private void Start()
        {
            if (activeStageLogic != null)
            {
                activeStageLogic.Initialize(this);

                // Handle Initial Dialogue
                if (activeStageLogic.stageDialogue != null && DialogueManager.Instance != null)
                {
                    isDialogueActive = true;
                    DialogueManager.Instance.StartDialogue(activeStageLogic.stageDialogue, () => {
                        isDialogueActive = false;
                        Debug.Log("Dialogue finished, game starting!");
                    });
                }
            }

            SetupPlayer();
            SpawnInitialEntities();

            // Hitung total fasilitas
            BreakableFacility[] allFacs = FindObjectsOfType<BreakableFacility>();
            totalFacilities = allFacs.Length;
            
            totalFilters = 0;
            foreach(var f in allFacs)
            {
                if (f.facilityType == FacilityType.Filter)
                    totalFilters++;
            }

            UpdateUI();
        }

        private void SetupPlayer()
        {
            if (playerSpawnPoint == null)
            {
                Debug.LogError("LevelManager: Player Spawn Point belum diisi!");
                return;
            }

            // 1. Cari atau Instansiasi Player
            playerInstance = GameObject.FindGameObjectWithTag("Player");
            
            if (playerInstance == null && playerPrefab != null)
            {
                playerInstance = Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
            }

            if (playerInstance != null)
            {
                // 2. Paksa pindah ke spawn point (Matikan controller sebentar agar tidak konflik posisi)
                CharacterController cc = playerInstance.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                playerInstance.transform.position = playerSpawnPoint.position;
                playerInstance.transform.rotation = playerSpawnPoint.rotation;
                
                if (cc != null) cc.enabled = true;

                // 3. Hubungkan Kamera
                if (Camera.main != null)
                {
                    CameraController cam = Camera.main.GetComponent<CameraController>();
                    if (cam != null)
                    {
                        cam.target = playerInstance.transform;
                        cam.SnapToTarget();
                    }
                }
            }
            else
            {
                Debug.LogError("LevelManager: Gagal menemukan atau membuat Player!");
            }
        }

        private void SpawnInitialEntities()
        {
            if (activeStageLogic == null) return;

            // --- Spawn Musuh di Titik Spesifik ---
            int spawnedEnemies = 0;
            for (int i = 0; i < enemySpawnPoints.Count && spawnedEnemies < activeStageLogic.initialEnemyCount; i++)
            {
                if (enemySpawnPoints[i] != null)
                {
                    GameObject enemy = Instantiate(enemyPrefab, enemySpawnPoints[i].position, enemySpawnPoints[i].rotation);
                    RegisterEnemy(enemy);
                    spawnedEnemies++;
                }
            }

            // --- Spawn Sisanya (sampah & sisa musuh) di Air secara acak ---
            if (MapGenerator.Instance == null || MapGenerator.Instance.waterPositions.Count == 0) return;

            List<Vector3> waters = new List<Vector3>(MapGenerator.Instance.waterPositions);
            
            // Sisa musuh jika enemyCount > spawn points
            while (spawnedEnemies < activeStageLogic.initialEnemyCount && waters.Count > 0)
            {
                int index = Random.Range(0, waters.Count);
                GameObject enemy = Instantiate(enemyPrefab, waters[index], Quaternion.identity);
                RegisterEnemy(enemy);
                waters.RemoveAt(index);
                spawnedEnemies++;
            }

            // Spawn Sampah
            for (int i = 0; i < activeStageLogic.initialTrashCount && waters.Count > 0; i++)
            {
                int index = Random.Range(0, waters.Count);
                Vector3 pos = waters[index];
                
                float offset = MapGenerator.Instance.blockSize * 0.5f;
                GameObject trash = Instantiate(trashPrefab, pos + Vector3.up * offset, Quaternion.identity);
                trash.transform.SetParent(MapGenerator.Instance.mapParent);
                
                // --- Dynamic Localization ---
                InteractableItem interact = trash.GetComponent<InteractableItem>();
                if (interact != null) interact.promptAction = activeStageLogic.trashPrompt;
                
                activeTrash.Add(trash);
                waters.RemoveAt(index);
            }
        }

        private void Update()
        {
            // Toggle Pause (New Input System)
            if (!isGameOver && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            if (isGameOver || isPaused || isDialogueActive) return;

            UpdateTimers();
            
            if (activeStageLogic != null)
            {
                activeStageLogic.UpdateLogic(this);
            }

            UpdateUI();
            CheckVictoryConditions();
        }

        private void CheckVictoryConditions()
        {
            if (isGameOver) return;

            bool victory = false;
            
            if (activeStageLogic != null)
            {
                // Use stage-specific logic
                victory = activeStageLogic.IsVictoryConditionMet(this);
            }
            else
            {
                // Fallback default
                victory = activeTrash.Count == 0 && brokenFacilities.Count == 0 && activeEnemies.Count == 0;
            }

            if (victory)
            {
                StartCoroutine(VictorySequence());
            }
        }

        private System.Collections.IEnumerator VictorySequence()
        {
            if (isGameOver) yield break;
            
            // Set logic flag manually if needed, but for now we just rely on EndGame to set isGameOver
            // To prevent multiple coroutines, we can check a temporary flag
            isGameOver = true; // Set here to stop Update calls
            
            Debug.Log("[VICTORY] Victory conditions met! Waiting for delay...");
            
            // But EndGame checks isGameOver! We need to bypass it or reset it.
            // Better: reset it right before calling EndGame.
            
            yield return new WaitForSeconds(victoryDelay);
            
            isGameOver = false; // Buka kunci sebentar agar EndGame bisa masuk
            EndGame(true);
        }

        private void UpdateTimers()
        {
            timeLimit -= Time.deltaTime;
            if (timeLimit <= 0)
            {
                timeLimit = 0;
                // Selalu kalah jika waktu habis sebelum tugas selesai
                EndGame(false);
            }
        }

        // Helper accessors for stage logic
        public List<BreakableFacility> GetBrokenFacilities() => brokenFacilities;
        public int GetTotalFilters() => totalFilters;

        public void TriggerGameOver(bool win)
        {
            EndGame(win);
        }


        // --- Clean Registry Methods ---
        public void RegisterTrash(GameObject trash) 
        { 
            if (!activeTrash.Contains(trash)) activeTrash.Add(trash); 
        }

        public void UnregisterTrash(GameObject trash) 
        { 
            activeTrash.Remove(trash); 
            ChangeContamination(-2f); // Bonus bersihkan sampah
        }

        public void RegisterBrokenFacility(BreakableFacility facility) 
        { 
            if (!brokenFacilities.Contains(facility)) brokenFacilities.Add(facility); 
        }

        public void UnregisterBrokenFacility(BreakableFacility facility, bool success = true) 
        { 
            brokenFacilities.Remove(facility); 
            if (activeStageLogic != null) activeStageLogic.OnFacilityRepaired(this, facility, success);
        }

        public void RegisterEnemy(GameObject enemy)
        {
            if (!activeEnemies.Contains(enemy)) activeEnemies.Add(enemy);
        }

        public void UnregisterEnemy(GameObject enemy)
        {
            activeEnemies.Remove(enemy);
        }

        public void ChangeContamination(float amount)
        {
            contamination = Mathf.Clamp(contamination + amount, 0, maxContamination);
        }

        private void UpdateUI()
        {
            if (activeStageLogic != null)
            {
                activeStageLogic.UpdateUI(this);
            }

            if (contaminationSlider != null) contaminationSlider.value = contamination / maxContamination;
            if (timerText != null) timerText.text = $"Time: {Mathf.CeilToInt(timeLimit)}s";
        }

        // --- Data Accessors for UI/Other Scripts ---
        public int GetTrashCount() => activeTrash.Count;
        public int GetBrokenFacilityCount() => brokenFacilities.Count;
        public int GetTotalFacilityCount() => totalFacilities;
        public int GetEnemyCount() => activeEnemies.Count;
        public float GetContaminationPercent() => (contamination / maxContamination) * 100f;

        // --- Typesafe Facility Counting ---
        public int GetTotalCountByType(FacilityType type)
        {
            int count = 0;
            BreakableFacility[] allFacs = FindObjectsOfType<BreakableFacility>();
            foreach (var f in allFacs)
            {
                if (f.facilityType == type) count++;
            }
            return count;
        }

        public int GetBrokenCountByType(FacilityType type)
        {
            int count = 0;
            foreach (var f in brokenFacilities)
            {
                if (f.facilityType == type) count++;
            }
            return count;
        }


        private void EndGame(bool win)
        {
            if (isGameOver) return;
            isGameOver = true;

            Debug.Log(win ? "STAGE CLEAR! River is safe." : "STAGE FAILED! Too much pollution.");

            // Matikan kontrol SEMUA player yang ada di scene
            PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
            foreach (var pc in allPlayers)
            {
                pc.canControl = false;
                Debug.Log($"[LEVEL] Control LOCKED for {pc.gameObject.name}");
                
                // Rekam ke instance lokal jika ini player utama
                if (pc.CompareTag("Player")) playerInstance = pc.gameObject;

                if (!win) pc.TriggerFail();
            }

            if (allPlayers.Length == 0)
            {
                Debug.LogWarning("[LEVEL] No PlayerController found to lock!");
            }

            // Tampilkan Panel sesuai hasil menggunakan Modular UI
            if (statusUI != null)
            {
                statusUI.Show(win ? GameStatusMode.Victory : GameStatusMode.GameOver);
            }
        }

        public void TogglePause()
        {
            if (isGameOver) return;

            isPaused = !isPaused;

            if (isPaused)
            {
                Time.timeScale = 0f;
                if (statusUI != null) statusUI.Show(GameStatusMode.Pause);
            }
            else
            {
                Time.timeScale = 1f;
                if (statusUI != null) statusUI.Hide();
            }
        }

        public void ContinueGame()
        {
            if (isPaused) TogglePause();
        }

        public void RestartLevel()
        {
            // Reset timeScale just in case (though Awake handles it)
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void LoadNextLevel()
        {
            if (currentIslandData == null)
            {
                Debug.LogWarning("[LevelManager] currentIslandData tidak diisi, kembali ke Main Menu.");
                LoadMainMenu();
                return;
            }

            int nextIndex = currentStageIndex + 1;
            
            if (nextIndex < currentIslandData.stageScenes.Count && !string.IsNullOrEmpty(currentIslandData.stageScenes[nextIndex]))
            {
                Time.timeScale = 1f;
                string nextScene = currentIslandData.stageScenes[nextIndex];
                Debug.Log($"[LevelManager] Loading Next Level: {nextScene}");
                SceneManager.LoadScene(nextScene);
            }
            else
            {
                Debug.Log("[LevelManager] Tidak ada stage selanjutnya di pulau ini. Kembali ke Main Menu.");
                LoadMainMenu();
            }
        }

        // --- Mobile UI Bridge ---
        public void OnMobileInteractionPressed()
        {
            if (isGameOver) return;
            
            // Cari player yang aktif
            if (playerInstance != null)
            {
                PlayerController pc = playerInstance.GetComponent<PlayerController>();
                if (pc != null && pc.canControl)
                {
                    pc.TryInteractOrAction();
                }
            }
        }

        public void OnMobileJumpPressed()
        {
            if (isGameOver) return;
            
            if (playerInstance != null)
            {
                PlayerController pc = playerInstance.GetComponent<PlayerController>();
                if (pc != null && pc.canControl)
                {
                    pc.RequestJump();
                }
            }
        }

        public void OnMobilePausePressed()
        {
            TogglePause();
        }

    }
}
