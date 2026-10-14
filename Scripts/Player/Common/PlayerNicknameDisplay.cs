using Mirror;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI; 

namespace PixelChroma
{
    public class PlayerNicknameDisplay
    {
        private readonly PlayerBase _owner;
        private readonly OneTextLabel _text;
        private readonly GameObject _root;
        private readonly OneTextLabel _likeCountText;
        private readonly RawImage _nicknameImage; 
        private Texture2D _cachedTexture;

        public PlayerNicknameDisplay(PlayerBase owner, OneTextLabel text, GameObject root,
            OneTextLabel likeCountText, RawImage nicknameImage)
        {
            _owner = owner;
            _text = text;
            _root = root;
            _likeCountText = likeCountText;
            _nicknameImage = nicknameImage;
        }


        public void Refresh()
        {
            bool showPixel = GameSettings.ShowPixelNickname;

            if (_nicknameImage != null)
                _nicknameImage.gameObject.SetActive(showPixel);

            if (_text != null)
            {
                _text.gameObject.SetActive(!showPixel);
                if (!showPixel)
                    _text.text = _owner.playerName;
            }

            RefreshLikeCount();
        }

        public void RefreshPixelNickname(byte[] pngData)
        {
            if (_nicknameImage == null) return;
            if (pngData == null || pngData.Length == 0) return;

            // 텍스처 로드/캐시만 수행
            if (_cachedTexture == null)
            {
                _cachedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            _cachedTexture.LoadImage(pngData);
            _cachedTexture.filterMode = FilterMode.Point;
            _nicknameImage.texture = _cachedTexture;

            // on/off 결정은 Refresh()에 위임
            Refresh();
        }

        public void Cleanup() 
        {
            if (_cachedTexture != null)
                Object.Destroy(_cachedTexture);
        }

        public void RefreshLikeCount()
        {
            if (_likeCountText != null)
                _likeCountText.text = _owner.LikeCount.ToString();
        }

        public void HandlePaintModeChanged(bool isPaintMode)
        {
            if (_root == null) return;
            if (!_owner.isLocalPlayer) return;
            if (isPaintMode) _root.SetActive(false);
            else UpdateVisibility();
        }

        public void UpdateVisibility()
        {
            if (_root == null) return;
            if (NetworkClient.localPlayer == null) return;

            PlayerBase localPlayer = NetworkClient.localPlayer.GetComponent<PlayerBase>();
            if (localPlayer == null) return;

            if (_owner is RunnerPlayer runner && runner.IsDead)
            {
                bool isCheckTime = GameRoundManager.Instance != null
                    && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;
                if (!isCheckTime) { _root.SetActive(false); return; }
            }

            if (_owner == localPlayer)
            {
                _root.SetActive(!_owner.IsPaintMode);
                return;
            }

            if (localPlayer.role == PlayerRole.None || _owner.role == PlayerRole.None)
            {
                _root.SetActive(true);
                return;
            }

            if (GameRoundManager.Instance != null
                && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime)
            {
                _root.SetActive(true);
                return;
            }

            if (localPlayer.role == PlayerRole.Runner && _owner.role == PlayerRole.Runner)
            {
                var round = GameRoundManager.Instance;
                _root.SetActive(round?.CurrentRule == null
                    || round.CurrentRule.CanSeeRunnerNickname(round.CurrentTimeState));
                return;
            }

            _root.SetActive(localPlayer.role == PlayerRole.Runner || _owner.role == PlayerRole.Seeker);
        }
    }
}