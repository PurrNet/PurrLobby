using PurrNet.Lobby;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestContextMenu : MonoBehaviour
{
    [SerializeField] private ViewStack _stack;

    void Update()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && _stack.top is not ContextMenuView)
        {
            var mousePos = Mouse.current.position.ReadValue();
            _stack.Push<ContextMenuView>().Setup(mousePos, "Test!", new[]
            {
                new ContextOption
                {
                    name = "Open Discord",
                    description = "Open discord in your browser",
                    icon = "discord",
                },
                new ContextOption
                {
                    name = "Open Discord 2",
                    description = "Open discord in your browser",
                    icon = "discord",
                    isDangerous = true
                },
                new ContextOption
                {
                    name = "Open Discord 3",
                    category = "Variants",
                    description = "Open discord in your browser"
                },
                new ContextOption
                {
                    name = "Open Discord 3"
                },
                new ContextOption
                {
                    name = "Open Discord 4"
                }
            }, OnClicked);
        }
    }

    private void OnClicked(int obj)
    {
        Debug.Log(obj);
    }
}
