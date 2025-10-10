using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public class BarrierOnOverHealQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;
        HealthComponent _healthComponent;

        bool _hadBarrier = false;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _healthComponent = _body.healthComponent;
        }

        void OnEnable()
        {
            _hadBarrier = _body.healthComponent.barrier > 0f;
        }

        void FixedUpdate()
        {
            bool hasBarrier = _body.healthComponent.barrier > 0f;
            if (hasBarrier != _hadBarrier)
            {
                _hadBarrier = hasBarrier;
                _body.MarkAllStatsDirty();
            }
        }
    }
}
