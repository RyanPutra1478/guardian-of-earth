using UnityEngine;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Manages the visual transition between a healthy tree and a stump.
    /// Works in conjunction with the BreakableFacility component.
    /// </summary>
    [RequireComponent(typeof(BreakableFacility))]
    public class TreeVisualManager : MonoBehaviour
    {
        [Header("Models")]
        public GameObject healthyModel;
        public GameObject stumpModel;

        private BreakableFacility m_Facility;

        private void Awake()
        {
            m_Facility = GetComponent<BreakableFacility>();
        }

        private void OnValidate()
        {
            // Update visuals in editor when fields change
            if (m_Facility == null) m_Facility = GetComponent<BreakableFacility>();
            RefreshVisuals();
        }

        private void Start()
        {
            RefreshVisuals();
        }

        private void Update()
        {
            if (m_Facility == null) 
            {
                m_Facility = GetComponent<BreakableFacility>();
                if (m_Facility == null) return;
            }

            // Sync visual status with facility's actual broken state
            bool isBroken = m_Facility.isBroken;
            
            // Check if visuals are out of sync
            bool healthyOutOfSync = (healthyModel != null && healthyModel.activeSelf == isBroken);
            bool stumpOutOfSync = (stumpModel != null && stumpModel.activeSelf != isBroken);

            if (healthyOutOfSync || stumpOutOfSync)
            {
                RefreshVisuals();
            }
        }

        private void OnDestroy()
        {
            // Nothing to cleanup
        }

        /// <summary>
        /// Toggles models based on the facility's broken state.
        /// </summary>
        public void RefreshVisuals()
        {
            if (m_Facility == null) m_Facility = GetComponent<BreakableFacility>();
            if (m_Facility == null) 
            {
                Debug.LogWarning($"[TREE] {gameObject.name} is missing a BreakableFacility component!");
                return;
            }

            bool isBroken = m_Facility.isBroken;

            if (healthyModel != null)
            {
                healthyModel.SetActive(!isBroken);
            }
            else
            {
                Debug.LogWarning($"[TREE] {gameObject.name}: Healthy Model slot is empty!");
            }

            if (stumpModel != null)
            {
                stumpModel.SetActive(isBroken);
            }
            else
            {
                Debug.LogWarning($"[TREE] {gameObject.name}: Stump Model slot is empty!");
            }

            Debug.Log($"[TREE] {gameObject.name} Visuals Synced. State: {(isBroken ? "STUMP" : "HEALTHY")}");
        }
    }
}
