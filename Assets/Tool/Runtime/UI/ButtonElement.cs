using System;
using JetBrains.Annotations;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace PurrLobby
{
    public class ButtonElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private RectangleGraphic _graphic;
        [Space]
        [SerializeField] AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Color _backgroundNormal = Color.white;
        [SerializeField] private Color _backgroundHover = Color.gray;
        [SerializeField] private float _scaleNormal = 1f;
        [SerializeField] private float _scaleClicked = 1.1f;
        [SerializeField] private float _transitionDuration = 0.2f;
        [Space]
        [SerializeField] private AudioClip[] _clickSounds;

        [UsedImplicitly]
        public UnityEvent onClickUnity;

        public event Action onClick;

        private float _backgroundTimer = 0f;
        private float _pressTimer = 0f;
        private bool _isHovering = false;
        private bool _isPressing = false;

        private Transform _trs;

        private void Awake()
        {
            _backgroundTimer = _transitionDuration;
            _pressTimer = _transitionDuration;
            _trs = transform;
        }

        private void Update()
        {
            float colorLerp = Mathf.Clamp01(_backgroundTimer / _transitionDuration);
            float pressLerp = Mathf.Clamp01(_pressTimer / _transitionDuration);

            colorLerp = _transitionCurve.Evaluate(colorLerp);
            pressLerp = _transitionCurve.Evaluate(pressLerp);

            var targetColor = _isHovering || _isPressing ? _backgroundHover : _backgroundNormal;
            var initialColor = _isHovering || _isPressing ? _backgroundNormal : _backgroundHover;

            var targetScale = _isPressing ? _scaleClicked : _scaleNormal;
            var initialScale = _isPressing ? _scaleNormal : _scaleClicked;

            _graphic.graphicColor = Color.Lerp(initialColor, targetColor, colorLerp);
            _trs.localScale = Vector3.Lerp(
                new Vector3(initialScale, initialScale, 1),
                new Vector3(targetScale, targetScale, 1), pressLerp);

            _backgroundTimer += Time.deltaTime;
            _pressTimer += Time.deltaTime;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            if (!_isPressing)
                _backgroundTimer = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            if (!_isPressing)
                _backgroundTimer = 0f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressing = true;
            _pressTimer = 0f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressing = false;
            if (!_isHovering)
                _backgroundTimer = 0f;
            _pressTimer = 0f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClickUnity?.Invoke();
            onClick?.Invoke();
            Sounds2D.Play(new AudioSession(_clickSounds).WithPitch(1, 0.1f));
        }
    }
}
