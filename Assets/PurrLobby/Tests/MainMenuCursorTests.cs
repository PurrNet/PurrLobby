using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Lobby.Tests
{
    public class MainMenuCursorTests
    {
        sealed class FakeCursor : CursorScope.ICursor
        {
            public CursorLockMode lockState { get; set; }
            public bool visible { get; set; } = true;
        }

        private MainMenuView _view;
        private FakeCursor _cursor;

        [SetUp]
        public void SetUp()
        {
            _cursor = new FakeCursor();
            CursorScope.cursor = _cursor;
            CursorScope.Release();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurrLobby/Prefabs/Views/MainMenu.prefab");
            Assert.IsNotNull(prefab);
            _view = Object.Instantiate(prefab).GetComponent<MainMenuView>();
            Assert.IsNotNull(_view);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view) Object.DestroyImmediate(_view.gameObject);
            CursorScope.Release();
            CursorScope.cursor = CursorScope.unityCursor;
        }

        private void LockLikeAGame()
        {
            _cursor.lockState = CursorLockMode.Locked;
            _cursor.visible = false;
        }

        [Test]
        public void ShowingMainMenu_UnlocksAndShowsLockedCursor()
        {
            LockLikeAGame();

            _view.OnPushed();

            Assert.AreEqual(CursorLockMode.None, _cursor.lockState);
            Assert.IsTrue(_cursor.visible);
        }

        [Test]
        public void ShowingMainMenu_WithPauseScopeStillOpen_LaterPopDoesNotRelock()
        {
            LockLikeAGame();
            CursorScope.PushUnlocked(); // pause menu open when the host left

            _view.OnPushed();
            CursorScope.PopRestore();   // stale pause menu closes afterwards

            Assert.AreEqual(CursorLockMode.None, _cursor.lockState);
            Assert.IsTrue(_cursor.visible);
        }

        [Test]
        public void PauseScope_StillRestoresGameCursor_WhenNoMenuShown()
        {
            LockLikeAGame();
            CursorScope.PushUnlocked();
            Assert.AreEqual(CursorLockMode.None, _cursor.lockState);

            CursorScope.PopRestore();

            Assert.AreEqual(CursorLockMode.Locked, _cursor.lockState);
            Assert.IsFalse(_cursor.visible);
        }
    }
}
