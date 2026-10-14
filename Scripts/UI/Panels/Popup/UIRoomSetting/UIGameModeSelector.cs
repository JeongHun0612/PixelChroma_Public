using System.Collections.Generic;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 게임 모드 선택(좌우 화살표 + 탭)을 담당하는 독립 컴포넌트.
    // 탭은 GameModeRegistrySO를 읽어 런타임에 생성하며, 선택 결과는 Bind로 받은 RoomOption에 반영한다.
    public class UIGameModeSelector : MonoBehaviour
    {
        [Header("Game Mode")]
        [SerializeField] private OneTextLabel nameText;
        [SerializeField] private Transform tabContent;
        [SerializeField] private UIFocusButton tabPrefab;
        [SerializeField] private GameModeRegistrySO registry;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private OneTextLabel descriptionText;

        private RoomOption _option;
        private bool _initialized;

        // 풀 위치 == registry.Modes 인덱스 (탭 i ↔ modes[i])
        private readonly List<UIFocusButton> _tabs = new();

        // ── Public ──

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (prevButton != null) prevButton.onClick.AddListener(() => Cycle(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Cycle(1));

            BuildTabs();
        }

        public void Bind(RoomOption option)
        {
            _option = option;
            Refresh();
        }

        // ── Private ──

        // 레지스트리의 모드 수만큼 탭을 생성한다. 풀 위치가 곧 modes 인덱스가 되도록 1:1로 만든다.
        private void BuildTabs()
        {
            if (tabContent == null || tabPrefab == null || registry == null) return;

            var modes = registry.Modes;
            if (modes == null) return;

            for (int i = 0; i < modes.Count; i++)
            {
                var tab = Instantiate(tabPrefab, tabContent);
                int index = i; // 클로저 캡처 (풀 위치 == modes 인덱스)
                tab.OnClicked += () => SelectAt(index);
                _tabs.Add(tab);
            }
        }

        private void SetMode(GameMode mode)
        {
            if (_option == null) return;

            _option.gameMode = mode;
            Refresh();
        }

        // 현재 모드의 레지스트리 인덱스. 못 찾으면 0.
        private int CurrentIndex()
        {
            var modes = registry != null ? registry.Modes : null;
            if (modes == null) return 0;

            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i] != null && modes[i].mode == _option.gameMode)
                    return i;
            }

            return 0;
        }

        private void Refresh()
        {
            if (_option == null) return;

            var data = registry != null ? registry.Find(_option.gameMode) : null;

            // 탭 Focus: 현재 모드 인덱스만 활성화
            int current = CurrentIndex();
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i] != null) _tabs[i].SetSelected(i == current);
            }

            // 중앙 라벨: 현재 모드 이름. (displayName 미설정 시 비움)
            if (nameText != null)
                nameText.text = data != null && !data.displayName.IsEmpty
                    ? data.displayName.GetLocalizedString()
                    : string.Empty;

            // SO나 description이 아직 안 채워진 모드는 설명을 비워둔다.
            if (descriptionText != null)
                descriptionText.text = data != null && !data.description.IsEmpty
                    ? data.description.GetLocalizedString()
                    : string.Empty;
        }

        // ── 이벤트 바인딩 ──

        // 좌우 화살표(◀/▶)로 레지스트리에 등록된 게임 모드를 순환한다. (양 끝에서 반대편으로 감김)
        private void Cycle(int delta)
        {
            if (_option == null || registry == null) return;

            var modes = registry.Modes;
            if (modes == null || modes.Count == 0) return;

            int index = (CurrentIndex() + delta + modes.Count) % modes.Count;
            SelectAt(index);
        }

        // 탭 클릭/화살표 순환 공용: 레지스트리 순서(= 탭 순서) 기준 인덱스로 모드를 선택한다.
        private void SelectAt(int index)
        {
            if (_option == null || registry == null) return;

            var modes = registry.Modes;
            if (modes == null || index < 0 || index >= modes.Count || modes[index] == null) return;

            SetMode(modes[index].mode);
        }
    }
}
