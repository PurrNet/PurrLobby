using System;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PurrLobby
{
    [ExecuteInEditMode]
    public class ToggleElement : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private bool _value = false;
        [Space]
        [SerializeField] RectangleGraphic _background;
        [SerializeField] RectangleGraphic _nob;
        [Space]
        [SerializeField] AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] Color _backgroundOn = Color.white;
        [SerializeField] Color _backgroundOff = Color.gray;
        [SerializeField] Vector2 _leftnobPosition = new Vector2(-0.5f, 0);
        [SerializeField] Vector2 _rightNobPosition = new Vector2(0.5f, 0);
        [SerializeField] float _transitionDuration = 0.2f;
        [Space]
        [SerializeField] private AudioClip[] _enableSound, _disableSound;

        public event Action<bool> onToggled;
        private float _timeSinceToggle = 0f;

#if UNITY_EDITOR
        private bool _lastValue = false;
#endif

        private void Awake()
        {
            _timeSinceToggle = _transitionDuration;
        }

        public bool value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                OnValueChanged();
            }
        }

        private void OnValueChanged()
        {
#if UNITY_EDITOR
            _lastValue = _value;
#endif
            _timeSinceToggle = 0f;
            onToggled?.Invoke(_value);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && _value != _lastValue)
            {
                OnValueChanged();
            }
        }
#endif

        public void Toggle()
        {
            value = !value;
        }

        private void Update()
        {
            float lerp = Mathf.Clamp01(_timeSinceToggle / _transitionDuration);
            lerp = _transitionCurve.Evaluate(lerp);

            var targetColor = _value ? _backgroundOn : _backgroundOff;
            var fromColor = _value ? _backgroundOff : _backgroundOn;
            var targetPosition = _value ? _rightNobPosition : _leftnobPosition;
            var fromPosition = _value ? _leftnobPosition : _rightNobPosition;

            _background.color = Color.Lerp(fromColor, targetColor, lerp);
            _nob.transform.localPosition = Vector3.Lerp(fromPosition, targetPosition, lerp);

            if (_timeSinceToggle <= _transitionDuration)
                _timeSinceToggle += Time.deltaTime;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Toggle();
            Sounds2D.Play(new AudioSession(_value ? _enableSound : _disableSound).WithPitch(1, 0.1f));
        }
    }
}
