using ItemQualities;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.MushroomBubbleDeployController
{
    public class MushroomBubbleDeploy : EntityState
    {
		static EffectIndex _bubbleShieldEndEffect = EffectIndex.Invalid;
		public static string initialSoundString;
		public static string destroySoundString;
		[SerializeField]
		public float destroyEffectRadius;

		float _lifetime;
		bool _appliedQualities = false;
		bool _startedMoving = false;
		CharacterBody _ownerBody;
		BeginRapidlyActivatingAndDeactivating _blinking;

		[SystemInitializer(typeof(EffectCatalogUtils))]
		static void Init()
		{
			_bubbleShieldEndEffect = EffectCatalogUtils.FindEffectIndex("BubbleShieldEndEffect");
			if (_bubbleShieldEndEffect == EffectIndex.Invalid)
			{
				Log.Warning("Failed to find charge effect index");
			}
		}

		public override void OnEnter()
		{
			base.OnEnter();
			Util.PlaySound(initialSoundString, base.gameObject);		
		}

		public override void FixedUpdate()
		{
			base.FixedUpdate();
			if (!_appliedQualities) {
				_blinking = GetComponent<BeginRapidlyActivatingAndDeactivating>();
				if (!_blinking) return;
				GenericOwnership getOwner = GetComponent<GenericOwnership>();
				if (!getOwner) return;
				if (!getOwner.ownerObject) return;
				_ownerBody = getOwner.ownerObject.GetComponent<CharacterBody>();
				if (!_ownerBody) return;

				ItemQualityCounts Mushroom = ItemQualitiesContent.ItemQualityGroups.Mushroom.GetItemCounts(_ownerBody.master.inventory);
				_lifetime = Mushroom.UncommonCount +
							Mushroom.RareCount * 3 +
							Mushroom.EpicCount * 6 +
							Mushroom.LegendaryCount * 12;

				float scaleMul = 1;
				switch (ItemQualitiesContent.ItemQualityGroups.Mushroom.GetHighestQualityInInventory(_ownerBody.master.inventory))
				{
					case QualityTier.Uncommon:
						scaleMul = 1.5f;
						break;
					case QualityTier.Rare:
						scaleMul = 1.25f;
						break;
					case QualityTier.Epic:
						scaleMul = 1f;
						break;
					case QualityTier.Legendary:
						scaleMul = 0.75f;
						break;
				}
				_blinking.delayBeforeBeginningBlinking = _lifetime - 0.5f;
				base.gameObject.transform.localScale = Vector3.one * scaleMul;
				destroyEffectRadius *= scaleMul;
				_appliedQualities = true;
			}

			if (!_ownerBody) return;
			if (_ownerBody.notMovingStopwatch == 0) {
				_startedMoving = true;
			}
			if (_startedMoving) {
				
				if (base.fixedAge >= _lifetime && NetworkServer.active)
				{
					EntityState.Destroy(base.gameObject);
				}
			} else {
				base.fixedAge = 0;
				_blinking.fixedAge = 0;
			}
		}

		public override void OnExit()
		{
			base.OnExit();
			EffectManager.SpawnEffect(_bubbleShieldEndEffect, new EffectData
			{
				origin = base.transform.position,
				rotation = base.transform.rotation,
				scale = destroyEffectRadius
			}, transmit: false);
			Util.PlaySound(destroySoundString, base.gameObject);
		}
	}
}
