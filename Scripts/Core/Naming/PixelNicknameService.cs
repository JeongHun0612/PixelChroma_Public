using System;
using UnityEngine;

namespace PixelChroma
{
    public static class PixelNicknameService
    {
        private const string PrefsKey = "PixelNickname";
        private const string PresetKeyPrefix = "PixelNickname_Preset";
        private const string ActivePresetKey = "PixelNickname_ActivePreset";
        private const string IsDefaultPresetKey = "PixelNickname_IsDefault";
        private const string DefaultImageIndexKey = "PixelNickname_DefaultImageIndex";
        public const int PresetCount = 3;

        // ── 메인 (네트워크 호환용) ──

        public static void Save(byte[] pngData)
        {
            if (pngData == null || pngData.Length == 0)
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetString(PrefsKey, Convert.ToBase64String(pngData));
            PlayerPrefs.Save();
        }

        public static byte[] Load()
        {
            string base64 = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(base64)) return null;

            try { return Convert.FromBase64String(base64); }
            catch { return null; }
        }

        // ── 프리셋 ──

        public static void SaveActivePresetIndex(int index)
        {
            PlayerPrefs.SetInt(ActivePresetKey, Mathf.Clamp(index, 0, PresetCount - 1));
            PlayerPrefs.Save();
        }

        public static int LoadActivePresetIndex()
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(ActivePresetKey, 0), 0, PresetCount - 1);
        }

        public static void SavePreset(int presetIndex, byte[] pngData)
        {
            string key = PresetKeyPrefix + presetIndex;
            if (pngData == null || pngData.Length == 0)
            {
                PlayerPrefs.DeleteKey(key);
            }
            else
            {
                PlayerPrefs.SetString(key, Convert.ToBase64String(pngData));
            }
            PlayerPrefs.Save();
        }

        public static byte[] LoadPreset(int presetIndex)
        {
            string key = PresetKeyPrefix + presetIndex;
            string base64 = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(base64)) return null;
            try { return Convert.FromBase64String(base64); }
            catch { return null; }
        }

        // ── 기본 프리셋 ──

        public static void SaveIsDefaultPreset(bool isDefault)
        {
            PlayerPrefs.SetInt(IsDefaultPresetKey, isDefault ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool LoadIsDefaultPreset()
        {
            return PlayerPrefs.GetInt(IsDefaultPresetKey, 0) == 1;
        }

        public static void SaveDefaultImageIndex(int index)
        {
            PlayerPrefs.SetInt(DefaultImageIndexKey, index);
            PlayerPrefs.Save();
        }

        public static int LoadDefaultImageIndex()
        {
            return PlayerPrefs.GetInt(DefaultImageIndexKey, 0);
        }
    }
}
