using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System;
using UnityEngine;

namespace ItemQualities.UI
{
    internal sealed class TooltipContext : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.UI.TooltipController.SetTooltipProvider += TooltipController_SetTooltipProvider;
        }

        private static void TooltipController_SetTooltipProvider(ILContext il)
        {
            if (!il.Method.TryFindParameter<TooltipProvider>(out ParameterDefinition tooltipProviderParameter))
            {
                Log.Error("Failed to find TooltipProvider parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(tooltipProviderParameter.Sequence),
                               x => x.MatchLdfld<TooltipProvider>(nameof(TooltipProvider.extraUIDisplayPrefab)),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<TooltipController>(nameof(TooltipController.extraUIPos)),
                               x => x.MatchCallOrCallvirt<UnityEngine.Object>(nameof(UnityEngine.Object.Instantiate))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg, tooltipProviderParameter);
            c.EmitDelegate<Action<GameObject, TooltipProvider>>(onCreateExtraUIDisplayPrefab);

            static void onCreateExtraUIDisplayPrefab(GameObject extraUIDisplay, TooltipProvider tooltipProvider)
            {
                if (extraUIDisplay && extraUIDisplay.TryGetComponent(out TooltipContext tooltipContext))
                {
                    tooltipContext.SourceTooltipProvider = tooltipProvider;
                    tooltipContext.OnTooltipProviderDiscovered?.Invoke();
                }
            }
        }

        public TooltipProvider SourceTooltipProvider { get; private set; }

        public event Action OnTooltipProviderDiscovered;
    }
}
