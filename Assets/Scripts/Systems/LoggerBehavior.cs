using UnityEngine;

namespace NinuNinu.Systems
{
    public class LoggerBehavior : BaseEnemyBehavior
    {
        public override void UpdateBehavior()
        {
            // Logger specific update logic could go here
        }

        public override BreakableFacility FindBestTarget(BreakableFacility[] allFacilities)
        {
            float minDistance = Mathf.Infinity;
            BreakableFacility best = null;

            // Priority 1: Trees
            foreach (var f in allFacilities)
            {
                if (f.isBroken || f.facilityType != FacilityType.Tree) continue;

                float dist = Vector3.Distance(transform.position, f.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    best = f;
                }
            }

            // Priority 2: Other facilities (if no trees found)
            if (best == null)
            {
                minDistance = Mathf.Infinity;
                foreach (var f in allFacilities)
                {
                    if (f.isBroken) continue;

                    float dist = Vector3.Distance(transform.position, f.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        best = f;
                    }
                }
            }

            return best;
        }

        public override void IdleWander()
        {
            // Loggers might wander in different patterns, e.g., staying near tree clusters
            base.IdleWander();
        }
    }
}
