namespace PurrNet.Lobby
{
    public struct APIResponse
    {
        public bool success;
        public string error;

        public static APIResponse Success() => new APIResponse { success = true };

        public static APIResponse Failure(string error) => new APIResponse { success = false, error = error };
    }
}
