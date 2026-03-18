using System.Collections.Generic;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PurrNet.Lobby
{
    public class CloseParentAndPassthrough : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private AudioClip[] _closeSounds;

        public bool canClose { get; set; } = true;

        private readonly List<RaycastResult> _results = new ();

        public void Close()
        {
            if (!canClose) return;

            var view = GetComponentInParent<MonoView>();
            if (view && view.parentStack)
            {
                Sounds2D.Play(new AudioSession(_closeSounds).WithPitch(1, 0.1f));
                view.CloseMe();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Close();

            EventSystem.current.RaycastAll(eventData, _results);

            foreach (var hit in _results)
            {
                ExecuteEvents.ExecuteHierarchy(hit.gameObject, eventData, ExecuteEvents.pointerClickHandler);
                break;
            }
        }
    }
}
