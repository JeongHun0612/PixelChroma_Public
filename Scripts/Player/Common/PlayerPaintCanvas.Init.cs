using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    // Start에서 한 번만 실행되는 초기화(원본 픽셀 읽기, 엠보스 법선, 실루엣, 마스크 생성).
    public partial class PlayerPaintCanvas
    {
        private void InitializeAllForms()
        {
            if (forms == null || forms.Length == 0) return;

            var shader = paintShader != null ? paintShader : Shader.Find("PixelChroma/PlayerPaintMask");
            if (shader == null)
            {
                Debug.LogError("PlayerPaintMask 셰이더를 찾을 수 없습니다. paintShader를 인스펙터에 배정하세요.", this);
                return;
            }

            _material = new Material(shader) { name = "PlayerPaintMask (Instance)" };
            targetRenderer.material = _material;
            //PushShadingParams();

            if (targetAnimator != null)
                _baseAnimatorController = targetAnimator.runtimeAnimatorController;

            _formLookup = new Dictionary<PaintFormType, PaintForm>(forms.Length);
            for (int i = 0; i < forms.Length; i++)
            {
                InitializeForm(forms[i]);
                _formLookup[forms[i].formType] = forms[i];
            }

            // 방 옵션(엠보스 하이라이트/쉐도우)을 최초 합성 전에 반영해 둔다.
            PullEmbossFromRoomOption();

            ActivateForm(PaintFormType.Default);
            Recomposite();
        }
        private void InitializeForm(PaintForm form)
        {
            if (form.sprites == null || form.sprites.Length == 0) return;

            int count = form.sprites.Length;
            var first = form.sprites[0];
            form.texWidth = (int)first.rect.width;
            form.texHeight = (int)first.rect.height;
            int pixelCount = form.texWidth * form.texHeight;

            form.spriteIndex = new Dictionary<Sprite, int>(count);
            form.undo = new List<PaintCommand>();
            form.redo = new List<PaintCommand>();

            // ── 1) 원본 픽셀 읽기 (기존과 동일하되, form에 보존) ──────────
            form.allOriginalPixels = new Color[count][];
            Texture2D lastSourceTex = null;
            Texture2D readableCopy = null;

            for (int i = 0; i < count; i++)
            {
                var srcSprite = form.sprites[i];
                var srcTex = srcSprite.texture;
                var rect = srcSprite.rect;

                if (srcTex != lastSourceTex)
                {
                    if (readableCopy != null) Destroy(readableCopy);
                    readableCopy = CreateReadableCopy(srcTex);
                    lastSourceTex = srcTex;
                }

                form.allOriginalPixels[i] = readableCopy.GetPixels(
                    (int)rect.x, (int)rect.y, form.texWidth, form.texHeight);
                form.spriteIndex[srcSprite] = i;
                // form.maskRects[i] = ComputeMaskRect(srcSprite);  ← 삭제
            }

            if (readableCopy != null) Destroy(readableCopy);

            // ── 2) 외곽선 법선 빌드 (form에 보존, 텍스처화 안 함) ────────
            form.allEdgeNormals = BuildEdgeNormals(
                form.allOriginalPixels, form.texWidth, form.texHeight);

            // ── 3) unionOpaque / referenceOpaque (기존과 동일) ───────────
            form.unionOpaque = new bool[pixelCount];
            form.referenceOpaque = new bool[pixelCount];
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < pixelCount; k++)
                {
                    bool opaque = form.allOriginalPixels[i][k].a >= 0.01f;
                    if (opaque) form.unionOpaque[k] = true;
                    if (i == 0) form.referenceOpaque[k] = opaque;
                }
                // embossTextures 생성 코드 전체 삭제
            }

            form.totalOpaquePixels = 0;
            for (int i = 0; i < pixelCount; i++)
                if (form.referenceOpaque[i]) form.totalOpaquePixels++;

            // ── 4) 빈 paint 데이터 + 합성 텍스처 생성 ─────────────────────
            form.maskPixels = new Color32[pixelCount];
            form.paintedCells = 0;
            form.isDirty = false;
            form.paintVisible = true;

            // 현재 프레임 = frame 0으로 초기화
            form.originalPixels = form.allOriginalPixels[0];
            form.edgeNormals = form.allEdgeNormals[0];

            // 합성 결과 버퍼 + 텍스처 + Sprite 생성
            form.compositedPixels = new Color32[pixelCount];
            form.compositedTexture = new Texture2D(
                form.texWidth, form.texHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // Sprite 생성: 원본 sprite와 동일한 pivot, PPU 사용
            Vector2 pivotNormalized = new Vector2(
                first.pivot.x / first.rect.width,
                first.pivot.y / first.rect.height);
            form.compositedSprite = Sprite.Create(
                form.compositedTexture,
                new Rect(0, 0, form.texWidth, form.texHeight),
                pivotNormalized,
                first.pixelsPerUnit);
        }


        // ── 텍스처 읽기 유틸리티 ────────────────────────────────────────────────

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply(false, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        private static Vector4 ComputeMaskRect(Sprite sprite)
        {
            var uv = sprite.uv;
            if (uv == null || uv.Length == 0)
            {
                var tex = sprite.texture;
                var r = sprite.rect;

                return new Vector4(r.x / tex.width, r.y / tex.height,
                    r.width / tex.width, r.height / tex.height);
            }

            Vector2 min = uv[0], max = uv[0];
            for (int i = 1; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            return new Vector4(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        // 실루엣 테두리 픽셀에서 투명한 쪽을 향하는 법선을 계산(BuildNormalPixels가 셰이더용
        // 텍스처로 인코딩).
        private static Vector2[][] BuildEdgeNormals(Color[][] originalPixels, int texWidth, int texHeight)
        {
            int count = originalPixels.Length;
            var edgeNormals = new Vector2[count][];

            // 프레임별 법선은 프리뷰와 공유하는 PaintEmboss.BuildEdgeNormals로 계산한다.
            for (int i = 0; i < count; i++)
                edgeNormals[i] = PaintEmboss.BuildEdgeNormals(originalPixels[i], texWidth, texHeight);

            return edgeNormals;
        }

        /// <summary>
        /// 외곽선 법선을 셰이더용 텍스처 픽셀로 인코딩한다.
        /// rg = normal.xy(0~1로 인코딩), b = 테두리 플래그(테두리 255, 내부/투명 0).
        /// 셰이더에서 edge = (rg*2-1)·빛방향 * b 로 복원한다.
        /// </summary>
        private static Color32[] BuildNormalPixels(Vector2[] normals)
        {
            var pixels = new Color32[normals.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                Vector2 n = normals[i];
                bool isEdge = n.x != 0f || n.y != 0f;
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255);
                pixels[i] = new Color32(r, g, (byte)(isEdge ? 255 : 0), 255);
            }
            return pixels;
        }
    }
}
