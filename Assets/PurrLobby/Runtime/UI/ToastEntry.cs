using PurrNet.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PurrNet.Lobby
{
    public class ToastEntry : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] RectangleGraphic _background;
        [SerializeField] TMPro.TMP_Text _title;
        [SerializeField] TMPro.TMP_Text _message;
        [SerializeField] float _duration = 3f;
        [SerializeField] private ColorInfo _errorColor = new() { enabled = true, color = ColorType.Danger };

        private float _timer;
        private float _outlineSize;

        private void Awake()
        {
            _outlineSize = _background.outlineSize;
        }

        public void Setup(string title, string message, bool error)
        {
            _title.text = title;
            _message.text = message;
            _timer = _duration;
            ThemeColors.Set(_background, _errorColor, 2);
            _background.outlineSize = error ? _outlineSize : 0f;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0)
                Destroy(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Destroy(gameObject);
        }
    }
}
