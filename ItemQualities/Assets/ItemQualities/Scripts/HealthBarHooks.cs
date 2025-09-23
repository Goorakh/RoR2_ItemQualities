using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    static class HealthBarHooks
    {
        readonly struct AdditionalBarInfos
        {
            public static readonly FieldInfo[] LowHealthUnderBarInfoFields = new FieldInfo[]
            {
                typeof(AdditionalBarInfos).GetField(nameof(WatchLowHealthUnderBarInfo))
            };

            public static readonly FieldInfo[] LowHealthOverBarInfoFields = new FieldInfo[]
            {
                typeof(AdditionalBarInfos).GetField(nameof(WatchLowHealthOverBarInfo))
            };

            public static readonly FieldInfo[] EndBarInfoFields = typeof(AdditionalBarInfos).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                                                                            .Where(f => f.FieldType == typeof(HealthBar.BarInfo))
                                                                                            .Except(LowHealthUnderBarInfoFields)
                                                                                            .Except(LowHealthOverBarInfoFields)
                                                                                            .ToArray();

            public readonly HealthBar.BarInfo WatchLowHealthUnderBarInfo;
            public readonly HealthBar.BarInfo WatchLowHealthOverBarInfo;

            public readonly int EnabledBarCount;

            public AdditionalBarInfos(HealthBar.BarInfo watchLowHealthUnderBarInfo, HealthBar.BarInfo watchLowHealthOverBarInfo)
            {
                int enabledBarCount = 0;
                WatchLowHealthUnderBarInfo = watchLowHealthUnderBarInfo;
                if (WatchLowHealthUnderBarInfo.enabled)
                    enabledBarCount++;

                WatchLowHealthOverBarInfo = watchLowHealthOverBarInfo;
                if (WatchLowHealthOverBarInfo.enabled)
                    enabledBarCount++;

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

                if (inventory)
                {
                    if (body.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    {
                        ItemQualityCounts watch = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCounts(inventory);
                        if (watch.TotalCount > 0 && extraStatsTracker.WatchBreakThreshold != HealthComponent.lowHealthFraction)
                        {
                            for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                            {
                                ignoreLowHealthItemIndices.Add(ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemIndex(qualityTier));
                            }
                        }
                    }
                }

                return ignoreLowHealthItemIndices;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdcI4((int)ItemTag.LowHealth),
                               x => x.MatchCallOrCallvirt(typeof(ItemCatalog), nameof(ItemCatalog.GetItemsWithTag)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount))))
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

            MethodReference handleBarMethod = null;
            VariableDefinition localsVar;

            static bool nameStartsWith(MethodReference methodReference, string value)
            {
                // This is dumb
                if (methodReference == null || methodReference.Name == null)
                    return false;

                return methodReference.Name.StartsWith(value);
            }

            if (!c.TryGotoNext(x => x.MatchCallOrCallvirt(out handleBarMethod) && nameStartsWith(handleBarMethod, "<ApplyBars>g__HandleBar|")))
            {
                Log.Error("Failed to find HandleBar method");
                return;
            }

            int localsVarIndex = -1;
            if (!c.TryGotoPrev(x => x.MatchLdloca(out localsVarIndex)))
            {
                Log.Error("Failed to find locals variable");
                return;
            }

            localsVar = il.Method.Body.Variables[localsVarIndex];

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

            HealthBar.BarInfo watchLowHealthUnderBarInfo = lowHealthUnderBarInfoTemplate;
            HealthBar.BarInfo watchLowHealthOverBarInfo = lowHealthOverBarInfoTemplate;
            watchLowHealthUnderBarInfo.enabled = false;
            watchLowHealthOverBarInfo.enabled = false;

            float watchBreakThreshold = extraStatsTracker.WatchBreakThreshold;
            int totalWatchCount = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCounts(inventory).TotalCount;

            if (watchBreakThreshold != HealthComponent.lowHealthFraction && totalWatchCount > 0)
            {
                bool isBelowWatchThreshold = healthComponent.IsHealthBelowThreshold(watchBreakThreshold);
                watchLowHealthUnderBarInfo.enabled = isBelowWatchThreshold;
                watchLowHealthUnderBarInfo.normalizedXMin = 0f;
                watchLowHealthUnderBarInfo.normalizedXMax = watchBreakThreshold * (1f - healthBarValues.curseFraction);

                watchLowHealthOverBarInfo.enabled = !isBelowWatchThreshold;
                watchLowHealthOverBarInfo.normalizedXMin = watchBreakThreshold * (1f - healthBarValues.curseFraction);
                watchLowHealthOverBarInfo.normalizedXMax = watchLowHealthOverBarInfo.normalizedXMin + 0.005f;
            }

            return new AdditionalBarInfos(watchLowHealthUnderBarInfo, watchLowHealthOverBarInfo);
        }
    }
}
