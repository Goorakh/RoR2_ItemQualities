using RoR2;
using RoR2.Projectile;
using System;
using System.Linq;

namespace ItemQualities
{
    static class ProjectileHooks
    {
        public delegate void ProjectileEventDelegate(ProjectileController projectileController);
        public static event ProjectileEventDelegate OnProjectileLinkedToGhostGlobal;

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.Projectile.ProjectileController.Start += ProjectileController_Start;
        }

        static void ProjectileController_Start(On.RoR2.Projectile.ProjectileController.orig_Start orig, ProjectileController self)
        {
            orig(self);

            if (OnProjectileLinkedToGhostGlobal != null && self && self.ghost)
            {
                foreach (ProjectileEventDelegate onProjectileLinkedToGhost in OnProjectileLinkedToGhostGlobal.GetInvocationList()
                                                                                                             .OfType<ProjectileEventDelegate>())
                {
                    if (onProjectileLinkedToGhost != null)
                    {
                        try
                        {
                            onProjectileLinkedToGhost(self);
                        }
                        catch (Exception e)
                        {
                            Log.Error_NoCallerPrefix($"Failed to execute {nameof(OnProjectileLinkedToGhostGlobal)} event: {e}");
                        }
                    }
                }
            }
        }
    }
}
