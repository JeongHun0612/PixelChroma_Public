using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 메달 위로 빛이 대각선으로 스쳐 지나가는 연출.
// 월드 오브젝트(SpriteRenderer)와 UI(Graphic/Image) 양쪽에 붙을 수 있다.
//
// UI는 CanvasRenderer로 그려져 MaterialPropertyBlock이 통하지 않는다.
// 게다가 Graphic.material은 Renderer.material과 달리 자동 인스턴스화되지 않아서,
// 그대로 쓰면 공유 머티리얼 에셋을 건드려 모든 메달이 함께 반짝인다
// (에디터에서는 .mat 파일 자체가 오염된다).
// 그래서 UI 경로에서는 인스턴스 머티리얼을 따로 만들어 쓴다.
[DisallowMultipleComponent]
public class MedalEffect : MonoBehaviour
{
    [Header("빛 스윕")]
    [SerializeField] private float shineDuration = 0.5f;
    [SerializeField] private float pauseDuration = 2.0f;

    private static readonly int PropShineProgress
        = Shader.PropertyToID("_ShineProgress");

    private const float ShineStart = -0.3f;  // 화면 밖 좌하단
    private const float ShineEnd = 1.3f;  // 화면 밖 우상단

    // 월드 오브젝트 경로
    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;

    // UI 경로
    private Graphic _graphic;
    private Material _uiMaterial;

    private Coroutine _loop;

    private bool HasTarget => _spriteRenderer != null || _uiMaterial != null;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _propertyBlock = new MaterialPropertyBlock();
            WarnIfShaderMissing(_spriteRenderer.sharedMaterial);
            return;
        }

        _graphic = GetComponent<Graphic>();
        if (_graphic == null)
        {
            Debug.LogWarning($"[MedalEffect] {name}: SpriteRenderer도 Graphic도 없어 연출을 끕니다.", this);
            enabled = false;
            return;
        }

        WarnIfShaderMissing(_graphic.material);

        _uiMaterial = new Material(_graphic.material);
        _graphic.material = _uiMaterial;
    }

    private void OnDestroy()
    {
        if (_uiMaterial != null) Destroy(_uiMaterial);
    }

    private void OnEnable()
    {
        if (!HasTarget) return;   // 대상이 없으면 조용히 넘어간다(스폰 경로에서 예외 금지)

        SetShine(ShineStart);
        _loop = StartCoroutine(ShineLoop());
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    private IEnumerator ShineLoop()
    {
        while (true)
        {
            // ── 빛 스윕 (↗ 방향) ──
            float elapsed = 0f;
            while (elapsed < shineDuration)
            {
                elapsed += Time.deltaTime;
                SetShine(Mathf.Lerp(ShineStart, ShineEnd, elapsed / shineDuration));
                yield return null;
            }
            SetShine(ShineStart);

            // ── 대기 ──
            yield return new WaitForSeconds(pauseDuration);
        }
    }

    private void SetShine(float value)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(PropShineProgress, value);
            _spriteRenderer.SetPropertyBlock(_propertyBlock);
            return;
        }

        if (_uiMaterial != null)
            _uiMaterial.SetFloat(PropShineProgress, value);
    }

    private void WarnIfShaderMissing(Material mat)
    {
        if (mat == null || !mat.HasProperty(PropShineProgress))
        {
            Debug.LogWarning(
                $"[MedalEffect] {name}: 머티리얼에 _ShineProgress가 없습니다. " +
                "Medal_Shine 머티리얼이 배정됐는지 확인하세요.", this);
        }
    }
}