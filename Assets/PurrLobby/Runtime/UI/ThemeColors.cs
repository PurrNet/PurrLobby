using PurrNet.UI;
using PurrNet.UI.HeroUI;
using UnityEngine;
using UnityEngine.UI;

namespace PurrNet.Lobby
{
    internal static class ThemeColors
    {
        public static void Set(Graphic graphic, ColorInfo color, int slot = 0)
        {
            if (!graphic)
                return;

            if (graphic.TryGetComponent<ColoredGraphic>(out var colored))
            {
                colored.SetColor(slot, color);
                colored.Refresh();
                return;
            }

            // Custom entry prefabs can still resolve their colors without a binding.
            var palette = graphic.GetComponentInParent<IPaletteProvider>(true)?.palette;
            if (!color.enabled || !palette)
                return;

            if (graphic is IColored slots)
                slots.SetColor(slot, color.GetColor(palette));
            else
                graphic.color = color.GetColor(palette);
        }

        public static void Set(ButtonElement button, ColorInfo normal, ColorInfo hover,
            ColorTone? normalTone = null, ColorTone? hoverTone = null)
        {
            if (!button)
                return;

            if (button.TryGetComponent<ThemedButton>(out var themed))
            {
                themed.SetColors(normal, hover, normalTone, hoverTone);
                return;
            }

            var palette = button.GetComponentInParent<IPaletteProvider>(true)?.palette;
            if (!palette)
                return;
            if (normal.enabled)
                button.backgroundNormal = normalTone.GetValueOrDefault().Apply(normal, palette);
            if (hover.enabled)
                button.backgroundHover = hoverTone.GetValueOrDefault().Apply(hover, palette);
        }
    }

    /// <summary>Connects the legacy HeroUI controls to their nearest palette.</summary>
    public abstract class ThemeBinding : MonoBehaviour
    {
        private IPaletteProvider _provider;
        private bool _dirty;

        protected ColorPalette palette => _provider?.palette;

        protected virtual void OnEnable()
        {
            ResolveProvider();
            ApplyPalette();
        }

        protected virtual void OnDisable()
        {
            if (_provider != null)
                _provider.onColorChange -= ApplyPalette;
            _provider = null;
        }

        protected virtual void Update()
        {
            if (_provider == null || _dirty)
            {
                ResolveProvider();
                if (_provider != null)
                    ApplyPalette();
                _dirty = false;
            }
        }

        private void OnTransformParentChanged()
        {
            if (!isActiveAndEnabled)
                return;
            ResolveProvider();
            ApplyPalette();
        }

        protected virtual void OnValidate()
        {
            // OnValidate may run while Unity loads objects; apply on the main loop.
            _dirty = true;
        }

        private void ResolveProvider()
        {
            var provider = GetComponentInParent<IPaletteProvider>(true);
            if (ReferenceEquals(provider, _provider))
                return;
            if (_provider != null)
                _provider.onColorChange -= ApplyPalette;
            _provider = provider;
            if (_provider != null)
                _provider.onColorChange += ApplyPalette;
        }

        protected abstract void ApplyPalette();
    }
}
