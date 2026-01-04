using ItemQualities.Utilities;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class BarrageOnBoss
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene;
        }

        private static void SceneDirector_PopulateScene(ILContext il)
        {
            ILLabel label = null;
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(
                    x => x.MatchLdsfld(typeof(DLC3Content.Artifacts), nameof(DLC3Content.Artifacts.Prestige)),
                    x => x.MatchCallOrCallvirt(typeof(RunArtifactManager), nameof(RunArtifactManager.IsArtifactEnabled))
                ) &&
                c.TryGotoNext(MoveType.Before,
                    x => x.MatchBrfalse(out label)
                ))
            {
                c.EmitDelegate<Func<bool, bool>>(checkWarBonds);
            }
            else
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
        }

        private static bool checkWarBonds(bool forcespawn)
        {
            ItemQualityCounts barrageOnBoss = ItemQualityUtils.GetTeamItemCounts(ItemQualitiesContent.ItemQualityGroups.BarrageOnBoss, TeamIndex.Player, false);
            if (barrageOnBoss.TotalQualityCount > 0)
            {
                return true;
            }
            return forcespawn; 
        }
    }
}
