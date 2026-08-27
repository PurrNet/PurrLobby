using System.Collections;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Menu page that fades in while sliding from the right; exits left on a
    /// successful hand-off (forward navigation) and right on cancel/back.
    /// </summary>
    public abstract class SlidePageView : MonoView
    {
        [SerializeField]
        [FormerlySerializedAs("_content")]
        [FormerlySerializedAs("_window")]
        private RectTransform _slideContent;

        protected RectTransform slideContent => _slideContent;

        /// <summary>Set true before navigating forward so the exit slides left instead of right.</summary>
        protected bool successfulExit { get; set; }

        protected override IEnumerator OnEnterTransition()
        {
            return ViewTransitions.Parallel(
                ViewTransitions.FadeIn(this),
                ViewTransitions.SlideFromRight(_slideContent));
        }

        protected override IEnumerator OnExitTransition()
        {
            return ViewTransitions.Parallel(
                ViewTransitions.FadeOut(this),
                successfulExit
                    ? ViewTransitions.SlideToLeft(_slideContent)
                    : ViewTransitions.SlideToRight(_slideContent));
        }
    }
}
