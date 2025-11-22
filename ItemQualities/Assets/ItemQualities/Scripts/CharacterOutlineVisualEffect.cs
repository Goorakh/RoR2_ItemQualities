using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(TemporaryVisualEffect))]
    public sealed class CharacterOutlineVisualEffect : MonoBehaviour
    {
        public Highlight.HighlightColor HighlightColor;

        public Color CustomHighlightColor = Color.black;

        public float HighlightStrength = 1f;

        TemporaryVisualEffect _visualEffect;

        Highlight _highlight;

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

            List<Renderer> highlightRenderers = new List<Renderer>();

            if (attachedCharacterModel)
            {
                foreach (CharacterModel.RendererInfo rendererInfo in attachedCharacterModel.baseRendererInfos)
                {
                    if (!rendererInfo.ignoreOverlays && rendererInfo.renderer)
                    {
                        highlightRenderers.Add(rendererInfo.renderer);
                    }
                }
            }

            if (highlightRenderers.Count > 0)
            {
                if (!_highlight)
                {
                    _highlight = gameObject.AddComponent<Highlight>();
                    _highlight.strength = HighlightStrength;
                    _highlight.highlightColor = HighlightColor;
                    _highlight.CustomColor = CustomHighlightColor;
                    _highlight.isOn = true;
                }
                else
                {
                    _highlight.enabled = true;
                }

                _highlight.SetTargetRendererList(highlightRenderers);
            }
        }

        void OnDisable()
        {
            if (_highlight)
            {
                _highlight.enabled = false;
            }
        }
    }
}
