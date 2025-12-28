using RoR2;

namespace ItemQualities.Items
{
    public sealed class FeatherQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.Feather;
        }

        CharacterBodyExtraStatsTracker _bodyStats;

        protected override void Awake()
        {
            base.Awake();

            _bodyStats = GetComponent<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
            _bodyStats.OnHitGroundServer += onHitGroundServer;
        }

        void OnDisable()
        {
            GlobalEventManager.onCharacterDeathGlobal -= onCharacterDeathGlobal;
            _bodyStats.OnHitGroundServer -= onHitGroundServer;
        }

        void onCharacterDeathGlobal(DamageReport report)
        {
            if (report.attackerBody != Body || (Body.characterMotor && Body.characterMotor.isGrounded))
                return;

            ItemQualityCounts feather = Stacks;

            int maxJumps = (feather.UncommonCount * 2) +
                           (feather.RareCount * 4) +
                           (feather.EpicCount * 6) +
                           (feather.LegendaryCount * 8);

            if (report.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.FeatherExtraJumps) < maxJumps)
            {
                report.attackerBody.AddBuff(ItemQualitiesContent.Buffs.FeatherExtraJumps);
            }
        }

        void onHitGroundServer(CharacterMotor.HitGroundInfo info)
        {
            Body.SetBuffCount(ItemQualitiesContent.Buffs.FeatherExtraJumps.buffIndex, 0);
        }
    }
}
