using UnityEngine;

namespace PixelChroma
{
    /// <summary>
    /// 체크타임에 현재 카메라 근처의 크로마를 찾아 좋아요 상호작용을 제공합니다.
    /// 관전 상태를 직접 참조하지 않으므로 일반 이동/대상 관전/자유 시점에서 같은 규칙을 사용합니다.
    /// </summary>
    public sealed class LikeInteractionController
    {
        private readonly PlayerBase _viewer;
        private readonly Camera _camera;
        private readonly PlayerInputActions _input;
        private readonly float _maxDistanceSqr;
        private readonly float _viewportMargin;
        private RunnerPlayer _currentTarget;

        public LikeInteractionController(
            PlayerBase viewer,
            Camera camera,
            PlayerInputActions input,
            float maxDistance,
            float viewportMargin)
        {
            _viewer = viewer;
            _camera = camera;
            _input = input;
            _maxDistanceSqr = maxDistance * maxDistance;
            _viewportMargin = Mathf.Clamp(viewportMargin, 0f, 0.49f);
        }

        public void Tick(bool inputEnabled)
        {
            RunnerPlayer nextTarget = inputEnabled ? FindBestTarget() : null;
            SetTarget(nextTarget);

            if (_currentTarget != null && _input.Like.WasPressedThisFrame())
                _viewer.TryRequestLike(_currentTarget);
        }

        public void Dispose()
        {
            SetTarget(null);
        }

        private RunnerPlayer FindBestTarget()
        {
            if (!PlayerBase.IsLikeAllowedNow || _camera == null)
                return null;

            var runners = InGameManager.Instance?.Runners;
            if (runners == null)
                return null;

            Vector2 cameraPosition = _camera.transform.position;
            RunnerPlayer bestTarget = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < runners.Count; i++)
            {
                var candidate = runners[i];
                if (!_viewer.CanRequestLike(candidate))
                    continue;

                float distanceSqr = ((Vector2)candidate.transform.position - cameraPosition).sqrMagnitude;
                if (distanceSqr > _maxDistanceSqr)
                    continue;

                Vector3 viewport = _camera.WorldToViewportPoint(candidate.transform.position);
                if (viewport.z <= 0f
                    || viewport.x < _viewportMargin || viewport.x > 1f - _viewportMargin
                    || viewport.y < _viewportMargin || viewport.y > 1f - _viewportMargin)
                    continue;

                // 겹쳐 있는 대상 중 화면 중앙에 가까운 크로마를 우선하고, 동률이면 가까운 대상을 선택합니다.
                float centerScore = ((Vector2)viewport - new Vector2(0.5f, 0.5f)).sqrMagnitude;
                float score = centerScore + distanceSqr * 0.0001f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestTarget = candidate;
            }

            return bestTarget;
        }

        private void SetTarget(RunnerPlayer target)
        {
            if (_currentTarget == target)
                return;

            if (_currentTarget != null)
                _currentTarget.HideWorldLikeGuide();

            _currentTarget = target;

            if (_currentTarget != null)
                _currentTarget.ShowWorldLikeGuide();
        }
    }
}
