using UnityEngine;
using UnityEngine.EventSystems;

namespace PurrLobby
{
    public class SliderSounds : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private AudioClip[] _clickSounds;

        public void OnPointerDown(PointerEventData eventData)
        {
            Sounds2D.Play(new AudioSession(_clickSounds).WithPitch(1, 0.1f));
        }
    }
}
