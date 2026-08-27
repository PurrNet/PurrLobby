using PurrNet.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PurrNet.Lobby
{
    public class PlaySoundOnSliderChanged : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private float _volume = 1;
        [SerializeField] private AudioClip[] _audioClips;

        private int _firstTick;

        private void Reset()
        {
            _slider = GetComponent<Slider>();
        }

        private void OnEnable()
        {
            _firstTick = Time.frameCount;
            if (_slider)
                _slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        private void OnDisable()
        {
            if (_slider)
                _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        private void OnSliderValueChanged(float _)
        {
            if (Time.frameCount == _firstTick)
                return;

            float lerp = Mathf.InverseLerp(_slider.minValue, _slider.maxValue, _slider.value);
            float pitch = Mathf.Lerp(0.8f, 1.2f, lerp);
            Sounds2D.Play(new AudioSession(_audioClips).WithPitch(pitch).WithVolume(_volume));
        }
    }
}
