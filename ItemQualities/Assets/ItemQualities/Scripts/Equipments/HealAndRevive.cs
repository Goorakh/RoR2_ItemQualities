using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    internal static class HealAndRevive
    {
        private static Xoroshiro128Plus _rng;

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.CharacterMaster.TrueKill += CharacterMaster_TrueKill;
            IL.RoR2.CharacterMaster.TryReviveOnBodyDeath += CharacterMaster_TryReviveOnBodyDeath;
            IL.RoR2.EquipmentSlot.FireHealAndRevive += EquipmentSlot_FireHealAndRevive;

            Run.onRunStartGlobal += onRunStartGlobal;
        }

        private static void onRunStartGlobal(Run run)
        {
            if (NetworkServer.active)
            {
                _rng = new Xoroshiro128Plus(run.runRNG.nextUlong);
            }
        }

        private static void onRevivedServer(CharacterMaster master, QualityTier qualityTier)
        {
            if (master && master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
            {
                masterExtraStats.TryPermanentUpgradeRandomItemToQualityTier(_rng, qualityTier);
            }
        }

        private static void CharacterMaster_TrueKill(On.RoR2.CharacterMaster.orig_TrueKill orig, CharacterMaster self)
        {
            if (self.inventory)
            {
                int equipmentSlotCount = self.inventory.GetEquipmentSlotCount();
                for (uint slot = 0; slot < equipmentSlotCount; slot++)
                {
                    int equipmentSetCount = self.inventory.GetEquipmentSetCount(slot);
                    for (uint set = 0; set < equipmentSetCount; set++)
                    {
                        EquipmentState equipmentState = self.inventory.GetEquipment(slot, set);
                        EquipmentIndex equipmentIndex = equipmentState.equipmentIndex;
                        QualityTier qualityTier = QualityCatalog.GetQualityTier(equipmentIndex);
                        EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex);

                        if (equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.HealAndRevive.GroupIndex && qualityTier > QualityTier.None)
                        {
                            EquipmentIndex consumedEquipmentIndex = ItemQualitiesContent.EquipmentQualityGroups.HealAndReviveConsumed.GetEquipmentIndex(qualityTier);
                            self.inventory.SetEquipmentIndexForSlot(consumedEquipmentIndex, slot, set);
                            CharacterMasterNotificationQueue.SendTransformNotification(self, equipmentIndex, consumedEquipmentIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                        }
                    }
                }
            }

            orig(self);
        }

        private static void CharacterMaster_TryReviveOnBodyDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILLabel afterHealAndReviveLabel = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdstr(nameof(CharacterMaster.RespawnExtraLifeHealAndRevive))) ||
                !c.TryGotoPrev(MoveType.After,
                               x => x.MatchLdloc(out _),
                               x => x.MatchBrfalse(out afterHealAndReviveLabel)))
            {
                Log.Error("Failed to find HealAndRevive location");
                return;
            }

            Instruction healAndReviveStartInstruction = c.Next;

            VariableDefinition healAndReviveQualityTierVar = il.AddVariable<QualityTier>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterMaster, QualityTier>>(getHealAndReviveQualityTier);
            c.Emit(OpCodes.Stloc, healAndReviveQualityTierVar);

            static QualityTier getHealAndReviveQualityTier(CharacterMaster master)
            {
                return master.inventory ? master.inventory.GetActiveEquipmentQualityTier() : QualityTier.None;
            }

            static bool matchHealAndReviveEquipment(Instruction instr)
            {
                return instr.MatchLdsfld(typeof(DLC2Content.Equipment), nameof(DLC2Content.Equipment.HealAndRevive)) ||
                       instr.MatchLdsfld(typeof(DLC2Content.Equipment), nameof(DLC2Content.Equipment.HealAndReviveConsumed));
            }

            int staticEquipmentDefPatchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => matchHealAndReviveEquipment(x)) &&
                   c.IsBefore(afterHealAndReviveLabel.Target))
            {
                c.Emit(OpCodes.Ldloc, healAndReviveQualityTierVar);
                c.EmitDelegate<Func<EquipmentDef, QualityTier, EquipmentDef>>(getQualityEquipment);

                static EquipmentDef getQualityEquipment(EquipmentDef equipmentDef, QualityTier qualityTier)
                {
                    if (qualityTier != QualityTier.None)
                    {
                        EquipmentIndex equipmentIndex = equipmentDef ? equipmentDef.equipmentIndex : EquipmentIndex.None;
                        if (equipmentIndex != EquipmentIndex.None)
                        {
                            EquipmentIndex qualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, qualityTier);
                            if (qualityEquipmentIndex != EquipmentIndex.None && qualityEquipmentIndex != equipmentIndex)
                            {
                                equipmentDef = EquipmentCatalog.GetEquipmentDef(qualityEquipmentIndex);
                                equipmentIndex = qualityEquipmentIndex;
                            }
                        }
                    }

                    return equipmentDef;
                }

                staticEquipmentDefPatchCount++;
            }

            if (staticEquipmentDefPatchCount == 0)
            {
                Log.Error("Failed to find static equipment reference patch location");
            }
            else
            {
                Log.Debug($"Found {staticEquipmentDefPatchCount} static equipment reference patch location(s)");
            }

            c.Goto(healAndReviveStartInstruction, MoveType.Before);

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, healAndReviveQualityTierVar);
            c.EmitDelegate<Action<CharacterMaster, QualityTier>>(tryQualityHealAndRevive);

            static void tryQualityHealAndRevive(CharacterMaster master, QualityTier qualityTier)
            {
                if (qualityTier != QualityTier.None)
                {
                    master.StartCoroutine(waitThenAddItemUpgrade(master, qualityTier));

                    static IEnumerator waitThenAddItemUpgrade(CharacterMaster master, QualityTier qualityTier)
                    {
                        yield return new WaitForSeconds(2.05f);
                        onRevivedServer(master, qualityTier);
                    }
                }
            }
        }

        private static void EquipmentSlot_FireHealAndRevive(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition revivedMasterVar = null;
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdloc<CharacterMaster>(il, out revivedMasterVar),
                              x => x.MatchCallOrCallvirt<Component>("get_" + nameof(Component.gameObject)),
                              x => x.MatchCallOrCallvirt(CommonReflectionCache.AddComponent.OfType<EquipmentSlot.HealAndReviveLock>.Method),
                              x => x.MatchPop()))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, revivedMasterVar);
                c.EmitDelegate<Action<EquipmentSlot, CharacterMaster>>(tryQualityHealAndRevive);

                static void tryQualityHealAndRevive(EquipmentSlot equipmentSlot, CharacterMaster revivedMaster)
                {
                    QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    if (qualityTier != QualityTier.None)
                    {
                        revivedMaster.StartCoroutine(waitThenAddItemUpgrade(revivedMaster, qualityTier));

                        static IEnumerator waitThenAddItemUpgrade(CharacterMaster master, QualityTier qualityTier)
                        {
                            yield return new WaitForSeconds(0.15f);
                            onRevivedServer(master, qualityTier);
                        }
                    }
                }
            }
            else
            {
                Log.Error("Failed to find revive patch location");
            }

            c.Goto(0);

            ILLabel afterHealAndReviveConsumeLabel = null;
            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification))) &&
                c.TryGotoPrev(MoveType.After,
                              x => x.MatchBrfalse(out afterHealAndReviveConsumeLabel)))
            {
                static bool matchHealAndReviveEquipment(Instruction instr)
                {
                    return instr.MatchLdsfld(typeof(DLC2Content.Equipment), nameof(DLC2Content.Equipment.HealAndRevive)) ||
                           instr.MatchLdsfld(typeof(DLC2Content.Equipment), nameof(DLC2Content.Equipment.HealAndReviveConsumed));
                }

                int staticEquipmentDefPatchCount = 0;

                while (c.TryGotoNext(MoveType.After,
                                     x => matchHealAndReviveEquipment(x)) &&
                       c.IsBefore(afterHealAndReviveConsumeLabel.Target))
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Func<EquipmentDef, EquipmentSlot, EquipmentDef>>(getQualityEquipment);

                    static EquipmentDef getQualityEquipment(EquipmentDef equipmentDef, EquipmentSlot equipmentSlot)
                    {
                        QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                        if (qualityTier != QualityTier.None)
                        {
                            EquipmentIndex equipmentIndex = equipmentDef ? equipmentDef.equipmentIndex : EquipmentIndex.None;
                            if (equipmentIndex != EquipmentIndex.None)
                            {
                                EquipmentIndex qualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, qualityTier);
                                if (qualityEquipmentIndex != EquipmentIndex.None && qualityEquipmentIndex != equipmentIndex)
                                {
                                    equipmentDef = EquipmentCatalog.GetEquipmentDef(qualityEquipmentIndex);
                                    equipmentIndex = qualityEquipmentIndex;
                                }
                            }
                        }

                        return equipmentDef;
                    }

                    staticEquipmentDefPatchCount++;
                }

                if (staticEquipmentDefPatchCount == 0)
                {
                    Log.Error("Failed to find consume equipment reference patch location");
                }
                else
                {
                    Log.Debug($"Found {staticEquipmentDefPatchCount} consume equipment reference patch location(s)");
                }
            }
            else
            {
                Log.Error("Failed to find consume patch location");
            }
        }
    }
}
