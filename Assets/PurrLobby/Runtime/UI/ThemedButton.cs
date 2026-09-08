using PurrNet.UI;
using PurrNet.UI.HeroUI;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>Keeps HeroUI button states themed while its own animation handles interaction.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonElement))]
    public sealed class ThemedButton : ThemeBinding
    {
        [SerializeField] private ButtonElement _button;
        [SerializeField] private RectangleGraphic _graphic;
        [SerializeField] private ColorInfo _normalColor = new() { enabled = true, color = ColorType.Accent };
        [SerializeField] private ColorInfo _hoverColor = new() { enabled = true, color = ColorType.Accent };
        [SerializeField] private ColorTone _normalTone;
        [SerializeField] private ColorTone _hoverTone;
        [SerializeField, Range(0f, 1f)] private float _normalOpacity = 1f;
        [SerializeField, Range(0f, 1f)] private float _hoverOpacity = 1f;
        [SerializeField, Min(0f)] private float _normalBrightness = 1f;
        [SerializeField, Min(0f)] private float _hoverBrightness = 1.12f;

        private void Reset() => _button = GetComponent<ButtonElement>();

        public void SetColors(ColorInfo normal, ColorInfo hover,
            ColorTone? normalTone = null, ColorTone? hoverTone = null)
        {
            _normalColor = normal;
            _hoverColor = hover;
            _normalTone = normalTone.GetValueOrDefault();
            _hoverTone = hoverTone.GetValueOrDefault();
            // Dynamic states use the new role at full strength; a tint inferred
            // from the prefab's original role need not suit its replacement.
            _normalBrightness = 1f;
            _hoverBrightness = normalTone.HasValue || hoverTone.HasValue ? 1f : 1.12f;
            ApplyPalette();
        }

        protected override void ApplyPalette()
        {
            if (!_button)
                _button = GetComponent<ButtonElement>();
            if (!_button || !palette)
                return;

            if (_normalColor.enabled)
            {
                var color = _normalTone.Apply(_normalColor, palette);
                color.r = Mathf.Clamp01(color.r * _normalBrightness);
                color.g = Mathf.Clamp01(color.g * _normalBrightness);
                color.b = Mathf.Clamp01(color.b * _normalBrightness);
                color.a *= _normalOpacity;
                _button.backgroundNormal = color;
                if (!Application.isPlaying && _graphic)
                    _graphic.graphicColor = color;
            }

            if (_hoverColor.enabled)
            {
                var color = _hoverTone.Apply(_hoverColor, palette);
                color.r = Mathf.Clamp01(color.r * _hoverBrightness);
                color.g = Mathf.Clamp01(color.g * _hoverBrightness);
                color.b = Mathf.Clamp01(color.b * _hoverBrightness);
                color.a *= _hoverOpacity;
                _button.backgroundHover = color;
            }
        }
    }
}
