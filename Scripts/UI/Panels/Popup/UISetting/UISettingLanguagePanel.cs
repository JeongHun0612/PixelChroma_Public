using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PixelChroma.UI
{
    public class UISettingLanguagePanel : MonoBehaviour
    {
        [Header("Language List")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LanguageButton languageButtonPrefab;

        [Header("Localization")]
        [SerializeField] private LanguageTextDataSO languageTable;

        private readonly List<LanguageButton> _spawned = new();
        private bool _built;

        public void Initialize()
        {
            BuildLanguageButtons();
        }

        // 팝업이 열릴 때 현재 선택된 로케일로 포커스를 갱신한다.
        public void Refresh()
        {
            if (!_built) BuildLanguageButtons();
            RefreshFocus();
        }

        private void RefreshFocus()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            var current = LocalizationSettings.SelectedLocale;

            for (int i = 0; i < _spawned.Count && i < locales.Count; i++)
                _spawned[i].SetSelected(locales[i] == current);
        }

        private void BuildLanguageButtons()
        {
            foreach (var btn in _spawned)
                if (btn != null) Destroy(btn.gameObject);
            _spawned.Clear();

            var locales = LocalizationSettings.AvailableLocales.Locales;
            var current = LocalizationSettings.SelectedLocale;

            foreach (var locale in locales)
            {
                var btn = Instantiate(languageButtonPrefab, contentRoot);
                _spawned.Add(btn);

                // 자국어 표기(endonym) 우선, 미등록 시 CultureInfo.NativeName → 코드 순 폴백.
                string name = languageTable != null
                              && languageTable.TryGetNativeName(locale.Identifier, out var display)
                    ? display
                    : (locale.Identifier.CultureInfo?.NativeName ?? locale.Identifier.Code);
                btn.Setup(name, locale == current);

                Locale captured = locale;   // 클로저 캡처용 지역 복사
                btn.FocusButton.OnClicked += () => OnLanguageSelected(captured);
            }

            _built = true;
        }

        private void OnLanguageSelected(Locale locale)
        {
            OnLanguageSelectedAsync(locale).Forget();
        }

        private async UniTaskVoid OnLanguageSelectedAsync(Locale locale)
        {
            // UILanguageSelect와 달리 여기서는 팝업을 닫지 않는다(설정창 유지).
            await LocalizationManager.SetLocaleAsync(locale);
            RefreshFocus();
        }
    }
}
