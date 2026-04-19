using HG;
using ItemQualities.Utilities;
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

            public static readonly FieldInfo[] HealthOverlayBarInfoFields = new FieldInfo[]
            {
                typeof(AdditionalBarInfos).GetField(nameof(TemporaryHealthBarInfo)),
            };

            public static readonly FieldInfo[] BarrierOverlayBarInfoFields = new FieldInfo[]
            {
                typeof(AdditionalBarInfos).GetField(nameof(BarrierOverflowBarInfo))
            };

            public static readonly FieldInfo[] EndBarInfoFields = Array.Empty<FieldInfo>();

            public readonly HealthBar.BarInfo StealthKitLowHealthUnderBarInfo;
            public readonly HealthBar.BarInfo StealthKitLowHealthOverBarInfo;

            public readonly HealthBar.BarInfo TemporaryShieldBarInfo;

            public readonly HealthBar.BarInfo TemporaryHealthBarInfo;

            public readonly HealthBar.BarInfo BarrierOverflowBarInfo;

            public readonly int EnabledBarCount;

            public AdditionalBarInfos(in HealthBar.BarInfo stealthKitLowHealthUnderBarInfo,
                                      in HealthBar.BarInfo stealthKitLowHealthOverBarInfo,
                                      in HealthBar.BarInfo temporaryShieldBarInfo,
                                      in HealthBar.BarInfo temporaryHealthBarInfo,
                                      in HealthBar.BarInfo barrierOverflowBarInfo)
            {
                int enabledBarCount = 0;

                void setBarInfo(out HealthBar.BarInfo barInfoField, in HealthBar.BarInfo barInfo)
                {
                    barInfoField = barInfo;
                    enabledBarCount += barInfoField.enabled ? 1 : 0;
                }

                setBarInfo(out StealthKitLowHealthUnderBarInfo, stealthKitLowHealthUnderBarInfo);

                setBarInfo(out StealthKitLowHealthOverBarInfo, stealthKitLowHealthOverBarInfo);

                setBarInfo(out TemporaryShieldBarInfo, temporaryShieldBarInfo);

                setBarInfo(out TemporaryHealthBarInfo, temporaryHealthBarInfo);

                setBarInfo(out BarrierOverflowBarInfo, barrierOverflowBarInfo);

                EnabledBarCount = enabledBarCount;
            }
        }

        static readonly Dictionary<Sprite, Sprite> _barToGreyscaleBarrierBarLookup = new Dictionary<Sprite, Sprite>();
        static readonly HashSet<Sprite> _greyscaleBars = new HashSet<Sprite>();

        static Sprite getGreyscaleBar(Sprite bar)
        {
            if (_barToGreyscaleBarrierBarLookup.TryGetValue(bar, out Sprite greyscaleBar))
                return greyscaleBar;

            if (_greyscaleBars.Contains(bar))
                return bar;

            Texture2D greyscaleTexture = TextureUtils.CreateAccessibleCopy(bar.texture);
            greyscaleTexture.name += "_BW";

            Color32[] colors = greyscaleTexture.GetPixels32();
            for (int i = 0; i < colors.Length; i++)
            {
                ref Color32 color = ref colors[i];
                byte a = color.a;
                Color.RGBToHSV(color, out float h, out float s, out float v);
                color = Color.HSVToRGB(h, 0, v);
                color.a = a;
            }

            greyscaleTexture.SetPixels32(colors);
            greyscaleTexture.Apply(true, true);

            greyscaleBar = Sprite.Create(greyscaleTexture, bar.rect, bar.pivot, bar.pixelsPerUnit, 0, SpriteMeshType.FullRect, bar.border);
            greyscaleBar.name = bar.name + "_BW";

            _barToGreyscaleBarrierBarLookup.Add(bar, greyscaleBar);
            _greyscaleBars.Add(greyscaleBar);

            return greyscaleBar;
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.UI.HealthBar.CheckInventory += HealthBar_CheckInventory;

            IL.RoR2.UI.HealthBar.ApplyBars += HealthBar_ApplyBars;

            IL.RoR2.HealthComponent.GetHealthBarValues += HealthComponent_GetHealthBarValues;
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

                HashSet<ItemIndex> ignoreLowHealthItemIndices = SetPool<ItemIndex>.RentCollection();
                if (inventory)
                {
                    void handleCustomQualityLowHealthThreshold(ItemQualityGroup itemGroup)
                    {
                        ItemQualityCounts itemCounts = inventory.GetItemCountsEffective(itemGroup);
                        if (itemCounts.TotalQualityCount > 0)
                        {
                            if (itemGroup.BaseItemIndex != ItemIndex.None)
                            {
                                ignoreLowHealthItemIndices.Add(itemGroup.BaseItemIndex);
                            }

                            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                            {
                                if (itemCounts[qualityTier] > 0)
                                {
                                    ItemIndex qualityItemIndex = itemGroup.GetItemIndex(qualityTier);
                                    if (qualityItemIndex != ItemIndex.None)
                                    {
                                        ignoreLowHealthItemIndices.Add(qualityItemIndex);
                                    }
                                }
                            }
                        }
                    }

                    handleCustomQualityLowHealthThreshold(ItemQualitiesContent.ItemQualityGroups.Phasing);
                }

                if (ignoreLowHealthItemIndices.Count == 0)
                {
                    ignoreLowHealthItemIndices = SetPool<ItemIndex>.ReturnCollection(ignoreLowHealthItemIndices);
                }

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
                return ignoreLowHealthItemIndices != null && ignoreLowHealthItemIndices.Contains(itemIndex) ? 0 : itemCount;
            }

            int retPatchCount = 0;

            c.Index = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchRet()))
            {
                c.Emit(OpCodes.Ldloca, ignoreLowHealthItemIndicesVar);
                c.EmitDelegate<CheckInventoryCleanupDelegate>(cleanup);
                
                static void cleanup(ref HashSet<ItemIndex> ignoreLowHealthItemIndices)
                {
                    if (ignoreLowHealthItemIndices != null)
                    {
                        ignoreLowHealthItemIndices = SetPool<ItemIndex>.ReturnCollection(ignoreLowHealthItemIndices);
                    }
                }

                c.SearchTarget = SearchTarget.Next;
                retPatchCount++;
            }

            if (retPatchCount == 0)
            {
                Log.Error("Failed to find ret patch location");
            }
            else
            {
                Log.Debug($"Found {retPatchCount} ret patch location(s)");
            }
        }

        delegate void CheckInventoryCleanupDelegate(ref HashSet<ItemIndex> ignoreLowHealthItemIndices);

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

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdflda<HealthBar.BarInfoCollection>(nameof(HealthBar.BarInfoCollection.trailingOverHealthbarInfo)),
                              x => x.MatchCallOrCallvirt(handleBarMethod)))
            {
                ILCursor overBarsCursor = new ILCursor(il);
                overBarsCursor.Goto(foundCursors[1].Next, MoveType.After);

                foreach (FieldInfo barInfoField in AdditionalBarInfos.HealthOverlayBarInfoFields)
                {
                    emitHandleBarInfoField(overBarsCursor, barInfoField);
                }
            }
            else
            {
                Log.Error("Failed to find health bar patch location");
            }

            if (c.TryFindNext(out foundCursors,
                              x => x.MatchLdflda<HealthBar.BarInfoCollection>(nameof(HealthBar.BarInfoCollection.barrierBarInfo)),
                              x => x.MatchCallOrCallvirt(handleBarMethod)))
            {
                ILCursor overBarsCursor = new ILCursor(il);
                overBarsCursor.Goto(foundCursors[1].Next, MoveType.After);

                foreach (FieldInfo barInfoField in AdditionalBarInfos.BarrierOverlayBarInfoFields)
                {
                    emitHandleBarInfoField(overBarsCursor, barInfoField);
                }
            }
            else
            {
                Log.Error("Failed to find barrier bar patch location");
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
            if (!inventory || !body || !body.TryGetComponentCached(out CharacterBodyExtraStatsTracker extraStatsTracker))
                return default;

            HealthComponent.HealthBarValues healthBarValues = healthComponent.GetHealthBarValues();

            HealthBar.BarInfo lowHealthUnderBarInfoTemplate = healthBar.barInfoCollection.lowHealthUnderBarInfo;
            HealthBar.BarInfo lowHealthOverBarInfoTemplate = healthBar.barInfoCollection.lowHealthOverBarInfo;
            HealthBar.BarInfo shieldBarInfoTemplate = healthBar.barInfoCollection.shieldBarInfo;
            HealthBar.BarInfo trailingOverHealthBarInfoTemplate = healthBar.barInfoCollection.trailingOverHealthbarInfo;
            HealthBar.BarInfo barrierBarInfoTemplate = healthBar.barInfoCollection.barrierBarInfo;

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

            ItemQualityCounts phasing = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Phasing);
            if (phasing.TotalQualityCount > 0)
            {
                setupHealthThresholdBarInfos(ref stealthKitLowHealthUnderBarInfo, ref stealthKitLowHealthOverBarInfo, extraStatsTracker.StealthKitActivationThreshold);
            }

            HealthBar.BarInfo temporaryShieldBarInfo = shieldBarInfoTemplate;
            temporaryShieldBarInfo.enabled = false;

            if (healthBarValues.shieldFraction > 0f && healthBar.style.shieldBarStyle.enabled)
            {
                float temporaryShieldFraction = body.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) / body.maxShield;
                if (temporaryShieldFraction > 0f)
                {
                    float shieldFillFraction = healthComponent.shield / body.maxShield;
                    float fullShieldBarSize = healthBarValues.shieldFraction / shieldFillFraction;

                    temporaryShieldBarInfo.enabled = true;
                    temporaryShieldBarInfo.normalizedXMax = shieldBarInfoTemplate.normalizedXMax;
                    temporaryShieldBarInfo.normalizedXMin = shieldBarInfoTemplate.normalizedXMax - Mathf.Min(healthBarValues.shieldFraction, fullShieldBarSize * temporaryShieldFraction);

                    Color.RGBToHSV(shieldBarInfoTemplate.color, out float h, out float s, out float v);
                    temporaryShieldBarInfo.color = Color.HSVToRGB(h, s, v * 1.5f);
                }
            }

            HealthBar.BarInfo temporaryHealthBarInfo = trailingOverHealthBarInfoTemplate;
            temporaryHealthBarInfo.enabled = false;

            if (healthBarValues.healthFraction > 0f && healthBar.style.trailingOverHealthBarStyle.enabled)
            {
                float temporaryHealthFraction = body.GetBuffCount(ItemQualitiesContent.Buffs.SlugHealth) / body.maxHealth;
                if (temporaryHealthFraction > 0f)
                {
                    float healthFillFraction = healthComponent.health / body.maxHealth;
                    float fullHealthBarSize = healthBarValues.healthFraction / healthFillFraction;

                    temporaryHealthBarInfo.enabled = true;
                    temporaryHealthBarInfo.normalizedXMax = trailingOverHealthBarInfoTemplate.normalizedXMax;
                    temporaryHealthBarInfo.normalizedXMin = trailingOverHealthBarInfoTemplate.normalizedXMax - Mathf.Min(healthBarValues.healthFraction, fullHealthBarSize * temporaryHealthFraction);

                    Color.RGBToHSV(trailingOverHealthBarInfoTemplate.color, out float h, out float s, out float v);
                    temporaryHealthBarInfo.color = Color.HSVToRGB(h, s, v * 1.5f);
                }
            }

            HealthBar.BarInfo barrierOverflowBarInfo = barrierBarInfoTemplate;
            barrierOverflowBarInfo.enabled = false;

            if (healthBar.style.barrierBarStyle.enabled)
            {
                float normalizedBarrierAmount = healthComponent.barrier / healthComponent.fullBarrier;

                static Color getBarColor(int barIndex)
                {
                    if (barIndex == 0)
                        return Color.white;

                    return QualityCatalog.GetQualityTierDef((QualityTier)((barIndex - 1) % (int)QualityTier.Count)).color;
                }

                Sprite greyscaleBarrierBar = getGreyscaleBar(healthBar.style.barrierBarStyle.sprite);

                if (normalizedBarrierAmount > 1f)
                {
                    float normalizedOverflowBarSize = normalizedBarrierAmount - (int)normalizedBarrierAmount;

                    barrierOverflowBarInfo.enabled = true;
                    barrierOverflowBarInfo.sprite = greyscaleBarrierBar;
                    barrierOverflowBarInfo.color = getBarColor((int)normalizedBarrierAmount);
                    barrierOverflowBarInfo.normalizedXMin = 0f;
                    barrierOverflowBarInfo.normalizedXMax = normalizedOverflowBarSize * (1f - healthBarValues.curseFraction);
                }

                // We are displaying more than 2 full bars, change color of normal barrier bar to match previous color
                if (normalizedBarrierAmount > 2f)
                {
                    healthBar.barInfoCollection.barrierBarInfo.sprite = greyscaleBarrierBar;
                    healthBar.barInfoCollection.barrierBarInfo.color = getBarColor((int)normalizedBarrierAmount - 1);
                }
                else if (healthBar.barInfoCollection.barrierBarInfo.sprite == greyscaleBarrierBar)
                {
                    // Reset base barrier bar if we don't need custom color
                    healthBar.barInfoCollection.barrierBarInfo.sprite = healthBar.style.barrierBarStyle.sprite;
                    healthBar.barInfoCollection.barrierBarInfo.color = Color.white;
                }
            }

            return new AdditionalBarInfos(stealthKitLowHealthUnderBarInfo, stealthKitLowHealthOverBarInfo, temporaryShieldBarInfo, temporaryHealthBarInfo, barrierOverflowBarInfo);
        }

        static void HealthComponent_GetHealthBarValues(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdarg(0),
                                 x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.barrier))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, HealthComponent, float>>(clampBarrier);

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static float clampBarrier(float barrier, HealthComponent healthComponent)
                {
                    return Math.Min(barrier, healthComponent.fullBarrier);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Warning("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
