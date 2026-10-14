using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// InGame HUD 씬의 베이스 클래스입니다. Runner / Seeker가 이 클래스를 상속합니다.
    /// 공통 UI 참조·로직은 <see cref="UIInGameHUDCommon"/>(Base 프리팹에 부착)에 있고,
    /// 이 클래스는 씬 라이프사이클과 공개 API를 그쪽으로 위임합니다.
    /// </summary>
    public class UIInGameHUD : UIScene, IPaintHUD
    {
        private UIInGameHUDCommon _common;
        protected UIInGameHUDCommon Common =>
            _common != null ? _common : (_common = GetComponent<UIInGameHUDCommon>());

        public UIColorPicker ColorPicker => Common.ColorPicker;
        public bool IsPaintModeActive => Common.IsPaintModeActive;


        protected virtual void Update()
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            Common.Initialize();
            ResetHUDState();
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            Common.SubscribeEvents();
        }

        public override void OnAfterHide()
        {
            base.OnAfterHide();
            Common.UnsubscribeEvents();
        }

        protected override UniTask OnShow()
        {
            ResetHUDState();
            Common.SyncToCurrentState();
            return base.OnShow();
        }

        public override void OnCancel()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }

        public void SetPaintMode(bool active) => Common.SetPaintMode(active);

        // Runner / Seeker가 자신의 전용 상태를 추가로 초기화할 수 있도록 virtual로 둡니다.
        protected virtual void ResetHUDState()
        {
            Common.ResetHUDState();
        }

        // 관전 모드 버튼 클릭. 구독/해제를 그대로 Common으로 넘깁니다.
        public event Action<SpectateMode> OnSpectateModeSelected
        {
            add => Common.OnSpectateModeSelected += value;
            remove => Common.OnSpectateModeSelected -= value;
        }

        public void SetSpectatorMode(bool active) => Common.SetSpectatorMode(active);

        public void SetActiveSpectateMode(SpectateMode mode) => Common.SetActiveSpectateMode(mode);

        public void SetSpectateTarget(PlayerBase target, bool isSelf) => Common.SetSpectateTarget(target, isSelf);

        public void SetSkipVoted() => Common.SetSkipVoted();
    }
}
