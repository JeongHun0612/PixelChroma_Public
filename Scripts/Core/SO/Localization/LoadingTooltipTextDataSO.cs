using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "LoadingTooltipTextData", menuName = "PixelChroma/Localization/LoadingTooltipTextData")]
public class LoadingTooltipTextDataSO : ScriptableObject
{
    [Header("Tooltips")]
    public LocalizedString[] tooltips;
}
