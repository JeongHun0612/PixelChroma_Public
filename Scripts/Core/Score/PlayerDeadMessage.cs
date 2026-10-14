using Mirror;

namespace PixelChroma
{
    public struct PlayerDeadMessage : NetworkMessage
    {
        public int connectionId;
    }
}