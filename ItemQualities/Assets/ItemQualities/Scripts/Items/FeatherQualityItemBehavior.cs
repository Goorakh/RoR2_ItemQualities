using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class FeatherQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.Feather;
        }

        private CharacterBodyExtraStatsTracker _bodyStats;

        protected override void Awake()
        {
            base.Awake();

            _bodyStats = this.GetComponentCached<CharacterBodyExtraStatsTracker>();
        }

        private void OnEnable()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
            _bodyStats.OnHitGroundServer += onHitGroundServer;
        }

        private void OnDisable()
        {
            GlobalEventManager.onCharacterDeathGlobal -= onCharacterDeathGlobal;
            _bodyStats.OnHitGroundServer -= onHitGroundServer;
        }

        private void onCharacterDeathGlobal(DamageReport report)
        {
            if (report.attackerBody != Body || (Body.characterMotor && Body.characterMotor.isGrounded))
                return;

            ref readonly ItemQualityCounts feather = ref Stacks;

            int maxJumps = (feather.UncommonCount * 2) +
                           (feather.RareCount * 4) +
                           (feather.EpicCount * 6) +
                           (feather.LegendaryCount * 8);

            if (report.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.FeatherExtraJumps) < maxJumps)
            {
                report.attackerBody.AddBuff(ItemQualitiesContent.Buffs.FeatherExtraJumps);
            }
        }

        private void onHitGroundServer(CharacterMotor.HitGroundInfo info)
        {
            Body.SetBuffCount(ItemQualitiesContent.Buffs.FeatherExtraJumps.buffIndex, 0);
        }
    }
}
