using HG;
using RoR2;
using System;

namespace ItemQualities
{
    public static class ExplosionInfoCatalog
    {
        static ExplosionInfoDef[] _explosionInfoDefs = Array.Empty<ExplosionInfoDef>();

        public static int ExplosionInfoDefCount => _explosionInfoDefs.Length;

        public static readonly CatalogModHelper<ExplosionInfoDef> ModHelper = new CatalogModHelper<ExplosionInfoDef>(
            (index, explosionInfoDef) => register(explosionInfoDef, (ExplosionInfoIndex)index),
            explosionInfoDef => explosionInfoDef.Name);

        static void register(ExplosionInfoDef explosionInfoDef, ExplosionInfoIndex explosionInfoIndex)
        {
            if (explosionInfoIndex > ExplosionInfoIndex.None && explosionInfoIndex < ExplosionInfoIndex.Count)
            {
                explosionInfoDef.Name = explosionInfoIndex.ToString();
            }

            explosionInfoDef.Index = explosionInfoIndex;
            _explosionInfoDefs[(int)explosionInfoIndex] = explosionInfoDef;
        }

        [SystemInitializer]
        static void Init()
        {
            _explosionInfoDefs = new ExplosionInfoDef[(int)ExplosionInfoIndex.Count];

            register(new ExplosionInfoDef
            {
                DefaultRangeGetter = () => EntityStates.CaptainSupplyDrop.HitGroundState.impactBulletRadius
            }, ExplosionInfoIndex.CaptainSupplyDropImpact);

            ModHelper.CollectAndRegisterAdditionalEntries(ref _explosionInfoDefs);
        }

        public static ExplosionInfoDef GetExplosionInfoDef(ExplosionInfoIndex index)
        {
            return ArrayUtils.GetSafe(_explosionInfoDefs, (int)index);
        }

        public static ExplosionInfoIndex FindExplosionInfoIndex(string name)
        {
            foreach (ExplosionInfoDef explosionInfoDef in _explosionInfoDefs)
            {
                if (explosionInfoDef.Name == name)
                {
                    return explosionInfoDef.Index;
                }
            }

            return ExplosionInfoIndex.None;
        }
    }
}
