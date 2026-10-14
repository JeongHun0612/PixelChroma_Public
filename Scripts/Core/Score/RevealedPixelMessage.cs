using Mirror;
using UnityEngine;

namespace PixelChroma
{
    public struct RevealedPixelMessage : NetworkMessage
    {
        public int connectionId;
        public Color32 color;
    }
}