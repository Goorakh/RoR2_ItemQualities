using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class BandolierQualityInfo : NetworkBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Bandolier.AmmoPack_prefab).OnSuccess(bandolierPrefab =>
            {
                bandolierPrefab.EnsureComponent<BandolierQualityInfo>();
            });
        }

        [NonSerialized]
        [HideInInspector]
        public int ExtraSkillChargesServer = 0;

        [Server]
        public void OnApplyAmmoPackServer(GameObject recipient)
        {
            if (!recipient)
                return;

            if (Util.HasEffectiveAuthority(recipient))
            {
                applyAmmoPack(recipient, ExtraSkillChargesServer);
            }
            else if (recipient.TryGetComponent(out NetworkIdentity networkIdentity) && networkIdentity.clientAuthorityOwner != null)
            {
                TargetApplyAmmoPack(networkIdentity.clientAuthorityOwner, recipient, ExtraSkillChargesServer);
            }
            else
            {
                Log.Warning($"Attempting to grant to non-networked object {Util.GetGameObjectHierarchyName(recipient)}");
            }
        }

        [TargetRpc]
        void TargetApplyAmmoPack(NetworkConnection connection, GameObject recipient, int extraSkillCharges)
        {
            applyAmmoPack(recipient, extraSkillCharges);
        }

        static void applyAmmoPack(GameObject recipient, int extraSkillCharges)
        {
            if (!recipient)
                return;

            if (!recipient.TryGetComponent(out SkillLocator skillLocator))
                return;
            
            List<GenericSkill> skills = new List<GenericSkill>(4);

            void tryAddSkill(GenericSkill genericSkill)
            {
                if (genericSkill &&
                    genericSkill.skillDef &&
                    genericSkill.baseRechargeInterval > 0f &&
                    (!genericSkill.skillDef.dontAllowPastMaxStocks || genericSkill.stock < genericSkill.maxStock))
                {
                    skills.Add(genericSkill);
                }
            }

            tryAddSkill(skillLocator.primary);
            tryAddSkill(skillLocator.secondary);
            tryAddSkill(skillLocator.utility);
            tryAddSkill(skillLocator.special);

            if (skills.Count == 0)
                return;

            Xoroshiro128Plus rng = new Xoroshiro128Plus(RoR2Application.rng.nextUlong);
            int skillIndex;
            GenericSkill skillToRestock;

            do
            {
                if(skills.Count == 0)
                    return;
                skillIndex = rng.RangeInt(0, skills.Count);
                skillToRestock = skills[skillIndex];
                skills.RemoveAt(skillIndex);
            } while (skillToRestock.skillDef.dontAllowPastMaxStocks);

            skillToRestock.stock += extraSkillCharges;

            Log.Debug($"Added {extraSkillCharges} stocks to {Util.GetGameObjectHierarchyName(recipient)} {skillLocator.FindSkillSlot(skillToRestock)}");
        }
    }
}
