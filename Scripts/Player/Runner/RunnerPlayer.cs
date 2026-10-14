using Cysharp.Threading.Tasks;
using Mirror;
using PixelChroma.UI;
using System;
using UnityEngine;

public struct KnockbackInfo
{
    public Vector2 dir;
    public float impulse;
    public float drag;
    public float rotSpeed;
}

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        private const float PaintRatioSyncInterval = 0.5f;
        private const float ServerCooldownGrace = 0.25f;

        [Header("Paint Mode Settings")]
        [Tooltip("페인트 모드를 켰을 때 적용되는 속도 배율 (예: 0.2 = 평소 속도의 20%로 매우 느려짐)")]
        [SerializeField, Range(0f, 1f)] private float paintModeSpeedMultiplier = 0.2f;

        [Header("Expose Skill")]
        [SerializeField] private float exposeCooldown = 1f;

        [Tooltip("사망 또는 서로 안보이게 할 때 껏다 켯다 할 렌더러")]
        [SerializeField] private SpriteRenderer visualRenderer;

        [Header("Like Interaction UI")]
        [Tooltip("RunnerPlayer 프리팹의 World Space 좋아요 캔버스에 붙인 View입니다. 비워두면 자식에서 자동 탐색합니다.")]
        [SerializeField] private WorldLikeGuideView likeGuideView;

        // 넉백~소멸 연출 구간. 이 동안엔 관전 입력이 카메라를 다시 뺏지 못하게 잠급니다.
        private bool _isDeathCamActive;

        private KnockbackInfo _pendingKnockback;

        private NetworkTransformBase _netTransform;
        private Mirror.NetworkAnimator _netAnimator;

        [SyncVar(hook = nameof(OnDeadStateChanged))] private bool _isDead;
        private bool _isVisuallyDead;
        [SyncVar] private float _paintRatio;

        private float _baseMoveSpeed;
        private float _lastExposeTime = float.NegativeInfinity;
        private float _lastExposeTimeServer = float.NegativeInfinity;
        private float _lastPaintRatioSyncTime;

        private bool _isDeathSpectatorLocked;
        private DeathSnapshot _deathSnapshot = new();
        private UIInGameHUD_Runner _runnerHUD;
        private RunnerDeathEffects _deathEffects;

        protected override UIInGameHUD InGameHUD => _runnerHUD;
        protected override bool CanTogglePaintMode => !_isDead && !IsCheckTimeSpectatorLocked;// 사망여부를 Paint 진입 가능 여부로 설정
        protected override bool CanUseFormWheel => !_isDead;
        public bool IsDead => _isDead;
        public bool IsVisuallyDead => _isVisuallyDead;
        public bool IsKnockedBack => _deathEffects != null && _deathEffects.IsKnockedBack;
        protected override bool CanEnterCheckTimeSpectator => !_isDead;

        public static event Action<string, string, uint, uint> OnPlayerCaught;

        protected override void Awake()
        {
            base.Awake();
            _baseMoveSpeed = moveSpeed;

            _deathEffects = GetComponent<RunnerDeathEffects>();
            _deathEffects.Init(Rb, visualRenderer, animator);

            _netTransform = GetComponent<NetworkTransformBase>();
            _netAnimator = GetComponent<Mirror.NetworkAnimator>();

            if (likeGuideView == null)
                likeGuideView = GetComponentInChildren<WorldLikeGuideView>(true);

            if (paintCanvas != null)
                paintCanvas.OnFormBottomYChanged += HandleFormBottomYChanged;
        }

        internal void ShowWorldLikeGuide()
        {
            likeGuideView?.Show();
        }

        internal void HideWorldLikeGuide()
        {
            likeGuideView?.Hide();
        }

        private void HandleFormBottomYChanged(float formBottomY)
        {
            likeGuideView?.SetFormBottomY(formBottomY);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // 닉네임 가시성과 같은 신호를 재사용해서, 누군가의 역할/사망 상태가 바뀔 때마다 내 몸도 다시 판단합니다.
            NicknameVisibilityChanged += RefreshVisualVisibilityForLocalViewer;

            // Hide→Seek→Check 전환마다 은닉 여부 자체가 바뀌므로, 상태 전환도 직접 구독합니다.
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChangedForVisibility;

            RefreshVisualVisibilityForLocalViewer();
        }

        public override void OnStopClient()
        {
            NicknameVisibilityChanged -= RefreshVisualVisibilityForLocalViewer;
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChangedForVisibility;
            base.OnStopClient();
        }

        protected override void OnDestroy()
        {
            // OnStopClient가 안 불린 채 파괴돼도 좀비 구독이 남지 않도록 백스톱 해제.
            NicknameVisibilityChanged -= RefreshVisualVisibilityForLocalViewer;
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChangedForVisibility;
            if (paintCanvas != null)
                paintCanvas.OnFormBottomYChanged -= HandleFormBottomYChanged;
            base.OnDestroy();
        }
        protected override void UpdateAnimation()
        {
            if (_isDead) return;   
            base.UpdateAnimation();
        }

        protected override void Update()
        {
            base.Update();

            if (!isLocalPlayer) return;

            SyncPaintRatioIfNeeded();

            if (IsGameplayInputBlocked) return;

            if (Input.ExposeSkill.WasPressedThisFrame() || Input.ExposeSkill.IsPressed())
                TryUseExposeSkill();


            HandleSpectateInput();
        }

        protected override void OnLocalPlayerStarted()
        {
            ShowHUDAsync().Forget();
            InitializeSpectator();
            PaintController.OnPaintModeChanged += HandlePaintModeChanged;
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            _runnerHUD = null;

            CleanupSpectator();
            PaintController.OnPaintModeChanged -= HandlePaintModeChanged;
        }

        //--------------------------------------사망시 로직(모드따라 다름)------------------------------------------
        [Server]
        public void ServerNotifyCaught(string catcherName, uint catcherNetId,
    Vector2 knockbackDir = default, float impulse = 0f, float drag = 5f, float rotSpeed = 720f)
        {
            if (_isDead) return;

            if (GameRoundManager.Instance != null && GameRoundManager.Instance.CurrentTimeState != TimeState.SeekTime) return;


            _isDead = true;

            if (ScoreManager.Instance != null && connectionToClient != null)
                ScoreManager.Instance.FreezeScore(connectionToClient.connectionId);

            _pendingKnockback = new KnockbackInfo { dir = knockbackDir, impulse = impulse, drag = drag, rotSpeed = rotSpeed };
            _deathSnapshot.Capture(transform, paintCanvas);
            RpcNotifyCaught(catcherName, playerName, catcherNetId, netId);
            GameRoundManager.Instance?.NotifyRunnerDead();
            GameRoundManager.Instance?.CurrentRule?.OnRunnerCaught(this);
        }

        [ClientRpc]
        private void RpcNotifyCaught(string catcherName, string caughtName, uint catcherNetId, uint caughtNetId)
        {
            OnPlayerCaught?.Invoke(catcherName, caughtName, catcherNetId, caughtNetId);
        }

        [Server]
        public void ServerBeginDeath(GameMode mode) => StartCoroutine(DeathSequenceServer(mode));

        [Server]
        private System.Collections.IEnumerator DeathSequenceServer(GameMode mode)
        {
            RpcPlayDeathAnimation();

            RpcKnockback(_pendingKnockback);
            yield return new WaitForSeconds(_deathEffects.KnockbackDuration);

            RpcDeathSequence();
            yield return new WaitForSeconds(_deathEffects.DeathBlinkDuration);

            RpcRestoreDeathPosition(_deathSnapshot.Position);
            RpcFreezeDeathFrame(_deathSnapshot.FormType, _deathSnapshot.FrameIndex, _deathSnapshot.FlipX);

            if (mode == GameMode.Basic)
            {
                RpcBecomeSpectator();
            }
            else if(mode == GameMode.Infection)
            {
                RpcBecomeGhost();
                (NetworkManager.singleton as ChromaNetworkManager)?.ServerConvertRunnerToSeeker(this);
            }
        }

        [ClientRpc]
        private void RpcKnockback(KnockbackInfo info)
        {
            if (isLocalPlayer)
            {
                // 사망 연출 동안 관전 입력을 잠가, 넉백·소멸 연출을 본인이 놓치지 않게 합니다.
                _isDeathCamActive = true;

                if (formWheel != null && formWheel.IsOpen)
                    formWheel.ForceClose();
            }

            if (_netTransform != null) _netTransform.enabled = false;

            if (paintCanvas != null)
            {
                paintCanvas.DisableActiveCollider();
                paintCanvas.SetPaintVisible(false);
            }

            if (isLocalPlayer && Spectator != null && Spectator.IsSpectating)
            {
                Spectator.ExitSpectate();
            }

            _deathEffects.PlayKnockbackVisuals(paintCanvas);
            if (Rb != null)
                _deathEffects.PlayKnockbackPhysics(info);
        }

        [ClientRpc]
        private void RpcDeathSequence() => _deathEffects.PlayDeathSequence();

        [ClientRpc]
        private void RpcBecomeGhost()
        {
            if (isLocalPlayer) _isDeathCamActive = false;

            bool isCheckTime = GameRoundManager.Instance != null
                    && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;

            _isVisuallyDead = !isCheckTime;

            if (paintCanvas != null) paintCanvas.DisableActiveCollider();
            RefreshVisualVisibilityForLocalViewer();
            RaiseNicknameVisibilityChanged();
        }

        [ClientRpc]
        private void RpcRestoreDeathPosition(Vector3 pos)
        {
            transform.position = pos;
            transform.rotation = Quaternion.identity;
            if (_netTransform != null) _netTransform.enabled = true;
        }
        
        [ClientRpc]
        private void RpcFreezeDeathFrame(PlayerPaintCanvas.PaintFormType formType, int index, bool flipX)
        {
            if (_netAnimator != null) _netAnimator.enabled = false;
            if (paintCanvas != null)
            {
                paintCanvas.FreezeFrame(formType, index, flipX);
                if (paintCanvas != null) paintCanvas.SetPaintVisible(true);
            }
        }

        [ClientRpc]
        private void RpcPlayDeathAnimation()
        {
            if (animator != null)
                animator.SetTrigger("Death");
        }

        [Server]
        public void ServerDisableCollider() => RpcDisableCollider();

        [ClientRpc]
        private void RpcDisableCollider()
        {
            if (paintCanvas != null) paintCanvas.DisableActiveCollider();
        }

        //----------------------------------------------------------------------------------------------------

        //--------------------------------------가시성 로직(모드따라 다름, 판단은 GameModeRule에 위임)------------------------------------------
        private void RefreshVisualVisibilityForLocalViewer()
        {
            if (visualRenderer == null) return;

            // 1순위: 죽었으면(사망 처리든 감염 전환이든) 무조건 숨김. 모드 규칙보다 항상 우선.
            if (_isVisuallyDead)
            {
                visualRenderer.enabled = false;
                return;
            }

            // 로컬 플레이어를 아직 못 찾으면 일단 보이게 (안전한 기본값)
            if (NetworkClient.localPlayer == null) { visualRenderer.enabled = true; return; }

            // 2순위: "보는 사람"이 러너가 아니거나(=시커),
            // 나 자신을 보는 경우면 은닉 규칙 대상이 아님 → 항상 보임
            var localPlayer = NetworkClient.localPlayer.GetComponent<PlayerBase>();
            if (localPlayer == null || localPlayer == (PlayerBase)this || localPlayer.role != PlayerRole.Runner)
            {
                visualRenderer.enabled = true;
                return;
            }

            var round = GameRoundManager.Instance;

            // 3순위: 정산 화면(CheckTime)에는 모드 규칙보다 우선해서 무조건 보임
            if (round != null && round.CurrentTimeState == TimeState.CheckTime)
            {
                visualRenderer.enabled = true;
                return;
            }

            // 4순위(남은 경우 = 러너가 다른 러너의 몸을 보는 상황): 모드 규칙에 위임
            bool canSee = round?.CurrentRule == null || round.CurrentRule.CanSeeRunnerBody(round.CurrentTimeState);
            visualRenderer.enabled = canSee;
        }

        //GameRoundManager.OnTimeStateChanged 구독용 콜백.시간대가 바뀔 때마다 닉네임 몸 다 
        private void HandleTimeStateChangedForVisibility(TimeState state, int remainingSeconds)
        {
            // 좀비 호출 방어: 네트워크 핸들러 안이라 예외가 나면 Mirror가 커넥션을 끊는다.
            if (this == null) return;

            if (state == TimeState.CheckTime && _isDead)
            {
                _isVisuallyDead = false;
         
            }

            RaiseNicknameVisibilityChanged();
            RefreshVisualVisibilityForLocalViewer();
        }
        //----------------------------------------------------------------------------------------------------

        protected override bool TryExitSpectateByEscape()
        {
            if (Spectator == null || !Spectator.IsSpectating) return false;

            // 사망 관전 / 체크타임 관전 중에는 ESC로 풀리지 않도록 유지
            if (_isDeathSpectatorLocked || IsCheckTimeSpectatorLocked) return false;

            Spectator.ExitSpectate();
            _runnerHUD?.SetSpectatorMode(false);
            return true;
        }

        protected override void HandleSpectateInput()
        {
            if (Spectator == null) return;

            // 사망 연출 중에는 관전 입력을 무시해 카메라가 내 몸에 고정되게 합니다.
            if (_isDeathCamActive) return;

            if (Input.ToggleSpectate.WasPressedThisFrame())
            {
                if (Spectator.IsSpectating && !_isDeathSpectatorLocked && !IsCheckTimeSpectatorLocked)
                {
                    Spectator.ExitSpectate();
                    _runnerHUD?.SetSpectatorMode(false);
                }
                else
                {
                    if (PaintController.IsPaintMode)
                        PaintController.TogglePaintMode();

                    Spectator.EnterRunnerMode();
                    _runnerHUD?.SetSpectatorMode(true);
                    _runnerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }
                return;
            }

            base.HandleSpectateInput();
        }

        private void SyncPaintRatioIfNeeded()
        {
            if (paintCanvas == null) return;
            if (Time.time - _lastPaintRatioSyncTime < PaintRatioSyncInterval) return;

            _lastPaintRatioSyncTime = Time.time;
            float ratio = paintCanvas.GetPaintRatio();

            if (!Mathf.Approximately(_paintRatio, ratio))
                CmdUpdatePaintRatio(ratio);
        }

        [Command]
        private void CmdUpdatePaintRatio(float ratio)
        {
            _paintRatio = Mathf.Clamp01(ratio);
        }

        private void RecalculateMoveSpeed()
        {
            float targetSpeed = _baseMoveSpeed;

            if (IsPaintMode)
                targetSpeed *= paintModeSpeedMultiplier;

            moveSpeed = targetSpeed;
        }

        [ClientRpc]
        private void RpcBecomeSpectator()
        {
            RaiseNicknameVisibilityChanged();

            bool isCheckTime = GameRoundManager.Instance != null
                    && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;

            _isVisuallyDead = !isCheckTime;
            if (paintCanvas != null) paintCanvas.DisableActiveCollider();
            RefreshVisualVisibilityForLocalViewer();

            if (isLocalPlayer)
            {
                _isDeathSpectatorLocked = true;
                _isDeathCamActive = false;   // 연출 종료 → 사망 관전으로 인계

                if (PaintController.IsPaintMode)
                    PaintController.TogglePaintMode();

                Spectator?.EnterDeathSpectator();
                _runnerHUD?.SetSpectatorMode(true);
                _runnerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
            }
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            _runnerHUD = await UIManager.Instance.ShowScene<UIInGameHUD_Runner>();
            PaintController.BindHUD(_runnerHUD);
            BindSpectateHUD(_runnerHUD);
        }

        private void TryUseExposeSkill()
        {
            if (_isDead) return;

            if (GameRoundManager.Instance != null && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime) return;

            if (Time.time < _lastExposeTime + exposeCooldown)
                return;

            _lastExposeTime = Time.time;

            if (_runnerHUD != null)
            {
                _runnerHUD.StartExposeSkillCooldown(exposeCooldown);
            }

            CmdExposeLocation();
        }

        [Server]
        public void ServerForceExpose()
        {
            if (_isDead) return;

            RpcPlayExposeSound();

            RevealRandomPaintedPixel();
        }

        [Command]
        private void CmdExposeLocation()
        {
            if (GameRoundManager.Instance != null && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime) return;

            if (Time.time < _lastExposeTimeServer + exposeCooldown - ServerCooldownGrace)
            {
                //Debug.LogWarning($"[Expose] 발동 거부(쿨타임): 경과 {Time.time - _lastExposeTimeServer:F2}s / 요구 {exposeCooldown - ServerCooldownGrace:F2}s");
                return;
            }

            _lastExposeTimeServer = Mathf.Max(Time.time, _lastExposeTimeServer + exposeCooldown);

            RpcPlayExposeSound();

            RevealRandomPaintedPixel();
        }

        // 도발(능동/강제) 시 러너 위치에서 노출 효과음을 재생합니다.
        // 모든 클라이언트에서 러너의 월드 위치를 기준으로 3D 재생되어 주변에 위치가 드러납니다.
        [ClientRpc]
        private void RpcPlayExposeSound()
        {
            if (!ShouldHearTauntSound()) return;
            SoundManager.Instance?.PlaySfxAt(SfxType.Expose, transform.position);
        }
        private bool ShouldHearTauntSound()
        {
            if (isLocalPlayer) return true;   // 본인 스킬 피드백은 유지

            var localIdentity = NetworkClient.localPlayer;
            if (localIdentity == null) return true;

            var localPlayer = localIdentity.GetComponent<PlayerBase>();
            if (localPlayer == null || localPlayer.role != PlayerRole.Runner) return true;

            var round = GameRoundManager.Instance;
            return round?.CurrentRule == null
                || round.CurrentRule.CanHearRunnerTauntSound(round.CurrentTimeState);
        }

        [Server]
        private void RevealRandomPaintedPixel()
        {
            if (paintCanvas == null) return;
            if (ScoreManager.Instance == null) return;
            if (connectionToClient == null) return;

            Color32 color = paintCanvas.GetRandomPaintedColor();
            if (color.a > 0)   // 칠해진 게 있을 때만
                ScoreManager.Instance.SetRevealedColor(connectionToClient.connectionId, color);
        }


        // 사망 상태 복제 시 모든 클라이언트에서 호출됩니다. 생존 인원 HUD 갱신을 트리거합니다.
        private void OnDeadStateChanged(bool oldVal, bool newVal)
        {
            if (InGameManager.Instance != null)
                InGameManager.Instance.NotifyRosterChanged();

            RaiseNicknameVisibilityChanged();
        }

        private void HandlePaintModeChanged(bool isPaint)
        {
            RecalculateMoveSpeed();

            if (isPaint && !_isDeathSpectatorLocked && Spectator != null && Spectator.IsSpectating)
            {
                Spectator.ExitSpectate();
                _runnerHUD?.SetSpectatorMode(false);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (ScoreManager.Instance == null) return;

            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, ScoreManager.Instance.VisionRange);
        }
#endif
    }
}
