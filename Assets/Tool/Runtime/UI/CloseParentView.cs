using PurrNet.UI;
using UnityEngine;

namespace PurrLobby
{
    public class CloseParentView : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _closeSounds;

        public bool canClose { get; set; } = true;

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
    }
}
