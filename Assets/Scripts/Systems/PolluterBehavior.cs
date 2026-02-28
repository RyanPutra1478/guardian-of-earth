using UnityEngine;

namespace NinuNinu.Systems
{
    public class PolluterBehavior : BaseEnemyBehavior
    {
        public override void UpdateBehavior()
        {
            // The Polluter behavior logic is mostly what was in the original EnemyController
            // We'll let EnemyController handle the core Update loop for now, 
            // but we can override specific decision points here.
        }

        public override BreakableFacility FindBestTarget(BreakableFacility[] allFacilities)
        {
            float minDistance = Mathf.Infinity;
            BreakableFacility best = null;

            foreach (var f in allFacilities)
            {
                if (f.isBroken) continue;
                
                // Polluters target everything equally, but might prefer filters/pipes
                float dist = Vector3.Distance(transform.position, f.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    best = f;
                }
            }
            return best;
        }

        // We can add River-specific logic here, like "Seek Water" if we want to move it out of Controller
    }
}
