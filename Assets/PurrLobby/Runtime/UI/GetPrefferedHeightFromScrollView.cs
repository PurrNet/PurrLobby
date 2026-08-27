using UnityEngine;
using UnityEngine.UI;

namespace PurrNet.Lobby
{
    [ExecuteInEditMode]
    public class GetPrefferedHeightFromScrollView : MonoBehaviour
    {
        [SerializeField] private LayoutElement _target;
        [SerializeField] private RectTransform _scrollParent;
        [SerializeField] private RectTransform _scrollContent;

        private void LateUpdate()
        {
            if (!_scrollParent || !_scrollContent || !_target)
                return;

            float bottom = _scrollParent.offsetMin.y;
            float top    = -_scrollParent.offsetMax.y;
            var contentHeight = _scrollContent.sizeDelta.y;
            _target.preferredHeight = contentHeight + bottom + top;
        }

        public void ForceRebuild()
        {
            LateUpdate();
        }
    }
}
