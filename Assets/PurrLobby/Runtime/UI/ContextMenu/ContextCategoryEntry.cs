using TMPro;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class ContextCategoryEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        
        public void Setup(string name)
        {
            _label.text = name;
        }
    }
}