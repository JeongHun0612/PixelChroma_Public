using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class SeekerPlayer : PlayerBase
    {
        private SeekerWaterGun _waterGun;
        private UIInGameHUD_Seeker _seekerHUD;

        protected override UIInGameHUD InGameHUD => _seekerHUD;
        protected override bool CanUseFormWheel => false;   // Seeker는 변신 불가
        protected override bool CanTogglePaintMode => true;
        protected override bool CanEnterCheckTimeSpectator => false;


        protected override void Awake()
        {
            base.Awake();
            _waterGun = GetComponent<SeekerWaterGun>();
        }

        protected override void Update()
        {
            base.Update();

            if (isServer && _waterGun != null)
                _waterGun.ServerTick();


            if (!isLocalPlayer) return;

            if (_waterGun != null)
                _waterGun.LocalRegenTick();

            if (Spectator != null && Spectator.IsSpectating)
            {
                if (!IsGameplayInputBlocked)
                    HandleSpectateInput();
                return;
            }

            if (!IsGameplayInputBlocked)
            {
                HandleSpectateInput();
                if (_waterGun != null)
                    _waterGun.LocalTick(IsPaintMode);
            }

            if (_seekerHUD != null && _waterGun != null)
            {
                _seekerHUD.UpdateWaterGunCooldown(_waterGun.CooldownFill01);
                _seekerHUD.UpdateAmmoDisplay(
                    _waterGun.CurrentAmmo,
                    _waterGun.MaxAmmo,
                    _waterGun.RegenFill01);    
            }
        }

        protected override void OnLocalPlayerStarted()
        {
            PaintController.OnPaintModeChanged += HandleLocalPaintModeChanged;
            ShowHUDAsync().Forget();
            InitializeSpectator();
            ApplyDefaultCursor();
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            PaintController.OnPaintModeChanged -= HandleLocalPaintModeChanged;

            CleanupSpectator();
        }

        // Seeker의 관전은 체크타임 잠금 전용 → ESC로 종료하지 않는다.
        // (해제는 OnCheckTimeSpectatorExit에서만 일어남)
        protected override bool TryExitSpectateByEscape()
        {
            if (Spectator == null || !Spectator.IsSpectating) return false;

            Spectator.ExitSpectate();
            _seekerHUD?.SetSpectatorMode(false);
            return true;
        }

        protected override void HandleSpectateInput()
        {
            if (Spectator == null || !IsCheckTimeSpectatorLocked) return;

            if (Input.ToggleSpectate.WasPressedThisFrame())
            {
                if (Spectator.IsSpectating)
                {
                    // 관전 중 → 관전 종료 (자기 캐릭터로 복귀)
                    Spectator.ExitSpectate();
                    _seekerHUD?.SetSpectatorMode(false);
                }
                else
                {
                    // 관전 아닌 상태 → 다시 관전 진입
                    Spectator.EnterRunnerMode();
                    _seekerHUD?.SetSpectatorMode(true);
                    _seekerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }
                return;
            }

            base.HandleSpectateInput();
        }

        protected override void OnCheckTimeSpectatorEnter()
        {
            _seekerHUD?.SetSpectateKeyHintVisible(true);
        }

        protected override void OnCheckTimeSpectatorExit()
        {
            _seekerHUD?.SetSpectateKeyHintVisible(false);

            if (Spectator != null && Spectator.IsSpectating)
            {
                Spectator.ExitSpectate();
                _seekerHUD?.SetSpectatorMode(false);
            }
        }

        public override CursorType GetDefaultCursorType()
        {
            return CursorType.Crosshair;
        }


        [ClientRpc]
        public void RpcTeleport(Vector3 newPosition)
        {
            transform.position = newPosition;

            if (Rb != null)
            {
                Rb.linearVelocity = Vector2.zero;
            }

            if (cinemachineCamera != null)
            {
                cinemachineCamera.PreviousStateIsValid = false;
            }

            Debug.Log("[Seeker] 본진으로 텔레포트 되었습니다!");
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            _seekerHUD = await UIManager.Instance.ShowScene<UIInGameHUD_Seeker>();
            PaintController.BindHUD(_seekerHUD);
            BindSpectateHUD(_seekerHUD);

            _seekerHUD.InitializeSlots();                    

            if (_waterGun != null)
            {
                _seekerHUD.SetWaterGunCooldownDuration(_waterGun.FireCooldownDuration);
                _waterGun.SetMaxAmmo(_seekerHUD.SlotCount);
            }

            _seekerHUD.SetSpectateKeyHintVisible(false);
        }

        private void HandleLocalPaintModeChanged(bool isPaint)
        {
            if (_waterGun != null)
                _waterGun.SetWeaponVisible(!isPaint);

            if (isPaint && Spectator != null && Spectator.IsSpectating)
            {
                Spectator.ExitSpectate();
                _seekerHUD?.SetSpectatorMode(false);
            }
        }

        public void NotifyEmptyFire()
        {
            _seekerHUD?.ShakeAmmoText();
        }
    }
}
