using OneText.UGUI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace PixelChroma
{
    /// <summary>
    /// PlayerBase 파생 프리팹(Seeker/Runner/Lobby)이 공통으로 쓰는 "역할 중립 + 베이스 계층" 참조·설정값과,
    /// 그 참조들만으로 동작하는 역할 중립 뷰(닉네임/좋아요 표시 등)를 담는 컴포넌트입니다.
    /// PlayerBase.prefab(베이스)에 한 번만 배선하면 각 변형이 그대로 상속받습니다.
    /// </summary>
    public class PlayerCommon : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerPaintCanvas paintCanvas;

        [Header("Nickname / Like UI")]
        [SerializeField] private GameObject nicknameRoot;
        [SerializeField] private OneTextLabel nicknameText;
        [SerializeField] private RawImage nicknameImage;
        [SerializeField] private OneTextLabel likeCountText;
        [SerializeField] private LikeRiseEffect likeRiseEffect;

        [Header("Like Interaction")]
        [Tooltip("카메라 중심에서 이 거리 이내의 크로마만 좋아요 대상으로 선택합니다.")]
        [SerializeField] private float likeInteractionDistance = 5f;
        [Tooltip("화면 가장자리에서 대상을 제외할 뷰포트 여백입니다.")]
        [SerializeField, Range(0f, 0.49f)] private float likeViewportMargin = 0.05f;

        [Tooltip("스프라이트 상단에서 닉네임까지의 간격 (월드 유닛)")]
        private float nicknameYPadding = 0.25f;

        [Header("Zoom (PPU)")]
        [SerializeField] private int maxPPU = 128;   // 기존 Seeker/Runner/Lobby 프리팹이 공통으로 쓰던 값
        [SerializeField] private int zoomStepPPU = 5;
        [SerializeField] private float manualZoomTweenDuration = 0.15f;
        [Tooltip("페인트 모드에서 빠져나올 때 최대 줌아웃 상태로 돌아가는 데 걸리는 시간(초)")]
        [SerializeField] private float forcedZoomOutDuration = 0.4f;

        [Header("Free Cam Settings")]
        [Tooltip("자유 시점(고정 모드)일 때 카메라의 이동 속도")]
        [SerializeField] private float freeCamSpeed = 15f;

        private PlayerNicknameDisplay _nicknameDisplay;
        public InputActionAsset InputActions => inputActions;
        public Animator Animator => animator;
        public PlayerPaintCanvas PaintCanvas => paintCanvas;

        public int MaxPPU => maxPPU;
        public int ZoomStepPPU => zoomStepPPU;
        public float ManualZoomTweenDuration => manualZoomTweenDuration;
        public float ForcedZoomOutDuration => forcedZoomOutDuration;

        public float FreeCamSpeed => freeCamSpeed;
        public float LikeInteractionDistance => likeInteractionDistance;
        public float LikeViewportMargin => likeViewportMargin;

        private void Awake()
        {
            // 폼 상단 Y가 바뀔 때 닉네임 위치를 맞춥니다. 참조(paintCanvas/nicknameRoot)를 이 컴포넌트가 소유하므로 여기서 관리합니다.
            if (paintCanvas != null)
                paintCanvas.OnFormTopYChanged += AdjustNicknamePosition;

            GameSettings.Changed += OnSettingsChanged;
        }

        private void OnDestroy()
        {
            if (paintCanvas != null)
                paintCanvas.OnFormTopYChanged -= AdjustNicknamePosition;

            GameSettings.Changed -= OnSettingsChanged;
            _nicknameDisplay?.Cleanup();
        }

        // ── 닉네임/좋아요 뷰 (PlayerBase가 네트워크 라이프사이클·SyncVar 훅에서 구동) ──────────────
        // PlayerBase.OnStartClient에서 호출. owner의 네트워크 상태(playerName/role/LikeCount)를 읽어 초기 표시합니다.
        public void InitializeNickname(PlayerBase owner)
        {
            _nicknameDisplay ??= new PlayerNicknameDisplay(
                owner, nicknameText, nicknameRoot, likeCountText, nicknameImage); 
            _nicknameDisplay.Refresh();
            _nicknameDisplay.UpdateVisibility();

            if (owner.pixelNicknamePng != null && owner.pixelNicknamePng.Length > 0)
                _nicknameDisplay.RefreshPixelNickname(owner.pixelNicknamePng);
        }

        public void RefreshNickname() => _nicknameDisplay?.Refresh();
        public void RefreshPixelNickname(byte[] pngData) => _nicknameDisplay?.RefreshPixelNickname(pngData);
        public void RefreshLikeCount() => _nicknameDisplay?.RefreshLikeCount();
        public void UpdateNicknameVisibility() => _nicknameDisplay?.UpdateVisibility();
        public void HandlePaintModeChanged(bool isPaintMode) => _nicknameDisplay?.HandlePaintModeChanged(isPaintMode);

        public void PlayLikeRise(int delta) => likeRiseEffect?.Play(delta);

        private void AdjustNicknamePosition(float formTopY)
        {
            if (nicknameRoot == null) return;

            Vector3 pos = nicknameRoot.transform.localPosition;
            pos.y = formTopY + nicknameYPadding;
            nicknameRoot.transform.localPosition = pos;
        }

        private void OnSettingsChanged()
        {
            _nicknameDisplay?.Refresh();
        }
    }
}
