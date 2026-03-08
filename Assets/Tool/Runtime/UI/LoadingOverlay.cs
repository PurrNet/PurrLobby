using System;
using UnityEngine;

namespace PurrLobby
{
    public class LoadingOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _speed = 5f;

        private bool _animating;
        private bool _showing;

        private void Awake()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        private void Update()
        {
            if (_animating)
            {
                var targetAlpha = _showing ? 1f : 0f;
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.deltaTime * _speed);

                if (Mathf.Approximately(_canvasGroup.alpha, targetAlpha))
                {
                    _canvasGroup.alpha = targetAlpha;
                    _animating = false;
                }
            }
        }

        public void Toggle(bool show)
        {
            _animating = true;
            _showing = show;
            _canvasGroup.blocksRaycasts = show;
        }
    }
}
