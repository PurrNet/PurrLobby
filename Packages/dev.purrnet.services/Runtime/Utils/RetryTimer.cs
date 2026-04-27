namespace PurrNet.Services
{
    public struct RetryTimer
    {
        const int MAX_RETRIES = 5;
        static readonly float[] BACKOFF_DELAYS = { 1f, 2f, 4f, 8f, 16f };

        int _attempt;
        float _elapsed;
        float _targetDelay;
        bool _active;

        public bool isActive => _active;
        public int attempt => _attempt;
        public bool retriesExhausted => _attempt >= MAX_RETRIES;

        public void Start()
        {
            if (_attempt >= MAX_RETRIES)
                return;

            _targetDelay = _attempt < BACKOFF_DELAYS.Length
                ? BACKOFF_DELAYS[_attempt]
                : BACKOFF_DELAYS[BACKOFF_DELAYS.Length - 1];
            _elapsed = 0f;
            _active = true;
            _attempt++;
        }

        public bool Tick(float deltaTime)
        {
            if (!_active)
                return false;

            _elapsed += deltaTime;

            if (_elapsed < _targetDelay)
                return false;

            _active = false;
            return true;
        }

        public void Reset()
        {
            _attempt = 0;
            _elapsed = 0f;
            _targetDelay = 0f;
            _active = false;
        }
    }
}
