using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "NicknameSettingTextData", menuName = "PixelChroma/Localization/NicknameSettingTextData")]
public class NicknameSettingTextDataSO : ScriptableObject
{
    public LocalizedString warningMaxLength;
    public LocalizedString warningBanned;
    public LocalizedString warningUnsupported;
}