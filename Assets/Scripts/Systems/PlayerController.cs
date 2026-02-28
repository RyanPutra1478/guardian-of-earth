using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace NinuNinu.Systems
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float rotationSpeed = 10f;
        public float rotationOffset = 0f; // Tambahkan ini untuk memutar model jika terbalik
        public float gravity = -9.81f;
        public float jumpHeight = 1.2f;
        public float interactionRange = 1.5f; // Diperkecil agar lebih akurat

        [Header("Animation Parameters")]
        public string isMovingParam = "isMoving";
        public string isActionParam = "isAction"; // Perubahan: Gunakan Bool untuk status Aksi
        public string actionTrigger = "Action";   // Tetap simpan Trigger untuk memulai cepat
        public string failTrigger = "Fail";
 
        public bool canControl = true;
        
        private bool mobileJumpPressed = false; // Flag untuk lompat dari mobile UI
        private float actionTimer = 0f;
        private bool isActionActive = false;
        private InteractableItem pendingInteractable; // Objek yang sedang diproses 

        private CharacterController controller;
        private Animator animator;
        private Vector3 velocity;
        private bool isGrounded;
        private Transform mainCamera;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            mainCamera = Camera.main.transform;
        }

        void Update()
        {
            if (!canControl)
            {
                // Berhenti total secara horizontal
                velocity.x = 0;
                velocity.z = 0;
                
                if (Time.frameCount % 100 == 0) Debug.Log("[PLAYER] Control is currently DISABLED.");

                if (animator != null)
                {
                    animator.applyRootMotion = false; // Hindari gerakan dari animasi
                    animator.SetBool(isMovingParam, false);
                    animator.SetBool(isActionParam, false);
                }
                
                // Masih terapkan gravitasi agar tidak melayang
                isGrounded = controller.isGrounded;
                if (isGrounded && velocity.y < 0) velocity.y = -2f;
                velocity.y += gravity * Time.deltaTime;
                controller.Move(velocity * Time.deltaTime);
                
                // Sembunyikan UI via LevelManager
                if (LevelManager.Instance != null && LevelManager.Instance.interactionUI != null) 
                    LevelManager.Instance.interactionUI.SetActive(false);
                
                return;
            }

            HandleMovement();
            UpdateInteractionUI();
        }

        private void UpdateInteractionUI()
        {
            if (LevelManager.Instance == null) return;

            GameObject ui = LevelManager.Instance.interactionUI;
            Text txt = LevelManager.Instance.interactionText;

            // Jika sedang beraksi, sembunyikan UI & Teks
            if (isActionActive)
            {
                if (ui != null) ui.SetActive(false);
                if (txt != null) txt.gameObject.SetActive(false);
                return;
            }

            // Cari objek terdekat
            InteractableItem target = GetNearestInteractable();
            if (target != null)
            {
                if (ui != null) ui.SetActive(true);
                if (txt != null)
                {
                    txt.gameObject.SetActive(true);
                    txt.text = target.promptAction;
                }
            }
            else
            {
                if (ui != null) ui.SetActive(false);
                if (txt != null) txt.gameObject.SetActive(false);
            }
        }

        // --- Mobile & UI Integration ---
        public void RequestJump()
        {
            if (!canControl) return;
            mobileJumpPressed = true;
        }

        [System.Obsolete("Gunakan LevelManager.OnMobileInteractionPressed untuk UI Button")]
        public void OnInteractionButtonPressed()
        {
            if (!canControl) return;
            TryInteractOrAction();
        }

        private void HandleMovement()
        {
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            // --- New Input System Polling ---
            Vector2 moveInput = Vector2.zero;
            bool jumpPressed = false;
            bool actionPressed = false;

            // Keyboard support
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveInput.y += 1;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveInput.y -= 1;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput.x -= 1;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput.x += 1;

                jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                actionPressed = Keyboard.current.eKey.wasPressedThisFrame;
            }

            // Gamepad support
            if (Gamepad.current != null)
            {
                moveInput += Gamepad.current.leftStick.ReadValue();
                if (Gamepad.current.buttonSouth.wasPressedThisFrame) jumpPressed = true;
                if (Gamepad.current.buttonWest.wasPressedThisFrame) actionPressed = true;
            }

            // Mobile UI support
            if (mobileJumpPressed)
            {
                jumpPressed = true;
                mobileJumpPressed = false; // Reset setelah dibaca
            }

            Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;

            // --- Logika Interupsi & Timer Aksi ---
            if (inputDir.magnitude >= 0.1f)
            {
                // Jika bergerak, batalkan aksi dan interaksi seketika
                isActionActive = false;
                actionTimer = 0;
                pendingInteractable = null; 
            }

            if (actionTimer > 0)
            {
                actionTimer -= Time.deltaTime;
                if (actionTimer <= 0)
                {
                    // Waktu habis, eksekusi efek interaksi jika tidak terganggu
                    if (isActionActive && pendingInteractable != null)
                    {
                        pendingInteractable.Interact();
                    }
                    isActionActive = false;
                    pendingInteractable = null;
                }
            }

            // Update status aksi di Animator
            if (animator != null) animator.SetBool(isActionParam, isActionActive);

            if (inputDir.magnitude >= 0.1f)
            {
                // Calculate camera-relative direction
                Vector3 forward = mainCamera.forward;
                Vector3 right = mainCamera.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                Vector3 moveDir = (forward * inputDir.z + right * inputDir.x).normalized;

                // Move
                controller.Move(moveDir * moveSpeed * Time.deltaTime);

                // Rotate
                Quaternion targetRotation = Quaternion.LookRotation(moveDir) * Quaternion.Euler(0, rotationOffset, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                if (animator != null) animator.SetBool(isMovingParam, true);
            }
            else
            {
                if (animator != null) animator.SetBool(isMovingParam, false);
            }

            // Jump
            if (jumpPressed && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Action Trigger
            if (actionPressed) TryInteractOrAction();


            // Gravity
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }


        public void TryInteractOrAction()
        {
            if (isActionActive) return; // Kunci: Jangan izinkan interaksi baru jika sedang beraksi

            // Cari apakah ada objek yang bisa diinteraksi
            InteractableItem target = GetNearestInteractable();
            
            if (target != null)
            {
                float duration = target.interactionDuration;
                pendingInteractable = target;

                // Jika durasi 0 (Tangkap Instan), eksekusi instan tanpa timer
                if (duration <= 0)
                {
                    Debug.Log($"[PLAYER] Interaction SUCCESS with: {target.itemName}");
                    pendingInteractable.Interact();
                    pendingInteractable = null;
                    if (animator != null) animator.SetTrigger(actionTrigger);
                    return;
                }
                
                PerformAction(duration);
            }
            else
            {
                Debug.LogWarning("[PLAYER] No interactable object found in range!");
            }
        }

        public void PerformAction(float duration)
        {
            isActionActive = true;
            actionTimer = duration;
            if (animator != null) animator.SetTrigger(actionTrigger);
        }

        private InteractableItem GetNearestInteractable()
        {
            // Gunakan OverlapSphere untuk mencari semua collider di sekitar
            Collider[] hitColliders = Physics.OverlapSphere(transform.position + transform.forward * 0.5f, interactionRange);
            float minDistance = Mathf.Infinity;
            InteractableItem nearest = null;

            foreach (var hitCollider in hitColliders)
            {
                // Cek di parent atau diri sendiri (Musuh biasanya Interactable-nya ada di Root)
                InteractableItem interactable = hitCollider.GetComponentInParent<InteractableItem>();
                
                if (interactable != null)
                {
                    float dist = Vector3.Distance(transform.position, interactable.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = interactable;
                    }
                }
            }
            return nearest;
        }

        // Hapus fungsi lama yang sudah tidak dipakai atau diganti namanya
        [System.Obsolete("Gunakan TryInteractOrAction")]
        public void PerformAction() { TryInteractOrAction(); }

        public void TriggerFail()
        {
            if (animator != null) animator.SetTrigger(failTrigger);
        }

        private void OnDrawGizmos()
        {
            // Visualisasi Jangkauan Interaksi Player (Putih)
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position + transform.forward * 0.5f, interactionRange);
        }
    }
}
