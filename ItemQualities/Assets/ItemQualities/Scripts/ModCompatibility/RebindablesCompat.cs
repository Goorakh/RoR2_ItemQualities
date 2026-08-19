using BepInEx.Bootstrap;
using Rebindables;
using RoR2;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities.ModCompatibility
{
    internal static class RebindablesCompat
    {
        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(Rebindables.Rebindables.PluginGUID);

        // Storing ModKeybinds as objects to avoid issues with runtime checks of unloaded types.
        private static object _sprintArmorDashKeybind;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RegisterKeybinds()
        {
            _sprintArmorDashKeybind = RebindAPI.RegisterModKeybind(new ModKeybind("QUALITY_SPRINT_ARMOR_DASH", KeyCode.Mouse4, 10));
        }

        public static InputBankTest.ButtonState GetSprintArmorDashButtonState(InputBankTest inputBank)
        {
            return getButtonStateInternal(inputBank, _sprintArmorDashKeybind);
        }

        private static InputBankTest.ButtonState getButtonStateInternal(InputBankTest inputBank, object keybind)
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            static InputBankTest.ButtonState getButtonState(InputBankTest inputBank, object keybind)
            {
                return inputBank.GetButtonState((ModKeybind)keybind);
            }

            return Enabled ? getButtonState(inputBank, keybind) : default;
        }
    }
}
