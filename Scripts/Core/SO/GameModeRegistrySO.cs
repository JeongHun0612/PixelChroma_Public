using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "GameModeRegistry", menuName = "PixelChroma/GameMode/GameModeRegistry")]
    public class GameModeRegistrySO : ScriptableObject
    {
        [SerializeField] private List<GameModeDataSO> modes = new();

        public IReadOnlyList<GameModeDataSO> Modes => modes;

        public GameModeDataSO Find(GameMode mode)
        {
            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i] != null && modes[i].mode == mode)
                    return modes[i];
            }

            return null;
        }
    }
}
