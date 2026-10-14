using UnityEngine;

namespace PixelChroma
{
    public struct ScoreEntry
    {
        public int rank;
        public int connectionId;
        public uint netId;
        public string playerName;
        public int score;
        public byte[] pixelNicknamePng;
        public bool isDead;
    }
}