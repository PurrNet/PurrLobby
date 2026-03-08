using PurrNet.UI;
using UnityEngine;

namespace PurrLobby
{
    public class CloseParentView : MonoBehaviour
    {
        public void Close()
        {
            var view = GetComponentInParent<MonoView>();
            if (view && view.parentStack) view.CloseMe();
        }
    }
}
