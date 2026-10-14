using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "GameModeData", menuName = "PixelChroma/GameMode/GameModeData")]
    public class GameModeDataSO : ScriptableObject
    {
        public GameMode mode;
        public LocalizedString displayName;
        public LocalizedString description;
        public Sprite icon;
    }
}
