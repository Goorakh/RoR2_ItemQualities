using RoR2;
using UnityEngine;

namespace ItemQualities
{
    public sealed class ProcDamageModifier : MonoBehaviour, IOnIncomingDamageServerReceiver
    {
        public float ProcCoefficientMultiplier = 1f;

        public DamageTypeCombo DamageTypeToAdd;

        public void OnIncomingDamageServer(DamageInfo damageInfo)
        {
            damageInfo.procCoefficient *= ProcCoefficientMultiplier;
            damageInfo.damageType |= DamageTypeToAdd;
        }
    }
}
