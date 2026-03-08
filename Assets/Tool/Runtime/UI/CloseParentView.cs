using PurrNet.UI;
using UnityEngine;

namespace PurrLobby
{
    public class CloseParentView : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _closeSounds;

        public void Close()
        {
            var view = GetComponentInParent<MonoView>();
            if (view && view.parentStack)
            {
                Sounds2D.Play(new AudioSession(_closeSounds).WithPitch(1, 0.1f));
                view.CloseMe();
            }
        }
    }
}
