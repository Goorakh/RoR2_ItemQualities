using HG;
using HG.GeneralSerializer;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Linq;
using System.Reflection;

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

            register(new ExplosionInfoDef
            {
                DefaultRangeGetter = getEntityStateInstanceFieldGetter(typeof(EntityStates.FalseSon.MeridiansWillFire), nameof(EntityStates.FalseSon.MeridiansWillFire.blastRadius))
            }, ExplosionInfoIndex.MeridiansWill);

            register(new ExplosionInfoDef
            {
                // FissureSlam adds 3 to the radius for the blast attack because ???
                DefaultRangeGetter = () => EntityStates.FalseSonBoss.FissureSlam.blastRadius + 3f
            }, ExplosionInfoIndex.FalseSonBossFissureSlam);

            register(new ExplosionInfoDef
            {
                DefaultRangeGetter = () => EntityStates.FalseSonBoss.PrimeDevastator.blastRadius
            }, ExplosionInfoIndex.FalseSonBossPrimeDevastator);

            register(new ExplosionInfoDef
            {
                DefaultRangeGetter = () => EntityStates.GolemMonster.ClapState.radius
            }, ExplosionInfoIndex.GolemClap);

            register(new ExplosionInfoDef
            {
                DefaultRangeGetter = getEntityStateInstanceFieldGetter(typeof(EntityStates.ImpBossMonster.BlinkState), nameof(EntityStates.ImpBossMonster.BlinkState.blastAttackRadius))
            }, ExplosionInfoIndex.ImpBossBlink);

            ExplosionInfoDef.GetDefaultRangeDelegate getEntityStateInstanceFieldGetter(Type entityStateType, string fieldName)
            {
                if (entityStateType is null)
                    throw new ArgumentNullException(nameof(entityStateType));

                if (string.IsNullOrEmpty(fieldName))
                    throw new ArgumentException($"'{nameof(fieldName)}' cannot be null or empty.", nameof(fieldName));

                EntityStateConfiguration stateConfiguration = ContentManager.entityStateConfigurations.FirstOrDefault(esc => (Type)esc.targetType == entityStateType);
                if (stateConfiguration)
                {
                    FieldInfo field = entityStateType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(float))
                    {
                        return () =>
                        {
                            foreach (SerializedField serializedField in stateConfiguration.serializedFieldsCollection.serializedFields)
                            {
                                if (serializedField.fieldName == field.Name)
                                {
                                    return (float)serializedField.fieldValue.GetValue(field);
                                }
                            }

                            return 0f;
                        };
                    }
                    else
                    {
                        Log.Error($"Failed to find target field '{fieldName}' in {entityStateType.FullName} ({stateConfiguration.name})");
                    }
                }
                else
                {
                    Log.Error($"Failed to find entity state configuration for type '{entityStateType.FullName}'");
                }

                return () => 0f;
            }

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
