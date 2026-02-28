using UnityEngine;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Base class for stage-specific enemy behaviors.
    /// Delegates decision making and specific actions from EnemyController.
    /// </summary>
    public abstract class BaseEnemyBehavior : MonoBehaviour
    {
        protected EnemyController m_Controller;

        public virtual void Initialize(EnemyController controller)
        {
            m_Controller = controller;
        }

        /// <summary>
        /// Called during EnemyController's Update.
        /// Returns true if this behavior is handling the update exclusively.
        /// </summary>
        public abstract void UpdateBehavior();

        /// <summary>
        /// Called when searching for the next target facility.
        /// </summary>
        public abstract BreakableFacility FindBestTarget(BreakableFacility[] allFacilities);

        /// <summary>
        /// Logic for when the enemy has no target or is waiting.
        /// </summary>
        public virtual void IdleWander()
        {
            m_Controller.Wander();
        }
    }
}
