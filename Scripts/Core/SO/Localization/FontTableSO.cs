using System;
using OneText;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "FontTable", menuName = "PixelChroma/Localization/FontTable")]
    public class FontTableSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public LocaleIdentifier[] locales;

            // UI/디스플레이용 폰트. 게임 분위기용 디스플레이 폰트(픽셀·둥근 폰트 등)를 쓴다.
            public AssetReferenceT<OneFontAsset> font;

            // 유저 생성 텍스트(채팅·닉네임·방 제목 등)용 폰트.
            // 임의 언어가 표출되므로 가독성 위주(Noto Sans 계열 등)로 지정한다.
            public AssetReferenceT<OneFontAsset> contentFont;
        }

        [SerializeField] private Entry[] entries;

        [SerializeField] private AssetReferenceT<OneFontAsset> fallback;

        [FormerlySerializedAs("chatFallback")]
        [SerializeField] private AssetReferenceT<OneFontAsset> contentFallback;

        public AssetReferenceT<OneFontAsset> Resolve(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
                foreach (var locale in entry.locales)
                    if (locale == identifier)
                        return entry.font;

            return fallback;
        }

        // 유저 생성 텍스트용 폰트 해석. contentFont가 없으면 contentFallback으로, 그마저 없으면
        // UI 폰트(entry.font/fallback)로 우아하게 대체해 아직 미설정 상태에서도 렌더된다.
        public AssetReferenceT<OneFontAsset> ResolveContent(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
                foreach (var locale in entry.locales)
                    if (locale == identifier)
                        return Pick(entry.contentFont, contentFallback, entry.font);

            return Pick(null, contentFallback, fallback);
        }

        private static AssetReferenceT<OneFontAsset> Pick(
            AssetReferenceT<OneFontAsset> primary,
            AssetReferenceT<OneFontAsset> secondary,
            AssetReferenceT<OneFontAsset> tertiary)
        {
            if (IsValid(primary)) return primary;
            if (IsValid(secondary)) return secondary;
            return tertiary;
        }

        private static bool IsValid(AssetReferenceT<OneFontAsset> reference) =>
            reference != null && reference.RuntimeKeyIsValid();
    }
}
