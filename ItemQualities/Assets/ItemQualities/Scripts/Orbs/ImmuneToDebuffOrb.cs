using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public class ImmuneToDebuffOrb : Orb
    {
        static EffectIndex _orbEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _orbEffectIndex = EffectCatalogUtils.FindEffectIndex("ChainVineOrbEffect");
            if (_orbEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find thorn orb effect index");
            }
        }

        CharacterMaster _attackerMaster;
        public GameObject Attacker
        {
            get => _attackerMaster ? _attackerMaster.GetBodyObject() : null;
            set
            {
                CharacterBody attackerBody = value ? value.GetComponent<CharacterBody>() : null;
                _attackerMaster = attackerBody ? attackerBody.master : null;
            }
        }

        public BuffIndex BuffIndex = BuffIndex.None;

        public float BuffDuration;

        public int BuffStackCount;

        public override void Begin()
        {
            duration = Mathf.Max(Time.fixedDeltaTime, distanceToTarget / 20f);

            if (_orbEffectIndex != EffectIndex.Invalid)
            {
                EffectData orbEffectData = new EffectData
                {
                    origin = origin,
                    genericFloat = duration
                };

                orbEffectData.SetHurtBoxReference(target);

                EffectManager.SpawnEffect(_orbEffectIndex, orbEffectData, true);
            }
        }

        public override void OnArrival()
        {
            HealthComponent victim = target ? target.healthComponent : null;
            CharacterBody victimBody = victim ? victim.body : null;
            if (victimBody)
            {
                BuffDef buffDef = BuffCatalog.GetBuffDef(BuffIndex);
                DotController.DotIndex dotIndex = DotController.GetDotDefIndex(buffDef);

                if (dotIndex != DotController.DotIndex.None)
                {
                    InflictDotInfo inflictDotInfo = new InflictDotInfo
                    {
                        attackerObject = Attacker,
                        victimObject = victimBody.gameObject,
                        damageMultiplier = 1f,
                        dotIndex = dotIndex,
                        duration = BuffDuration
                    };

                    if (_attackerMaster)
                    {
                        StrengthenBurnUtils.CheckDotForUpgrade(_attackerMaster.inventory, ref inflictDotInfo);
                    }

                    for (int i = 0; i < BuffStackCount; i++)
                    {
                        InflictDotInfo modifiableDotInfo = inflictDotInfo;
                        DotController.InflictDot(ref modifiableDotInfo);
                    }
                }
                else
                {
                    for (int i = 0; i < BuffStackCount; i++)
                    {
                        victimBody.AddTimedBuff(BuffIndex, BuffDuration);
                    }
                }

                if (_attackerMaster)
                {
                    GlobalEventManager.ProcDeathMark(target.gameObject, victimBody, _attackerMaster);
                }

                Util.PlaySound("Play_item_proc_triggerEnemyDebuffs", victimBody.gameObject);
            }
        }
    }
}
