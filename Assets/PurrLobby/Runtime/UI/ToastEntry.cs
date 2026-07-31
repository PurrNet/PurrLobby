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

        private float _timer;

        public void Setup(string title, string message, bool error)
        {
            _title.text = title;
            _message.text = message;
            _timer = _duration;
            _background.outlineColor = error ? Color.red : Color.clear;
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
