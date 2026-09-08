using PurrNet.UI;
using PurrNet.UI.HeroUI;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>Themes a HeroUI toggle without changing its input, audio or knob animation.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ToggleElement))]
    public sealed class ThemedToggle : ThemeBinding
    {
        [SerializeField] private ToggleElement _toggle;
        [SerializeField] private RectangleGraphic _background;
        [SerializeField] private ColorInfo _onColor = new() { enabled = true, color = ColorType.Accent };
        [SerializeField] private ColorInfo _offColor = new() { enabled = true, color = ColorType.Surface };
        [SerializeField] private ColorTone _onTone;
        [SerializeField] private ColorTone _offTone = new()
        {
            shade = new ColorInfo { enabled = true, color = ColorType.Surface, contrast = true },
            blend = 0.065f
        };
        [SerializeField, Range(0f, 1f)] private float _offOpacity = 1f;
        [SerializeField] private float _transitionDuration = 0.2f;
        [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Color _fromColor;
        private Color _targetColor;
        private Color _currentColor;
        private float _elapsed;
        private bool _initialized;
        private bool _lastValue;

        private void Reset() => _toggle = GetComponent<ToggleElement>();

        protected override void OnEnable()
        {
            if (!_toggle)
                _toggle = GetComponent<ToggleElement>();
            _initialized = false;
            base.OnEnable();
        }

        protected override void ApplyPalette()
        {
            if (!_toggle || !_background || !palette)
                return;

            _lastValue = _toggle.value;
            var info = _lastValue ? _onColor : _offColor;
            if (!info.enabled)
                return;

            var tone = _lastValue ? _onTone : _offTone;
            _targetColor = tone.Apply(info, palette);
            if (!_lastValue)
                _targetColor.a *= _offOpacity;
            _fromColor = _initialized ? _currentColor : _targetColor;
            _currentColor = _fromColor;
            _elapsed = 0f;
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_toggle || !_background || !palette)
                return;
            if (!_initialized || _lastValue != _toggle.value)
                ApplyPalette();
            if (!_initialized || !(_toggle.value ? _onColor : _offColor).enabled)
                return;

            _elapsed += Time.deltaTime;
            var progress = !Application.isPlaying || _transitionDuration <= 0f
                ? 1f : Mathf.Clamp01(_elapsed / _transitionDuration);
            _currentColor = Color.Lerp(_fromColor, _targetColor, _transitionCurve.Evaluate(progress));
            // ToggleElement writes a master tint in Update. Keep that tint neutral
            // and use the fill slot so its legacy RGB cannot multiply the palette.
            _background.color = Color.white;
            _background.graphicColor = _currentColor;
        }
    }
}
