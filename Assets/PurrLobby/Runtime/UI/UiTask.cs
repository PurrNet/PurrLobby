using System;
using System.Threading.Tasks;
using PurrNet.UI;
using PurrNet.UI.HeroUI;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Runs an async UI action behind a loading overlay: toggles the overlay (and
    /// optional interaction/close guards) around the body, and toasts + logs on
    /// exception. Uses Unity lifetime checks (<c>if (x)</c>, never <c>?.</c>) so a
    /// view destroyed mid-await is handled.
    /// </summary>
    public static class UiTask
    {
        public static void Run(Func<Task> body, string errorTitle,
            LoadingOverlay overlay = null, CanvasGroup disableGroup = null, CloseParentView closeGuard = null)
        {
            RunAsync(body, errorTitle, overlay, disableGroup, closeGuard).Forget(errorTitle);
        }

        public static async Task RunAsync(Func<Task> body, string errorTitle,
            LoadingOverlay overlay = null, CanvasGroup disableGroup = null, CloseParentView closeGuard = null)
        {
            try
            {
                if (overlay) overlay.Toggle(true);
                if (disableGroup) disableGroup.interactable = false;
                if (closeGuard) closeGuard.canClose = false;

                await body();
            }
            catch (Exception e)
            {
                Toaster.PushError(errorTitle, e);
                Debug.LogException(e);
            }
            finally
            {
                if (overlay) overlay.Toggle(false);
                if (disableGroup) disableGroup.interactable = true;
                if (closeGuard) closeGuard.canClose = true;
            }
        }
    }
}
