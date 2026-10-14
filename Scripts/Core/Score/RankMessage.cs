using Mirror;

public struct RankMessage : NetworkMessage
{
    public uint firstNetId;    // 1등 (금메달)
    public uint secondNetId;   // 2등 (은메달)
    public uint thirdNetId;    // 3등 (동메달)
    public uint lastNetId;     // 꼴지 (💩)
}