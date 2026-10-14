using Mirror;
using PixelChroma.UI;
using System;
using OneText.UGUI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using DG.Tweening;


namespace PixelChroma
{
    public abstract class PlayerBase : NetworkBehaviour, IEscapeHandler
    {
        protected static readonly int MoveX = Animator.StringToHash("MoveX");
        protected static readonly int MoveY = Animator.StringToHash("MoveY");
        protected static readonly int IsMoving = Animator.StringToHash("IsMoving");

        private const int OriginalPPU = 16;

        [Header("Settings")]
        public float moveSpeed = 5f;

        [Header("Form Wheel")]
        [SerializeField] protected RadialMenu formWheel;

        // 역할 중립 + 베이스 계층 참조/설정값은 PlayerCommon(베이스 프리팹에 1회 배선)으로 이관했습니다.
        private PlayerCommon _common;
        protected PlayerCommon Common => _common != null ? _common : (_common = GetComponent<PlayerCommon>());

        // 서브클래스·기존 호출부 호환을 위해 동명 위임 프로퍼티로 노출합니다(읽기 전용, 값은 Common이 소유).
        public Animator animator => Common.Animator;
        public PlayerPaintCanvas paintCanvas => Common.PaintCanvas;

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName = "Player";

        [SyncVar(hook = nameof(OnPixelNicknameChanged))]
        public byte[] pixelNicknamePng;

        [SyncVar(hook = nameof(OnRoleChanged))]
        public PlayerRole role = PlayerRole.None;

        [SyncVar(hook = nameof(OnLikeCountChanged))]
        private int _likeCount;

        protected CinemachineCamera cinemachineCamera;

        private PlayerInputActions _input;
        private PlayerPaintController _paintController;
        private Camera _mainCamera;
        private PixelPerfectCamera _pixelPerfectCamera;
        private Tween _zoomTween;
        private SpectatorController _spectator;
        private LikeInteractionController _likeInteraction;
        private UIInGameHUD _boundSpectateHUD;
        private bool _isChatActive;
        private bool _wasPopupOpen;
        private bool _likedByLocal;
        private bool _likeRequestPending;
        [SyncVar] private int _ownerConnectionId = -1;

        protected static event Action NicknameVisibilityChanged;
        public static event Action OnAnyPlayerNameChanged;
        public static event Action<float> OnLocalSkipHoldProgress;
        public int LikeCount => _likeCount;
        public bool CanReceiveLike => role == PlayerRole.Runner;
        public bool LikedByLocal => _likedByLocal;

        // ── 스킵 투표 ──
        private const float SkipHoldDuration = 1.5f;   // Hold Time과 동일하게 맞출 것
        private float _skipHoldTimer;
        private bool _hasVotedSkip;

        // 분리된 컴포넌트(SeekerWaterGun 등)가 페인트 모드 여부만 좁게 조회할 수 있도록 공개합니다.
        public bool IsPaintMode => _paintController != null && _paintController.IsPaintMode;

        protected PlayerInputActions Input => _input;
        protected PlayerPaintController PaintController => _paintController;
        protected SpectatorController Spectator => _spectator;
        protected Rigidbody2D Rb { get; private set; }
        protected bool IsChatActive => _isChatActive;
        protected bool IsPopupOpen => UIManager.Instance != null && UIManager.Instance.IsPopupOpen;
        protected bool IsGameplayInputBlocked => _isChatActive || IsPopupOpen;
        protected bool IsCheckTimeSpectatorLocked { get; private set; }

        protected virtual UIInGameHUD InGameHUD => null;
        protected virtual bool CanTogglePaintMode => !IsCheckTimeSpectatorLocked;   // PaintMode 진입 가능 여부
        protected virtual bool CanUseFormWheel => true;      // Form(변신) 휠 사용 가능 여부. Seeker 등은 오버라이드로 제한.
        protected virtual bool CanEnterCheckTimeSpectator => true;
        // CheckTime 동안에만 좋아요를 주고받을 수 있습니다. 클라이언트 입력 차단과 서버 검증에 함께 씁니다.
        internal static bool IsLikeAllowedNow =>
            GameRoundManager.Instance != null
            && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
        }

