using System;
using System.Collections.Generic;
using PurrNet.Pooling;

namespace PurrNet.Services.Telemetry
{
    public struct PurrTelemetryProps : IDisposable
    {
        DisposableDictionary<string, object> _dict;
        bool _allocated;

        public static PurrTelemetryProps Rent()
        {
            return new PurrTelemetryProps
            {
                _dict = DisposableDictionary<string, object>.Create(),
                _allocated = true
            };
        }

        public PurrTelemetryProps With(string key, object value)
        {
            if (!_allocated)
            {
                _dict = DisposableDictionary<string, object>.Create();
                _allocated = true;
            }
            _dict[key] = value;
            return this;
        }

        public object this[string key]
        {
            get => _allocated ? _dict[key] : null;
            set
            {
                if (!_allocated)
                {
                    _dict = DisposableDictionary<string, object>.Create();
                    _allocated = true;
                }
                _dict[key] = value;
            }
        }

        public int Count => _allocated ? _dict.Count : 0;

        internal Dictionary<string, object> RawDictionary => _allocated ? _dict.dictionary : null;

        public void Dispose()
        {
            if (!_allocated) return;
            _dict.Dispose();
            _allocated = false;
        }
    }
}
