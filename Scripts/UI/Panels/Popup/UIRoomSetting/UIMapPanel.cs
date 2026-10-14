using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 맵 목록을 "기본(내장) / 창작마당" 두 소스로 나눠 보여준다.
    //
    // 탭은 목록을 거르는 필터일 뿐이고, 선택된 맵은 탭을 옮겨도 유지된다.
    // (탭을 옮겼다는 이유로 방 옵션의 맵이 바뀌면 안 된다.)
    // 그래서 선택의 단일 원본은 목록 인덱스가 아니라 맵 ID다.
    public class UIMapPanel : MonoBehaviour
    {
        private enum MapSource { BuiltIn, Workshop }

        // 내장맵과 창작마당 맵을 목록 관점에서 같은 모양으로 다루기 위한 뷰 모델.
        // 이걸 두면 풀 인덱스 == _entries 인덱스가 되어, 두 소스를 이어 붙이며 쓰던 오프셋 산술이 사라진다.
        private readonly struct MapEntryView
        {
            public readonly string NetworkId;    // RoomOption.mapName 에 그대로 들어가는 값
            public readonly string DisplayName;
            public readonly Sprite Preview;
            public readonly bool Selectable;     // 아직 내려받는 중인 창작마당 맵은 고를 수 없다

            public MapEntryView(string networkId, string displayName, Sprite preview, bool selectable = true)
            {
                NetworkId = networkId;
                DisplayName = displayName;
                Preview = preview;
                Selectable = selectable;
            }
        }

        [Header("Preview / List")]
        [SerializeField] private Image previewImage;
        [SerializeField] private ScrollRect listScrollRect;
        [SerializeField] private Transform listContent;
        [SerializeField] private UIMapItem itemPrefab;

        [Header("Source Tabs")]
        [SerializeField] private UIFocusButton builtInTabButton;
        [SerializeField] private UIFocusButton workshopTabButton;

        [Header("Workshop")]
        // 창작마당 탭이 비었을 때 보여줄 안내. 문구는 이 오브젝트에 붙은 LocalizeStringEvent가 담당하므로
        // 여기서는 켜고 끄기만 한다.
        [SerializeField] private GameObject emptyRoot;

        [Header("Localization")]
        [SerializeField] private RoomSettingTextDataSO textData;

        private readonly List<UIMapItem> _pool = new();
        private readonly List<MapEntryView> _entries = new();

        // fileId → 메타데이터를 읽어둔 엔트리.
        // LoadMetadata()는 manifest 파싱 + 프리뷰 텍스처 생성(회수되지 않는다)이라,
        // 탭을 오갈 때마다 다시 부르면 디스크 IO와 텍스처 누수가 반복된다. 설치된 맵만 캐시한다.
        private readonly Dictionary<ulong, WorkshopMapEntry> _metadataCache = new();

        private MapRegistrySO _registry;
        private MapSource _source = MapSource.BuiltIn;
        private string _selectedMapId;
        private bool _initialized;

        public event Action<string> OnMapSelected;

        // ── Public ──────────────────────────────────────

        public void Initialize(MapRegistrySO registry)
        {
            if (_initialized) return;
            _initialized = true;

            // 탭/버튼 배선은 레지스트리 유무와 무관하게 항상 해둔다.
            if (builtInTabButton != null)
                builtInTabButton.OnClicked += () => SetSource(MapSource.BuiltIn);

            if (workshopTabButton != null)
                workshopTabButton.OnClicked += () => SetSource(MapSource.Workshop);

            if (listContent == null) return;

            _registry = registry;

            foreach (Transform child in listContent)
            {
                var item = child.GetComponent<UIMapItem>();
                if (item != null)
                {
                    item.OnClicked += Select;
                    item.gameObject.SetActive(false);
                    _pool.Add(item);
                }
            }

            if (_registry == null) return;

            int needed = _registry.Maps.Count - _pool.Count;
            for (int i = 0; i < needed; i++)
                _pool.Add(CreateItem());
        }

        public void Refresh(MapRegistrySO registry, string currentMapName)
        {
            if (registry != null) _registry = registry;

            _selectedMapId = currentMapName;

            // 현재 고른 맵이 있는 쪽 탭으로 연다. (편집 모드에서 다시 열었을 때 바로 보이도록)
            _source = WorkshopMapId.IsWorkshop(currentMapName) ? MapSource.Workshop : MapSource.BuiltIn;

            ApplyTabVisual();
            RebuildList();

            int index = IndexOf(_selectedMapId);

            if (index >= 0)
                ApplySelection(_entries[index]);   // 프리뷰 복원 + 옵션 재확정
            else
                SelectFirstAvailable();            // 최초 생성이거나, 고른 맵이 사라진 경우

            RefreshHighlight();
            UpdateWorkshopUI();
        }

        // 맨 위로 올리면 아래쪽에 있는 선택 항목이 화면 밖으로 나가므로, 선택 항목이 맨 위에 오도록 맞춘다.
        // 선택이 다른 탭에 있거나(-1) 목록이 viewport 안에 다 들어오면 그냥 맨 위.
        public void ResetScroll()
        {
            if (listScrollRect == null || listScrollRect.content == null || listScrollRect.viewport == null) return;

            // 호출 시점에 따라 목록 크기가 아직 반영되지 않았을 수 있다. 재보기 전에 확정시킨다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(listScrollRect.content);

            int index = IndexOf(_selectedMapId);
            float scrollable = listScrollRect.content.rect.height - listScrollRect.viewport.rect.height;

            if (index < 0 || index >= _pool.Count || scrollable <= 0f)
            {
                listScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            // 인덱스 비율이 아니라 실제 좌표로 계산해야 한다.
            // 비율은 스크롤 가능 거리(content - viewport)가 아니라 항목 수에 비례해서, 항목이 엉뚱한 높이에 걸린다.
            var item = (RectTransform)_pool[index].transform;
            float itemTop = listScrollRect.content.rect.max.y - (item.localPosition.y + item.rect.max.y);

            // 맨 위=1, 맨 아래=0.
            listScrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - itemTop / scrollable);
        }

        // ── 스팀 이벤트 구독 ─────────────────────────────
        // 팝업이 열려 있는 동안에만 구독한다. (UIPanel.Hide가 gameObject를 끄므로 OnDisable이 확실히 온다)

        private void OnEnable()
        {
            if (!GameManager.SteamActive) return;

            WorkshopSubscriptionService.EnsureCallbacks();
            WorkshopSubscriptionService.OnItemDownloaded += HandleItemDownloaded;
            WorkshopSubscriptionService.OnOverlayClosed += HandleOverlayClosed;
        }

        private void OnDisable()
        {
            WorkshopSubscriptionService.OnItemDownloaded -= HandleItemDownloaded;
            WorkshopSubscriptionService.OnOverlayClosed -= HandleOverlayClosed;
        }

        // 다운로드가 끝나면 그 맵의 캐시를 버리고(미설치 상태로 잡혀 있다) 목록을 다시 그린다.
        private void HandleItemDownloaded(ulong fileId)
        {
            _metadataCache.Remove(fileId);
            if (_source == MapSource.Workshop) RebuildAndKeepSelection();
        }

        // 오버레이에서 새로 구독하고 돌아왔을 수 있으므로 목록을 다시 훑는다.
        private void HandleOverlayClosed()
        {
            if (_source == MapSource.Workshop) RebuildAndKeepSelection();
        }

        // ── 탭 ──────────────────────────────────────────

        private void SetSource(MapSource source)
        {
            if (_source == source) return;
            _source = source;

            ApplyTabVisual();
            RebuildAndKeepSelection();

            // 탭을 바꾸면 목록이 통째로 달라지므로 이전 탭의 스크롤 위치는 의미가 없다.
            // (다운로드/오버레이로 인한 갱신은 보던 자리를 유지해야 하므로 여기서만 되돌린다)
            ResetScroll();
        }

        private void ApplyTabVisual()
        {
            if (builtInTabButton != null) builtInTabButton.SetSelected(_source == MapSource.BuiltIn);
            if (workshopTabButton != null) workshopTabButton.SetSelected(_source == MapSource.Workshop);
        }

        // 목록만 다시 그리고 선택은 건드리지 않는다. (다른 탭을 구경해도 방 옵션은 그대로)
        private void RebuildAndKeepSelection()
        {
            RebuildList();
            RefreshHighlight();
            UpdateWorkshopUI();
        }

        // ── 선택 ────────────────────────────────────────

        private void Select(int index)
        {
            if (index < 0 || index >= _entries.Count) return;

            var entry = _entries[index];
            if (!entry.Selectable) return;

            ApplySelection(entry);
            RefreshHighlight();
        }

        private void ApplySelection(MapEntryView entry)
        {
            _selectedMapId = entry.NetworkId;

            if (previewImage != null)
            {
                previewImage.sprite = entry.Preview;
                previewImage.enabled = entry.Preview != null;
            }

            OnMapSelected?.Invoke(entry.NetworkId);
        }

        // 현재 탭의 첫 유효 항목을 고른다. 현재 탭이 비었으면 기본 탭으로 되돌아간다.
        private void SelectFirstAvailable()
        {
            if (!TrySelectFirst() && _source != MapSource.BuiltIn)
            {
                _source = MapSource.BuiltIn;
                ApplyTabVisual();
                RebuildList();
                TrySelectFirst();
            }
        }

        private bool TrySelectFirst()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].Selectable) continue;

                ApplySelection(_entries[i]);
                return true;
            }

            return false;
        }

        private int IndexOf(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return -1;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].NetworkId == mapId) return i;
            }

            return -1;
        }

        // ── 목록 구성 ───────────────────────────────────

        private void RebuildList()
        {
            _entries.Clear();

            if (_source == MapSource.BuiltIn) BuildBuiltInEntries();
            else BuildWorkshopEntries();

            for (int i = 0; i < _entries.Count; i++)
            {
                var item = GetOrCreateItem(i);
                item.Setup(i, _entries[i].DisplayName);
                item.SetInteractable(_entries[i].Selectable);
                item.gameObject.SetActive(true);
            }

            for (int i = _entries.Count; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        private void BuildBuiltInEntries()
        {
            if (_registry == null) return;

            for (int i = 0; i < _registry.Maps.Count; i++)
            {
                var map = _registry.Maps[i];
                if (map == null) continue;

                _entries.Add(new MapEntryView(map.name, map.GetDisplayName(), map.previewSprite));
            }
        }

        private void BuildWorkshopEntries()
        {
            if (!GameManager.SteamActive) return;

            var subscribed = WorkshopSubscriptionService.GetSubscribedMaps();

            foreach (var entry in subscribed)
            {
                if (!entry.isInstalled)
                {
                    // 구독은 됐지만 아직 안 받아진 맵. 다운로드를 걸고 목록에는 '다운로드 중'으로 남긴다.
                    // (목록에서 아예 빼버리면 유저는 구독한 맵이 왜 안 보이는지 알 수 없다)
                    WorkshopSubscriptionService.RequestDownload(entry.publishedFileId);

                    _entries.Add(new MapEntryView(
                        WorkshopMapId.From(entry.publishedFileId),
                        DownloadingLabel(),
                        null,
                        selectable: false));
                    continue;
                }

                var loaded = GetOrLoadMetadata(entry);
                string displayName = string.IsNullOrEmpty(loaded.Title)
                    ? $"Workshop #{loaded.publishedFileId}"
                    : loaded.Title;

                _entries.Add(new MapEntryView(loaded.NetworkId, displayName, loaded.Preview));
            }
        }

        private WorkshopMapEntry GetOrLoadMetadata(WorkshopMapEntry entry)
        {
            if (_metadataCache.TryGetValue(entry.publishedFileId, out var cached))
                return cached;

            entry.LoadMetadata();
            _metadataCache[entry.publishedFileId] = entry;
            return entry;
        }

        private UIMapItem GetOrCreateItem(int index)
        {
            if (index < _pool.Count)
                return _pool[index];

            var item = CreateItem();
            _pool.Add(item);
            return item;
        }

        private UIMapItem CreateItem()
        {
            var item = Instantiate(itemPrefab, listContent);
            item.OnClicked += Select;
            item.gameObject.SetActive(false);
            return item;
        }

        private void RefreshHighlight()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeSelf) continue;

                // 다른 탭에 있는 맵을 고른 상태면 이 탭에는 강조할 항목이 없다.
                _pool[i].SetSelected(i < _entries.Count && _entries[i].NetworkId == _selectedMapId);
            }
        }

        // ── 창작마당 안내 ────────────────────────────────

        // 창작마당 탭인데 보여줄 맵이 하나도 없을 때만 안내를 띄운다.
        private void UpdateWorkshopUI()
        {
            if (emptyRoot == null) return;

            emptyRoot.SetActive(_source == MapSource.Workshop && _entries.Count == 0);
        }

        // 목록 항목 라벨은 코드가 써넣는 값이라 LocalizeStringEvent를 쓸 수 없어 SO에서 가져온다.
        private string DownloadingLabel()
            => textData != null && !textData.workshopDownloading.IsEmpty
                ? textData.workshopDownloading.GetLocalizedString()
                : "Downloading...";
    }
}
