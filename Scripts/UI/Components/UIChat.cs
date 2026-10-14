using Mirror;
using PixelChroma.Network;
using System;
using System.Collections.Generic;
using OneText.UGUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PixelChroma.UI
{
    public class UIChat : UIPanel, IEscapeHandler
    {
        private const string ActionMapName = "PlayerControll";
        private const string ActionChatToggle = "ChatToggle";

        [Header("Chat Settings")]
        [SerializeField] private int maxMessages = 50;

        private InputAction _chatToggleAction;

        // 각 메시지가 표시된 뒤 사라지기 시작하는 시간(초)
        [SerializeField] private float messageDisplayDuration = 5f;
        // 페이드아웃에 걸리는 시간(초)
        [SerializeField] private float messageFadeDuration = 1f;

        [Header("Panel Fade")]
        [Tooltip("마지막 메시지 수신 후 패널이 보이는 시간(초)")]
        [SerializeField] private float panelDisplayDuration = 5f;
        [Tooltip("패널 페이드아웃에 걸리는 시간(초)")]
        [SerializeField] private float panelFadeDuration = 1f;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Chat Colors")]
        // 본인 채팅 색상 (주황)
        [SerializeField] private Color myMessageColor = new Color(1f, 0.6f, 0.2f);
        // 타인 채팅 색상 (파랑)
        [SerializeField] private Color otherMessageColor = new Color(0.4f, 0.7f, 1f);

        [Header("Chat UI Elements")]
        [SerializeField] private Image panelBackground;
        [SerializeField] private CanvasGroup chatCanvasGroup;    // 채팅창 전체 투명도 제어용
        [SerializeField] private Button openChatButton;          // InputField 활성화 버튼
        [SerializeField] private OneTextInputField chatInputField;
        [SerializeField] private Transform chatContentParent;
        [SerializeField] private GameObject chatTextPrefab;
        [SerializeField] private ScrollRect scrollRect;

        // InputField 활성/비활성 이벤트 (PlayerBase의 입력 차단용)
        public static event Action<bool> OnChatToggled;

        private readonly Queue<ChatMessage> _activeMessages = new();
        private readonly Queue<UIChatEntry> _inactivePool = new();

        // InputField가 현재 포커스 중인지
        private bool _isInputActive;
        // ActivateInputField 후 실제 포커스가 잡힐 때까지 대기 중인지
        private bool _waitingForFocus;
        // 제출로 닫힌 프레임 기록: 같은 엔터 입력이 창을 다시 여는 것 방지
        private int _lastCloseFrame = -1;


        private float _lastMessageTime = -100f;
        private float _bgBaseAlpha;
        // ── 메시지 항목: TMP_Text + 타이머 정보를 묶어서 관리 ──
        private struct ChatMessage
        {
            public UIChatEntry entry;
            public float spawnTime;
            public bool isFading;
        }

        private void OnEnable()
        {
            // 변경: 시그니처에 netId 추가
            PlayerChat.OnMessageReceived += HandleNewMessage;
            chatInputField.onSubmit.AddListener(OnChatSubmit);

            if (openChatButton != null)
                openChatButton.AddClickListener(() => ToggleInput(!_isInputActive));

            SceneManager.sceneLoaded += OnSceneLoaded;

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(ActionMapName);
                _chatToggleAction = map?.FindAction(ActionChatToggle);
                _chatToggleAction?.Enable();
            }

            // ESC는 UIManager가 유일 리더로서 최우선 처리하도록 위임한다.
            if (UIManager.Instance != null)
                UIManager.Instance.ChatEscapeHandler = this;
        }

        private void OnDisable()
        {
            PlayerChat.OnMessageReceived -= HandleNewMessage;
            chatInputField.onSubmit.RemoveListener(OnChatSubmit);

            if (openChatButton != null)
                openChatButton.onClick.RemoveAllListeners();

            _chatToggleAction?.Disable();

            if (UIManager.Instance != null && ReferenceEquals(UIManager.Instance.ChatEscapeHandler, this))
                UIManager.Instance.ChatEscapeHandler = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            // 채팅창은 항상 활성, InputField만 비활성 상태로 시작
            chatInputField.gameObject.SetActive(false);
            _isInputActive = false;
            SetChatRaycastBlocking(false);

            // 기존 초기화 코드 위치에 추가
            if (panelBackground != null)
            {
                _bgBaseAlpha = panelBackground.color.a;   // Inspector의 170/255 ≈ 0.667
                SetBgAlpha(0f);                           // 시작 시 투명
            }
        }

        private void Update()
        {
            HandleInputToggle();
            HandleFocusLost();
            UpdateMessageFade();
            UpdateChatVisibility();
            UpdateChatRaycastBlocking();
        }

        // ── T키 처리: InputField 포커스만 제어
        private void HandleInputToggle()
        {
            if (_chatToggleAction != null && _chatToggleAction.WasPressedThisFrame())
            {
                // 이번 프레임에 제출로 닫혔다면, 그 엔터 입력으로 다시 열리는 것 방지
                if (Time.frameCount == _lastCloseFrame) return;
                if (chatInputField.isFocused) return;
                if (IsTypingElsewhere()) return;
                ToggleInput(!_isInputActive);
            }
        }

        // InputAction은 UI 포커스와 무관하게 발동하므로, 다른 입력창에 타이핑 중인지 직접 걸러낸다.
        private bool IsTypingElsewhere()
        {
            var selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            // EventSystem은 선택된 오브젝트가 비활성이 되어도 선택을 비우지 않는다.
            if (selected == null || !selected.activeInHierarchy) return false;
            if (selected == chatInputField.gameObject) return false;

            return selected.GetComponent<OneTextInputField>() != null
                || selected.GetComponent<TMP_InputField>() != null;
        }

        // ── ESC 처리: 입력창이 열려 있으면 닫고 소비.
        public bool HandleEscape()
        {
            if (!_isInputActive) return false;

            ToggleInput(false);
            return true;
        }

        // ── 마우스 클릭 등으로 InputField 포커스를 잃었을 때 비활성화 ──
        private void HandleFocusLost()
        {
            if (!_isInputActive) return;

            // 아직 포커스가 잡히기 전이면 잡힐 때까지 판정 보류
            if (_waitingForFocus)
            {
                if (chatInputField.isFocused)
                    _waitingForFocus = false;
                return;
            }

            if (!chatInputField.isFocused)
                ToggleInput(false);
        }

        // ── 각 메시지의 페이드아웃 처리 ──
        private void UpdateMessageFade()
        {
            float now = Time.time;

            while (_activeMessages.Count > 0)
            {
                var msg = _activeMessages.Peek();

                float elapsed = now - msg.spawnTime;
                float fadeStart = messageDisplayDuration;
                float fadeEnd = messageDisplayDuration + messageFadeDuration;

                if (elapsed >= fadeEnd)
                {
                    _activeMessages.Dequeue();
                    msg.entry.gameObject.SetActive(false);
                    msg.entry.ResetAlpha();
                    _inactivePool.Enqueue(msg.entry);
                }
                else
                {
                    if (elapsed >= fadeStart)
                    {
                        float t = (elapsed - fadeStart) / messageFadeDuration;
                        msg.entry.CanvasGroup.alpha = 1f - t;
                    }
                    break;
                }
            }

            // 큐 중간 항목 페이드
            var array = _activeMessages.ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                float elapsed = now - array[i].spawnTime;
                if (elapsed >= messageDisplayDuration)
                {
                    float t = Mathf.Clamp01((elapsed - messageDisplayDuration) / messageFadeDuration);
                    array[i].entry.CanvasGroup.alpha = 1f - t;
                }
            }
        }
        // ── 채팅창 전체 투명도 제어 ──
        private void UpdateChatVisibility()
        {
            if (panelBackground == null) return;

            if (_isInputActive)
            {
                SetBgAlpha(1f);
                return;
            }

            float elapsed = Time.time - _lastMessageTime;

            if (elapsed < panelDisplayDuration)
            {
                SetBgAlpha(1f);
            }
            else if (elapsed < panelDisplayDuration + panelFadeDuration)
            {
                float t = (elapsed - panelDisplayDuration) / panelFadeDuration;
                SetBgAlpha(1f - t);
            }
            else
            {
                SetBgAlpha(0f);
            }
        }

        private void SetBgAlpha(float ratio)
        {
            if (panelBackground == null) return;
            var c = panelBackground.color;
            c.a = _bgBaseAlpha * ratio;
            panelBackground.color = c;
        }

        private void SetChatRaycastBlocking(bool block)
        {
            if (chatCanvasGroup == null) return;
            chatCanvasGroup.blocksRaycasts = block;
            chatCanvasGroup.interactable = block;
        }

        private void UpdateChatRaycastBlocking()
        {
            SetChatRaycastBlocking(_isInputActive || _activeMessages.Count > 0);
        }

        // ── InputField 토글 (기존 ToggleChat 대체) ──
        public void ToggleInput(bool open)
        {
            _isInputActive = open;
            chatInputField.gameObject.SetActive(open);

            if (open)
            {
                chatInputField.text = string.Empty;
                chatInputField.Select();
                chatInputField.ActivateInputField();
                _waitingForFocus = true;

                // InputField 열릴 때 기존 메시지들을 다시 보이게 (페이드 리셋)
                RefreshAllMessageAlpha();

                // 패널 배경 즉시 보이게
                SetBgAlpha(1f);
            }
            else
            {
                chatInputField.DeactivateInputField();
                _waitingForFocus = false;
                _lastCloseFrame = Time.frameCount;
                _lastMessageTime = Time.time;
            }

            // PlayerBase에 입력 차단 통지 (기존 연동 유지)
            OnChatToggled?.Invoke(open);
        }

        // InputField 열릴 때 모든 메시지를 불투명하게 복원
        private void RefreshAllMessageAlpha()
        {
            foreach (var msg in _activeMessages)
            {
                msg.entry.ResetAlpha();
            }
        }

        // ── 새 메시지 수신 ──
        private void HandleNewMessage(uint senderNetId, string sender, string message)
        {
            UIChatEntry chatEntry;

            // 풀에서 가져오거나 새로 생성
            if (_activeMessages.Count >= maxMessages)
            {
                var oldest = _activeMessages.Dequeue();
                chatEntry = oldest.entry;
            }
            else if (_inactivePool.Count > 0)
            {
                chatEntry = _inactivePool.Dequeue();
            }
            else
            {
                GameObject newChat = Instantiate(chatTextPrefab, chatContentParent);
                chatEntry = newChat.GetComponent<UIChatEntry>();
            }

            chatEntry.gameObject.SetActive(true);
            chatEntry.transform.SetAsLastSibling();

            // 본인 여부 판별 → 색상
            bool isMine = NetworkClient.localPlayer != null
                          && NetworkClient.localPlayer.netId == senderNetId;
            string colorHex = ColorUtility.ToHtmlStringRGB(isMine ? myMessageColor : otherMessageColor);

            // ★ 픽셀 닉네임 데이터 조회
            var senderPlayer = PixelNicknameHelper.FindPlayer(senderNetId);
            byte[] pixelPng = PixelNicknameHelper.GetPixelData(senderPlayer);

            chatEntry.Setup(sender, message, colorHex, pixelPng);
            chatEntry.ResetAlpha();

            // 활성 큐에 등록
            _activeMessages.Enqueue(new ChatMessage
            {
                entry = chatEntry,
                spawnTime = Time.time,
                isFading = false,
            });
            UpdateChatRaycastBlocking();

            _lastMessageTime = Time.time;
            SetBgAlpha(0.7f);

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        // ── 메시지 전송 ──
        // ── 메시지 전송 ──
        private void OnChatSubmit(string message)
        {
            PlayerChat localPlayerChat = NetworkClient.localPlayer?.GetComponent<PlayerChat>();

            if (localPlayerChat != null && !string.IsNullOrWhiteSpace(message))
                localPlayerChat.SendChat(message);

            chatInputField.text = string.Empty;

            // 엔터로 전송한 뒤에는 채팅창에서 빠져나온다
            ToggleInput(false);
        }

        // ── 씬 전환 시 정리 ──
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearChat();
        }

        public void ClearChat()
        {
            while (_activeMessages.Count > 0)
            {
                var msg = _activeMessages.Dequeue();
                msg.entry.gameObject.SetActive(false);
                msg.entry.ResetAlpha();
                _inactivePool.Enqueue(msg.entry);
            }

            if (chatInputField != null) chatInputField.text = string.Empty;
            UpdateChatRaycastBlocking();
        }
    }
}
