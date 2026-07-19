using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;

namespace ItemQualities
{
    public partial class DamageTypes
    {
        [InitDuringStartupPhase(GameInitPhase.DuringIntro)]
        private static void Init_Void()
        {
            On.EntityStates.VoidInfestor.Infest.OnEnter += Infest_OnEnter;
            On.RoR2.DotController.OnDotStackAddedServer += DotController_OnDotStackAddedServer;
            IL.RoR2.FogDamageController.MyFixedUpdate += FogDamageController_MyFixedUpdate;
            On.RoR2.Orbs.MissileVoidOrb.Begin += MissileVoidOrb_Begin;
            On.RoR2.Orbs.VoidLightningOrb.Begin += VoidLightningOrb_Begin;
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC1_ElementalRingVoid.ElementalRingVoidBlackHole_prefab).OnSuccess(elementalRingVoidBlackHole =>
            {
                elementalRingVoidBlackHole.AddComponent<AddVoidDamageType>();
            });
        }

        private static void VoidLightningOrb_Begin(On.RoR2.Orbs.VoidLightningOrb.orig_Begin orig, RoR2.Orbs.VoidLightningOrb self)
        {
            self.damageType.AddModdedDamageType(Void);
            orig(self);
        }

        private static void MissileVoidOrb_Begin(On.RoR2.Orbs.MissileVoidOrb.orig_Begin orig, RoR2.Orbs.MissileVoidOrb self)
        {
            self.damageType.AddModdedDamageType(Void);
            orig(self);
        }

        private static void Infest_OnEnter(On.EntityStates.VoidInfestor.Infest.orig_OnEnter orig, EntityStates.VoidInfestor.Infest self)
        {
            orig(self);

            if (self.attack != null)
            {
                self.attack.damageType.AddModdedDamageType(Void);
            }
        }

        private static void DotController_OnDotStackAddedServer(On.RoR2.DotController.orig_OnDotStackAddedServer orig, DotController self, object _dotStack)
        {
            orig(self, _dotStack);

            DotController.DotStack dotStack = (DotController.DotStack)_dotStack;
            if (dotStack.dotIndex == DotController.DotIndex.Fracture)
            {
                dotStack.damageType.AddModdedDamageType(Void);
            }
        }

        private static void FogDamageController_MyFixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition fogDamageTypeVar = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdloc<DamageTypeCombo>(il, out fogDamageTypeVar),
                               x => x.MatchStfld<DamageInfo>(nameof(DamageInfo.damageType))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloca, fogDamageTypeVar);
            c.EmitDelegate<AddVoidDamageTypeDelegate>(addVoidDamageType);
            static void addVoidDamageType(ref DamageTypeCombo fogDamageType)
            {
                fogDamageType.AddModdedDamageType(Void);
            }
        }

        private delegate void AddVoidDamageTypeDelegate(ref DamageTypeCombo damageType);

        private static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!ItemHooks.TryGotoNextItemCountVariable(c,
                                                        typeof(DLC1Content.Items),
                                                        nameof(DLC1Content.Items.ExplodeOnDeathVoid),
                                                        out VariableDefinition explodeOnDeathVoidCountVariable))
            {
                Log.Error("Failed to find ExplodeOnDeathVoid item count variable");
                return;
            }

            ILLabel afterExplodeOnDeathVoidLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(explodeOnDeathVoidCountVariable),
                               x => x.MatchLdcI4(0),
                               x => x.MatchBle(out afterExplodeOnDeathVoidLabel)))
            {
                Log.Error("Failed to find ExplodeOnDeathVoid item count check location");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(CommonReflectionCache.GetComponent.OfType<DelayBlast>.Method))
                || !c.IsBefore(afterExplodeOnDeathVoidLabel.Target))
            {
                Log.Error("Failed to find ExplodeOnDeathVoid DelayBlast patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.EmitDelegate<Action<DelayBlast>>(addVoidDamageType);

            static void addVoidDamageType(DelayBlast delayBlast)
            {
                if (delayBlast)
                {
                    delayBlast.damageType.AddModdedDamageType(Void);
                }
            }
        }

        private sealed class AddVoidDamageType : MonoBehaviour
        {
            private ProjectileDamage _projectileDamage;

            private void Awake()
            {
                _projectileDamage = GetComponent<ProjectileDamage>();
            }

            private void Start()
            {
                if (_projectileDamage)
                {
                    _projectileDamage.damageType.AddModdedDamageType(Void);
                }
            }
        }
    }
}
