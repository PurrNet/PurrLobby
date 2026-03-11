using PurrNet.UI;
using UnityEngine;

namespace PurrLobby
{
    public class SelectedOutlineInputField : MonoBehaviour
    {
        [SerializeField] private  TMPro.TMP_InputField _input;
        [SerializeField] private RectangleGraphic _graphic;
        [SerializeField] AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Color _outlineColor = Color.white;
        [SerializeField] private float _outlineWidth = 2f;
        [SerializeField] private float _outlineWidthNotSelected = 0f;
        [SerializeField] private float _transitionDuration = 0.2f;

        private float _timeSinceToggle;
        private bool _isSelected;

        public Color outlineColor
        {
            get => _outlineColor;
            set => _outlineColor = value;
        }

        public float outlineWidthNotSelected
        {
            get => _outlineWidthNotSelected;
            set => _outlineWidthNotSelected = value;
        }

        private void Awake()
        {
            _timeSinceToggle = _transitionDuration;
        }

        private void Update()
        {
            bool isCurrentlySelected = _input.isFocused;

            if (_isSelected != isCurrentlySelected)
            {
                _isSelected = isCurrentlySelected;
                _timeSinceToggle = 0f;
            }

            var lerp = Mathf.Clamp01(_timeSinceToggle / _transitionDuration);
            lerp = _transitionCurve.Evaluate(lerp);

            var targetWidth = _isSelected ? _outlineWidth : _outlineWidthNotSelected;
            var initialWidth = _isSelected ? _outlineWidthNotSelected : _outlineWidth;

            if (_graphic)
            {
                _graphic.outlineColor = _outlineColor;
                _graphic.outlineSize = Mathf.Lerp(initialWidth, targetWidth, lerp);
            }

            _timeSinceToggle += Time.deltaTime;
        }
    }
}
