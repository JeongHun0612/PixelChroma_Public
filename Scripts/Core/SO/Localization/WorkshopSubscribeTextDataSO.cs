using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "WorkshopSubscribeTextData", menuName = "PixelChroma/Localization/WorkshopSubscribeTextData")]
public class WorkshopSubscribeTextDataSO : ScriptableObject
{
    [Header("Message")]
    public LocalizedString message;

    [Header("Button")]
    public LocalizedString confirmButton;

    [Header("Status")]
    public LocalizedString statusSubscribing;
    public LocalizedString statusDownloading;
    public LocalizedString statusTimeout;
    public LocalizedString statusComplete;
    public LocalizedString statusFailed;
}
