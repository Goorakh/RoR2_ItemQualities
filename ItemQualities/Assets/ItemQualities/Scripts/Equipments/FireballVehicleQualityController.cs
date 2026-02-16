using RoR2;
using UnityEngine;

namespace ItemQualities.Equipments
{
    public sealed class FireballVehicleQualityController : MonoBehaviour
    {
        public float BlastRadiusBonusPerHit;

        public float BlastDamageCoefficientBonusPerHit;

        FireballVehicle _fireballVehicle;

        void Awake()
        {
            _fireballVehicle = GetComponent<FireballVehicle>();

            ComponentCache.Add(gameObject, this);
        }

        void OnDestroy()
        {
            ComponentCache.Remove(gameObject, this);
        }

        public void OnOverlapAttackHitServer()
        {
            _fireballVehicle.blastDamageCoefficient += BlastDamageCoefficientBonusPerHit;
            _fireballVehicle.blastRadius += BlastRadiusBonusPerHit;
        }
    }
}