        protected virtual void OnDestroy()
        {
            // OnStopLocalPlayer가 안 불린 채 파괴돼도 좀비 구독이 남지 않도록 백스톱 해제.
            UIChat.OnChatToggled -= HandleChatToggled;
            GameRoundManager.OnTimeStateChanged -= HandleCheckTimeSpectator;
            GameRoundManager.OnTimeStateChanged -= HandleSkipVoteReset;
            UnbindSpectateHUD();   // HUD는 플레이어보다 오래 산다.
        }

        protected virtual void Update()
        {
            if (!isLocalPlayer) return;

            UpdatePopupCursor();
            HandleSkipVoteInput();

            if (IsGameplayInputBlocked)
            {
                _likeInteraction?.Tick(false);
                StopLocalGameplayMotion();
                _paintController?.CancelTransientTool();
                _paintController?.HidePreview();
                return;
            }

            if (CanUseFormWheel && formWheel != null
                && (_spectator == null || !_spectator.IsSpectating))
            {
                if (_input.FormWheel.WasPressedThisFrame())
                    formWheel.Open();

                if (_input.FormWheel.WasReleasedThisFrame())
                {
                    int selected = formWheel.Close();
                    if (selected >= 0)
                    {
                        //if (_paintController.IsPaintMode)
                        //    _paintController.TogglePaintMode();

                        paintCanvas.SwitchForm((PlayerPaintCanvas.PaintFormType)selected);
                    }
                }
            }

            UpdateAnimation();

            if (formWheel != null && formWheel.IsOpen)
            {
                _likeInteraction?.Tick(false);
                _paintController?.CancelTransientTool();
                return;
            }

            _likeInteraction?.Tick(true);

            if (CanTogglePaintMode && _input.TogglePaint.WasPressedThisFrame())
                _paintController.TogglePaintMode();

            HandleScrollWheel();

            if (_paintController.IsPaintMode)
                _paintController.Tick();

        }

        protected virtual void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            if (IsGameplayInputBlocked)
            {
                StopLocalGameplayMotion();
                return;
            }
            //if (formWheel != null && formWheel.IsOpen) return; // 변신중 움직임 금지
            HandleMovement();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (connectionToClient != null)
            {
                _ownerConnectionId = connectionToClient.connectionId;

            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // 스폰 시 인게임 명부에 통지합니다. 어떤 타입을 명부에 올릴지는 InGameManager가 결정합니다.
            if (InGameManager.Instance != null)
                InGameManager.Instance.RegisterPlayer(this);

            Common.InitializeNickname(this);
            NicknameVisibilityChanged += Common.UpdateNicknameVisibility;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (InGameManager.Instance != null)
                InGameManager.Instance.UnregisterPlayer(this);

            NicknameVisibilityChanged -= Common.UpdateNicknameVisibility;
        }

        public override void OnStartLocalPlayer()
        {
            _input = new PlayerInputActions(Instantiate(Common.InputActions));
            _input.EnableAll();

            if (UIManager.Instance != null)
                UIManager.Instance.GameplayEscapeHandler = this;

            _paintController = new PlayerPaintController(this, paintCanvas, _input);
            _paintController.OnPaintModeChanged += HandlePaintModeChangedForZoom;
            _paintController.OnPaintModeChanged += Common.HandlePaintModeChanged;

            UIChat.OnChatToggled += HandleChatToggled;

            SetupCinemachineFollow();
            _likeInteraction = new LikeInteractionController(
                this,
                _mainCamera,
                _input,
                Common.LikeInteractionDistance,
                Common.LikeViewportMargin);
            OnLocalPlayerStarted();

            RaiseNicknameVisibilityChanged();

            string mySavedName = TextNicknameService.LoadOrDefault();
            CmdRegisterMyNameWithManager(mySavedName);

            byte[] pixelPng = PixelNicknameService.Load();
            if (pixelPng != null && pixelPng.Length > 0)
                CmdRegisterPixelNickname(pixelPng);


            CmdRegisterMyLikeCount(LikeService.Load());// 로컬에 누적된 좋아요 카운트를 서버에 전달하여 SyncVar를 초기화한다.
            // 이미 칠해둔 다른 플레이어들의 현재 페인트 상태를 요청(late-join 동기화).
            CmdRequestPaintSnapshots();

            GameRoundManager.OnTimeStateChanged += HandleCheckTimeSpectator;
            GameRoundManager.OnTimeStateChanged += HandleSkipVoteReset;

            if (GameRoundManager.Instance != null
            && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime)
            {
                HandleCheckTimeSpectator(TimeState.CheckTime, GameRoundManager.Instance.CurrentTimer);
            }
        }

