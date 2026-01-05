using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System.Collections.Generic;

namespace ItemQualities
{
    static class PrayerBeadsIgnoreXp
    {
        static readonly List<IgnoreXpChunk> _ignoreXpChunks = new List<IgnoreXpChunk>();

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.TeamManager.GiveTeamExperience += TeamManager_GiveTeamExperience;
        }

        public static void IgnoreXpGain(ulong amount, float duration)
        {
            if (amount == 0 || duration <= 0 || !Run.instance)
                return;

            if (_ignoreXpChunks.Count == 0)
            {
                Run.onRunDestroyGlobal += onRunDestroyGlobal;
            }

            IgnoreXpChunk chunk = new IgnoreXpChunk(amount, Run.FixedTimeStamp.now + duration);
            int chunkIndex = _ignoreXpChunks.BinarySearch(chunk, Comparer<IgnoreXpChunk>.Create((a, b) =>
            {
                return a.ExpirationTime.CompareTo(b.ExpirationTime);
            }));

            if (chunkIndex < 0)
            {
                chunkIndex = ~chunkIndex;
            }

            _ignoreXpChunks.Insert(chunkIndex, chunk);
        }

        static void onRunDestroyGlobal(Run run)
        {
            _ignoreXpChunks.Clear();
        }

        static void TeamManager_GiveTeamExperience(ILContext il)
        {
            if (!il.Method.TryFindParameter<TeamIndex>(out ParameterDefinition teamIndexParameter))
            {
                Log.Error("Failed to find TeamIndex parameter");
                return;
            }

            if (!il.Method.TryFindParameter<ulong>("experience", out ParameterDefinition experienceParameter))
            {
                Log.Error("Failed to find experience parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            ILLabel skipPrayerBeadsXpLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(teamIndexParameter.Sequence),
                               x => x.MatchLdcI4((int)TeamIndex.Player),
                               x => x.MatchBneUn(out skipPrayerBeadsXpLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarga, experienceParameter);
            c.EmitDelegate<AllowPrayerBeadsXpGainDelegate>(allowPrayerBeadsXpGain);
            c.Emit(OpCodes.Brfalse, skipPrayerBeadsXpLabel);

            static bool allowPrayerBeadsXpGain(ref ulong experience)
            {
                if (experience > 0 && _ignoreXpChunks.Count > 0)
                {
                    while (_ignoreXpChunks.Count > 0)
                    {
                        IgnoreXpChunk chunk = _ignoreXpChunks[0];
                        if (!chunk.ExpirationTime.hasPassed)
                        {
                            if (chunk.Amount > experience)
                            {
                                chunk.Amount -= experience;
                                experience = 0;
                                break;
                            }

                            experience -= chunk.Amount;
                        }

                        _ignoreXpChunks.RemoveAt(0);
                    }

                    if (_ignoreXpChunks.Count == 0)
                    {
                        Run.onRunDestroyGlobal -= onRunDestroyGlobal;
                    }

                    if (experience == 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        delegate bool AllowPrayerBeadsXpGainDelegate(ref ulong experience);

        sealed class IgnoreXpChunk
        {
            public ulong Amount;
            public readonly Run.FixedTimeStamp ExpirationTime;

            public IgnoreXpChunk(ulong amount, Run.FixedTimeStamp expirationTime)
            {
                Amount = amount;
                ExpirationTime = expirationTime;
            }
        }
    }
}
