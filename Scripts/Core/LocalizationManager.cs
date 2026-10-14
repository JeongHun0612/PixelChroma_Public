using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OneText;
using OneText.UGUI;
using Steamworks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PixelChroma
{
    // 로컬라이즈 폰트의 용도(역할)
    // Ui      : 우리가 관리하는 로컬라이즈 문자열용(디스플레이 폰트).
    // Content : 유저가 입력한 임의 언어 텍스트용(채팅·닉네임·방 제목 등, 가독성 폰트).
    public enum FontRole { Ui, Content }

    public static class LocalizationManager
    {
        private const string PrefsKey = "SelectedLocaleCode";

        // 역할별 폰트 채널. 등록 라벨 풀 + 현재 폰트 + Addressables 핸들을 함께 관리한다.
        private sealed class FontChannel
        {
            public readonly HashSet<OneTextLabel> Labels = new();
            public readonly Func<FontTableSO, LocaleIdentifier, AssetReferenceT<OneFontAsset>> Resolve;

            public OneFontAsset CurrentFont;
            public AsyncOperationHandle<OneFontAsset> Handle;
            public object CurrentRuntimeKey;

            public FontChannel(Func<FontTableSO, LocaleIdentifier, AssetReferenceT<OneFontAsset>> resolve)
            {
                Resolve = resolve;
            }
        }

        private static readonly FontChannel _ui = new((table, id) => table.Resolve(id));
        private static readonly FontChannel _content = new((table, id) => table.ResolveContent(id));

        private static FontTableSO _fontTable;

        public static OneFontAsset CurrentFont => _ui.CurrentFont;

        public static void Initialize(FontTableSO fontTable)
        {
            _fontTable = fontTable;
        }

        // ── Register ────────────────────────────────────────────────────

        public static void Register(OneTextLabel label, FontRole role = FontRole.Ui)
        {
            if (label == null)
                return;

            var channel = Channel(role);
            if (!channel.Labels.Add(label))
                return;

            if (channel.CurrentFont != null)
                label.Font = channel.CurrentFont;
        }

        public static void Unregister(OneTextLabel label)
        {
            if (label == null)
                return;

            // 어느 역할로 등록됐는지 몰라도 되도록 양쪽 풀에서 제거한다(라벨은 한쪽에만 있음).
            _ui.Labels.Remove(label);
            _content.Labels.Remove(label);
        }

        private static FontChannel Channel(FontRole role) => role == FontRole.Content ? _content : _ui;

        // ── Locale ──────────────────────────────────────────────────────

        // 현재 선택된 로케일 코드. 선택된 로케일이 없으면 빈 문자열.
        public static string CurrentLanguageCode =>
            LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : "";

        public static async UniTask ApplySavedLocaleAsync()
        {
            string savedCode = PlayerPrefs.GetString(PrefsKey, "");

            // 저장된 선택이 없으면(최초 실행 등) Steam 게임 언어를 기본값으로 시도합니다.
            // Steam이 실제 초기화된 경우에만 조회합니다(초기화 실패 시 경고 패널 경로에서는 건너뜀).
            if (string.IsNullOrEmpty(savedCode) && GameManager.SteamInitialized)
                savedCode = TryGetSteamLocaleCode();

            if (!string.IsNullOrEmpty(savedCode))
            {
                var saved = LocalizationSettings.AvailableLocales.GetLocale(savedCode);
                if (saved != null)
                    LocalizationSettings.SelectedLocale = saved;
            }

            var locale = LocalizationSettings.SelectedLocale;
            if (locale != null)
                await ApplyFontAsync(locale);
        }

        public static async UniTask SetLocaleAsync(Locale locale)
        {
            if (locale == null) return;

            await ApplyFontAsync(locale);
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(PrefsKey, locale.Identifier.Code);
        }

        // ── Cleanup ─────────────────────────────────────────────────────
        public static void Release()
        {
            ReleaseChannel(_ui);
            ReleaseChannel(_content);
        }

        private static void ReleaseChannel(FontChannel channel)
        {
            channel.Labels.Clear();
            channel.CurrentFont = null;
            channel.CurrentRuntimeKey = null;

            if (channel.Handle.IsValid())
            {
                Addressables.Release(channel.Handle);
                channel.Handle = default;
            }
        }

        // ── Font ────────────────────────────────────────────────────────
        private static async UniTask ApplyFontAsync(Locale locale)
        {
            if (_fontTable == null)
            {
                Debug.LogError("[LocalizationManager] FontTable이 초기화되지 않았습니다.");
                return;
            }

            // UI/콘텐츠 폰트를 동시에 로드·적용한다.
            await UniTask.WhenAll(
                ApplyChannelAsync(_ui, locale, "UI"),
                ApplyChannelAsync(_content, locale, "Content"));
        }

        private static async UniTask ApplyChannelAsync(FontChannel channel, Locale locale, string roleName)
        {
            var reference = channel.Resolve(_fontTable, locale.Identifier);
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[LocalizationManager] locale '{locale.Identifier.Code}'의 {roleName} 폰트가 없습니다.");
                return;
            }

            if (channel.CurrentRuntimeKey != null && channel.CurrentRuntimeKey.Equals(reference.RuntimeKey))
                return;

            var newHandle = Addressables.LoadAssetAsync<OneFontAsset>(reference.RuntimeKey);
            var newFont = await newHandle.ToUniTask();
            var oldHandle = channel.Handle;

            channel.CurrentFont = newFont;
            channel.Handle = newHandle;
            channel.CurrentRuntimeKey = reference.RuntimeKey;

            channel.Labels.RemoveWhere(l => l == null);
            foreach (var label in channel.Labels)
                label.Font = newFont;

            // 새 폰트를 모든 라벨에 반영한 뒤에 이전 핸들을 해제한다.
            if (oldHandle.IsValid())
                Addressables.Release(oldHandle);
        }

        private static string TryGetSteamLocaleCode()
        {
            string steamLang = SteamApps.GetCurrentGameLanguage();
            return SteamLangToLocaleCode(steamLang);
        }

        private static string SteamLangToLocaleCode(string steamLang) => steamLang switch
        {
            "english" => "en",
            "koreana" => "ko",   // Steam은 한국어를 "korean"이 아닌 "koreana"로 반환
            "japanese" => "ja",
            "schinese" => "zh-Hans",
            "tchinese" => "zh-Hant",
            "french" => "fr",
            "german" => "de",
            "spanish" => "es",   // Spanish-Spain
            "latam" => "es",   // Spanish-Latin America → es로 통합
            "italian" => "it",
            "polish" => "pl",
            "russian" => "ru",
            "turkish" => "tr",
            "brazilian" => "pt-BR",
            "portuguese" => "pt-BR",
            _ => "" // 미지원 언어 → 시스템/기본 로케일로 fallback
        };
    }
}
