using System;
using Mirror;

namespace PixelChroma
{

    public static class RankClient
    {
        public static event Action OnRanksChanged;

        public static uint FirstNetId { get; private set; }
        public static uint SecondNetId { get; private set; }
        public static uint ThirdNetId { get; private set; }
        public static uint LastNetId { get; private set; }

        public static void RegisterHandlers()
        {
            NetworkClient.RegisterHandler<RankMessage>(OnRankMessage);
        }

        public static void UnregisterHandlers()
        {
            NetworkClient.UnregisterHandler<RankMessage>();
        }

        /// <summary>씬 전환 시 이전 판의 순위를 폐기합니다.</summary>
        public static void Reset()
        {
            FirstNetId = SecondNetId = ThirdNetId = LastNetId = 0;
            OnRanksChanged?.Invoke();
        }

        private static void OnRankMessage(RankMessage msg)
        {
            FirstNetId = msg.firstNetId;
            SecondNetId = msg.secondNetId;
            ThirdNetId = msg.thirdNetId;
            LastNetId = msg.lastNetId;

            OnRanksChanged?.Invoke();
        }
    }
}