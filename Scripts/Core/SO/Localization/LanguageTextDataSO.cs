using UnityEngine;
using UnityEngine.Identifiers;
using UnityEngine.Localization;

// 로케일별 언어 이름 표기를 한 곳에서 관리한다.
// - nativeName    : 자국어 표기(고정). 언어 선택 UI(UILanguageSelect)용.
// - localizedName : 현재 UI 언어 기준 이름. 필터/목록 UI(UIRoomList)용.
[CreateAssetMenu(fileName = "LanguageTextData", menuName = "PixelChroma/Localization/LanguageTextData")]
public class LanguageTextDataSO : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public LocaleIdentifier locale;

        [Tooltip("자국어 표기(고정). 예: 日本語, English, 简体中文")]
        public string nativeName;

        [Tooltip("현재 UI 언어 기준 이름. 예: ko 로케일에서 '일본어'")]
        public LocalizedString localizedName;
    }

    [SerializeField] private Entry[] entries;

    // 자국어 표기(고정)
    public bool TryGetNativeName(LocaleIdentifier identifier, out string name)
    {
        name = null;
        if (entries == null)
            return false;

        foreach (var entry in entries)
        {
            if (entry.locale != identifier) continue;
            if (string.IsNullOrEmpty(entry.nativeName)) return false;

            name = entry.nativeName;
            return true;
        }

        return false;
    }

    // 현재 UI 언어 기준 이름
    public bool TryGetLocalizedName(LocaleIdentifier identifier, out string name)
    {
        name = null;
        if (entries == null)
            return false;

        foreach (var entry in entries)
        {
            if (entry.locale != identifier) continue;
            if (entry.localizedName == null || entry.localizedName.IsEmpty) return false;

            name = entry.localizedName.GetLocalizedString();
            return true;
        }

        return false;
    }
}
