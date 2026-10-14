using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "SettingTextData", menuName = "PixelChroma/Localization/SettingTextData")]
public class SettingTextDataSO : ScriptableObject
{
    [Header("Screen Mode Options")]
    public LocalizedString optionFullscreen;
    public LocalizedString optionBorderless;
    public LocalizedString optionWindowed;
}
