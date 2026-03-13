using PurrNet.Transports;

namespace PurrNet.Lobby
{
    public interface IProvideConnectionToPlayerID
    {
        public bool TryGetConnection(string playerID, out Connection conn);

        public bool TryGetPlayerID(Connection conn, out string playerID);
    }
}