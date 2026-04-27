using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Services.Telemetry
{
    public static class PurrTelemetry
    {
        static string _sourceOverride;

        public static bool isReady
        {
            get
            {
                try { return PurrTelemetrySettings.isLinked; }
                catch { return false; }
            }
        }

        public static void SetSource(string source)
        {
            _sourceOverride = source;
        }

        internal static string CurrentSource =>
            _sourceOverride ?? (Application.isEditor ? "editor" : "runtime");

        public static void Track(string eventName)
        {
            try
            {
                if (string.IsNullOrEmpty(eventName)) return;
                PurrTelemetrySender.Enqueue(eventName, null);
            }
            catch (Exception e) { LogIfEditor(e); }
        }

        public static void Track(string eventName, IReadOnlyDictionary<string, object> properties)
        {
            try
            {
                if (string.IsNullOrEmpty(eventName)) return;
                PurrTelemetrySender.Enqueue(eventName, properties);
            }
            catch (Exception e) { LogIfEditor(e); }
        }

        public static void Track(string eventName, PurrTelemetryProps properties)
        {
            try
            {
                if (string.IsNullOrEmpty(eventName)) return;
                PurrTelemetrySender.Enqueue(eventName, properties.RawDictionary);
            }
            catch (Exception e) { LogIfEditor(e); }
            finally
            {
                try { properties.Dispose(); } catch { }
            }
        }

        public static Task FlushAsync()
        {
            try { return PurrTelemetrySender.FlushAsync(); }
            catch (Exception e) { LogIfEditor(e); return Task.CompletedTask; }
        }

        public static void Flush()
        {
            try { _ = PurrTelemetrySender.FlushAsync(); }
            catch (Exception e) { LogIfEditor(e); }
        }

        internal static void LogIfEditor(Exception e)
        {
            if (Application.isEditor)
                Debug.LogWarning($"[PurrTelemetry] {e}");
        }
    }
}
