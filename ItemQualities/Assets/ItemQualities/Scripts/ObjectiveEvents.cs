using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    internal static class ObjectiveEvents
    {
        [SystemInitializer]
        private static void Init()
        {
            MoonBatteryMissionController.onInstanceChangedGlobal += onMoonBatteryMissionControllerInstanceChangedGlobal;

            IL.RoR2.VoidStageMissionController.Start += VoidStageMissionController_Start;
        }

        #region MoonBatteryMissionController
        public static event Action<HoldoutZoneController> OnMoonPillarChargedGlobal;
        public static event Action<HoldoutZoneController> OnFinalMoonPillarChargedGlobal;

        private static void onMoonBatteryMissionControllerInstanceChangedGlobal()
        {
            if (MoonBatteryMissionController.instance)
            {
                MoonBatteryMissionController.instance.gameObject.EnsureComponent<MoonBatteryMissionControllerEvents>();
            }
        }

        private sealed class MoonBatteryMissionControllerEvents : MonoBehaviour
        {
            private MoonBatteryMissionController _missionController;

            private readonly List<HoldoutZoneController> _chargedOrder = new List<HoldoutZoneController>();

            private void Awake()
            {
                _missionController = GetComponent<MoonBatteryMissionController>();
            }

            private void OnEnable()
            {
                foreach (HoldoutZoneController holdoutZoneController in _missionController.batteryHoldoutZones)
                {
                    holdoutZoneController.onCharged.AddListener(onPillarCharged);
                }
            }

            private void OnDisable()
            {
                foreach (HoldoutZoneController holdoutZoneController in _missionController.batteryHoldoutZones)
                {
                    holdoutZoneController.onCharged.RemoveListener(onPillarCharged);
                }
            }

            private void FixedUpdate()
            {
                if (_chargedOrder.Count >= _missionController.numRequiredBatteries)
                {
                    HoldoutZoneController finalPillarHoldoutZone = _chargedOrder[_missionController.numRequiredBatteries - 1];

                    Log.Debug($"Final pillar charged, invoking completed event and stopping tracking");

                    OnFinalMoonPillarChargedGlobal?.Invoke(finalPillarHoldoutZone);

                    Destroy(this);
                }
            }

            private void onPillarCharged(HoldoutZoneController pillarHoldoutZone)
            {
                _chargedOrder.Add(pillarHoldoutZone);

                Log.Debug($"Pillar {Util.GetGameObjectHierarchyName(pillarHoldoutZone.gameObject)} charged, invoking events");

                OnMoonPillarChargedGlobal?.Invoke(pillarHoldoutZone);
            }
        }
        #endregion

        #region VoidStageMissionController
        public static event Action<HoldoutZoneController> OnVoidStagePillarChargedServer;
        public static event Action<HoldoutZoneController> OnFinalVoidStagePillarChargedServer;

        static void VoidStageMissionController_Start(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition voidBatterySpawnRequestVar = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<DirectorCore>("get_" + nameof(DirectorCore.instance)),
                               x => x.MatchLdloc<DirectorSpawnRequest>(il, out voidBatterySpawnRequestVar),
                               x => x.MatchCallOrCallvirt<DirectorCore>(nameof(DirectorCore.TrySpawnObject))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition eventsControllerVar = il.AddVariable<VoidStageMissionControllerEvents>();
            {
                ILCursor cursor = c.Clone().Goto(0);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<VoidStageMissionController, VoidStageMissionControllerEvents>>(setupComponent);
                cursor.Emit(OpCodes.Stloc, eventsControllerVar);

                static VoidStageMissionControllerEvents setupComponent(VoidStageMissionController missionController)
                {
                    // component is only relevant on server
                    return NetworkServer.active ? missionController.gameObject.EnsureComponent<VoidStageMissionControllerEvents>() : null;
                }
            }

            c.Emit(OpCodes.Ldloc, voidBatterySpawnRequestVar);
            c.Emit(OpCodes.Ldloc, eventsControllerVar);
            c.EmitDelegate<Action<DirectorSpawnRequest, VoidStageMissionControllerEvents>>(addVoidBatterySpawnedEventListener);

            static void addVoidBatterySpawnedEventListener(DirectorSpawnRequest voidBatterySpawnRequest, VoidStageMissionControllerEvents eventsController)
            {
                if (eventsController)
                {
                    voidBatterySpawnRequest.onSpawnedServer += eventsController.OnVoidBatterySpawned;
                }
            }
        }

        private sealed class VoidStageMissionControllerEvents : MonoBehaviour
        {
            private VoidStageMissionController _missionController;

            private readonly List<HoldoutZoneController> _batteryHoldoutZones = new List<HoldoutZoneController>();

            private readonly List<HoldoutZoneController> _chargedOrder = new List<HoldoutZoneController>();

            private void Awake()
            {
                _missionController = GetComponent<VoidStageMissionController>();
            }

            private void OnEnable()
            {
                foreach (HoldoutZoneController batteryHoldoutZone in _batteryHoldoutZones)
                {
                    batteryHoldoutZone.onCharged.AddListener(onBatteryZoneCharged);
                }
            }

            private void OnDisable()
            {
                foreach (HoldoutZoneController batteryHoldoutZone in _batteryHoldoutZones)
                {
                    batteryHoldoutZone.onCharged.RemoveListener(onBatteryZoneCharged);
                }
            }

            private void FixedUpdate()
            {
                if (_chargedOrder.Count >= _missionController.numBatteriesSpawned)
                {
                    HoldoutZoneController finalBatteryHoldoutZone = _chargedOrder[_missionController.numBatteriesSpawned - 1];

                    Log.Debug($"Final void battery charged, invoking completed event and stopping tracking");

                    OnFinalVoidStagePillarChargedServer?.Invoke(finalBatteryHoldoutZone);

                    Destroy(this);
                }
            }

            private void onBatteryZoneCharged(HoldoutZoneController batteryHoldoutZone)
            {
                _chargedOrder.Add(batteryHoldoutZone);

                Log.Debug($"Void battery {Util.GetGameObjectHierarchyName(batteryHoldoutZone.gameObject)} charged, invoking events");

                OnVoidStagePillarChargedServer?.Invoke(batteryHoldoutZone);
            }

            public void OnVoidBatterySpawned(SpawnCard.SpawnResult spawnResult)
            {
                // If listener is somehow invoked after this component is destroyed
                if (!this)
                    return;

                if (spawnResult.success &&
                    spawnResult.spawnedInstance &&
                    spawnResult.spawnedInstance.TryGetComponent(out HoldoutZoneController batteryHoldoutZone))
                {
                    _batteryHoldoutZones.Add(batteryHoldoutZone);

                    if (isActiveAndEnabled)
                    {
                        batteryHoldoutZone.onCharged.AddListener(onBatteryZoneCharged);
                    }
                }
            }
        }
        #endregion
    }
}
