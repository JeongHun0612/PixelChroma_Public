using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace PixelChroma.UI
{
    /// <summary>
    /// Runner / Seeker HUD가 공통으로 사용하는 UI 참조와 로직을 담는 컴포넌트입니다.
    /// Base 프리팹에 부착해 한 번만 인스펙터 배선하면, 각 Variant(UIInGameHUD_Runner/_Seeker)가
    /// 이 컴포넌트를 그대로 상속받아 재사용합니다. 씬 컴포넌트(UIInGameHUD)가 라이프사이클을 위임합니다.
    /// </summary>
    public class UIInGameHUDCommon : MonoBehaviour
    {
        [Header("Paint UI (Common)")]
        [SerializeField] private GameObject paintModeRoot;
        [SerializeField] private UIColorPicker colorPicker;

        [Header("Game State UI (Common)")]
        [SerializeField] private OneTextLabel stateText;
        [SerializeField] private UIPunchText timerText;
        [SerializeField] private OneTextLabel forcedExposeTimerText;

        [Header("Result UI")]
        [SerializeField] private CanvasGroup resultOverlayGroup;
        [SerializeField] private OneTextLabel gameResultOverlayText;
        [SerializeField] private float resultAppearDuration = 0.35f;
        [SerializeField] private float resultAppearStartScale = 0.5f;
        [SerializeField] private float resultPopOvershoot = 2.5f;
        [SerializeField] private float resultHoldDuration = 2f;
        [SerializeField] private float resultExitDuration = 0.2f;
        [SerializeField] private float resultExitScale = 0f;

        [Header("Score Board")]
        [SerializeField] private UIScoreBoard scoreBoard;

        [Header("Game Mode UI")]
        [SerializeField] private OneTextLabel modeText;
        [SerializeField] private GameModeRegistrySO gameModeRegistry;

        [Header("Player Count UI")]
        [SerializeField] private UIPlayerCountIcons runnerCountIcons;
        [SerializeField] private UIPlayerCountIcons seekerCountIcons;

        [Header("Spectator Mode UI (Common)")]
        [SerializeField] private UISpectatorModePanel spectatorPanel;

        [Header("Catch Log")]
        [SerializeField] private UICatchLog catchLog;

        [Header("Localization")]
        [SerializeField] private HUDTextDataSO textData;

        [Header("Setting Button")]
        [SerializeField] private Button settingButton;

        [Header("Skip Vote")]
        [SerializeField] private GameObject skipVoteRoot;
        [SerializeField] private Button skipButton;
        [SerializeField] private Image skipHoldGauge;
        [SerializeField] private OneTextLabel skipButtonText;
        [SerializeField] private OneTextLabel skipVoteCountText;


        [Header("Transition Band")]
        [SerializeField] private GameObject transitionBandRoot;
        [SerializeField] private RectTransform bandTop;
        [SerializeField] private RectTransform bandBottom;
        [SerializeField] private Image bandTopImage;
        [SerializeField] private Image bandBottomImage;
        [SerializeField] private CanvasGroup bandWordGroup;
        [SerializeField] private OneTextLabel bandText;

        [Header("Transition Band Color")]
        [SerializeField] private Color seekTimeBandColor = new Color(0.85f, 0.27f, 0.18f);
        [SerializeField] private Color checkTimeBandColor = new Color(0.35f, 0.45f, 0.6f);

        [Header("Transition Band Timing")]
        [SerializeField] private float bandInDuration = 0.26f;
        [SerializeField] private float bandOutDuration = 0.30f;
        [Tooltip("밴드가 완전히 들어온 뒤 머무는 시간. 사운드 길이와 무관하게 이 값을 그대로 씁니다.")]
        [SerializeField] private float bandHoldDuration = 0.5f;
        [Tooltip("밴드가 퇴장을 시작한 뒤 결과 팝이 터지기까지의 지연(초). bandOutDuration - (0.45 * resultAppearDuration)")]
        [SerializeField] private float resultRevealOffset = 0.14f;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        // 관전 모드 버튼 클릭. 로컬 플레이어가 구독해 실제 모드 전환을 수행합니다.
        public event Action<SpectateMode> OnSpectateModeSelected;

        private Vector2 resultBaseAnchoredPos;
        private PlayerBase _currentSpectateTarget;
        private bool _currentSpectateIsSelf;
        private PlayerRole? _pendingWinner;

        public void Initialize()
        {
            if (colorPicker != null)
                colorPicker.Initialize();

            if (settingButton != null) settingButton.AddClickListener(OnClickSetting);

            if (resultOverlayGroup != null)
            {
                resultBaseAnchoredPos = ((RectTransform)resultOverlayGroup.transform).anchoredPosition;
                resultOverlayGroup.gameObject.SetActive(false);
            }
            if (scoreBoard != null) scoreBoard.Initialize();

            if (skipButton != null) skipButton.AddClickListener(OnClickSkipVote);
            if (skipVoteRoot != null) skipVoteRoot.SetActive(false);
            if (skipHoldGauge != null) skipHoldGauge.fillAmount = 0f;

            if (spectatorPanel != null)
            {
                // Initialize가 두 번 불려도 중복 구독되지 않도록 해제 후 구독합니다.
                spectatorPanel.OnModeSelected -= HandleSpectateModeSelected;
                spectatorPanel.OnModeSelected += HandleSpectateModeSelected;
            }
        }

        private void HandleSpectateModeSelected(SpectateMode mode)
        {
            OnSpectateModeSelected?.Invoke(mode);
        }

        public void SubscribeEvents()
        {
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
            GameRoundManager.OnTimerTick += HandleTimerTick;
            GameRoundManager.OnWinnerDecided += HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged += HandlePlayerCountChanged;
            GameSettings.Changed += OnSettingsChanged;
            GameRoundManager.OnSkipVoteUpdated += HandleSkipVoteUpdated;
            PlayerBase.OnLocalSkipHoldProgress += HandleSkipHoldProgress;
        }

        public void UnsubscribeEvents()
        {
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
            GameRoundManager.OnTimerTick -= HandleTimerTick;
            GameRoundManager.OnWinnerDecided -= HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged -= HandlePlayerCountChanged;
            GameSettings.Changed -= OnSettingsChanged;
            GameRoundManager.OnSkipVoteUpdated -= HandleSkipVoteUpdated;
            PlayerBase.OnLocalSkipHoldProgress -= HandleSkipHoldProgress;
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(active);

            if (scoreBoard != null) scoreBoard.SetVisible(!active);
        }

        public void ResetHUDState()
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(false);
            if (resultOverlayGroup != null) resultOverlayGroup.gameObject.SetActive(false);
            if (spectatorPanel != null) spectatorPanel.Hide();
            if (catchLog != null) catchLog.ClearAll();
            if (skipVoteRoot != null) skipVoteRoot.SetActive(false);
            if (skipHoldGauge != null) skipHoldGauge.fillAmount = 0f;
            if (skipVoteCountText != null) skipVoteCountText.text = "";

            _pendingWinner = null;

            if (transitionBandRoot != null)
            {
                DOTween.Kill(transitionBandRoot);
                bandTop?.DOKill();
                bandBottom?.DOKill();
                bandWordGroup?.DOKill();
                transitionBandRoot.SetActive(false);
            }
        }

        public void SetSpectatorMode(bool active)
        {
            if (spectatorPanel == null) return;

            if (active) spectatorPanel.Show();
            else spectatorPanel.Hide();
        }

        public void SetActiveSpectateMode(SpectateMode mode)
        {
            if (spectatorPanel != null)
                spectatorPanel.SetActiveMode(mode);
        }

        public void SetSpectateTarget(PlayerBase target, bool isSelf)
        {
            _currentSpectateTarget = target;
            _currentSpectateIsSelf = isSelf;

            if (spectatorPanel == null) return;

            // 대상이 없으면 자유 시점입니다. 표시 문구는 HUD 텍스트 데이터가 가지고 있습니다.
            string displayName = target != null
                ? target.playerName
                : (textData != null ? textData.spectateFreeView.GetLocalizedString() : string.Empty);

            byte[] pixelPng = PixelNicknameHelper.GetPixelData(target);
            spectatorPanel.SetTargetName(displayName, isSelf, pixelPng);
        }

        public void SyncToCurrentState()
        {
            var round = GameRoundManager.Instance;
            if (round == null) return;

            UpdateStateUI(round.CurrentTimeState, round.CurrentTimer);

            if (modeText != null)
            {
                var mode = round.CurrentRule?.Mode ?? GameMode.Basic;
                var data = gameModeRegistry != null ? gameModeRegistry.Find(mode) : null;
                modeText.text = data != null ? data.displayName.GetLocalizedString() : "";
            }

            var inGame = InGameManager.Instance;
            if (inGame != null)
                HandlePlayerCountChanged(inGame.GetAliveRunnerCount(), inGame.GetAliveSeekerCount());
        }

        private void HandlePlayerCountChanged(int aliveRunners, int aliveSeekers)
        {
            if (runnerCountIcons != null) runnerCountIcons.SetCount(aliveRunners);
            if (seekerCountIcons != null) seekerCountIcons.SetCount(aliveSeekers);
        }

        private void UpdateStateUI(TimeState state, int remainingSeconds)
        {
            if (stateText != null)
            {
                stateText.text = state switch
                {
                    TimeState.Waiting => textData.stateWaiting.GetLocalizedString(),
                    TimeState.HideTime => textData.stateHideTime.GetLocalizedString(),
                    TimeState.SeekTime => textData.stateSeekTime.GetLocalizedString(),
                    TimeState.CheckTime => textData.stateCheckTime.GetLocalizedString(),
                    TimeState.GameEnd => textData.stateGameEnd.GetLocalizedString(),
                    _ => ""
                };
            }

            if (forcedExposeTimerText != null)
            {
                var round = GameRoundManager.Instance;
                bool exposeOn = round != null && round.CurrentRoomOption != null
                    && round.CurrentRoomOption.forcedExposeEnabled;
                forcedExposeTimerText.gameObject.SetActive(state == TimeState.SeekTime && exposeOn);
            }

            UpdateTimerText(remainingSeconds);

            // 스킵 투표 UI: HideTime, CheckTime에서만 표시
            if (skipVoteRoot != null)
            {
                bool showSkip = state == TimeState.HideTime || state == TimeState.CheckTime;
                skipVoteRoot.SetActive(showSkip);

                // 상태 전환 시 UI 초기화
                if (showSkip)
                {
                    if (skipButton != null) skipButton.interactable = true;
                    if (skipButtonText != null) skipButtonText.text = textData.skipVoteLabel.GetLocalizedString();
                    if (skipHoldGauge != null) skipHoldGauge.fillAmount = 0f;

                    var round = GameRoundManager.Instance;
                    if (skipVoteCountText != null && round != null)
                        skipVoteCountText.text = $"(0/{round.RequiredSkipVotes})";
                }
            }
        }

        private void ShowWinnerOverlay(PlayerRole winner)
        {
            if (resultOverlayGroup == null || gameResultOverlayText == null) return;

            gameResultOverlayText.text = winner == PlayerRole.Seeker
                ? textData.resultSeekerWin.GetLocalizedString()
                : textData.resultRunnerWin.GetLocalizedString();

            var rect = (RectTransform)resultOverlayGroup.transform;

            resultOverlayGroup.DOKill();
            rect.DOKill();

            resultOverlayGroup.gameObject.SetActive(true);
            resultOverlayGroup.alpha = 1f;
            rect.anchoredPosition = resultBaseAnchoredPos;
            rect.localScale = Vector3.one * resultAppearStartScale;

            SoundManager.Instance?.PlaySfx(SfxType.ResultAppear);

            DOTween.Sequence().SetTarget(resultOverlayGroup)
                .Append(rect.DOScale(Vector3.one, resultAppearDuration)
                    .SetEase(Ease.OutBack, resultPopOvershoot))
                .AppendInterval(resultHoldDuration)
                .Append(rect.DOScale(Vector3.one * resultExitScale, resultExitDuration)
                    .SetEase(Ease.InBack))
                .OnComplete(() => resultOverlayGroup.gameObject.SetActive(false));
        }

        private void UpdateTimerText(int remainingSeconds)
        {
            if (timerText != null)
                timerText.SetText(Mathf.Max(0, remainingSeconds).ToString());
        }

        private void HandleTimeStateChanged(TimeState state, int remainingSeconds)
        {
            UpdateStateUI(state, remainingSeconds);
            ShowTransitionBand(state);
        }

        

        private void HandleTimerTick(int remainingSeconds)
        {
            UpdateTimerText(remainingSeconds);

            if (forcedExposeTimerText != null)
            {
                var round = GameRoundManager.Instance;
                forcedExposeTimerText.text = round != null ? round.SecondsUntilNextForcedExpose.ToString() : "";
            }
        }

        private void HandleWinnerDecided(PlayerRole winner)
        {
            _pendingWinner = winner;
        }

        private void FlushPendingWinner()
        {
            if (_pendingWinner == null) return;

            var winner = _pendingWinner.Value;
            _pendingWinner = null;
            ShowWinnerOverlay(winner);
        }
        private void OnSettingsChanged()
        {
            if (spectatorPanel != null && spectatorPanel.gameObject.activeInHierarchy)
                SetSpectateTarget(_currentSpectateTarget, _currentSpectateIsSelf);
        }

        // --------------------- 클릭 이벤트-----------------------------------
        private void OnClickSetting()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }

        // --------------------- 스킵 투표 -----------------------------------

        private void HandleSkipVoteUpdated(int currentVotes, int requiredVotes)
        {
            if (skipVoteCountText != null)
                skipVoteCountText.text = $"({currentVotes}/{requiredVotes})";
        }

        private void HandleSkipHoldProgress(float progress)
        {
            if (skipHoldGauge != null)
                skipHoldGauge.fillAmount = progress;
        }

        public void SetSkipVoted()
        {
            if (skipButton != null) skipButton.interactable = false;
            if (skipButtonText != null) skipButtonText.text = textData.skipVoteVotedLabel.GetLocalizedString();
            if (skipHoldGauge != null) skipHoldGauge.fillAmount = 1f;
        }

        private void OnClickSkipVote()
        {
            NetworkClient.Send(new SkipVoteRequest());
            SetSkipVoted();  // 공용 메서드 호출
        }

        //----------------------------------------TimeStateChange 연출-----------------------------------

        private void ShowTransitionBand(TimeState state)
        {
            if (transitionBandRoot == null || bandTop == null || bandBottom == null)
            {
                FlushPendingWinner();
                return;
            }
            if (!TryGetBandContent(state, out string message, out Color color))
            {
                FlushPendingWinner();
                return;
            }

            bandText.text = message;
            if (bandTopImage != null) bandTopImage.color = color;
            if (bandBottomImage != null) bandBottomImage.color = color;

            // 진행 중인 연출을 끊고 시작 상태로 되돌립니다(연속 전환 시 잔상 방지).
            DOTween.Kill(transitionBandRoot);
            bandTop.DOKill();
            bandBottom.DOKill();
            bandWordGroup.DOKill();
            transitionBandRoot.SetActive(true);

            float enterX = bandTop.rect.width;   // 밴드 자신의 폭 = 화면 밖 시작점. 해상도에 안전.
            bandTop.anchoredPosition = new Vector2(-enterX, bandTop.anchoredPosition.y);
            bandBottom.anchoredPosition = new Vector2(enterX, bandBottom.anchoredPosition.y);
            bandWordGroup.alpha = 0f;

            float revealAt = bandInDuration + bandHoldDuration + resultRevealOffset;

            DOTween.Sequence().SetTarget(transitionBandRoot)
                .Append(bandTop.DOAnchorPosX(0f, bandInDuration).SetEase(Ease.OutCubic))
                .Join(bandBottom.DOAnchorPosX(0f, bandInDuration).SetEase(Ease.OutCubic))
                .Join(bandWordGroup.DOFade(1f, bandInDuration * 0.8f))
                .AppendInterval(bandHoldDuration)
                .Append(bandTop.DOAnchorPosX(enterX, bandOutDuration).SetEase(Ease.InCubic))
                .Join(bandBottom.DOAnchorPosX(-enterX, bandOutDuration).SetEase(Ease.InCubic))
                .Join(bandWordGroup.DOFade(0f, bandOutDuration * 0.7f))
                .InsertCallback(revealAt, FlushPendingWinner)
                .OnComplete(() => transitionBandRoot.SetActive(false));
        }

        private bool TryGetBandContent(TimeState state, out string message, out Color color)
        {
            message = string.Empty;
            color = seekTimeBandColor;

            switch (state)
            {
                case TimeState.SeekTime:
                    message = textData.transitionSeekTime.GetLocalizedString();
                    color = seekTimeBandColor;
                    return true;

                case TimeState.CheckTime:
                    // 승자가 술래면 전원 사망, 러너면 시간 종료로 끝난 라운드입니다.
                    // WinnerDecidedMessage가 TimeStateMessage보다 먼저 도착하므로 여기서 이미 확정입니다.
                    bool wipeout = GameRoundManager.Instance != null
                        && GameRoundManager.Instance.CurrentWinner == PlayerRole.Seeker;
                    message = wipeout
                        ? textData.transitionAllCaught.GetLocalizedString()
                        : textData.transitionTimeUp.GetLocalizedString();
                    color = checkTimeBandColor;
                    return true;

                default:
                    return false;
            }
        }


    }
}
