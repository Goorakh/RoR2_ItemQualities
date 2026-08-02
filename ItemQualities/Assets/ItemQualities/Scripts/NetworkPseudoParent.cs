using RoR2;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class NetworkPseudoParent : NetworkBehaviour
    {
        [SyncVar(hook = nameof(SyncPseudoParent))]
        public GameObject pseudoParent;

        [SyncVar(hook = nameof(SyncPseudoParentChildLocatorString))]
        public string pseudoParentChildLocatorString = string.Empty;

        private Transform targetTransform;

        public override void OnStartClient()
        {
            base.OnStartClient();
            RefreshTargetTransform();
        }

        private void LateUpdate()
        {
            if (targetTransform)
            {
                transform.position = targetTransform.position;
            }
        }

        private void RefreshTargetTransform()
        {
            targetTransform = pseudoParent ? pseudoParent.transform : null;
            if (!string.IsNullOrWhiteSpace(pseudoParentChildLocatorString) &&
                targetTransform &&
                targetTransform.TryGetComponent(out ModelLocator modelLocator) &&
                modelLocator.modelChildLocator &&
                modelLocator.modelChildLocator.TryFindChild(pseudoParentChildLocatorString, out Transform targetChildTransform))
            {
                targetTransform = targetChildTransform;
            }
        }

        private void SyncPseudoParent(GameObject newPseudoParent)
        {
            pseudoParent = newPseudoParent;
            RefreshTargetTransform();
        }

        private void SyncPseudoParentChildLocatorString(string newPseudoParentChildLocatorString)
        {
            pseudoParentChildLocatorString = newPseudoParentChildLocatorString;
            RefreshTargetTransform();
        }
    }
}
