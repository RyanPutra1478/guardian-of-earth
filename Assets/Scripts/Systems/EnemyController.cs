using UnityEngine;
using UnityEngine.AI;

namespace NinuNinu.Systems
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 3.5f;
        public float acceleration = 8f;
        public float stoppingDistance = 1.2f;
        public float rotationOffset = 0f; // New: Offset untuk memutar model jika terbalik

        [Header("Sabotage Settings")]
        public float sabotageDuration = 2f;
        public float searchInterval = 2f; // Seberapa sering mencari target baru

        [Header("Flee Settings")]
        public float detectionRange = 5f;
        public float fleeSpeed = 6f;
        public float fleeDistance = 8f;

        [Header("Trash Throwing Settings")]
        public GameObject trashPrefab;
        public float throwInterval = 10f; // Lempar sampah tiap 10 detik
        public float trashVisualYOffset = 0f; // Offset ketinggian sampah
        public LayerMask waterLayer; // Layer untuk mendeteksi air
        private float nextThrowTime;
        private bool isThrowingTrash = false;
        private bool isSearchingForWater = false;
        private Vector3 waterDestination;
        private float throwTimer = 0f;

        [Header("Animation Parameters (Same as Player)")]
        public string isMovingParam = "isMoving";
        public string isActionParam = "isAction";
        public string actionTrigger = "Action";
        public string failTrigger = "Fail";
        public float captureDelay = 1.5f; // Jeda sebelum menghilang (detik)
        
        [Header("Modular AI")]
        public BaseEnemyBehavior activeBehavior;

        private NavMeshAgent agent;
        private Animator animator;
        private BreakableFacility targetFacility;
        private Transform player;
        private bool isSabotaging = false;
        private bool isFleeing = false;
        private bool isCaught = false; // New: Flag untuk cegah double capture
        private float sabotageTimer = 0f;
        private float searchTimer = 0f;

        void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            
            if (agent == null) Debug.LogError($"{gameObject.name} is missing NavMeshAgent!");
            if (animator == null) Debug.LogWarning($"{gameObject.name} is missing Animator in children!");

            // --- Pastikan ada Collider agar bisa ditangkap Player ---
            if (GetComponent<Collider>() == null)
            {
                CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 1f, 0); // Atur tengahnya (asumsi tinggi manusia)
                col.radius = 0.5f;
                col.height = 2f;
                Debug.Log($"{gameObject.name}: Added CapsuleCollider automatically for interaction.");
            }

            // --- Pastikan ada InteractableItem agar bisa memunculkan menu 'E' ---
            InteractableItem interactable = GetComponent<InteractableItem>();
            if (interactable != null)
            {
                interactable.destroyOnInteract = false; // Musuh jangan hilang instan, tunggu animasi fail selesai
                interactable.promptAction = "Capture!";
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: Missing InteractableItem! You won't be able to press 'E' to catch this enemy.");
            }

            agent.speed = moveSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = stoppingDistance;
            agent.updateRotation = false; // Disable auto-rotation so we can use rotationOffset

            nextThrowTime = Time.time + throwInterval + Random.Range(0, 5f);

            // --- Modular AI Initialization ---
            if (activeBehavior == null) activeBehavior = GetComponent<BaseEnemyBehavior>();
            if (activeBehavior != null) activeBehavior.Initialize(this);

            // --- Snap to NavMesh on Start ---
            SnapToNavMesh();
        }

        private void SnapToNavMesh()
        {
            NavMeshHit hit;
            // Tingkatkan radius pencarian (misal musuh ada di atas air, NavMesh ada di dasar)
            if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"{gameObject.name} snapped to NavMesh at {hit.position}");
            }
            else
            {
                Debug.LogWarning($"{gameObject.name} could not find NavMesh nearby! Check your Bake.");
            }
        }

        void Update()
        {
            if (agent == null || isCaught) return;

            if (!agent.isOnNavMesh)
            {
                // Tampilkan pesan sekali-sekali agar tidak spam console
                if (Time.frameCount % 60 == 0) 
                    Debug.LogWarning($"{gameObject.name} is NOT on NavMesh. Make sure NavMesh is baked and enemy is touching the blue area.");
                return;
            }

            if (!agent.isActiveAndEnabled) return;

            CheckForPlayer();

            if (isThrowingTrash)
            {
                HandleThrowingTrash();
            }
            else if (isFleeing)
            {
                HandleFleeing();
            }
            else if (isSearchingForWater)
            {
                HandleWaterSeeking();
            }
            else if (isSabotaging)
            {
                HandleSabotage();
            }
            else
            {
                HandleMovement();
                CheckThrowTimer();
            }

            UpdateAnimator();
        }

        private void CheckThrowTimer()
        {
            if (Time.time >= nextThrowTime && trashPrefab != null)
            {
                if (IsOverWater())
                {
                    StartThrowingTrash();
                }
                else
                {
                    FindNearestWater();
                }
            }
        }

        private void FindNearestWater()
        {
            if (MapGenerator.Instance == null || MapGenerator.Instance.waterPositions.Count == 0)
            {
                nextThrowTime = Time.time + 5f; // Coba lagi nanti kalau tidak ada air
                return;
            }

            float minDistance = Mathf.Infinity;
            Vector3 nearestWater = Vector3.zero;
            bool found = false;

            foreach (Vector3 waterPos in MapGenerator.Instance.waterPositions)
            {
                float dist = Vector3.Distance(transform.position, waterPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestWater = waterPos;
                    found = true;
                }
            }

            if (found)
            {
                isSearchingForWater = true;
                waterDestination = nearestWater;
                agent.isStopped = false;
                agent.SetDestination(waterDestination);
                Debug.Log($"{gameObject.name} is looking for nearest water at {waterDestination}");
            }
            else
            {
                nextThrowTime = Time.time + 5f;
            }
        }

        private void HandleWaterSeeking()
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (IsOverWater())
                {
                    isSearchingForWater = false;
                    StartThrowingTrash();
                }
                else
                {
                    // Kalau sampai tapi belum di air (mungkin meleset sedikit), coba lapor
                    isSearchingForWater = false;
                    nextThrowTime = Time.time + 2f;
                }
            }
        }

        private bool IsOverWater()
        {
            RaycastHit hit;
            // Tembakkan ray ke bawah untuk mencari layer air
            return Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 3f, waterLayer);
        }

        private void StartThrowingTrash()
        {
            isThrowingTrash = true;
            throwTimer = 1.5f; // Durasi animasi buang sampah
            agent.isStopped = true;
            
            if (animator != null) animator.SetTrigger(actionTrigger);
            Debug.Log($"{gameObject.name} is throwing trash!");
        }

        private void HandleThrowingTrash()
        {
            throwTimer -= Time.deltaTime;
            if (throwTimer <= 0)
            {
                ThrowTrash();
                isThrowingTrash = false;
                agent.isStopped = false;
                nextThrowTime = Time.time + throwInterval + Random.Range(0, 5f);
            }
        }

        private void ThrowTrash()
        {
            if (trashPrefab != null)
            {
                float gridSize = MapGenerator.Instance != null ? MapGenerator.Instance.blockSize : 1f;

                // Munculkan sampah di depan musuh sedikit
                Vector3 spawnPos = transform.position + transform.forward * 0.5f;
                
                // Snap ke grid agar rapi seperti generator
                float snappedX = Mathf.Floor(spawnPos.x / gridSize) * gridSize + (gridSize * 0.5f);
                float snappedZ = Mathf.Floor(spawnPos.z / gridSize) * gridSize + (gridSize * 0.5f);
                
                Vector3 finalPos = new Vector3(snappedX, transform.position.y + trashVisualYOffset, snappedZ);

                GameObject trash = Instantiate(trashPrefab, finalPos, Quaternion.identity);

                // Masukkan ke container map agar rapi di Hierarchy
                if (MapGenerator.Instance != null && MapGenerator.Instance.mapParent != null)
                {
                    trash.transform.SetParent(MapGenerator.Instance.mapParent);
                }

                // Register ke LevelManager agar kontaminasi naik
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.RegisterTrash(trash);
                }

                // Pastikan sampah punya collider agar bisa diambil/interaksi
                BoxCollider col = trash.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = trash.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.5f, 0.5f, 0.5f); // Ukuran standar sampah
                }
                col.isTrigger = true; // Set trigger agar player bisa lewat & interaksi
            }
        }

        private void CheckForPlayer()
        {
            if (player == null) return;

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer < detectionRange)
            {
                if (!isFleeing) StartFleeing();
            }
            else if (isFleeing && distanceToPlayer > detectionRange * 1.5f)
            {
                StopFleeing();
            }
        }

        private void StartFleeing()
        {
            isFleeing = true;
            isSabotaging = false; // Batalkan sabotase jika sedang lari
            isThrowingTrash = false; // Batalkan buang sampah jika player datang
            isSearchingForWater = false; // Batalkan cari air jika player datang
            
            agent.isStopped = false;
            agent.speed = fleeSpeed;
            Debug.Log($"{gameObject.name} is fleeing from player!");
        }

        private void StopFleeing()
        {
            isFleeing = false;
            agent.speed = moveSpeed;
            Debug.Log($"{gameObject.name} stopped fleeing.");
        }

        private void HandleFleeing()
        {
            if (player == null) return;

            // Arah dari player ke musuh (arah menjauh)
            Vector3 awayFromPlayer = (transform.position - player.position).normalized;
            
            // Daftar sudut yang akan dicoba (dalam derajat)
            // 0 = langsung menjauh, 45/-45 = serong, 90/-90 = samping
            float[] anglesToTry = { 0f, 45f, -45f, 90f, -90f };
            
            foreach (float angle in anglesToTry)
            {
                // Putar arah menjauh sebesar angle
                Vector3 searchDirection = Quaternion.Euler(0, angle, 0) * awayFromPlayer;
                Vector3 targetPos = transform.position + searchDirection * fleeDistance;

                NavMeshHit hit;
                // 1. Cek apakah posisi ada di NavMesh
                if (NavMesh.SamplePosition(targetPos, out hit, 2.0f, NavMesh.AllAreas))
                {
                    // 2. Cek apakah jalannya valid (tidak terputus tembok)
                    NavMeshPath path = new NavMeshPath();
                    if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        agent.SetDestination(hit.position);
                        return; // Berhasil menemukan jalan lari!
                    }
                }
            }

            // Jika semua sudut buntu, lari ke posisi random terjauh yang terdeteksi
            Debug.LogWarning($"{gameObject.name} is STUCK! No flee path found.");
        }

        private void HandleMovement()
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0 || targetFacility == null || targetFacility.isBroken)
            {
                FindNearestTarget();
                searchTimer = searchInterval;
            }

            if (targetFacility != null)
            {
                agent.isStopped = false;
                
                // Cari titik terdekat di NavMesh dari posisi fasilitas
                // Radius diperbesar (5.0f) karena fasilitas (pipa) mungkin ada di lantai 2 sementara NavMesh di lantai 1
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetFacility.transform.position, out hit, 5.0f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    
                    // Diagnosa Jalur (Hanya muncul jika ganti target atau ada error)
                    if (agent.pathStatus == NavMeshPathStatus.PathPartial || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    {
                        if (Time.frameCount % 100 == 0)
                            Debug.LogWarning($"{gameObject.name}: Path to {targetFacility.facilityName} is {agent.pathStatus}! Check NavMesh gaps.");
                    }
                }
                else
                {
                    if (Time.frameCount % 100 == 0)
                        Debug.LogError($"{gameObject.name}: Cannot find NavMesh point near {targetFacility.facilityName}");
                }

                // Cek jika sampai di target
                // Gunakan jarak horizontal (planar) agar lebih akurat di grid
                Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
                Vector3 flatTarget = new Vector3(targetFacility.transform.position.x, 0, targetFacility.transform.position.z);
                
                if (!agent.pathPending && Vector3.Distance(flatPos, flatTarget) <= agent.stoppingDistance + 0.2f)
                {
                    StartSabotage();
                }
            }
            else
            {
                // Jika tidak ada target, jalan santai (Wander) ke arah acak
                agent.isStopped = false;
                if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath))
                {
                    if (activeBehavior != null) activeBehavior.IdleWander();
                    else Wander();
                }
            }
        }
        public void Wander()
        {
            Vector3 randomDirection = Random.insideUnitSphere * 8f;
            randomDirection += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, 8f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        private void FindNearestTarget()
        {
            BreakableFacility[] facilities = FindObjectsOfType<BreakableFacility>();
            
            if (activeBehavior != null)
            {
                targetFacility = activeBehavior.FindBestTarget(facilities);
            }
            else
            {
                // Fallback to simple distance check if no behavior assigned
                float minDistance = Mathf.Infinity;
                targetFacility = null;
                foreach (var f in facilities)
                {
                    if (f.isBroken) continue;
                    float dist = Vector3.Distance(transform.position, f.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        targetFacility = f;
                    }
                }
            }

            if (targetFacility != null)
                Debug.Log($"{gameObject.name} found target: {targetFacility.facilityName} (Type: {targetFacility.facilityType})");
            else
                Debug.Log($"{gameObject.name} no unbroken facilities found. Wandering...");
        }

        private void StartSabotage()
        {
            // Pastikan jarak benar-benar dekat sebelum mulai animasi
            float dist = Vector3.Distance(transform.position, targetFacility.transform.position);
            if (dist > stoppingDistance + 0.5f) 
            {
                Debug.Log($"{gameObject.name} tried to sabotage but too far: {dist}");
                return;
            }

            Debug.Log($"{gameObject.name} START Sabotage on {targetFacility.facilityName}");
            isSabotaging = true;
            sabotageTimer = sabotageDuration;
            agent.isStopped = true;

            // Putar musuh menghadap ke arah Pipa
            Vector3 lookPos = targetFacility.transform.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);
            transform.Rotate(0, rotationOffset, 0);

            if (animator != null) animator.SetTrigger(actionTrigger);
        }

        private void OnDrawGizmos()
        {
            if (agent != null && agent.hasPath)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, agent.destination);
                Gizmos.DrawSphere(agent.destination, 0.3f);
            }

            // Visualisasi Jangkauan Deteksi (Kuning) - Musuh kabur jika player masuk sini
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        private void HandleSabotage()
        {
            sabotageTimer -= Time.deltaTime;
            
            if (sabotageTimer <= 0)
            {
                // Sabotase Selesai
                if (targetFacility != null)
                {
                    targetFacility.BreakFacility();
                }
                
                isSabotaging = false;
                agent.isStopped = false;
                targetFacility = null; // Cari target baru setelah ini
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            // Gerak (Gunakan velocity NavMeshAgent)
            bool moving = agent.velocity.magnitude > 0.1f && !agent.isStopped;
            animator.SetBool(isMovingParam, moving);
            
            // Perbaiki Rotasi Model jika terbalik
            if (moving)
            {
                Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized) * Quaternion.Euler(0, rotationOffset, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }

            // Aksi
            animator.SetBool(isActionParam, isSabotaging);
        }

        // Tahap 1: Dipanggil saat Player menekan 'E' (lewat InteractableItem Event)
        public void BeCaught()
        {
            if (isCaught) return;
            isCaught = true;

            Debug.Log($"[ENEMY] {gameObject.name} is caught! Playing fail animation...");
            
            // Hentikan semua gerakan
            isFleeing = false;
            isSabotaging = false;
            isThrowingTrash = false;
            isSearchingForWater = false;
            
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // Matikan collider dan interaksi agar tidak bisa ditangkap 2x saat animasi
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            InteractableItem interactable = GetComponent<InteractableItem>();
            if (interactable != null) interactable.enabled = false;

            // Jalankan animasi Fail (Misal: musuh terjatuh atau kaget)
            if (animator != null)
            {
                animator.SetBool(isMovingParam, false);
                animator.SetTrigger(failTrigger);
            }
        }

        // Tahap 2: Dipanggil lewat Animation Event di frame terakhir animasi fail
        public void FinalizeCapture()
        {
            if (!isCaught) return; // Kunci: Jangan izinkan finalize jika tidak benar-benar tertangkap

            Debug.Log($"[ENEMY] {gameObject.name} finalized capture. Disappearing in {captureDelay}s.");
            
            // Hadiah: Mengurangi kontaminasi
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ChangeContamination(-5f);
                LevelManager.Instance.UnregisterEnemy(gameObject);
            }

            // Hancurkan objek dengan jeda tambahan agar terlihat di tanah sebentar
            Destroy(gameObject, captureDelay);
        }
    }
}
