using System.Collections;
using PurrNet.UI;

namespace PurrNet.Lobby
{
    public class PauseMenuView : MonoView
    {
        protected override IEnumerator OnEnterTransition() => ViewTransitions.FadeIn(this, 0.1f);

        protected override IEnumerator OnExitTransition() => ViewTransitions.FadeOut(this, 0.1f);
    }
}
