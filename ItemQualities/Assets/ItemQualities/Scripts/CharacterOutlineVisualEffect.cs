using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(TemporaryVisualEffect))]
    public class CharacterOutlineVisualEffect : MonoBehaviour
    {
        public Highlight.HighlightColor HighlightColor;

        public Color CustomHighlightColor = Color.black;

        public float HighlightStrength = 1f;

        TemporaryVisualEffect _visualEffect;

        readonly List<Highlight> _highlights = new List<Highlight>();

        void Awake()
        {
            _visualEffect = GetComponent<TemporaryVisualEffect>();
        }

        void OnEnable()
        {
            CharacterModel attachedCharacterModel = null;
            if (_visualEffect && _visualEffect.healthComponent)
            {
                CharacterBody body = _visualEffect.healthComponent.body;
                if (body)
                {
                    ModelLocator modelLocator = body.modelLocator;
                    if (modelLocator && modelLocator.modelTransform)
                    {
                        attachedCharacterModel = modelLocator.modelTransform.GetComponent<CharacterModel>();
                    }
                }
            }

            if (attachedCharacterModel)
            {
                foreach (CharacterModel.RendererInfo rendererInfo in attachedCharacterModel.baseRendererInfos)
                {
                    if (!rendererInfo.ignoreOverlays && rendererInfo.renderer)
                    {
                        Highlight highlight = rendererInfo.renderer.gameObject.AddComponent<Highlight>();
                        highlight.targetRenderer = rendererInfo.renderer;
                        highlight.strength = HighlightStrength;
                        highlight.highlightColor = HighlightColor;
                        highlight.CustomColor = CustomHighlightColor;
                        highlight.isOn = true;

                        _highlights.Add(highlight);
                    }
                }
            }
        }

        void OnDisable()
        {
            foreach (Highlight highlight in _highlights)
            {
                Destroy(highlight);
            }

            _highlights.Clear();
        }
    }
}
