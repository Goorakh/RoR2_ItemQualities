using RoR2;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ItemQualities
{
    static class CustomInteractableChestRevealerAdder
    {
        [SystemInitializer]
        static void Init()
        {
            bool addedAnyInteractable = false;
            List<Type> interactableTypes = new List<Type>(ChestRevealer.TypesToCheck);
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (typeof(IInteractable).IsAssignableFrom(type))
                {
                    if (!interactableTypes.Contains(type))
                    {
                        interactableTypes.Add(type);
                        addedAnyInteractable = true;
                    }
                }
            }

            if (addedAnyInteractable)
            {
                ChestRevealer.typesToCheck = interactableTypes.ToArray();
            }
        }
    }
}
