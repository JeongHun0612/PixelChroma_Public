using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIRoomList : UIPopup
    {
        [Header("Filters")]
        [SerializeField] private OneTextDropdown gameModeDropdown;
        [SerializeField] private OneTextDropdown mapDropdown;
        [SerializeField] private OneTextDropdown languageDropdown;
        [SerializeField] private OneTextInputField roomNameInput;

        [Header("Actions")]
        [SerializeField] private Button searchButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button closeButton;

        [Header("List")]
        [SerializeField] private Transform listContent;
        [SerializeField] private UIRoomListItem itemPrefab;

        [Header("Status")]
        [SerializeField] private GameObject emptyRoot;
        [SerializeField] private OneTextLabel statusText;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        [Header("Localization")]
        [SerializeField] private RoomListTextDataSO textData;
        [SerializeField] private LanguageTextDataSO languageTable;

        [Header("Registry")]
        [SerializeField] private GameModeRegistrySO gameModeRegistry;

        [Header("Auto Refresh")]
        [SerializeField] private float autoRefreshInterval = 5f;

        private CancellationTokenSource _refreshCts;

        private readonly List<UIRoomListItem> _items = new();

        // 드롭다운 인덱스 → 실제 값 매핑 (인덱스 0 = "전체" = null/빈값)
        private readonly List<GameMode?> _gameModeValues = new();
        private readonly List<string> _mapIdValues = new();
        private readonly List<string> _languageCodeValues = new();

        // ── 초기화 ──────────────────────────────────────────

        public override void Initialize()
        {
            base.Initialize();

            if (searchButton != null) searchButton.AddClickListener(RequestSearch);
            if (refreshButton != null) refreshButton.AddClickListener(RequestSearch);
            if (closeButton != null) closeButton.AddClickListener(OnClickBack);
        }
        private void OnDestroy()
        {
            StopAutoRefresh();
        }


        private void PopulateGameModeDropdown()
        {
            if (gameModeDropdown == null) return;

            gameModeDropdown.ClearOptions();
            _gameModeValues.Clear();

            string allLabel = textData.allFilterLabel.GetLocalizedString();
            var options = new List<OneTextDropdown.OptionData> { new(allLabel) };
            _gameModeValues.Add(null);

            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                var data = gameModeRegistry != null ? gameModeRegistry.Find(mode) : null;
                string label = data != null ? data.displayName.GetLocalizedString() : mode.ToString();
                options.Add(new OneTextDropdown.OptionData(label));
                _gameModeValues.Add(mode);
            }

            gameModeDropdown.AddOptions(options);
        }

        private void PopulateMapDropdown()
        {
            if (mapDropdown == null) return;

            mapDropdown.ClearOptions();
            _mapIdValues.Clear();

            var options = new List<OneTextDropdown.OptionData> { new(textData.allFilterLabel.GetLocalizedString()) };
            _mapIdValues.Add(null);

            var registry = LobbyManager.Instance != null ? LobbyManager.Instance.MapRegistry : null;
            if (registry != null)
            {
                foreach (var map in registry.Maps)
                {
                    if (map == null) continue;
                    string label = map.GetDisplayName();
                    options.Add(new OneTextDropdown.OptionData(label));
                    _mapIdValues.Add(map.name);
                }
            }

            mapDropdown.AddOptions(options);
        }

        private void PopulateLanguageDropdown()
        {
            if (languageDropdown == null) return;

            languageDropdown.ClearOptions();
            _languageCodeValues.Clear();

            var options = new List<OneTextDropdown.OptionData> { new(textData.allFilterLabel.GetLocalizedString()) };
            _languageCodeValues.Add(null);

            var locales = LocalizationSettings.AvailableLocales?.Locales;
            if (locales != null)
            {
                foreach (var locale in locales)
                {
                    string code = locale.Identifier.Code;

                    // 라벨은 현재 UI 언어 기준 이름 우선, 미등록 시 자국어 표기로 폴백
                    string label = languageTable != null && languageTable.TryGetLocalizedName(locale.Identifier, out var localized)
                        ? localized
                        : locale.Identifier.CultureInfo != null
                            ? locale.Identifier.CultureInfo.NativeName
                            : code;

                    options.Add(new OneTextDropdown.OptionData(label));
                    _languageCodeValues.Add(code); // 필터 값은 항상 로케일 코드
                }
            }

            languageDropdown.AddOptions(options);
        }

        // ── 팝업 라이프사이클 ────────────────────────────────

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            PopulateGameModeDropdown();
            PopulateMapDropdown();
            PopulateLanguageDropdown();

            if (gameModeDropdown != null) gameModeDropdown.SetValueWithoutNotify(0);
            if (mapDropdown != null) mapDropdown.SetValueWithoutNotify(0);
            if (languageDropdown != null) languageDropdown.SetValueWithoutNotify(0);
            if (roomNameInput != null) roomNameInput.SetTextWithoutNotify("");

            ClearList();
            SetEmpty(false);
            SetStatus("");

            if (panel != null) panel.localScale = Vector3.zero;
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack).ToUniTask();
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack).ToUniTask();
        }

        protected override UniTask OnShow()
        {
            var searcher = RoomListSearcher.Instance;
            if (searcher != null)
                searcher.OnSearchCompleted += HandleSearchCompleted;

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed += HandleJoinFailed;

            RequestSearch();
            StartAutoRefresh();

            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            StopAutoRefresh();

            var searcher = RoomListSearcher.Instance;
            if (searcher != null)
            {
                searcher.OnSearchCompleted -= HandleSearchCompleted;
                searcher.StopSearch();
            }

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleJoinFailed;

            return UniTask.CompletedTask;
        }

        // ── 필터 → 검색 ─────────────────────────────────────

        private RoomListFilter BuildFilter()
        {
            GameMode? mode = gameModeDropdown != null && gameModeDropdown.value < _gameModeValues.Count
                ? _gameModeValues[gameModeDropdown.value]
                : null;

            string mapId = mapDropdown != null && mapDropdown.value < _mapIdValues.Count
                ? _mapIdValues[mapDropdown.value]
                : null;

            string lang = languageDropdown != null && languageDropdown.value < _languageCodeValues.Count
                ? _languageCodeValues[languageDropdown.value]
                : null;

            string nameQuery = roomNameInput != null ? roomNameInput.text.Trim() : null;
            if (string.IsNullOrEmpty(nameQuery)) nameQuery = null;

            return new RoomListFilter(mode, mapId, lang, nameQuery);
        }

        private void RequestSearch()
        {
            ClearList();
            SetEmpty(false);
            SetStatus(textData.searchingStatus.GetLocalizedString());

            if (refreshButton != null) refreshButton.interactable = false;

            RoomListSearcher.Instance?.Search(BuildFilter());
        }

        // ── 검색 결과 ────────────────────────────────────────

        private void HandleSearchCompleted(List<RoomListEntry> entries)
        {
            if (refreshButton != null) refreshButton.interactable = true;
            SetStatus("");

            ClearList();

            if (entries == null || entries.Count == 0)
            {
                SetEmpty(true);
                return;
            }

            SetEmpty(false);

            foreach (var entry in entries)
            {
                if (itemPrefab == null || listContent == null) break;

                var item = Instantiate(itemPrefab, listContent);
                item.Setup(entry);
                item.OnJoinClicked += HandleJoinRoom;
                item.gameObject.SetActive(true);
                _items.Add(item);
            }
        }

        // ── 입장 ─────────────────────────────────────────────

        private void HandleJoinRoom(RoomListEntry entry)
        {
            ProceedJoinRoom(entry);
        }

        private void ProceedJoinRoom(RoomListEntry entry)
        {
            StopAutoRefresh();

            foreach (var item in _items)
                item.SetInteractable(false);

            SetStatus(textData.joiningStatus.GetLocalizedString());

            RoomListSearcher.Instance?.StopSearch();
            LobbyManager.Instance?.JoinRoom(entry);
        }

        private void HandleJoinFailed(LobbyFailureReason reason)
        {
            UIManager.Instance.ShowPopup<UIwarningTextPopup>(p => p.SetReason(reason)).Forget();

            RequestSearch();
            StartAutoRefresh();
        }

        // ── 유틸 ─────────────────────────────────────────────

        private void ClearList()
        {
            foreach (var item in _items)
            {
                item.OnJoinClicked -= HandleJoinRoom;
                Destroy(item.gameObject);
            }
            _items.Clear();
        }

        private void SetEmpty(bool show)
        {
            if (emptyRoot != null) emptyRoot.SetActive(show);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void StartAutoRefresh()
        {
            StopAutoRefresh();
            _refreshCts = new CancellationTokenSource();
            AutoRefreshLoop(_refreshCts.Token).Forget();
        }

        private void StopAutoRefresh()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = null;
        }

        private async UniTaskVoid AutoRefreshLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(autoRefreshInterval),
                    cancellationToken: token);

                RequestSearch();
            }
        }
    }
}