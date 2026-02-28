using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Provides a simple scale animation for UI elements when hovered or clicked.
    /// Works well with AlphaRaycastFilter for non-rectangular buttons.
    /// </summary>
    public class UIButtonScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Animation Settings")]
        [Tooltip("Scale multiplier when the mouse enters the button area.")]
        public float hoverScale = 1.05f;
        
        [Tooltip("Scale multiplier when the button is pressed.")]
        public float clickScale = 0.95f;
        
        [Tooltip("How fast the transition should be (in seconds).")]
        public float duration = 0.1f;

        private Vector3 originalScale;
        private Coroutine activeRoutine;

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        private void OnDisable()
        {
            // Reset scale if disabled to avoid getting stuck in a scaled state
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            transform.localScale = originalScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StartScale(originalScale * hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StartScale(originalScale);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StartScale(originalScale * clickScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Return to hover scale after clicking, if mouse is still over it
            StartScale(originalScale * hoverScale);
        }

        private void StartScale(Vector3 target)
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(ScaleRoutine(target));
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            Vector3 startScale = transform.localScale;
            float elapsedTime = 0;

            while (elapsedTime < duration)
            {
                transform.localScale = Vector3.Lerp(startScale, target, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            transform.localScale = target;
            activeRoutine = null;
        }
    }
}
