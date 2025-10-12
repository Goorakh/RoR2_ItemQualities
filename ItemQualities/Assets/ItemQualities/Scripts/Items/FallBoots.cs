using MonoMod.Cil;
using RoR2;
using Mono.Cecil.Cil;
using EntityStates.Headstompers;
using System;

namespace ItemQualities.Items
{
    public class Headset
    {
        
        [SystemInitializer]
        static void Init()
        {
           
            IL.EntityStates.Headstompers.HeadstompersFall.DoStompExplosionAuthority += QualityStomp;

        }

        private static void QualityStomp(ILContext il)
        {
            var c = new ILCursor(il);
            
            c.Emit(OpCodes.Ldarg_0); 
            c.EmitDelegate<Action<HeadstompersFall>>((self) =>
            {
                //this is clunky but need to reset the base states so they dont re apply
                HeadstompersFall.maxDistance = 30f;
                HeadstompersFall.minimumDamageCoefficient = 10f;
                HeadstompersFall.maximumDamageCoefficient = 100f;

                ItemQualityCounts headset = ItemQualitiesContent.ItemQualityGroups.FallBoots.GetItemCounts(self.body.inventory);
                
                //this will keep it positive worst case 0 if you have like max int of each quality
                
                HeadstompersFall.maxDistance -= (3f  * (1f - 1f/ (1f + .9f * headset.UncommonCount))) +
                                                (6f  * (1f - 1f/ (1f + .9f * headset.RareCount))) +
                                                (9f  * (1f - 1f/ (1f + .9f * headset.EpicCount))) +
                                                (12f * (1f - 1f/ (1f + .9f * headset.LegendaryCount)));
                
                HeadstompersFall.minimumDamageCoefficient += (1f  * headset.UncommonCount) +
                                                             (2f  * headset.RareCount) +
                                                             (3f  * headset.EpicCount) +
                                                             (5f  * headset.LegendaryCount);
                
                HeadstompersFall.maximumDamageCoefficient += (10f  * headset.UncommonCount) +
                                                             (20f  * headset.RareCount) +
                                                             (30f  * headset.EpicCount) +
                                                             (50f  * headset.LegendaryCount);

            });
            
        }

    }
}
