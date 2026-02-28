using UnityEngine;
using System.Collections;

namespace NinuNinu.Systems
{
    public class VehicleController : MonoBehaviour
    {
        public enum VehicleState { Spawning, MovingToStop, Waiting, Leaving, Destroyed }

        [Header("References")]
        public BreakableFacility facility;
        public MeshRenderer bodyRenderer;
        public string colorPropertyName = "_BaseColor";
        public GameObject smokeEffect;

        [Header("Movement Settings")]
        public float moveSpeed = 8f;
        public float leaveSpeedMultiplier = 1.5f;
        public float waitDuration = 10f;
        public float currentWaitTime;

        [Header("Visual Adjustment")]
        public Vector3 rotationOffset = new Vector3(0, 0, 0);

        [HideInInspector] public LaneData laneReference;
        private Vector3 m_StartPoint;
        private Vector3 m_StopPoint;
        private Vector3 m_ExitPoint;
        private VehicleState m_State = VehicleState.Spawning;

        private void Start()
        {
            if (facility == null) facility = GetComponent<BreakableFacility>();
            
            // Randomize Color
            if (bodyRenderer != null)
            {
                Material instanceMat = new Material(bodyRenderer.sharedMaterial);
                instanceMat.SetColor(colorPropertyName, new Color(Random.value, Random.value, Random.value));
                bodyRenderer.material = instanceMat;
            }

            if (smokeEffect != null) smokeEffect.SetActive(false);
        }

        public void SetupLane(LaneData lane, Vector3 start, Vector3 stop, Vector3 exit)
        {
            laneReference = lane;
            m_StartPoint = start;
            m_StopPoint = stop;
            m_ExitPoint = exit;
            
            transform.position = m_StartPoint;
            m_State = VehicleState.MovingToStop;

            // Snap rotation immediately
            Vector3 direction = (m_StopPoint - m_StartPoint).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(rotationOffset);
            }
        }

        private void Update()
        {
            switch (m_State)
            {
                case VehicleState.MovingToStop:
                    MoveTowards(m_StopPoint, moveSpeed, () => {
                        m_State = VehicleState.Waiting;
                        currentWaitTime = waitDuration;
                        if (facility != null) facility.BreakFacility(); // Mark as needing service
                    });
                    break;

                case VehicleState.Waiting:
                    HandleWait();
                    break;

                case VehicleState.Leaving:
                    MoveTowards(m_ExitPoint, moveSpeed * leaveSpeedMultiplier, () => {
                        m_State = VehicleState.Destroyed;
                        Destroy(gameObject);
                    });
                    break;
            }
        }

        private void MoveTowards(Vector3 target, float speed, System.Action onReached)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            
            // Look rotation with offset
            Vector3 direction = (target - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot * Quaternion.Euler(rotationOffset), 10f * Time.deltaTime);
            }

            if (Vector3.Distance(transform.position, target) < 0.1f)
            {
                onReached?.Invoke();
            }
        }

        private void HandleWait()
        {
            if (facility != null && !facility.isBroken)
            {
                // Repaired! Start leaving immediately
                StartLeaving(false);
                return;
            }

            currentWaitTime -= Time.deltaTime;
            if (currentWaitTime <= 0)
            {
                // Timeout!
                StartLeaving(true);
                
                if (LevelManager.Instance != null)
                {
                    // Penalty for not servicing
                    LevelManager.Instance.ChangeContamination(10f);
                    // Also unregister since it's leaving broken
                    LevelManager.Instance.UnregisterBrokenFacility(facility, false);
                }
            }
        }

        private void StartLeaving(bool isFailed)
        {
            if (m_State == VehicleState.Leaving) return;

            m_State = VehicleState.Leaving;
            if (smokeEffect != null) smokeEffect.SetActive(isFailed);

            // Disable interaction
            var interactable = GetComponent<InteractableItem>();
            if (interactable != null) interactable.enabled = false;

            // Clear lane occupancy so next car can start moving
            if (laneReference != null)
            {
                laneReference.currentVehicle = null;
                laneReference = null;
            }
        }
    }
}
