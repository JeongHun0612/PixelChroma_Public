using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "RoomCodeEntryTextData", menuName = "PixelChroma/Localization/RoomCodeEntryTextData")]
    public class RoomCodeEntryTextDataSO : ScriptableObject
    {
        public LocalizedString invalidCharwarningText;
    }
}