using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.ContentManagement;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace ItemQualities.Items
{
    static class BossDamageBonus
    {
        public static Transform tickUI;
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        public static void updateTickVisual(CharacterMaster master) {
            Debug.Log("updatevisual");
            if (!master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
                return;
            Debug.Log(master.localPlayerAuthority);
            if (master.localPlayerAuthority)
            {
                HUD gameHud = HUD.instancesList[0];
                if (!gameHud || !tickUI)
                    return;

                ChildLocator childLocator = tickUI.GetComponent<ChildLocator>();
                Debug.Log(childLocator);
                Debug.Log(tickUI.name);
                if (!childLocator)
                    return;

                Debug.Log((float)masterExtraStats.BossDamageBonusTicks / 5);
                for (int i = 0; i < (float)masterExtraStats.BossDamageBonusTicks / 5; i++)
                {
                    Debug.Log("test");
                    Transform child = childLocator.FindChild(i);
                    child.gameObject.SetActive(true);
                    Image image = child.GetComponent<Image>();
                    Sprite sprite = (masterExtraStats.BossDamageBonusTicks - 5 * i) switch
                    {
                        1 => ItemQualitiesContent.Sprites.hitlistTick_1,
                        2 => ItemQualitiesContent.Sprites.hitlistTick_2,
                        3 => ItemQualitiesContent.Sprites.hitlistTick_3,
                        4 => ItemQualitiesContent.Sprites.hitlistTick_4,
                        _ => ItemQualitiesContent.Sprites.hitlistTick_5,
                    };
                    image.sprite = sprite;
                }
            }
        }

        private static void onCharacterDeathGlobal(DamageReport report)
        {
            if (!report.attackerBody || !report.attackerBody.inventory || !report.victimBody)
                return;

            if (report.victimBody.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker))
            {
                ItemQualityCounts bossDamageBonus = report.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BossDamageBonus);

                int maxHitlistBonus = bossDamageBonus.HighestQuality switch {
                    QualityTier.Uncommon => 20,
                    QualityTier.Rare => 40,
                    QualityTier.Epic => 60,
                    QualityTier.Legendary => 80,
                    _ => 0,
                };

                if (!report.attackerBody.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
                    return;
                masterExtraStats.BossDamageBonusTicks += 1;
                updateTickVisual(report.attackerBody.master);
            }
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.BossDamageBonus)),
                               x => x.MatchLdcR4(0.2f),
                               x => x.MatchMul()))
            {
                Log.Error("Failed to find damage patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.Before); // ldsfld RoR2Content.Items.BossDamageBonus
            if (!c.TryGotoPrev(MoveType.After,
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.isBoss))))
            {
                Log.Error("Failed to find isBoss patch location");
                return;
            }

            VariableDefinition isMiniBossVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<bool, HealthComponent, DamageInfo, bool>>(isMiniBoss);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, isMiniBossVar);
            c.Emit(OpCodes.Or);

            static bool isMiniBoss(bool isBoss, HealthComponent victim, DamageInfo damageInfo)
            {
                if (isBoss)
                    return false;

                if (!victim || !victim.body)
                    return false;

                CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                if (!attackerInventory)
                    return false;

                ItemQualityCounts bossDamageBonus = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BossDamageBonus);
                return bossDamageBonus.TotalQualityCount > 0 && victim.body.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // mul

            c.Emit(OpCodes.Ldloc, isMiniBossVar);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, bool, DamageInfo, float>>(getBossDamageMultiplier);

            static float getBossDamageMultiplier(float damageMultiplier, bool isMiniBoss, DamageInfo damageInfo)
            {
                if (isMiniBoss)
                {
                    CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                    if (attackerBody && attackerBody.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats)) {
                        damageMultiplier = masterExtraStats.BossDamageBonusTicks * 0.01f;
                    }
                }

                return damageMultiplier;
            }
        }
    }
}
