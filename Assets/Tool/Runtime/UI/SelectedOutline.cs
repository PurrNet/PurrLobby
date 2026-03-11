using PurrNet.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PurrNet.Lobby
{
    public class SelectedOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectangleGraphic _graphic;
        [SerializeField] AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Color _outlineColor = Color.white;
        [SerializeField] private float _outlineWidth = 2f;
        [SerializeField] private float _outlineWidthNotSelected = 0f;
        [SerializeField] private float _transitionDuration = 0.2f;

        private float _timeSinceToggle;
        private bool _isSelected;
        private bool _isFocused;

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

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isSelected = true;
            _timeSinceToggle = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isSelected = false;
            _timeSinceToggle = 0f;
        }

        private void Update()
        {
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
