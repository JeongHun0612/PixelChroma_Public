using System.Collections;
using UnityEngine;

namespace PixelChroma
{
    // 물총 발사 연출(레이저/스플래시)을 담당하는 순수 클라이언트 컴포넌트입니다.
    // 네트워크 로직이 없으므로 SeekerPlayer의 RpcPlayFireEffect가 실행만 위임합니다.
    public class WaterGunEffects : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("LineRenderer를 가진 레이저 프리팹")]
        [SerializeField] private GameObject laserPrefab;
        [Tooltip("착탄 지점에 생성될 물 스플래시 프리팹")]
        [SerializeField] private GameObject waterSplashPrefab;

        [Header("Timing")]
        [Tooltip("레이저가 초당 뻗어나가는 속도(유닛/초)")]
        [SerializeField] private float laserSpeed = 40f;
        //[Tooltip("레이저 라인의 두께")]
        //[SerializeField] private float laserWidth = 0.1f;
        //[Tooltip("레이저 빔이 뻗어나갈 때 보이는 선분의 길이(유닛). 사거리와 무관하게 항상 이 길이로 고정됩니다.")]
        //[SerializeField] private float laserSegmentLength = 3f;
        [Tooltip("물 스플래시 이펙트가 서서히 사라지는 시간(초)")]
        [SerializeField] private float splashFadeDuration = 0.6f;

        [Header("Splash Punch (착탄 스케일 팝)")]
        [Tooltip("스플래시가 최대로 튀어오르는 배율(프리팹 기준 크기 대비). 1이면 팝 없음")]
        [SerializeField] private float splashPunchScale = 1.3f;
        [Tooltip("0 → overshoot → 기준크기로 팝하는 데 걸리는 시간(초). 페이드보다 짧게")]
        [SerializeField] private float splashPunchDuration = 0.13f;


        [Header("Muzzle Flash")]
        [Tooltip("발사 순간 총구에 잠깐 뜨는 플래시 프리팹(스프라이트 또는 파티클)")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [Tooltip("플래시가 유지되는 시간(초)")]
        [SerializeField] private float muzzleFlashDuration = 0.05f;
        [Tooltip("매 발 크기를 이 범위에서 랜덤(x=최소, y=최대) — 반복돼도 안 지루하게")]
        [SerializeField] private Vector2 muzzleFlashScaleRange = new Vector2(0.85f, 1.15f);
        [Tooltip("매 발 회전을 ±이 각도만큼 랜덤(도)")]
        [SerializeField] private float muzzleFlashRandomRot = 12f;

        public void PlayFireEffect(Transform firePoint, Vector2 targetPos, bool didHit, Vector2 direction, Vector2 hitNormal)
        {
            StartCoroutine(AnimateLaserAndSplash(firePoint, targetPos, didHit, direction, hitNormal));
        }

        private void SpawnMuzzleFlash(Transform firePoint, Vector2 direction)
        {
            if (muzzleFlashPrefab == null || firePoint == null) return;

            // 조준 방향 + 랜덤 회전
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                          + Random.Range(-muzzleFlashRandomRot, muzzleFlashRandomRot);

            // 총구(firePoint)에 부착 → 조준을 따라가고 총 끝에 딱 붙음
            GameObject flash = Instantiate(
                muzzleFlashPrefab, firePoint.position,
                Quaternion.Euler(0f, 0f, angle), firePoint);

            float s = Random.Range(muzzleFlashScaleRange.x, muzzleFlashScaleRange.y);
            flash.transform.localScale = Vector3.one * s;

            Destroy(flash, muzzleFlashDuration);
        }

        private IEnumerator AnimateLaserAndSplash(Transform firePoint, Vector2 targetPos, bool didHit, Vector2 direction, Vector2 hitNormal)
        {
            SpawnMuzzleFlash(firePoint, direction);

            GameObject laserObj = null;

            Vector2 startPos = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            float totalDistance = Vector2.Distance(startPos, targetPos);
            float duration = laserSpeed > 0f ? Mathf.Max(totalDistance / laserSpeed, 0.01f) : 0.01f;

            if (laserPrefab != null)
            {
                float travelAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                laserObj = Instantiate(laserPrefab, startPos, Quaternion.Euler(0, 0, travelAngle));
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float ratio = elapsed / duration;

                if (laserObj != null)
                    laserObj.transform.position = Vector2.Lerp(startPos, targetPos, ratio);

                yield return null;
            }

            if (laserObj != null) Destroy(laserObj);

            if (!didHit) yield break;

            if (waterSplashPrefab != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion splashRotation = Quaternion.Euler(0, 0, angle);   // 스프라이트 기본 방향에 따라 필요시 오프셋 조정

                GameObject splash = Instantiate(waterSplashPrefab, targetPos, splashRotation);

                if (splashPunchScale > 1f && splashPunchDuration > 0f)
                    StartCoroutine(PunchScale(splash.transform, splashPunchScale, splashPunchDuration));

                var splashRenderer = splash.GetComponentInChildren<SpriteRenderer>();

                if (splashRenderer != null)
                    StartCoroutine(FadeOutAndDestroy(splash, splashRenderer, splashFadeDuration));
                else
                    Destroy(splash, splashFadeDuration);
            }
        }

        private IEnumerator FadeOutAndDestroy(GameObject target, SpriteRenderer sr, float duration)
        {
            float t = 0f;
            Color startColor = sr.color;

            while (t < duration)
            {
                t += Time.deltaTime;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t / duration));
                yield return null;
            }

            Destroy(target);
        }

        private IEnumerator PunchScale(Transform target, float overshoot, float duration)
        {
            if (target == null) yield break;

            Vector3 baseScale = target.localScale;   // 프리팹이 지정한 기준 크기
            float e = 0f;

            while (e < duration)
            {
                if (target == null) yield break;     // 페이드가 먼저 파괴했을 때 방어

                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / duration);

                float s;
                if (k < 0.5f)
                {
                    // 전반: 0 → overshoot (빠르게 커짐, ease-out)
                    float p = k / 0.5f;
                    s = Mathf.Lerp(0f, overshoot, 1f - (1f - p) * (1f - p));
                }
                else
                {
                    // 후반: overshoot → 1 (수축해 안착, smoothstep)
                    float p = (k - 0.5f) / 0.5f;
                    s = Mathf.Lerp(overshoot, 1f, p * p * (3f - 2f * p));
                }

                target.localScale = baseScale * s;
                yield return null;
            }

            if (target != null) target.localScale = baseScale;   // 정확히 기준 크기로 안착
        }
    }
}
