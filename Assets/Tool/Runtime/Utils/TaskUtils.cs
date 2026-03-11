using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Lobby.Utils
{
    public static class TaskUtils
    {
        public static Task DelaySeconds(float seconds) => PurrRunner.DelaySeconds(seconds);

        public static async void Forget(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
