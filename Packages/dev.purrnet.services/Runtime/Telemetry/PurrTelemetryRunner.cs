using System;
using UnityEngine;

namespace PurrNet.Services.Telemetry
{
    [DefaultExecutionOrder(-1000)]
    internal sealed class PurrTelemetryRunner : MonoBehaviour
    {
        void Update()
        {
            try { PurrTelemetrySender.TickFromRunner(Time.unscaledDeltaTime); }
            catch (Exception e) { PurrTelemetry.LogIfEditor(e); }
        }

        void OnApplicationPause(bool paused)
        {
            try
            {
                if (paused)
                    _ = PurrTelemetrySender.FlushAsync();
            }
            catch (Exception e) { PurrTelemetry.LogIfEditor(e); }
        }

        void OnApplicationQuit()
        {
            try
            {
                _ = PurrTelemetrySender.FlushAsync();
                PurrTelemetrySender.PersistPending();
            }
            catch (Exception e) { PurrTelemetry.LogIfEditor(e); }
        }
    }
}
