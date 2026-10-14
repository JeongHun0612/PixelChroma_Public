#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PixelChroma.EditorTools
{
    /// <summary>
    /// <see cref="LocalizationCharSetWindow"/>가 뽑은 코드포인트 집합을 TMP 정적 폰트 에셋으로 굽는다.
    /// Font Asset Creator 창을 손으로 여는 대신, 같은 결과물(SDF 아틀라스 .asset)을 코드로 생성한다.
    ///
    /// TMP의 고수준 API(<see cref="TMP_FontAsset.CreateFontAsset"/> + TryAddCharacters)로 글리프를 렌더링하므로
    /// FontEngine을 직접 재구현하지 않는다. Font Asset Creator의 "Auto Sizing"만 고수준 API에 없어서
    /// point size를 이진탐색으로 직접 찾는다(<see cref="FindAutoPointSize"/>).
    ///
    /// 저장은 FAC의 Save_SDF_FontAsset과 같은 정책:
    /// 경로에 에셋이 없으면 새로 만들고, 있으면 GUID/fileID를 보존한 채 내용만 갱신한다(프리팹·머티리얼 참조 유지).
    /// </summary>
    internal static class FontAtlasBaker
    {
        public struct Settings
        {
            public bool AutoSize;              // true면 PointSize 무시하고 최적값 탐색
            public int PointSize;              // AutoSize=false일 때 사용할 고정 sampling point size
            public int Padding;
            public int AtlasWidth;
            public int AtlasHeight;
            public GlyphRenderMode RenderMode;
            public AtlasPopulationMode PopulationMode;
            public bool MultiAtlas;            // 한 장에 안 들어가면 아틀라스를 여러 장으로
        }

        public struct Result
        {
            public bool Ok;
            public string Message;
            public TMP_FontAsset Asset;
            public bool Overwritten;
            public int PointSize;
            public int GlyphCount;
            public int AtlasCount;
            public int UnsupportedCount; // 폰트가 아예 안 가진 글자 수(구울 대상에서 제외됨)
        }

        private const int MinAutoPoint = 8;
        private const int MaxAutoPoint = 256;
        private const int FilterProbePoint = 16; // 지원 여부만 볼 땐 작은 크기로 빠르게

        public static Result Bake(Font font, IList<int> codePoints, Settings s, string projectPath)
        {
            var result = new Result();

            if (font == null) { result.Message = "원본 폰트가 지정되지 않았습니다."; return result; }
            if (codePoints == null || codePoints.Count == 0) { result.Message = "구울 문자가 없습니다."; return result; }
            if (string.IsNullOrWhiteSpace(projectPath) || !projectPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            { result.Message = "저장 경로가 올바르지 않습니다(.asset 이어야 함)."; return result; }
            if (!projectPath.Replace('\\', '/').StartsWith("Assets/"))
            { result.Message = "저장 경로는 Assets/ 아래여야 합니다."; return result; }

            uint[] unicodes = codePoints.Select(cp => (uint)cp).Distinct().ToArray();

            try
            {
                EditorUtility.DisplayProgressBar("폰트 아틀라스 굽기", "폰트 지원 글리프 확인 중...", 0.1f);
                uint[] supported = FilterSupported(font, unicodes, s, out int unsupported);
                if (supported.Length == 0) { result.Message = "이 폰트가 지원하는 글자가 하나도 없습니다."; return result; }
                result.UnsupportedCount = unsupported;

                int pointSize = s.PointSize;
                if (s.AutoSize)
                {
                    EditorUtility.DisplayProgressBar("폰트 아틀라스 굽기", "Auto Sizing — 최적 point size 탐색 중...", 0.3f);
                    pointSize = FindAutoPointSize(font, supported, s);
                }
                pointSize = Mathf.Clamp(pointSize, MinAutoPoint, MaxAutoPoint);
                result.PointSize = pointSize;

                EditorUtility.DisplayProgressBar("폰트 아틀라스 굽기", $"글리프 렌더링 중 (pt {pointSize})...", 0.6f);
                TMP_FontAsset temp = Generate(font, supported, pointSize, s, s.MultiAtlas, out _);
                if (temp == null) { result.Message = "폰트 에셋 생성에 실패했습니다."; return result; }

                // FAC가 재편집/원본 폰트 표시에 쓰는 메타데이터. 없으면 FAC에서 Source Font가 None으로 뜨고 경고가 난다.
                temp.creationSettings = BuildCreationSettings(font, pointSize, s, supported);

                result.GlyphCount = temp.characterTable?.Count ?? 0;
                result.AtlasCount = temp.atlasTextures?.Length ?? 0;

                EditorUtility.DisplayProgressBar("폰트 아틀라스 굽기", "에셋 저장 중...", 0.9f);
                TMP_FontAsset saved = SaveOrOverwrite(temp, projectPath, s, out bool overwritten);
                if (saved == null) { result.Message = "에셋 저장에 실패했습니다."; CleanupTemp(temp); return result; }

                result.Ok = true;
                result.Asset = saved;
                result.Overwritten = overwritten;
                result.Message =
                    $"{(overwritten ? "갱신" : "생성")} 완료 · pt {pointSize} · 글리프 {result.GlyphCount}자 · 아틀라스 {result.AtlasCount}장"
                    + (unsupported > 0 ? $" · 폰트 미지원 {unsupported}자 제외" : string.Empty)
                    + (result.AtlasCount > 1 ? " · (한 장에 안 들어가 멀티 아틀라스로 저장됨)" : string.Empty);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result.Message = "예외 발생: " + e.Message;
                return result;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ── 생성 ──────────────────────────────────────────────────────────────

        private static TMP_FontAsset Generate(Font font, uint[] unicodes, int pointSize, Settings s,
            bool multiAtlas, out uint[] missing)
        {
            missing = Array.Empty<uint>();
            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(
                font, pointSize, s.Padding, s.RenderMode,
                s.AtlasWidth, s.AtlasHeight, AtlasPopulationMode.Dynamic, multiAtlas);
            if (fa == null) return null;

            fa.TryAddCharacters(unicodes, out missing, includeFontFeatures: false);
            return fa;
        }

        /// <summary>폰트 자체가 안 가진 글자(글리프 인덱스 0)를 걸러 실제 구울 수 있는 집합만 남긴다.</summary>
        private static uint[] FilterSupported(Font font, uint[] unicodes, Settings s, out int unsupportedCount)
        {
            // 멀티 아틀라스 ON + 작은 point size라 공간 부족으로는 안 빠지고, 폰트가 없는 글자만 missing에 담긴다.
            TMP_FontAsset probe = Generate(font, unicodes, FilterProbePoint, s, true, out uint[] missing);
            var missingSet = new HashSet<uint>(missing ?? Array.Empty<uint>());
            CleanupTemp(probe);

            unsupportedCount = missingSet.Count;
            return unicodes.Where(u => !missingSet.Contains(u)).ToArray();
        }

        /// <summary>모든 글자가 아틀라스 한 장에 들어가는 최대 point size를 이진탐색한다(FAC의 Auto Sizing과 동일한 목표).</summary>
        private static int FindAutoPointSize(Font font, uint[] supported, Settings s)
        {
            int lo = MinAutoPoint, hi = MaxAutoPoint, best = MinAutoPoint;

            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                TMP_FontAsset fa = Generate(font, supported, mid, s, false, out uint[] missing);
                bool fits = fa != null && (missing == null || missing.Length == 0);
                CleanupTemp(fa);

                if (fits) { best = mid; lo = mid + 1; }
                else hi = mid - 1;
            }

            return best;
        }

        /// <summary>
        /// FAC의 <c>SaveFontCreationSettings</c>와 같은 메타데이터를 만든다.
        /// 이게 있어야 FAC에서 Source Font가 표시되고 "Creation Settings 없음" 경고가 사라지며 재편집이 된다.
        /// 문자 집합은 파일 참조 대신 실제 시퀀스를 임베드(Custom Characters 모드)해 자기완결적으로 둔다.
        /// </summary>
        private static FontAssetCreationSettings BuildCreationSettings(Font font, int pointSize, Settings s, uint[] supported)
        {
            return new FontAssetCreationSettings
            {
                sourceFontFileName = font.name,
                sourceFontFileGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font)),
                faceIndex = 0,
                pointSizeSamplingMode = s.AutoSize ? 0 : 1, // 0 = Auto Sizing, 1 = Custom Size
                pointSize = pointSize,
                padding = s.Padding,
                paddingMode = 2,                             // 2 = Pixel (px)
                packingMode = 0,                             // 0 = Fast
                atlasWidth = s.AtlasWidth,
                atlasHeight = s.AtlasHeight,
                characterSetSelectionMode = 7,               // 7 = Custom Characters
                characterSequence = ToSequence(supported),
                referencedFontAssetGUID = string.Empty,
                referencedTextAssetGUID = string.Empty,
                fontStyle = 0,
                fontStyleModifier = 0f,
                renderMode = (int)s.RenderMode,
                includeFontFeatures = false,
            };
        }

        private static string ToSequence(uint[] codes)
        {
            var sb = new StringBuilder(codes.Length);
            foreach (uint u in codes) sb.Append(char.ConvertFromUtf32((int)u));
            return sb.ToString();
        }

        // ── 저장 ──────────────────────────────────────────────────────────────

        private static TMP_FontAsset SaveOrOverwrite(TMP_FontAsset temp, string path, Settings s, out bool overwritten)
        {
            overwritten = false;

            string fileName = Path.GetFileNameWithoutExtension(path);
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(dir);

            temp.atlasPopulationMode = s.PopulationMode;
            ApplyStandardMaterial(temp, s);

            TMP_FontAsset dst = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (dst == null)
            {
                // 신규 생성 — temp를 그대로 에셋으로 저장한다.
                temp.name = fileName;
                AssetDatabase.CreateAsset(temp, path);
                AttachSubAssets(temp, fileName);
                EditorUtility.SetDirty(temp);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path);
                temp.ReadFontAssetDefinition();
                return temp;
            }

            // 덮어쓰기 — dst의 GUID/fileID를 유지한 채 내용만 갈아끼운다.
            overwritten = true;
            UnityEngine.Object[] oldSubs = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

            EditorUtility.CopySerialized(temp, dst); // 내부 필드까지 전부 복사(faceInfo/glyphTable/atlasTextures/material 등)
            dst.name = fileName;

            AttachSubAssets(dst, fileName);          // temp에서 넘어온 텍스처·머티리얼을 dst 서브에셋으로 편입

            if (oldSubs != null)                      // 교체 전 서브에셋 제거
                foreach (UnityEngine.Object o in oldSubs)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o, true);

            UnityEngine.Object.DestroyImmediate(temp); // 서브오브젝트는 dst로 이관됐으니 껍데기 SO만 제거

            EditorUtility.SetDirty(dst);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            dst.ReadFontAssetDefinition();
            return dst;
        }

        private static void AttachSubAssets(TMP_FontAsset fa, string fileName)
        {
            Texture2D[] textures = fa.atlasTextures;
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    if (textures[i] == null) continue;
                    textures[i].name = i == 0 ? fileName + " Atlas" : $"{fileName} Atlas {i}";
                    if (!AssetDatabase.Contains(textures[i]))
                        AssetDatabase.AddObjectToAsset(textures[i], fa);
                }
            }

            if (fa.material != null)
            {
                fa.material.name = fileName + " Material";
                if (!AssetDatabase.Contains(fa.material))
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
        }

        /// <summary>FAC 저장물과 동일하게 "TextMeshPro/Distance Field" 셰이더 + 표준 파라미터를 맞춘다.</summary>
        private static void ApplyStandardMaterial(TMP_FontAsset fa, Settings s)
        {
            if (fa.material == null || fa.atlasTexture == null) return;
            if (IsBitmapMode(s.RenderMode)) return; // 비트맵 계열은 TMP 기본 머티리얼 유지

            Shader sdf = Shader.Find("TextMeshPro/Distance Field");
            if (sdf != null && fa.material.shader != sdf)
                fa.material.shader = sdf;

            Material mat = fa.material;
            mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            mat.SetFloat(ShaderUtilities.ID_TextureWidth, fa.atlasTexture.width);
            mat.SetFloat(ShaderUtilities.ID_TextureHeight, fa.atlasTexture.height);
            mat.SetFloat(ShaderUtilities.ID_GradientScale, s.Padding + 1); // FAC: spread = padding + 1
            mat.SetFloat(ShaderUtilities.ID_WeightNormal, fa.normalStyle);
            mat.SetFloat(ShaderUtilities.ID_WeightBold, fa.boldStyle);
        }

        private static bool IsBitmapMode(GlyphRenderMode mode) =>
            mode == GlyphRenderMode.SMOOTH || mode == GlyphRenderMode.SMOOTH_HINTED ||
            mode == GlyphRenderMode.RASTER || mode == GlyphRenderMode.RASTER_HINTED;

        private static void CleanupTemp(TMP_FontAsset fa)
        {
            if (fa == null) return;

            if (fa.atlasTextures != null)
                foreach (Texture2D t in fa.atlasTextures)
                    if (t != null) UnityEngine.Object.DestroyImmediate(t);

            if (fa.material != null) UnityEngine.Object.DestroyImmediate(fa.material);
            UnityEngine.Object.DestroyImmediate(fa);
        }

        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;

            string[] parts = dir.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
