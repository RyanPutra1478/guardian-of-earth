using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    public abstract class BaseStageLogic : MonoBehaviour
    {
        [Header("Stage Settings")]
        public string stageDisplayName = "New Stage";
        public string trashPrompt = "Pick Up";
        public int initialTrashCount = 10;
        public int initialEnemyCount = 2;
        public DialogueData stageDialogue;

        public virtual bool IsVictoryConditionMet(LevelManager manager)
        {
            // Default condition for River and Forest stages
            return manager.GetTrashCount() == 0 && 
                   manager.GetBrokenFacilityCount() == 0 && 
                   manager.GetEnemyCount() == 0;
        }

        [Header("Atmosphere Settings")]
        public bool useFog = true;
        public Color fogColor = Color.gray;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public float fogDensity = 0.01f;
        public float fogStartDistance = 0f;
        public float fogEndDistance = 300f;

        /// <summary>
        /// Applies the atmosphere settings to the scene.
        /// </summary>
        protected virtual void ApplyAtmosphere()
        {
            RenderSettings.fog = useFog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;
        }

        /// <summary>
        /// Called during LevelManager's Start or when logic is assigned.
        /// </summary>
        public abstract void Initialize(LevelManager manager);

        /// <summary>
        /// Called every frame from LevelManager's Update.
        /// </summary>
        public abstract void UpdateLogic(LevelManager manager);

        /// <summary>
        /// Called every frame from LevelManager's UpdateUI.
        /// </summary>
        public abstract void UpdateUI(LevelManager manager);

        /// <summary>
        /// Called when a facility is repaired in the scene.
        /// </summary>
        public virtual void OnFacilityRepaired(LevelManager manager, BreakableFacility facility, bool successfullyRepaired) { }
    }
}
