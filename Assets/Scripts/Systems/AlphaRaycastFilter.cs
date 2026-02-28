using UnityEngine;
using UnityEngine.UI;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Enables non-rectangular hit detection for Unity UI Images based on their alpha channel.
    /// This allows overlapping UI buttons to work correctly by ignoring clicks on transparent areas.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("NinuNinu/UI/Alpha Raycast Filter")]
    public class AlphaRaycastFilter : MonoBehaviour
    {
        [Tooltip("The minimum alpha value (0 to 1) required for a click to be registered. 0.1 is usually good.")]
        [SerializeField, Range(0f, 1f)]
        private float alphaThreshold = 0.1f;

        private void Awake()
        {
            Image image = GetComponent<Image>();
            if (image != null)
            {
                // Set the minimum alpha threshold for hit testing.
                // NOTE: This requires 'Read/Write Enabled' to be checked in the Sprite's Import Settings.
                image.alphaHitTestMinimumThreshold = alphaThreshold;
            }
        }

        // Allow changing the threshold at runtime if needed
        public void SetAlphaThreshold(float value)
        {
            alphaThreshold = Mathf.Clamp01(value);
            GetComponent<Image>().alphaHitTestMinimumThreshold = alphaThreshold;
        }
    }
}
