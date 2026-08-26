using HG;
using ItemQualities.Items;
using ItemQualities.Orbs;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Orbs;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Equipments
{
    internal static class Jetpack
    {
        private static DamageColorIndex _bugColorIndex;

        [SystemInitializer]
        private static void Init()
        {
            _bugColorIndex = ColorsAPI.RegisterDamageColor(new Color32(0x93, 0x8A, 0x71, 0xFF));

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Jetpack.JetpackController_prefab).OnSuccess(jetpackAttachment =>
            {
                jetpackAttachment.EnsureComponent<JetpackQualityController>();
            });

            On.RoR2.EquipmentSlot.FireJetpack += EquipmentSlot_FireJetpack;

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static bool EquipmentSlot_FireJetpack(On.RoR2.EquipmentSlot.orig_FireJetpack orig, EquipmentSlot self)
        {
            bool fired = orig(self);

            if (fired)
            {
                JetpackController jetpackController = JetpackController.FindJetpackController(self.gameObject);
                if (jetpackController && jetpackController.TryGetComponent(out JetpackQualityController jetpackQualityController))
                {
                    jetpackQualityController.ActiveQualityTier = self.GetCurrentEquipmentActionQualityTier();
                }
            }

            return fired;
        }

        private static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null || damageReport.damageDealt <= 0 || damageReport.damageInfo.procCoefficient <= 0)
                return;

            if (!damageReport.attackerBody || !damageReport.victimBody || !damageReport.victimBody.mainHurtBox)
                return;

            BuffQualityCounts bugBlock = damageReport.attackerBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.BugBlock);
            if (bugBlock.TotalQualityCount > 0 && !damageReport.damageInfo.procChainMask.HasModdedProc(ProcTypes.Bug))
            {
                QualityTier qualityTier = bugBlock.HighestQuality;

                float damageCoefficient;
                switch (qualityTier)
                {
                    case QualityTier.Uncommon:
                        damageCoefficient = 0.8f;
                        break;
                    case QualityTier.Rare:
                        damageCoefficient = 1.2f;
                        break;
                    case QualityTier.Epic:
                        damageCoefficient = 1.6f;
                        break;
                    case QualityTier.Legendary:
                        damageCoefficient = 2.0f;
                        break;
                    default:
                        damageCoefficient = 0f;
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                if (damageCoefficient > 0)
                {
                    ProcChainMask procChainMask = damageReport.damageInfo.procChainMask;
                    procChainMask.AddModdedProc(ProcTypes.Bug);

                    int moreMissileCount = damageReport.attackerBody.inventory ? damageReport.attackerBody.inventory.GetItemCountEffective(DLC1Content.Items.MoreMissile) : 0;

                    float damageValue = Util.OnHitProcDamage(damageReport.damageInfo.damage, damageReport.attackerBody.damage, damageCoefficient) * MissileUtils.GetMoreMissileDamageMultiplier(moreMissileCount);

                    int fireCount = moreMissileCount > 0 ? 3 : 1;
                    fireCount += MoreMissile.RollAdditionalMissileCount(damageReport.attackerBody, procChainMask.HasProc(ProcType.SureProc));

                    for (int i = 0; i < fireCount; i++)
                    {
                        OrbManager.instance.AddOrb(new BugOrb
                        {
                            attacker = damageReport.attacker,
                            origin = damageReport.attackerBody.aimOrigin,
                            teamIndex = damageReport.attackerBody.teamComponent.teamIndex,
                            target = damageReport.victimBody.mainHurtBox,
                            damageValue = damageValue,
                            procChainMask = procChainMask,
                            procCoefficient = 0.2f,
                            isCrit = damageReport.damageInfo.crit,
                            damageColorIndex = _bugColorIndex,
                        });
                    }
                }

                BuffIndex buffIndex = ItemQualitiesContent.BuffQualityGroups.BugBlock.GetBuffIndex(qualityTier);
                if (buffIndex != BuffIndex.None)
                {
                    damageReport.attackerBody.RemoveBuff(buffIndex);
                }
            }
        }
    }
}
