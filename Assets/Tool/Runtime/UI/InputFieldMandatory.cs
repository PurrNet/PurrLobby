using TMPro;
using UnityEngine;

namespace PurrLobby
{
    public class InputFieldMandatory : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private SelectedOutlineInputField _graphic;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _subLabel;
        [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float _transitionDuration = 0.2f;
        [Space]
        [SerializeField] private float _minOutline = 0f;
        [SerializeField] private float _minOutlineError = 1f;
        [SerializeField] private Color _textColor;
        [SerializeField] private Color _errorTextColor;
        [SerializeField] private Color _outlineColor;
        [SerializeField] private Color _errorOutlineColor;

        private bool _wasModifiedOnce;
        private bool _isEmpty;
        private bool _shouldError;
        private float _shouldErrorTimer;
        private float _lastLerp;

        private void Awake()
        {
            _shouldErrorTimer = _transitionDuration;
        }

        private void OnEnable()
        {
            _inputField.onValueChanged.AddListener(OnValueUpdated);
        }

        private void OnDisable()
        {
            _inputField.onValueChanged.RemoveListener(OnValueUpdated);

            if (_label)
                _label.color = _textColor;
            if (_subLabel)
                _subLabel.color = _textColor;
            if (_graphic)
                _graphic.outlineColor = _outlineColor;
        }

        private void Update()
        {
            float lerp = Mathf.Clamp01(_shouldErrorTimer / _transitionDuration);
            lerp = _transitionCurve.Evaluate(lerp);

            if (Mathf.Approximately(lerp, _lastLerp))
                return;

            var targetTextColor = _shouldError ? _errorTextColor : _textColor;
            var initialTextColor = _shouldError ? _textColor : _errorTextColor;

            var targetOutlineColor = _shouldError ? _errorOutlineColor : _outlineColor;
            var initialOutlineColor = _shouldError ? _outlineColor : _errorOutlineColor;
            var targetMinOutline = _shouldError ? _minOutlineError : _minOutline;
            var initialMinOutline = _shouldError ? _minOutline : _minOutlineError;

            if (_label)
                _label.color = Color.Lerp(initialTextColor, targetTextColor, lerp);
            if (_subLabel)
                _subLabel.color = Color.Lerp(initialTextColor, targetTextColor, lerp);
            if (_graphic)
            {
                _graphic.outlineColor = Color.Lerp(initialOutlineColor, targetOutlineColor, lerp);
                _graphic.outlineWidthNotSelected = Mathf.Lerp(initialMinOutline, targetMinOutline, lerp);
            }

            _lastLerp = lerp;
            _shouldErrorTimer += Time.deltaTime;
        }

        private void OnValueUpdated(string newVal)
        {
            _isEmpty = string.IsNullOrEmpty(newVal);

            if (!_wasModifiedOnce && !_isEmpty)
                _wasModifiedOnce = true;

            var shouldError =_wasModifiedOnce && _isEmpty;
            if (_shouldError != shouldError)
            {
                _shouldError = shouldError;
                _shouldErrorTimer = 0f;
            }
        }
    }
}
