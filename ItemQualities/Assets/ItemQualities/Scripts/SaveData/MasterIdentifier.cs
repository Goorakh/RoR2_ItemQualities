using ItemQualities.Serialization;
using ProperSave.Data;
using RoR2;
using System.Linq;

namespace ItemQualities.SaveData
{
    internal sealed class MasterIdentifier : IBinarySerializable
    {
        public UserIDData UserID { get; private set; }

        public int MinionIndex { get; private set; }

        public static MasterIdentifier FromMaster(CharacterMaster master)
        {
            UserIDData userID = null;
            int minionIndex = -1;
            if (master.playerCharacterMasterController)
            {
                userID = HeaderUserData.Create(master.playerCharacterMasterController).UserId;
            }
            else if (master.minionOwnership && master.minionOwnership.ownerMaster && master.minionOwnership.ownerMaster.playerCharacterMasterController)
            {
                userID = HeaderUserData.Create(master.minionOwnership.ownerMaster.playerCharacterMasterController).UserId;

                for (int i = 0; i < master.minionOwnership.group.memberCount; i++)
                {
                    if (ReferenceEquals(master.minionOwnership.group.members[i], master.minionOwnership))
                    {
                        minionIndex = i;
                        break;
                    }
                }
            }

            if (userID == null)
            {
                return null;
            }

            return new MasterIdentifier
            {
                UserID = userID,
                MinionIndex = minionIndex,
            };
        }

        public CharacterMaster ResolveMaster()
        {
            if (UserID == null)
                return null;

            NetworkUserId networkUserId = UserID.Load();

            NetworkUser networkUser = NetworkUser.readOnlyInstancesList.FirstOrDefault(n => n.id.Equals(networkUserId));
            if (!networkUser || !networkUser.master)
                return null;

            CharacterMaster master = networkUser.master;

            if (MinionIndex != -1)
            {
                MinionOwnership.MinionGroup minionGroup = MinionOwnership.MinionGroup.FindGroup(master.netId);
                if (minionGroup != null && MinionIndex < minionGroup.memberCount)
                {
                    MinionOwnership minion = minionGroup.members[MinionIndex];
                    if (minion && minion.TryGetComponent(out CharacterMaster minionMaster))
                    {
                        return minionMaster;
                    }
                }

                Log.Warning($"Failed to resolve minion index {MinionIndex} for {Util.GetBestMasterName(master)}");

                return null;
            }

            return master;
        }

        public void Serialize(SerializerContext context)
        {
            context.Write(UserID.Load());
            context.WritePackedIndex32(MinionIndex);
        }

        public void Deserialize(DeserializerContext context)
        {
            UserID = UserIDData.Create(context.ReadNetworkUserId());
            MinionIndex = context.ReadPackedIndex32();
        }
    }
}
