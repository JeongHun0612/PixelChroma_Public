using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "QuickMatchTextData", menuName = "PixelChroma/Localization/QuickMatchTextData")]
public class QuickMatchTextDataSO : ScriptableObject
{
    public LocalizedString findingRoomStatus;
    public LocalizedString searchCancelledStatus;
}