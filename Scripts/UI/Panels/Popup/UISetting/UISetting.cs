using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UISetting : UIPopup
    {
        // 탭 버튼 ↔ 내용 패널을 한 쌍으로 묶어 인스펙터에서 관리한다.
        [Serializable]
        private class Tab
        {
            public UIFocusButton button;   // 버튼 + 선택 표시(focus)를 한 컴포넌트로 묶음
            public GameObject panel;
        }

        [Header("Tabs")]
        [SerializeField] private List<Tab> tabs = new();

        [Header("Sub Panels")]
        [SerializeField] private UISettingSoundPanel soundPanel;
        [SerializeField] private UISettingDisplayPanel displayPanel;
        [SerializeField] private UISettingLanguagePanel languagePanel;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button exitToTitleButton;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        public override void Initialize()
        {
            base.Initialize();

            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;
                if (tabs[i].button != null)
                    tabs[i].button.OnClicked += () => SelectTab(index);
            }

            if (closeButton != null) closeButton.AddClickListener(OnClickBack);
            if (exitToTitleButton != null) exitToTitleButton.AddClickListener(OnClickExitToTitle);

            soundPanel?.Initialize();
            displayPanel?.Initialize();
            languagePanel?.Initialize();
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            // 세션(로비/인게임) 중에만 '타이틀로' 노출. 타이틀에선 이미 타이틀이므로 숨긴다.
            bool inSession = GameManager.Instance != null
                             && GameManager.Instance.CurrentState != GameState.MainMenu;
            if (exitToTitleButton != null)
                exitToTitleButton.gameObject.SetActive(inSession);

            // 다른 경로로 값이 바뀌었을 수 있으므로 열 때마다 현재 값으로 갱신한 뒤 첫 탭을 연다.
            soundPanel?.Refresh();
            displayPanel?.Refresh();
            languagePanel?.Refresh();
            SelectTab(0);

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

        private void SelectTab(int index)
        {
            if (index < 0 || index >= tabs.Count) return;

            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == index;
                if (tabs[i].panel != null) tabs[i].panel.SetActive(on);
                if (tabs[i].button != null) tabs[i].button.SetSelected(on);
            }
        }

        private void OnClickExitToTitle()
        {
            UIManager.Instance.HideAllPopups().Forget();
            GameManager.Instance?.LeaveSession();
        }
    }
}
