using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 엠보스 슬라이더 값이 실제로 캐릭터에 얼마나 적용되는지 보여주는 프리뷰.
    // 캐릭터를 배경(paintColor)과 같은 색으로 칠했을 때, 외곽선 엠보스만으로
    // 실루엣이 얼마나 살아남는지("배경 동화" 정도)를 눈으로 확인하는 용도.
    //
    // 실제 인게임 결과와 어긋나지 않도록 합성은 PlayerPaintCanvas.Recomposite와
    // 동일한 PaintEmboss 수식을 재사용한다. lightAngle/shadingStrength는 RoomOption에
    // 없는 값이므로, 반드시 실제 PlayerPaintCanvas 프리팹의 인스펙터 값과 맞춰야 한다.
    public class UIEmbossPreview : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("기본 폼 스프라이트를 인스펙터에 직접 지정. (Read/Write 미설정이어도 blit으로 읽음)")]
        [SerializeField] private Sprite previewSprite;

        [Tooltip("캐릭터 채움색. 배경과 같은 색으로 두면 '동화' 정도를 확인할 수 있다.")]
        [SerializeField] private Color paintColor = Color.white;

        [Header("Targets")]
        [Tooltip("합성 결과(캐릭터)를 표시할 Image.")]
        [SerializeField] private Image targetImage;

        [Tooltip("선택: 배경 Image. 지정하면 paintColor로 칠해 캐릭터와 같은 색 배경을 만든다.")]
        [SerializeField] private Image backgroundImage;

        [Header("Shading (실제 PlayerPaintCanvas 값과 일치시킬 것)")]
        [Range(0f, 360f)]
        [SerializeField] private float lightAngle = 135f;
        [Range(0f, 1f)]
        [SerializeField] private float shadingStrength = 1f;

        // 캐시 (Initialize 1회 계산)
        private Color[] _pixels;      // previewSprite 원본 픽셀
        private Vector2[] _normals;   // 외곽선 법선
        private Color32[] _output;    // 합성 버퍼
        private Texture2D _texture;
        private Sprite _sprite;
        private int _width;
        private int _height;

        private float _highlight;
        private float _shadow;
        private bool _ready;
        private bool _initialized;

        public Color PaintColor => paintColor;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _ready = false;

            if (previewSprite == null)
            {
                Debug.LogWarning("[UIEmbossPreview] previewSprite가 비어 있습니다.", this);
                return;
            }

            var rect = previewSprite.rect;
            _width = (int)rect.width;
            _height = (int)rect.height;
            int count = _width * _height;

            _pixels = ReadSpritePixels(previewSprite);
            if (_pixels == null || _pixels.Length != count)
            {
                Debug.LogWarning("[UIEmbossPreview] 스프라이트 픽셀 읽기 실패.", this);
                return;
            }

            _normals = PaintEmboss.BuildEdgeNormals(_pixels, _width, _height);
            _output = new Color32[count];

            _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Vector2 pivotNormalized = new Vector2(
                previewSprite.pivot.x / rect.width,
                previewSprite.pivot.y / rect.height);
            _sprite = Sprite.Create(
                _texture,
                new Rect(0, 0, _width, _height),
                pivotNormalized,
                previewSprite.pixelsPerUnit);

            if (targetImage != null)
            {
                targetImage.sprite = _sprite;
                targetImage.preserveAspect = true;
            }

            ApplyBackgroundColor();

            _ready = true;
            Rebuild();
        }

        // UIEmbossSetting.OnEmbossChanged 구독 지점. (highlight, shadow)는 0~1.
        public void SetEmboss(float highlight, float shadow)
        {
            _highlight = Mathf.Clamp01(highlight);
            _shadow = Mathf.Clamp01(shadow);
            Rebuild();
        }

        public void SetPaintColor(Color color)
        {
            paintColor = color;
            ApplyBackgroundColor();
            Rebuild();
        }

        private void Rebuild()
        {
            if (!_ready) return;

            Vector2 ld = PaintEmboss.LightDir(lightAngle);
            int count = _width * _height;

            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Clamp01(_pixels[i].a);
                if (a < 0.01f)
                {
                    _output[i] = new Color32(0, 0, 0, 0);   // 투명 실루엣 밖
                    continue;
                }

                // 전체를 paintColor로 칠한 상태 가정 + 외곽선 엠보스 적용
                float r = paintColor.r;
                float g = paintColor.g;
                float b = paintColor.b;
                PaintEmboss.Shade(ref r, ref g, ref b, _normals[i], ld,
                    _highlight, _shadow, shadingStrength);

                _output[i] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f),
                    (byte)Mathf.RoundToInt(a * 255f));
            }

            _texture.SetPixels32(_output);
            _texture.Apply();

            if (targetImage != null) targetImage.SetAllDirty();
        }

        private void ApplyBackgroundColor()
        {
            if (backgroundImage != null) backgroundImage.color = paintColor;
        }

        // Read/Write 설정과 무관하게 스프라이트 텍스처를 읽기 위해 RenderTexture로 blit 후 ReadPixels.
        private static Color[] ReadSpritePixels(Sprite sprite)
        {
            var srcTex = sprite.texture;
            var rect = sprite.rect;

            var rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(srcTex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);
            readable.Apply(false, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = readable.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            Destroy(readable);
            return pixels;
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
            if (_sprite != null) Destroy(_sprite);
        }
    }
}
