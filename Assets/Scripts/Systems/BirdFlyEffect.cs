using UnityEngine;

namespace NinuNinu.Systems
{
    /// <summary>
    /// Script untuk menggerakkan burung terbang setelah muncul dari sangkar.
    /// Pasang script ini pada prefab burung yang akan dijadikan Particle Effect.
    /// </summary>
    public class BirdFlyEffect : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float flySpeed = 5f;
        public float upwardForce = 2f;
        public float rotationBop = 20f; // Kecepatan gelombang saat terbang
        public float lifeSpan = 5f; // Berapa lama burung terlihat sebelum hancur

        [Header("Randomness")]
        public float randomAngle = 45f;

        private Vector3 flyDirection;

        void Start()
        {
            // Ambil arah depan acak (sedikit mendongak ke atas)
            float randY = Random.Range(-randomAngle, randomAngle);
            flyDirection = Quaternion.Euler(-30f, randY, 0) * Vector3.forward;

            // Pastikan burung menghadap arah terbangnya
            transform.rotation = Quaternion.LookRotation(flyDirection);

            // Hancurkan otomatis setelah beberapa detik
            Destroy(gameObject, lifeSpan);
        }

        void Update()
        {
            // Gerakan Terbang ke depan dan ke atas
            transform.Translate(Vector3.forward * flySpeed * Time.deltaTime);
            transform.Translate(Vector3.up * upwardForce * Time.deltaTime, Space.World);

            // Efek visual: Burung sedikit bergoyang (bop) agar tidak terlalu kaku
            float s = Mathf.Sin(Time.time * 5f) * rotationBop;
            transform.Rotate(0, 0, s * Time.deltaTime);
        }
    }
}
