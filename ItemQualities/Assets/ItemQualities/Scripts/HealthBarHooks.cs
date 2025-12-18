using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities
{
    static class HealthBarHooks
    {
        readonly struct AdditionalBarInfos
        {
            public static readonly FieldInfo[] LowHealthUnderBarInfoFields = new FieldInfo[]
            {
                typeof(AdditionalBarInfos).GetField(nameof(StealthKitLowHealthUnderBarInfo)),
            };

            public static readonly FieldInfo[] LowHealthOverBarInfoFields = new FieldInfo[]
            {
                typeof(AdditionalBarInfos).GetField(nameof(StealthKitLowHealthOverBarInfo)),
            };

            public static readonly FieldInfo[] ShieldOverlayBarInfoFields = new FieldInfo[]
            {
                typeof(AdditionalBarInfos).GetField(nameof(TemporaryShieldBarInfo)),
            };

            public static readonly FieldInfo[] EndBarInfoFields = Array.Empty<FieldInfo>();

            public readonly HealthBar.BarInfo StealthKitLowHealthUnderBarInfo;
            public readonly HealthBar.BarInfo StealthKitLowHealthOverBarInfo;

            public readonly HealthBar.BarInfo TemporaryShieldBarInfo;

            public readonly int EnabledBarCount;

            public AdditionalBarInfos(HealthBar.BarInfo stealthKitLowHealthUnderBarInfo, HealthBar.BarInfo stealthKitLowHealthOverBarInfo, HealthBar.BarInfo temporaryShieldBarInfo)
            {
                int enabledBarCount = 0;

                void setBarInfo(out HealthBar.BarInfo barInfoField, HealthBar.BarInfo barInfo)
                {
                    barInfoField = barInfo;
                    enabledBarCount += barInfoField.enabled ? 1 : 0;
                }

                setBarInfo(out StealthKitLowHealthUnderBarInfo, stealthKitLowHealthUnderBarInfo);

                setBarInfo(out StealthKitLowHealthOverBarInfo, stealthKitLowHealthOverBarInfo);

                setBarInfo(out TemporaryShieldBarInfo, temporaryShieldBarInfo);

                EnabledBarCount = enabledBarCount;
            }
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.UI.HealthBar.CheckInventory += HealthBar_CheckInventory;

            IL.RoR2.UI.HealthBar.ApplyBars += HealthBar_ApplyBars;
        }

        static void HealthBar_CheckInventory(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition ignoreLowHealthItemIndicesVar = il.AddVariable<HashSet<ItemIndex>>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<HealthBar, HashSet<ItemIndex>>>(getIgnoreLowHealthItemIndices);
            c.Emit(OpCodes.Stloc, ignoreLowHealthItemIndicesVar);

            static HashSet<ItemIndex> getIgnoreLowHealthItemIndices(HealthBar healthBar)
            {
                HealthComponent healthComponent = healthBar ? healthBar.source : null;
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;

                HashSet<ItemIndex> ignoreLowHealthItemIndices = new HashSet<ItemIndex>();

                void handleCustomQualityLowHealthThreshold(ItemQualityGroup itemGroup)
                {
                    ItemQualityCounts itemCounts = itemGroup.GetItemCountsEffective(inventory);
                    if (itemCounts.TotalQualityCount > 0)
                    {
                        for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            if (itemCounts[qualityTier] > 0)
                            {
                                ignoreLowHealthItemIndices.Add(itemGroup.GetItemIndex(qualityTier));
                            }
                        }
                    }
                }

                handleCustomQualityLowHealthThreshold(ItemQualitiesContent.ItemQualityGroups.Phasing);

                return ignoreLowHealthItemIndices;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdcI4((int)ItemTag.LowHealth),
                               x => x.MatchCallOrCallvirt(typeof(ItemCatalog), nameof(ItemCatalog.GetItemsWithTag)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective))))
            {
                Log.Error($"Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            if (!c.TryFindForeachVariable(out VariableDefinition itemIndexForeachVar))
            {
                Log.Error($"Failed to find itemIndex foreach variable");
                return;
            }

            c.Emit(OpCodes.Ldloc, itemIndexForeachVar);
            c.Emit(OpCodes.Ldloc, ignoreLowHealthItemIndicesVar);
            c.EmitDelegate<Func<int, ItemIndex, HashSet<ItemIndex>, int>>(getItemCount);

            static int getItemCount(int itemCount, ItemIndex itemIndex, HashSet<ItemIndex> ignoreLowHealthItemIndices)
            {
                return ignoreLowHealthItemIndices.Contains(itemIndex) ? 0 : itemCount;
            }
        }

        static void HealthBar_ApplyBars(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            MethodReference handleBarMethodRef = null;

            if (!c.TryGotoNext(x => x.MatchCallOrCallvirt(out handleBarMethodRef) && handleBarMethodRef?.Name?.StartsWith("<ApplyBars>g__HandleBar|") == true))
            {
                Log.Error("Failed to find HandleBar method");
                return;
            }

            MethodBase handleBarMethod = handleBarMethodRef.ResolveReflection();
            if (handleBarMethod == null)
            {
                Log.Error($"Failed to resolve HandleBar method: {handleBarMethodRef.FullName}");
                return;
            }

            int localsVarIndex = -1;
            if (!c.TryGotoPrev(x => x.MatchLdloca(out localsVarIndex)))
            {
                Log.Error("Failed to find locals variable");
                return;
            }

            VariableDefinition localsVar = il.Method.Body.Variables[localsVarIndex];

            c.Index = 0;

            VariableDefinition customBarInfosVar = il.AddVariable<AdditionalBarInfos>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<HealthBar, AdditionalBarInfos>>(collectBarInfos);

            c.Emit(OpCodes.Stloc, customBarInfosVar);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<HealthBar.BarInfoCollection>(nameof(HealthBar.BarInfoCollection.GetActiveCount))))
            {
                Log.Error("Failed to find bar count patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, customBarInfosVar);
            c.EmitDelegate<Func<int, AdditionalBarInfos, int>>(addCustomBarsToCount);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int addCustomBarsToCount(int activeCount, AdditionalBarInfos customBarInfos)
            {
                return activeCount + customBarInfos.EnabledBarCount;
            }

            void emitHandleBarInfoField(ILCursor c, FieldInfo barInfoField)
            {
                c.Emit(OpCodes.Ldarg_0);

                c.Emit(OpCodes.Ldloca, customBarInfosVar);
                c.Emit(OpCodes.Ldflda, barInfoField);

                c.Emit(OpCodes.Ldloca, localsVar);

                c.Emit(OpCodes.Call, handleBarMethod);
            }

            ILCursor[] foundCursors;
            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdflda<HealthBar.BarInfoCollection>(nameof(HealthBar.BarInfoCollection.lowHealthUnderBarInfo)),
                              x => x.MatchCallOrCallvirt(handleBarMethod)))
            {
                ILCursor underBarsCursor = new ILCursor(il);
                underBarsCursor.Goto(foundCursors[1].Next, MoveType.After);

                foreach (FieldInfo barInfoField in AdditionalBarInfos.LowHealthUnderBarInfoFields)
                {
                    emitHandleBarInfoField(underBarsCursor, barInfoField);
                }
            }
            else
            {
                Log.Error("Failed to find low health under bars patch location");
            }

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdflda<HealthBar.BarInfoCollection>(nameof(HealthBar.BarInfoCollection.lowHealthOverBarInfo)),
                              x => x.MatchCallOrCallvirt(handleBarMethod)))
            {
                ILCursor overBarsCursor = new ILCursor(il);
                overBarsCursor.Goto(foundCursors[1].Next, MoveType.After);

                foreach (FieldInfo barInfoField in AdditionalBarInfos.LowHealthOverBarInfoFields)
                {
                    emitHandleBarInfoField(overBarsCursor, barInfoField);
                }
            }
            else
            {
                Log.Error("Failed to find low health over bars patch location");
            }

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdflda<HealthBar.BarInfoCollection>(nameof(HealthBar.BarInfoCollection.shieldBarInfo)),
                              x => x.MatchCallOrCallvirt(handleBarMethod)))
            {
                ILCursor overBarsCursor = new ILCursor(il);
                overBarsCursor.Goto(foundCursors[1].Next, MoveType.After);

                foreach (FieldInfo barInfoField in AdditionalBarInfos.ShieldOverlayBarInfoFields)
                {
                    emitHandleBarInfoField(overBarsCursor, barInfoField);
                }
            }
            else
            {
                Log.Error("Failed to find shield bars patch location");
            }

            c.Index = -1;
            if (c.TryGotoPrev(MoveType.After,
                              x => x.MatchCallOrCallvirt(handleBarMethod)))
            {
                foreach (FieldInfo barInfoField in AdditionalBarInfos.EndBarInfoFields)
                {
                    emitHandleBarInfoField(c, barInfoField);
                }
            }
            else
            {
                Log.Error("Failed to find end bars patch location");
            }
        }

        static AdditionalBarInfos collectBarInfos(HealthBar healthBar)
        {
            HealthComponent healthComponent = healthBar ? healthBar.source : null;
            CharacterBody body = healthComponent ? healthComponent.body : null;
            Inventory inventory = body ? body.inventory : null;
            if (!inventory || !body.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                return default;

            HealthComponent.HealthBarValues healthBarValues = healthComponent.GetHealthBarValues();

            HealthBar.BarInfo lowHealthUnderBarInfoTemplate = healthBar.barInfoCollection.lowHealthUnderBarInfo;
            HealthBar.BarInfo lowHealthOverBarInfoTemplate = healthBar.barInfoCollection.lowHealthOverBarInfo;
            HealthBar.BarInfo shieldBarInfoTemplate = healthBar.barInfoCollection.shieldBarInfo;

            void setupHealthThresholdBarInfos(ref HealthBar.BarInfo underBarInfo, ref HealthBar.BarInfo overBarInfo, float healthThreshold)
            {
                bool isBelowThreshold = healthComponent.IsHealthBelowThreshold(healthThreshold);

                underBarInfo.enabled = isBelowThreshold && healthBar.style.lowHealthUnderStyle.enabled;
                underBarInfo.normalizedXMin = 0f;
                underBarInfo.normalizedXMax = healthThreshold * (1f - healthBarValues.curseFraction);

                overBarInfo.enabled = !isBelowThreshold && healthBar.style.lowHealthOverStyle.enabled;
                overBarInfo.normalizedXMin = healthThreshold * (1f - healthBarValues.curseFraction);
                overBarInfo.normalizedXMax = overBarInfo.normalizedXMin + 0.005f;
            }

            HealthBar.BarInfo stealthKitLowHealthUnderBarInfo = lowHealthUnderBarInfoTemplate;
            HealthBar.BarInfo stealthKitLowHealthOverBarInfo = lowHealthOverBarInfoTemplate;
            stealthKitLowHealthUnderBarInfo.enabled = false;
            stealthKitLowHealthOverBarInfo.enabled = false;

            ItemQualityCounts phasing = ItemQualitiesContent.ItemQualityGroups.Phasing.GetItemCountsEffective(inventory);
            if (phasing.TotalQualityCount > 0)
            {
                setupHealthThresholdBarInfos(ref stealthKitLowHealthUnderBarInfo, ref stealthKitLowHealthOverBarInfo, extraStatsTracker.StealthKitActivationThreshold);
            }

            HealthBar.BarInfo temporaryShieldBarInfo = shieldBarInfoTemplate;
            temporaryShieldBarInfo.enabled = false;

            if (healthBarValues.shieldFraction > 0f)
            {
                float temporaryShieldFraction = body.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) / body.maxShield;
                if (temporaryShieldFraction > 0f)
                {
                    float shieldFillFraction = healthComponent.shield / body.maxShield;
                    float fullShieldBarSize = healthBarValues.shieldFraction / shieldFillFraction;

                    temporaryShieldBarInfo.enabled = healthBar.style.shieldBarStyle.enabled;
                    temporaryShieldBarInfo.normalizedXMax = shieldBarInfoTemplate.normalizedXMax;
                    temporaryShieldBarInfo.normalizedXMin = shieldBarInfoTemplate.normalizedXMax - Mathf.Min(healthBarValues.shieldFraction, fullShieldBarSize * temporaryShieldFraction);

                    Color.RGBToHSV(shieldBarInfoTemplate.color, out float h, out float s, out float v);
                    temporaryShieldBarInfo.color = Color.HSVToRGB(h, s, v * 1.5f);
                }
            }

            return new AdditionalBarInfos(stealthKitLowHealthUnderBarInfo, stealthKitLowHealthOverBarInfo, temporaryShieldBarInfo);
        }
    }
}
