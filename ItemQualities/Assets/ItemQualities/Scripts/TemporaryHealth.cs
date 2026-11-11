using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;

namespace ItemQualities
{
    public static class TemporaryHealth
    {
        private static readonly List<TemporaryHealthSourceEntry> _temporaryHealthSources = new List<TemporaryHealthSourceEntry>();
        private sealed class TemporaryHealthSourceEntry : IComparable<TemporaryHealthSourceEntry>
        {
            public readonly ITemporaryHealthSource source;
            public readonly float priority;

            public TemporaryHealthSourceEntry(ITemporaryHealthSource source, float priority)
            {
                this.source = source;
                this.priority = priority;
            }

            public int CompareTo(TemporaryHealthSourceEntry other)
            {
                // Sort entries by priorities descending (highest priority first)
                return -priority.CompareTo(other.priority);
            }
        }

        public static float GetTemporaryHealthBonus(this CharacterBody body)
        {
            float totalHealthBonus = 0f;

            foreach (TemporaryHealthSourceEntry sourceEntry in _temporaryHealthSources)
            {
                totalHealthBonus += sourceEntry.source.GetTemporaryHealthBonus(body);
            }

            return totalHealthBonus;
        }

        /// <summary>
        /// Attempts to take an amount of health from all sources. In order of priority.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="temporaryHealthToTake"></param>
        /// <returns>How much temporary health was actually deducted, may be less than <paramref name="temporaryHealthToTake"/> if the body does not have enough to "afford" it.</returns>
        public static float TryTakeTemporaryHealth(this CharacterBody body, float temporaryHealthToTake)
        {
            float totalTaken = 0f;
            float remainingToTake = temporaryHealthToTake;

            foreach (TemporaryHealthSourceEntry sourceEntry in _temporaryHealthSources)
            {
                float takenFromSource = sourceEntry.source.TryTakeTemporaryHealthBonus(body, remainingToTake);
                totalTaken += takenFromSource;
                remainingToTake -= takenFromSource;

                if (remainingToTake <= 0f)
                {
                    break;
                }
            }

            return totalTaken;
        }
        
        /// <summary>
        /// Registers a temporary health source for all bodies
        /// </summary>
        /// <param name="source"></param>
        /// <param name="priority">The priority of this source, higher priorities will be consumed first.</param>
        public static void RegisterTempoaryHealthSource(ITemporaryHealthSource source, float priority = 0f)
        {
            TemporaryHealthSourceEntry sourceEntry = new TemporaryHealthSourceEntry(source, priority);
            int index = _temporaryHealthSources.BinarySearch(sourceEntry);
            if (index < 0)
            {
                index = ~index;
            }

            _temporaryHealthSources.Insert(index, sourceEntry);
        }

        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;

            RegisterTempoaryHealthSource(new BuffTemporaryHealthSource(ItemQualitiesContent.Buffs.SlugHealth));
            RegisterTempoaryHealthSource(new BuffTemporaryHealthSource(ItemQualitiesContent.Buffs.FruitTempHealth));
        }

        private static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition healthDamageDealtVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.shield)),
                               x => x.MatchLdloc(typeof(float), il, out healthDamageDealtVar),
                               x => x.MatchSub(),
                               x => x.MatchCallOrCallvirt<HealthComponent>("set_" + nameof(HealthComponent.Networkshield))))
            {
                Log.PatchError(il, "Failed to find damageDealt variable index");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<GlobalEventManager>(nameof(GlobalEventManager.ServerDamageDealt))))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, healthDamageDealtVar);
            c.EmitDelegate<Action<HealthComponent, float>>(onDamageDealt);

            static void onDamageDealt(HealthComponent healthComponent, float healthDamageDealt)
            {
                if (!healthComponent || !healthComponent.body)
                    return;

                if (healthDamageDealt > 0f)
                {
                    TryTakeTemporaryHealth(healthComponent.body, healthDamageDealt);
                }
            }
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            args.baseHealthAdd += sender.GetTemporaryHealthBonus();
        }
    }
}