        public override void OnStopLocalPlayer()
        {
            // 아래 정리 중 예외가 나도 구독이 남지 않도록 static 이벤트 해제를 최우선 처리.
            UIChat.OnChatToggled -= HandleChatToggled;
            GameRoundManager.OnTimeStateChanged -= HandleCheckTimeSpectator;
            GameRoundManager.OnTimeStateChanged -= HandleSkipVoteReset;

            _input?.DisableAll();

            if (UIManager.Instance != null && ReferenceEquals(UIManager.Instance.GameplayEscapeHandler, this))
                UIManager.Instance.GameplayEscapeHandler = null;

            if (_paintController != null)
            {
                _paintController.OnPaintModeChanged -= HandlePaintModeChangedForZoom;

                // 파괴 타이밍에 Common(GetComponent)이 null일 수 있어 가드.
                var common = Common;
                if (common != null)
                    _paintController.OnPaintModeChanged -= common.HandlePaintModeChanged;
            }

            _paintController?.Dispose();
            _likeInteraction?.Dispose();
            _likeInteraction = null;

            OnLocalPlayerStopped();
        }

        protected virtual void OnLocalPlayerStarted() { }
        protected virtual void OnLocalPlayerStopped() { }

        protected static void RaiseNicknameVisibilityChanged() => NicknameVisibilityChanged?.Invoke();

        // 이동/애니메이션이 참조할 입력값. 자식이 오버라이드해서 입력을 가공할 수 있습니다(예: 프리즈 시 0).
        protected virtual Vector2 GetMoveInput()
        {
            if (_spectator != null && _spectator.IsSpectating) return Vector2.zero;
            return _input.Move.ReadValue<Vector2>();
        }

        protected virtual void HandleMovement()
        {
            if (Rb == null) return;
            if (_spectator != null && _spectator.IsSpectating)
            {
                Rb.linearVelocity = Vector2.zero;
                if (_spectator.Mode == SpectateMode.Free)
                    _spectator.HandleFreeCamMovement(_input.Move.ReadValue<Vector2>());
                return;
            }

            if (this is RunnerPlayer runner && (runner.IsDead || runner.IsKnockedBack)) return;

            Vector2 input = GetMoveInput();
            Rb.linearVelocity = input.sqrMagnitude > 0.01f
                ? input.normalized * moveSpeed
                : Vector2.zero;
        }

        protected virtual void UpdateAnimation()
        {
            if (animator == null) return;

            Vector2 input = GetMoveInput();
            bool moving = input.sqrMagnitude > 0.01f;

            animator.SetBool(IsMoving, moving);
            if (moving)
            {
                animator.SetFloat(MoveX, input.x);
                animator.SetFloat(MoveY, input.y);
            }
        }

        public virtual CursorType GetDefaultCursorType()
        {
            return CursorType.Default;
        }

        public void ApplyDefaultCursor()
        {
            _paintController?.RefreshCursor();
        }

        // ── 스킵 투표 ──────────────────────────────────────────────────

        private bool CanSkipVote()
        {
            if (_hasVotedSkip) return false;
            if (IsGameplayInputBlocked) return false;

            var round = GameRoundManager.Instance;
            if (round == null) return false;

            return round.CurrentTimeState == TimeState.HideTime
                || round.CurrentTimeState == TimeState.CheckTime;
        }

        private void HandleSkipVoteInput()
        {
            // 스킵 불가 상태이거나 이미 투표함 → 게이지 리셋
            if (!CanSkipVote())
            {
                if (_skipHoldTimer > 0f)
                {
                    _skipHoldTimer = 0f;
                    OnLocalSkipHoldProgress?.Invoke(0f);
                }
                return;
            }

            // Y키를 누르고 있는 동안 게이지 진행
            if (_input.SkipVote.IsPressed())
            {
                _skipHoldTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_skipHoldTimer / SkipHoldDuration);
                OnLocalSkipHoldProgress?.Invoke(progress);
            }
            else
            {
                // 손을 뗌 → 게이지 리셋
                if (_skipHoldTimer > 0f)
                {
                    _skipHoldTimer = 0f;
                    OnLocalSkipHoldProgress?.Invoke(0f);
                }
            }

