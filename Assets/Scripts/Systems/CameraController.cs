using UnityEngine;

namespace NinuNinu.Systems
{
    public class CameraController : MonoBehaviour
    {
        [Header("Follow Settings")]
        public Transform target;
        public Vector3 offset = new Vector3(10f, 10f, -10f); // Default isometric offset
        public bool autoOffsetFromStart = true;
        public float smoothTime = 0.2f;

        [Header("Camera Type")]
        public bool useOrthographic = true;
        public float orthoSize = 5f;
        public float fieldOfView = 60f; // Tambahkan untuk mode Perspective

        private Vector3 currentVelocity = Vector3.zero;
        private Camera cam;

        void Start()
        {
            cam = GetComponent<Camera>();
            
            if (target == null)
            {
                Debug.LogWarning("CameraController: Target (Player) belum dipasang! Kamera tidak akan bergerak.");
                return;
            }

            if (autoOffsetFromStart)
            {
                offset = transform.position - target.position;
            }

            // Snap di awal agar tidak "terbang" dari posisi default kamera di scene ke player
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            currentVelocity = Vector3.zero; // Reset velocity damp
        }

        void LateUpdate()
        {
            if (target == null) return;

            // Follow Logic
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);

            // Sync Settings
            if (cam != null)
            {
                cam.orthographic = useOrthographic;
                if (useOrthographic) cam.orthographicSize = orthoSize;
                else cam.fieldOfView = fieldOfView;
            }
        }

        [ContextMenu("Lock Current Offset")]
        public void LockCurrentOffset()
        {
            if (target != null)
            {
                offset = transform.position - target.position;
                Debug.Log($"Camera Offset Locked to: {offset}");
            }
        }
    }
}
