using RoR2;
using System;

namespace ItemQualities
{
    internal static class MinionHooks
    {
        public delegate void MinionMemberDelegate(MinionOwnership.MinionGroup minionGroup, CharacterMaster ownerMaster, CharacterMaster memberMaster);

        public static event MinionMemberDelegate OnMinionGroupMemberDiscoveredGlobal;
        public static event MinionMemberDelegate OnMinionGroupMemberLostGlobal;

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.MinionOwnership.HandleGroupDiscovery += MinionOwnership_HandleGroupDiscovery;
        }

        private static void MinionOwnership_HandleGroupDiscovery(On.RoR2.MinionOwnership.orig_HandleGroupDiscovery orig, MinionOwnership self, MinionOwnership.MinionGroup newGroup)
        {
            CharacterMaster memberMaster = null;
            if (OnMinionGroupMemberLostGlobal != null || OnMinionGroupMemberDiscoveredGlobal != null)
            {
                memberMaster = self.GetComponent<CharacterMaster>();
            }

            if (!ReferenceEquals(self.group?.resolvedOwnerMaster, null) && OnMinionGroupMemberLostGlobal != null)
            {
                try
                {
                    OnMinionGroupMemberLostGlobal(self.group, self.group.resolvedOwnerMaster, memberMaster);
                }
                catch (Exception e)
                {
                    Log.Error_NoCallerPrefix(e.ToString());
                }
            }

            orig(self, newGroup);

            if (!ReferenceEquals(self.group?.resolvedOwnerMaster, null) && OnMinionGroupMemberDiscoveredGlobal != null)
            {
                try
                {
                    OnMinionGroupMemberDiscoveredGlobal(self.group, self.group.resolvedOwnerMaster, memberMaster);
                }
                catch (Exception e)
                {
                    Log.Error_NoCallerPrefix(e.ToString());
                }
            }
        }
    }
}