            // Hold 완료 → 투표 전송
            if (_input.SkipVote.WasPerformedThisFrame())
            {
                _hasVotedSkip = true;
                _skipHoldTimer = 0f;
                OnLocalSkipHoldProgress?.Invoke(1f);
                SendSkipVote();
                InGameHUD?.SetSkipVoted();
            }
        }

        private void SendSkipVote()
        {
            NetworkClient.Send(new SkipVoteRequest());
        }

        private void HandleSkipVoteReset(TimeState state, int remainingSeconds)
        {
            _hasVotedSkip = false;
            _skipHoldTimer = 0f;
            OnLocalSkipHoldProgress?.Invoke(0f);
        }

        // ── ESC 처리 ──────────────────────────────────────────────────
        protected virtual bool TryExitSpectateByEscape() => false;
        public bool HandleEscape()
        {
            // 채팅 입력 중엔 게임플레이 취소를 소비만 하고 무시(설정 팝업 오작동 방지)
            if (IsChatActive) return true;

            if (TryExitSpectateByEscape()) return true;   // 1순위: 관전 종료(롤별 잠금 반영)

            if (_paintController != null && _paintController.IsPaintMode)  // 2순위: 페인트 종료
            {
                _paintController.TogglePaintMode();
                return true;
            }

            return false;
        }

        // 팝업(설정창 등) 열림/닫힘 전환 시 커서를 전환한다..
        private void UpdatePopupCursor()
        {
            bool popupOpen = IsPopupOpen;
            if (popupOpen == _wasPopupOpen) return;
            _wasPopupOpen = popupOpen;

            if (CursorManager.Instance == null) return;

            if (popupOpen)
                CursorManager.Instance.SetCursorType(CursorType.Default);
            else
                ApplyDefaultCursor();
        }

        // ── 관전 공용 ──────────────────────────────────────────────────
        protected virtual void HandleSpectateInput()
        {
            if (_spectator == null || !_spectator.IsSpectating) return;

            if (_input.SpectateRunner.WasPressedThisFrame() && TrySetSpectateMode(SpectateMode.Runner))
                return;

            if (_input.SpectateSeeker.WasPressedThisFrame() && TrySetSpectateMode(SpectateMode.Seeker))
                return;

            if (_input.SpectateFree.WasPressedThisFrame() && TrySetSpectateMode(SpectateMode.Free))
                return;

            if (_spectator.Mode == SpectateMode.Runner || _spectator.Mode == SpectateMode.Seeker)
            {
                if (_input.CyclePrev.WasPressedThisFrame()) _spectator.CycleTarget(-1);
                if (_input.CycleNext.WasPressedThisFrame()) _spectator.CycleTarget(1);
            }
        }

        /// <summary>
        /// 관전 모드를 전환합니다. 키 입력과 HUD 버튼 클릭이 모두 이 경로를 사용합니다.
        /// 관전 중이 아니거나 이미 같은 모드면 아무것도 하지 않고 false를 반환합니다.
        /// </summary>
        protected bool TrySetSpectateMode(SpectateMode mode)
        {
            if (_spectator == null || !_spectator.IsSpectating) return false;
            if (_spectator.Mode == mode) return false;

            switch (mode)
            {
                case SpectateMode.Runner: _spectator.EnterRunnerMode(); break;
                case SpectateMode.Seeker: _spectator.EnterSeekerMode(); break;
                case SpectateMode.Free: _spectator.EnterFreeMode(); break;
                default: return false;
            }

            InGameHUD?.SetActiveSpectateMode(mode);
            return true;
        }

        internal bool CanRequestLike(PlayerBase target)
        {
            return IsLikeAllowedNow
                && target != null
                && !IsSameOwner(target)
                && target.CanReceiveLike
                && !target._likedByLocal
                && !target._likeRequestPending;
        }

        internal void TryRequestLike(PlayerBase target)
        {
            if (!CanRequestLike(target))
                return;

            target._likeRequestPending = true;
            target.CmdAddLike();
        }

        private bool IsSameOwner(PlayerBase other)
        {
            if (other == this) return true;
            return _ownerConnectionId >= 0
                && other._ownerConnectionId >= 0
                && _ownerConnectionId == other._ownerConnectionId;
        }

        private void HandleCheckTimeSpectator(TimeState state, int remainingSeconds) // CheckTime 전용
        {
            // 좀비 호출 방어: 네트워크 핸들러 안이라 예외가 나면 Mirror가 커넥션을 끊는다.
            if (this == null || _spectator == null) return;

            if (state == TimeState.CheckTime)
            {
                IsCheckTimeSpectatorLocked = true;

                if (_paintController != null && _paintController.IsPaintMode)
                    _paintController.TogglePaintMode();

                if (!_spectator.IsSpectating && CanEnterCheckTimeSpectator)
                {
                    _spectator.EnterRunnerMode();
                    InGameHUD?.SetSpectatorMode(true);
                    InGameHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }
                else if (_spectator.IsSpectating)
                {
                    // 이미 관전 중이었다면 CheckTime 기준으로 명부가 넓어졌으니 인덱스를 다시 맞춘다.
                    _spectator.RefreshSpectatableTargets();
                }

                OnCheckTimeSpectatorEnter();
            }
            else
            {
                if (IsCheckTimeSpectatorLocked)
                {
                    IsCheckTimeSpectatorLocked = false;
                    OnCheckTimeSpectatorExit();
                }
            }
        }

        protected virtual void OnCheckTimeSpectatorEnter() { }
        protected virtual void OnCheckTimeSpectatorExit() { }

        protected void InitializeSpectator()
        {
            _spectator = new SpectatorController(this, cinemachineCamera, Common.FreeCamSpeed);
            _spectator.OnTargetChanged += HandleSpectateTargetChanged;
        }

        protected void CleanupSpectator()
        {
            UnbindSpectateHUD();

            if (_spectator != null)
                _spectator.OnTargetChanged -= HandleSpectateTargetChanged;

            _spectator?.Dispose();
            _spectator = null;
        }

        // HUD는 비동기로 생성되므로 InitializeSpectator 시점엔 아직 없습니다.
        // PaintController.BindHUD와 같은 자리에서 배선합니다.
        protected void BindSpectateHUD(UIInGameHUD hud)
        {
            UnbindSpectateHUD();
            if (hud == null) return;

            _boundSpectateHUD = hud;
            _boundSpectateHUD.OnSpectateModeSelected += HandleSpectateModeSelected;
        }

        protected void UnbindSpectateHUD()
        {
            if (_boundSpectateHUD == null) return;

            _boundSpectateHUD.OnSpectateModeSelected -= HandleSpectateModeSelected;
            _boundSpectateHUD = null;
        }

        private void HandleSpectateModeSelected(SpectateMode mode)
        {
            // HUD가 플레이어보다 오래 살기 때문에 좀비 호출을 방어합니다.
            if (this == null) return;

            TrySetSpectateMode(mode);
        }

        private void HandleSpectateTargetChanged(PlayerBase target)
        {
            InGameHUD?.SetSpectateTarget(target, target == this);
        }

        // 스폰된 다른 플레이어 캔버스들이 각자의 현재 페인트를 요청자에게 보내도록 한다.
        [Command]
        private void CmdRequestPaintSnapshots()
        {
            var conn = connectionToClient;
            if (conn == null) return;

            foreach (var identity in NetworkServer.spawned.Values)
            {
                if (identity == null || identity == netIdentity) continue;   // 내 캔버스는 빈 상태
                var canvas = identity.GetComponentInChildren<PlayerPaintCanvas>();
                if (canvas != null)
                    canvas.SendPaintSnapshotTo(conn);
            }
        }

        [Command]
        private void CmdRegisterMyNameWithManager(string nameToRegister)
        {
            if (string.IsNullOrEmpty(nameToRegister)) return;

            string trimmed = nameToRegister.Trim();
            NicknameRegistry.Register(connectionToClient.connectionId, trimmed);
            playerName = trimmed;
        }

        [Command]
        private void CmdRegisterPixelNickname(byte[] pngData)
        {
            if (pngData == null || pngData.Length == 0) return;
            NicknameRegistry.RegisterPixel(connectionToClient.connectionId, pngData);
            pixelNicknamePng = pngData;
        }

        [Command(requiresAuthority = false)]
        public void CmdAddLike(NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            bool accepted = IsLikeAllowedNow && CanReceiveLike && _ownerConnectionId >= 0;
            var senderPlayer = sender.identity?.GetComponent<PlayerBase>();
            accepted &= senderPlayer != null
                && senderPlayer != this
                && sender.connectionId != _ownerConnectionId;

            if (accepted)
                accepted = LikeManager.TryAddRecord(sender.connectionId, _ownerConnectionId);

            if (accepted)
            {
                _likeCount++;

                // 실제 좋아요가 성사된 경우에만 연출을 재생시킵니다.
                RpcPlayLikeRise();
            }

            TargetLikeResult(sender, accepted);
        }

        [TargetRpc]
        private void TargetLikeResult(NetworkConnectionToClient targetConnection, bool accepted)
        {
            _likeRequestPending = false;
            if (accepted)
                _likedByLocal = true;
        }

        // CmdAddLike가 성사됐을 때만 서버가 관측자(관전자 포함) 전원에게 연출을 재생시킵니다.
        [ClientRpc]
        private void RpcPlayLikeRise()
        {
            Common.PlayLikeRise(1);
        }

        [Command]
        private void CmdRegisterMyLikeCount(int savedCount)
        {
            _likeCount = Mathf.Max(0, savedCount);
        }

        private void SetupCinemachineFollow()
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCamera != null)
                cinemachineCamera.Follow = transform;

            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _pixelPerfectCamera = _mainCamera.GetComponent<PixelPerfectCamera>();
                if (_pixelPerfectCamera != null)
                    _pixelPerfectCamera.assetsPPU = OriginalPPU;
            }
        }

        private void HandleScrollWheel()
        {
            float scroll = GetScrollValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (IsBrushSizeModifierHeld())
            {
                if (_paintController != null && _paintController.IsPaintMode)
                    _paintController.HandleBrushScroll(scroll);
                return;
            }

            if (_paintController != null && _paintController.IsPaintMode)
                HandleZoom(scroll);
        }

        private void HandleZoom(float scroll)
        {
            if (_pixelPerfectCamera == null) return;

            _zoomTween?.Kill();

            int current = _pixelPerfectCamera.assetsPPU;
            int delta = scroll > 0 ? Common.ZoomStepPPU : -Common.ZoomStepPPU;
            int target = Mathf.Clamp(current + delta, OriginalPPU, Common.MaxPPU);

            _zoomTween = DOTween.To(
                    () => _pixelPerfectCamera.assetsPPU,
                    v => _pixelPerfectCamera.assetsPPU = v,
                    target,
                    Common.ManualZoomTweenDuration)
                .SetEase(Ease.OutQuad);
        }

        private void HandlePaintModeChangedForZoom(bool isPaintMode)
        {
            if (isPaintMode) return;
            StartForcedZoomOut();
        }

        private void StartForcedZoomOut()
        {
            if (_pixelPerfectCamera == null) return;

            _zoomTween?.Kill();   // 이전에 남아있던 트윈(수동 줌이든 이전 줌아웃이든) 취소
            _zoomTween = DOTween.To(
                    () => _pixelPerfectCamera.assetsPPU,
                    v => _pixelPerfectCamera.assetsPPU = v,
                    OriginalPPU,
                    Common.ForcedZoomOutDuration)
                .SetEase(Ease.OutQuad);
        }

        private float GetScrollValue()
        {
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.y.ReadValue();
        }

        private bool IsBrushSizeModifierHeld()
        {
            return _input.BrushSizeModifier.IsPressed();
        }

        private void OnPlayerNameChanged(string oldName, string newName)
        {
            Common.RefreshNickname();
            OnAnyPlayerNameChanged?.Invoke();
        }

        private void OnPixelNicknameChanged(byte[] oldPng, byte[] newPng)
        {
            Common.RefreshPixelNickname(newPng);
            OnAnyPlayerNameChanged?.Invoke();
        }


        private void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
        {
            RaiseNicknameVisibilityChanged();
        }

        private void OnLikeCountChanged(int oldVal, int newVal)
        {
            Common.RefreshLikeCount();

            if (isLocalPlayer && newVal > oldVal)
                LikeService.Save(newVal);
        }

        private void HandleChatToggled(bool isActive)
        {
            if (this == null) return;   // 좀비 호출 방어

            _isChatActive = isActive;

            if (_isChatActive)
            {
                StopLocalGameplayMotion();
                _paintController?.HidePreview();
            }
        }

        private void StopLocalGameplayMotion()
        {
            if (formWheel != null && formWheel.IsOpen)
                formWheel.ForceClose();

            if (Rb != null)
                Rb.linearVelocity = Vector2.zero;

            if (animator != null)
                animator.SetBool(IsMoving, false);
        }

        //---------------------------BackGround로 빠지는 경우-------------------------------

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isLocalPlayer)
            {
                if (formWheel != null && formWheel.IsOpen)
                    formWheel.ForceClose();
            }
        }
    }
}
