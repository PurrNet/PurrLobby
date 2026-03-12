using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace PurrNet.Lobby
{
    public class ButtonElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private RectangleGraphic _graphic;
        [SerializeField] private bool _interactable = true;
        [Space]
        [SerializeField] AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Color _backgroundNormal = Color.white;
        [SerializeField] private Color _backgroundHover = Color.gray;
        [SerializeField] private float _disabledAlpha = 0.5f;
        [SerializeField] private float _scaleNormal = 1f;
        [SerializeField] private float _scaleClicked = 1.1f;
        [SerializeField] private float _transitionDuration = 0.2f;
        [Space]
        [SerializeField] private AudioClip[] _clickSounds;

        [UsedImplicitly]
        public UnityEvent onClickUnity;

        public event Action onClick;

        private float _interactableTimer = 0f;
        private float _backgroundTimer = 0f;
        private float _pressTimer = 0f;
        private bool _isHovering = false;
        private bool _isPressing = false;

        private Transform _trs;

        private bool _parentGroupInteractable = true;

        public bool isInteractable => _parentGroupInteractable && _interactable;

        public bool interactable
        {
            get => _interactable;
            set
            {
                var old = isInteractable;
                _interactable = value;
                var @new = isInteractable;

                if (old != @new)
                    InteractableChanged();
            }
        }

        private readonly List<CanvasGroup> _canvasGroupCache = new ();

        protected void OnCanvasGroupChanged()
        {
            var old = isInteractable;
            _parentGroupInteractable = ParentGroupAllowsInteraction();
            var @new = isInteractable;
            if (old != @new)
                InteractableChanged();
        }

        bool ParentGroupAllowsInteraction()
        {
            var t = transform;
            while (t)
            {
                t.GetComponents(_canvasGroupCache);
                for (var i = 0; i < _canvasGroupCache.Count; i++)
                {
                    if (_canvasGroupCache[i].enabled && !_canvasGroupCache[i].interactable)
                        return false;

                    if (_canvasGroupCache[i].ignoreParentGroups)
                        return true;
                }

                t = t.parent;
            }

            return true;
        }

        private void Awake()
        {
            _backgroundTimer = _transitionDuration;
            _interactableTimer = _transitionDuration;
            _pressTimer = _transitionDuration;
            _trs = transform;
        }

        private void InteractableChanged()
        {
            _interactableTimer = 0f;
        }

        private void Update()
        {
            float colorLerp = Mathf.Clamp01(_backgroundTimer / _transitionDuration);
            float pressLerp = Mathf.Clamp01(_pressTimer / _transitionDuration);
            float interactableLerp = Mathf.Clamp01(_interactableTimer / _transitionDuration);

            colorLerp = _transitionCurve.Evaluate(colorLerp);
            pressLerp = _transitionCurve.Evaluate(pressLerp);
            interactableLerp = _transitionCurve.Evaluate(interactableLerp);

            bool canInteract = isInteractable;
            bool hoveringOrPressed = canInteract && (_isHovering || _isPressing);
            bool pressed = canInteract && _isPressing;

            var targetColor = hoveringOrPressed ? _backgroundHover : _backgroundNormal;
            var initialColor = hoveringOrPressed ? _backgroundNormal : _backgroundHover;

            var targetScale = pressed ? _scaleClicked : _scaleNormal;
            var initialScale = pressed ? _scaleNormal : _scaleClicked;

            var targetAlphaMult = canInteract ? 1f : _disabledAlpha;
            var initialAlphaMult = canInteract ? _disabledAlpha : 1f;

            var color = Color.Lerp(initialColor, targetColor, colorLerp);
            color.a *= Mathf.Lerp(initialAlphaMult, targetAlphaMult, interactableLerp);

            _graphic.graphicColor  = color;
            _trs.localScale = Vector3.Lerp(
                new Vector3(initialScale, initialScale, 1),
                new Vector3(targetScale, targetScale, 1), pressLerp);

            _backgroundTimer += Time.deltaTime;
            _interactableTimer  += Time.deltaTime;
            _pressTimer += Time.deltaTime;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            if (!_isPressing && isInteractable)
                _backgroundTimer = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            if (!_isPressing && isInteractable)
                _backgroundTimer = 0f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isInteractable)
            {
                _isPressing = true;
                _pressTimer = 0f;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressing = false;
            if (!_isHovering && isInteractable)
                _backgroundTimer = 0f;

            if (isInteractable)
                _pressTimer = 0f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable)
                return;

            onClickUnity?.Invoke();
            onClick?.Invoke();
            Sounds2D.Play(new AudioSession(_clickSounds).WithPitch(1, 0.1f));
        }
    }
}
