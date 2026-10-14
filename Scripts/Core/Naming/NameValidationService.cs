using TMPro;

namespace PixelChroma
{
    public enum NameValidation
    {
        Valid,
        Empty,
        TooLong,
        Banned,
        Unsupported,
    }

    // 방 이름/텍스트 닉네임처럼 문자열 이름에 공통으로 적용되는 검증 규칙.
    public static class NameValidationService
    {
        public static NameValidation Validate(string name, BannedWordsSO bannedWords, int maxLength, TMP_FontAsset font = null, bool allowEmpty = true)
        {
            if (string.IsNullOrEmpty(name))
                return allowEmpty ? NameValidation.Valid : NameValidation.Empty;

            if (name.Length > maxLength)
                return NameValidation.TooLong;

            if (bannedWords != null && bannedWords.Contains(name))
                return NameValidation.Banned;

            if (font != null && HasUnsupportedCharacter(name, font))
                return NameValidation.Unsupported;

            return NameValidation.Valid;
        }

        private static bool HasUnsupportedCharacter(string name, TMP_FontAsset font)
        {
            foreach (char c in name)
            {
                if (char.IsWhiteSpace(c))
                    continue;

                if (!font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: true))
                    return true;
            }

            return false;
        }
    }
}
