using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    internal static class HealAndReviveConsumed
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.EquipmentSlot.FireSproutOfLife += EquipmentSlot_FireSproutOfLife;
        }

        private static bool EquipmentSlot_FireSproutOfLife(On.RoR2.EquipmentSlot.orig_FireSproutOfLife orig, EquipmentSlot self)
        {
            bool success = orig(self);

            if (success && self.sproutIsSpawned && self.sprout)
            {
                QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();
                if (qualityTier != QualityTier.None)
                {
                    if (self.sprout.TryGetComponent(out SproutOfLifeHealOrbSpawn sproutOfLifeHealOrbSpawn))
                    {
                        float durationIncrease;
                        switch (qualityTier)
                        {
                            case QualityTier.Uncommon:
                                durationIncrease = 1f;
                                break;
                            case QualityTier.Rare:
                                durationIncrease = 5f;
                                break;
                            case QualityTier.Epic:
                                durationIncrease = 10f;
                                break;
                            case QualityTier.Legendary:
                                durationIncrease = 15f;
                                break;
                            default:
                                durationIncrease = 0f;
                                Log.Warning($"Quality tier {qualityTier} is not implemented");
                                break;
                        }

                        if (durationIncrease > 0)
                        {
                            // Orbs are spawned twice per second
                            sproutOfLifeHealOrbSpawn.seedOfLifeOrbsLeft += durationIncrease * 2f;
                        }
                    }

                    GameObject sproutAttachmentObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.HealAndReviveSproutAttachment, self.sprout.transform);

                    HealTargetOnDamaged healTargetOnDamaged = sproutAttachmentObj.GetComponent<HealTargetOnDamaged>();
                    healTargetOnDamaged.HealTarget = self.characterBody;

                    TeamIndex teamIndex = self.characterBody.teamComponent.teamIndex;

                    TeamFilter teamFilter = sproutAttachmentObj.GetComponent<TeamFilter>();
                    teamFilter.teamIndex = teamIndex;

                    TauntZone tauntZone = sproutAttachmentObj.GetComponent<TauntZone>();
                    tauntZone.Attacker = self.characterBody;

                    NetworkServer.Spawn(sproutAttachmentObj);
                }
            }

            return success;
        }
    }
}
