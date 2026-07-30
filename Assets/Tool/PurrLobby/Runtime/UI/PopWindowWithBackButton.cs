using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class PopWindowWithBackButton : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _closeSounds;

        private void Update()
        {
            if (!BackInput.WasBackPressed())
                return;

            var parentWindow = GetComponentInParent<MonoView>();
            if (parentWindow && parentWindow.isTopMost && BackInput.TryConsume())
            {
                Sounds2D.Play(new AudioSession(_closeSounds).WithPitch(1, 0.1f));
                parentWindow.PopMe();
            }
        }
    }
}
