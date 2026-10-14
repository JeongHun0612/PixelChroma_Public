#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OneText.UGUI;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

namespace PixelChroma.EditorTools
{
    /// <summary>
    /// String Table에서 실제 사용된 문자만 뽑아 TMP Font Asset Creator의
    /// "Characters from File"용 텍스트 파일을 만든다.
    ///
    /// 언어별로 파일을 쪼개던 <see cref="LocalizationCharSetExporter"/>와 달리
    /// 여러 로케일을 묶어 "폰트 하나에 들어갈 한 덩어리"로 뽑는다.
    /// (예: M PLUS Rounded 1c 하나로 라틴 8개 + 러시아어 + 일본어를 커버)
    ///
    /// 굽기 전에 원본 폰트 cmap을 읽어 없는 글리프를 미리 잡아준다.
    ///
    /// 메뉴: PixelChroma > Localization > Character Set Builder
    /// </summary>
    public class LocalizationCharSetWindow : EditorWindow
    {
        private const string OutputFolder = "Assets/Language/CharSets";
        private const int AsciiFirst = 0x20;
        private const int AsciiLast = 0x7E;
        private const int MaxRangeExpansion = 200000;

        // FontTable 구성과 맞춘 프리셋
        private static readonly (string Label, string[] Locales)[] Presets =
        {
            ("M PLUS 그룹", new[] { "en", "es", "pt-BR", "de", "fr", "it", "pl", "tr", "ru", "ja" }),
            ("한국어", new[] { "ko" }),
            ("중국어 간체", new[] { "zh-Hans" }),
            ("중국어 번체", new[] { "zh-Hant" }),
        };

        private readonly SortedDictionary<string, HashSet<int>> _byLocale = new();
        private readonly Dictionary<string, bool> _selected = new();

        // OnGUI는 초당 수십 번 돈다. 폰트 파일은 수십 MB라 경로별로 한 번만 읽는다.
        private readonly Dictionary<string, HashSet<int>> _cmapCache = new();

        private bool _includeAscii = true;
        private string _extraRanges = string.Empty;
        private Font _validationFont;
        private string _outputName = "MPLUSRounded1c";
        private Vector2 _scroll;

        // ── 굽기(Bake) 세팅 ─────────────────────────────────────────────────
        private static readonly int[] AtlasSizes = { 256, 512, 1024, 2048, 4096, 8192 };
        private const string BakePathPrefKeyPrefix = "PixelChroma.CharSetBaker.path.";

        private bool _showBakeSettings;
        private bool _autoSize = true;
        private int _pointSize = 90;
        private int _padding = 5;
        private int _atlasWidth = 1024;
        private int _atlasHeight = 1024;
        private GlyphRenderMode _renderMode = GlyphRenderMode.SDFAA;
        private AtlasPopulationMode _populationMode = AtlasPopulationMode.Static;
        private bool _multiAtlas = true;
        private string _bakePath = string.Empty;

        // 마지막 굽기 결과. 콘솔을 열지 않아도 창 안에서 보이게 한다.
        private string _lastBakeMessage = string.Empty;
        private bool _lastBakeOk;

        // ── 단계 표시 ────────────────────────────────────────────────────
        private const int LocaleColumns = 3;
        private const float WideLayoutMinWidth = 860f;
        private const float NarrowContentWidth = 380f;
        private const float RoomyFlowMinWidth = 600f;
        private static readonly Color Accent = new Color(0.231f, 0.435f, 0.910f);
        private static GUIStyle _stepTitleStyle;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _summaryStyle;
        private static GUIStyle _flowStyle;
        private static GUIStyle _flowRoomyStyle;

        [MenuItem("PixelChroma/Localization/Character Set Builder")]
        private static void Open()
        {
            var window = GetWindow<LocalizationCharSetWindow>("Character Set Builder");
            window.minSize = new Vector2(460f, 520f);
            window.Scan();
        }

        private void OnEnable()
        {
            if (_byLocale.Count == 0) Scan();
        }

        // ── String Table 스캔 ───────────────────────────────────────────────

        private void Scan()
        {
            _byLocale.Clear();
            _cmapCache.Clear(); // 폰트 파일을 교체했을 수 있으므로 같이 비운다

            var collections = LocalizationEditorSettings.GetStringTableCollections();
            if (collections == null) return;

            foreach (var collection in collections)
            {
                foreach (var table in collection.StringTables)
                {
                    if (table == null) continue;

                    string code = table.LocaleIdentifier.Code;
                    if (string.IsNullOrEmpty(code)) continue;

                    if (!_byLocale.TryGetValue(code, out var set))
                    {
                        set = new HashSet<int>();
                        _byLocale[code] = set;
                    }

                    foreach (StringTableEntry entry in table.Values)
                    {
                        if (string.IsNullOrEmpty(entry?.Value)) continue;

                        foreach (int cp in CodePoints(entry.Value))
                        {
                            if (cp < AsciiFirst) continue; // 개행 등 제어문자 제외
                            set.Add(cp);
                        }
                    }
                }
            }

            foreach (string code in _byLocale.Keys)
                if (!_selected.ContainsKey(code)) _selected[code] = false;
        }

        // ── GUI ────────────────────────────────────────────────────────────
        // 창 구성이 곧 작업 순서다: ① 글자 추출 → ② 폰트 검사 → ③ 아틀라스 굽기
        // 창이 넓으면 세 단계를 가로로 나란히 두고, 좁으면 세로로 쌓는다.

        private void OnGUI()
        {
            if (_byLocale.Count == 0)
            {
                EditorGUILayout.HelpBox("String Table Collection을 찾지 못했습니다.", MessageType.Warning);
                if (GUILayout.Button("다시 스캔")) Scan();
                return;
            }

            EnsureStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawFlow(position.width >= RoomyFlowMinWidth);

            bool wide = position.width >= WideLayoutMinWidth;
            float stepWidth = wide ? Mathf.Floor((position.width - 48f) / 3f) : 0f;
            float contentWidth = wide ? stepWidth - 12f : position.width - 24f;
            float labelWidth = EditorGUIUtility.labelWidth;
            if (wide)
            {
                EditorGUIUtility.labelWidth = 72f;
                EditorGUILayout.Space(6f);
                EditorGUILayout.BeginHorizontal();
            }

            BeginStep(1, "글자 추출", "String Table에서 실제로 쓰인 글자만 폰트 묶음별로 모읍니다.", stepWidth);
            DrawExtractStep(contentWidth);
            var codePoints = BuildSet();
            DrawSummary($"언어 {_selected.Count(kv => kv.Value)}개 선택 · 총 {codePoints.Count}자");
            EndStep();

            BeginStep(2, "폰트 검사", "원본 폰트 파일을 직접 읽어, 폰트에 없는 글자를 굽기 전에 찾습니다.", stepWidth);
            DrawValidateStep(codePoints);
            EndStep();

            BeginStep(3, "아틀라스 굽기", "한 장에 모두 들어가는 가장 큰 글자 크기를 찾아 굽고, 기존 에셋은 참조를 유지한 채 덮어씁니다.", stepWidth);
            DrawBakeStep(codePoints, contentWidth < NarrowContentWidth);
            EndStep();

            if (wide)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUIUtility.labelWidth = labelWidth;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("String Table 다시 스캔", EditorStyles.miniButton)) Scan();

            EditorGUILayout.EndScrollView();
        }

        // ── 단계 틀 ────────────────────────────────────────────────────────

        private static void EnsureStyles()
        {
            if (_stepTitleStyle != null) return;

            _stepTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            _badgeStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            _badgeStyle.normal.textColor = Color.white;
            _summaryStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
            _flowStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            _flowRoomyStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        }

        private static void DrawFlow(bool roomy)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, roomy ? 28f : 24f);
            EditorGUI.DrawRect(rect, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
            GUI.Label(rect, "String Table  →  ① 글자 추출  →  ② 폰트 검사  →  ③ 아틀라스 굽기  →  Font Asset",
                roomy ? _flowRoomyStyle : _flowStyle);
        }

        /// <param name="width">0보다 크면 그 폭의 열로 그린다(가로 배치). 0이면 창 폭을 그대로 쓴다(세로 배치).</param>
        private static void BeginStep(int number, string title, string description, float width)
        {
            if (width > 0f)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            }
            else
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            }

            Rect row = EditorGUILayout.GetControlRect(false, 22f);
            var badge = new Rect(row.x, row.y + 1f, 20f, 20f);
            EditorGUI.DrawRect(badge, Accent);
            GUI.Label(badge, number.ToString(), _badgeStyle);
            GUI.Label(new Rect(badge.xMax + 6f, row.y, row.width - 26f, row.height), title, _stepTitleStyle);

            // LabelField는 한 줄 높이로 고정이라 긴 설명이 잘린다. 줄바꿈 높이를 계산하는 GUILayout.Label을 쓴다.
            GUILayout.Label(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2f);
        }

        private static void EndStep() => EditorGUILayout.EndVertical();

        /// <summary>단계의 결과 한 줄. 캡처만 봐도 무엇이 나왔는지 읽히도록 강조색 바탕에 굵게 쓴다.</summary>
        private static void DrawSummary(string text)
        {
            EditorGUILayout.Space(2f);
            Rect rect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(rect, new Color(Accent.r, Accent.g, Accent.b, 0.15f));
            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), text, _summaryStyle);
        }

        // ── ① 글자 추출 ────────────────────────────────────────────────────

        private void DrawExtractStep(float contentWidth)
        {
            bool narrow = contentWidth < NarrowContentWidth;

            GUILayout.Label("폰트 묶음", EditorStyles.miniBoldLabel);
            int presetsPerRow = narrow ? 2 : Presets.Length;
            for (int i = 0; i < Presets.Length; i += presetsPerRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int p = i; p < Mathf.Min(i + presetsPerRow, Presets.Length); p++)
                    {
                        var (label, locales) = Presets[p];
                        if (!GUILayout.Button(label, EditorStyles.miniButton)) continue;

                        foreach (string code in _byLocale.Keys.ToArray())
                            _selected[code] = locales.Contains(code);
                        _outputName = label switch
                        {
                            "M PLUS 그룹" => "MPLUSRounded1c",
                            "한국어" => "Korean",
                            "중국어 간체" => "ChineseSC",
                            _ => "ChineseTC",
                        };
                        _bakePath = EditorPrefs.GetString(BakePathPrefKeyPrefix + _outputName, string.Empty);
                        _lastBakeMessage = string.Empty; // 다른 묶음의 결과가 남아 보이지 않게
                        GUI.FocusControl(null);
                    }
                }
            }

            // 언어가 13개라 한 줄에 하나씩 두면 창의 절반을 차지한다. 여러 열로 접는다.
            GUILayout.Label("포함할 언어", EditorStyles.miniBoldLabel);
            string[] codes = _byLocale.Keys.ToArray();
            int columns = narrow ? 2 : LocaleColumns;
            float cell = Mathf.Floor((contentWidth - 8f) / columns);
            for (int i = 0; i < codes.Length; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < columns && i + c < codes.Length; c++)
                    {
                        string code = codes[i + c];
                        _selected[code] = EditorGUILayout.ToggleLeft(
                            $"{code}  {_byLocale[code].Count}자", _selected[code], GUILayout.Width(cell));
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전체 선택", EditorStyles.miniButton))
                    foreach (string code in _byLocale.Keys.ToArray()) _selected[code] = true;
                if (GUILayout.Button("전체 해제", EditorStyles.miniButton))
                    foreach (string code in _byLocale.Keys.ToArray()) _selected[code] = false;
            }

            _includeAscii = EditorGUILayout.ToggleLeft(
                new GUIContent($"기본 ASCII 포함 ({AsciiLast - AsciiFirst + 1}자)",
                    "런타임 보간값({0} 숫자·구분자)이 번역문에 안 잡히므로 켜두는 것을 권장합니다."),
                _includeAscii);

            _extraRanges = EditorGUILayout.TextField(
                new GUIContent("추가 범위", "쉼표 구분. 예: 0400-04FF, 2010-2027, 20AC"), _extraRanges);
        }

        // ── ② 폰트 검사 ────────────────────────────────────────────────────

        private void DrawValidateStep(List<int> codePoints)
        {
            _validationFont = (Font)EditorGUILayout.ObjectField(
                new GUIContent("원본 폰트", "구울 대상 .ttf/.otf. cmap을 읽어 없는 글리프를 미리 잡아내고, 아틀라스도 이 폰트로 굽습니다."),
                _validationFont, typeof(Font), false);

            if (_validationFont == null)
                EditorGUILayout.HelpBox("원본 폰트를 지정하면 폰트에 없는 글자를 굽기 전에 확인합니다.", MessageType.None);
            else if (codePoints.Count > 0)
                DrawValidation(codePoints);
        }

        // ── ③ 아틀라스 굽기 ────────────────────────────────────────────────

        private void DrawBakeStep(List<int> codePoints, bool narrow)
        {
            _outputName = EditorGUILayout.TextField("파일 이름", _outputName);
            string charSetPath = $"{OutputFolder}/{_outputName}.txt";
            bool hasBakePath = !string.IsNullOrEmpty(_bakePath);
            string saveMode = hasBakePath && File.Exists(_bakePath)
                ? "기존 에셋을 덮어씁니다 — 프리팹·머티리얼 참조 유지"
                : "새 에셋으로 생성합니다";

            if (narrow)
            {
                // 열이 좁으면 이름표·값 두 칸으로는 경로가 잘린다. 줄바꿈되는 한 줄로 쓴다.
                GUILayout.Label($"글자 목록  {charSetPath}", EditorStyles.wordWrappedMiniLabel);
                if (hasBakePath)
                {
                    GUILayout.Label($"저장 위치  {_bakePath}", EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Label(saveMode, EditorStyles.wordWrappedMiniLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("글자 목록", charSetPath, EditorStyles.miniLabel);
                if (hasBakePath)
                {
                    EditorGUILayout.LabelField("저장 위치", _bakePath, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(" ", saveMode, EditorStyles.miniLabel);
                }
            }

            _showBakeSettings = EditorGUILayout.Foldout(_showBakeSettings, "굽기 세팅", true);
            if (_showBakeSettings)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _autoSize = EditorGUILayout.ToggleLeft(
                        new GUIContent("Auto Sizing", "아틀라스 한 장에 최대 크기로 들어가는 point size를 자동으로 찾습니다."),
                        _autoSize);

                    using (new EditorGUI.DisabledScope(_autoSize))
                        _pointSize = Mathf.Max(8, EditorGUILayout.IntField(
                            new GUIContent("Sampling Point Size", "Auto Sizing이 꺼졌을 때 사용할 고정 크기."), _pointSize));

                    _padding = Mathf.Max(0, EditorGUILayout.IntField("Padding", _padding));
                    _atlasWidth = SizePopup("Atlas Width", _atlasWidth);
                    _atlasHeight = SizePopup("Atlas Height", _atlasHeight);
                    _renderMode = (GlyphRenderMode)EditorGUILayout.EnumPopup(
                        new GUIContent("Character Render Mode", "기본 SDFAA. SDF 계열은 Distance Field 셰이더로 저장됩니다."), _renderMode);
                    _populationMode = (AtlasPopulationMode)EditorGUILayout.EnumPopup(
                        new GUIContent("Atlas Population Mode", "Static: 지금 구운 글자로 고정. Dynamic: 런타임에 추가 가능."), _populationMode);
                    _multiAtlas = EditorGUILayout.ToggleLeft(
                        new GUIContent("Multi Atlas", "한 장에 다 못 들어가면 아틀라스를 여러 장으로 나눕니다."), _multiAtlas);
                }
            }

            EditorGUILayout.Space(2f);
            bool canExport = codePoints.Count > 0 && !string.IsNullOrWhiteSpace(_outputName);
            bool canBake = _validationFont != null && codePoints.Count > 0;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canExport))
                {
                    if (GUILayout.Button("Export (txt만)", GUILayout.Height(30f)))
                        Export(codePoints);
                }

                // 저장 대화상자·진행 표시는 모달이라 OnGUI 레이아웃 도중에 띄우지 않고 다음 틱으로 미룬다.
                using (new EditorGUI.DisabledScope(!canBake))
                {
                    if (GUILayout.Button("아틀라스 굽기…", GUILayout.Height(30f)))
                        EditorApplication.delayCall += () => PromptAndBake(codePoints, alsoExport: false);
                    if (GUILayout.Button("Export + 굽기…", GUILayout.Height(30f)))
                        EditorApplication.delayCall += () => PromptAndBake(codePoints, alsoExport: true);
                }
            }

            if (!string.IsNullOrEmpty(_lastBakeMessage))
                EditorGUILayout.HelpBox(_lastBakeMessage, _lastBakeOk ? MessageType.Info : MessageType.Error);
            else if (_validationFont == null)
                EditorGUILayout.HelpBox("굽기를 하려면 ②에서 '원본 폰트'를 지정하세요.", MessageType.None);
            else
                GUILayout.Label(
                    "버튼을 누르면 저장 위치를 고릅니다. 기존 파일을 고르면 참조를 유지한 채 덮어씁니다.",
                    EditorStyles.wordWrappedMiniLabel);
        }

        private static int SizePopup(string label, int current)
        {
            int index = System.Array.IndexOf(AtlasSizes, current);
            if (index < 0) index = System.Array.IndexOf(AtlasSizes, 1024);
            var labels = AtlasSizes.Select(s => new GUIContent(s.ToString())).ToArray();
            index = EditorGUILayout.Popup(new GUIContent(label), index, labels);
            return AtlasSizes[index];
        }

        /// <summary>저장 위치를 대화상자로 고른 뒤 굽는다. 취소하면 아무 것도 하지 않는다.</summary>
        private void PromptAndBake(List<int> codePoints, bool alsoExport)
        {
            if (_validationFont == null || codePoints.Count == 0) return;

            string startDir = "Assets";
            string defName = _outputName + "_Static";
            if (!string.IsNullOrEmpty(_bakePath))
            {
                startDir = Path.GetDirectoryName(_bakePath)?.Replace('\\', '/') ?? "Assets";
                defName = Path.GetFileNameWithoutExtension(_bakePath);
            }

            string chosen = EditorUtility.SaveFilePanelInProject(
                "폰트 에셋 저장 위치", defName, "asset",
                "구울 TMP 폰트 에셋 경로를 지정하세요. 기존 파일을 고르면 참조를 유지한 채 덮어씁니다.", startDir);
            if (string.IsNullOrEmpty(chosen)) return; // 취소

            _bakePath = chosen;
            EditorPrefs.SetString(BakePathPrefKeyPrefix + _outputName, _bakePath);

            if (alsoExport) Export(codePoints);
            BakeAtlas(codePoints);
        }

        private void BakeAtlas(List<int> codePoints)
        {
            if (_validationFont == null || string.IsNullOrWhiteSpace(_bakePath) || codePoints.Count == 0)
                return;

            var settings = new FontAtlasBaker.Settings
            {
                AutoSize = _autoSize,
                PointSize = _pointSize,
                Padding = _padding,
                AtlasWidth = _atlasWidth,
                AtlasHeight = _atlasHeight,
                RenderMode = _renderMode,
                PopulationMode = _populationMode,
                MultiAtlas = _multiAtlas,
            };

            FontAtlasBaker.Result r = FontAtlasBaker.Bake(_validationFont, codePoints, settings, _bakePath);
            _lastBakeOk = r.Ok;
            _lastBakeMessage = r.Ok ? $"아틀라스 {r.Message}" : $"굽기 실패: {r.Message}";
            Repaint();
            if (r.Ok)
            {
                EditorPrefs.SetString(BakePathPrefKeyPrefix + _outputName, _bakePath);
                Debug.Log($"[CharSet] 아틀라스 {r.Message}\n  → {_bakePath}", r.Asset);
                EditorGUIUtility.PingObject(r.Asset);
                Selection.activeObject = r.Asset;
            }
            else
            {
                Debug.LogError($"[CharSet] 아틀라스 굽기 실패: {r.Message}");
                EditorUtility.DisplayDialog("굽기 실패", r.Message, "확인");
            }
        }

        private void DrawValidation(List<int> codePoints)
        {
            string path = AssetDatabase.GetAssetPath(_validationFont);
            if (!_cmapCache.TryGetValue(path, out var available))
            {
                available = FontCmapReader.Read(path);
                _cmapCache[path] = available;
            }

            if (available.Count == 0)
            {
                EditorGUILayout.HelpBox($"cmap을 읽지 못했습니다: {path}", MessageType.Warning);
                return;
            }

            var missing = codePoints.Where(cp => !available.Contains(cp)).ToList();
            if (missing.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"{_validationFont.name}이(가) {codePoints.Count}자를 모두 가지고 있습니다.", MessageType.Info);
                return;
            }

            var preview = new StringBuilder();
            foreach (int cp in missing.Take(40))
                preview.Append($"U+{cp:X4} {char.ConvertFromUtf32(cp)}  ");
            if (missing.Count > 40) preview.Append($"... 외 {missing.Count - 40}자");

            EditorGUILayout.HelpBox(
                $"{_validationFont.name}에 없는 글자 {missing.Count}자 — 구워도 아틀라스에 안 들어갑니다.\n{preview}",
                MessageType.Error);
        }

        // ── 집합 구성 ──────────────────────────────────────────────────────

        private List<int> BuildSet()
        {
            var set = new HashSet<int>();

            if (_includeAscii)
                for (int cp = AsciiFirst; cp <= AsciiLast; cp++) set.Add(cp);

            foreach (var kv in _byLocale)
                if (_selected.TryGetValue(kv.Key, out bool on) && on)
                    set.UnionWith(kv.Value);

            foreach (int cp in ParseRanges(_extraRanges)) set.Add(cp);

            var list = set.ToList();
            list.Sort();
            return list;
        }

        private static IEnumerable<int> ParseRanges(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;

            int budget = MaxRangeExpansion;

            foreach (string token in text.Split(','))
            {
                string trimmed = token.Trim();
                if (trimmed.Length == 0) continue;

                string[] parts = trimmed.Split('-');
                if (!TryParseHex(parts[0], out int from)) continue;

                int to = from;
                if (parts.Length > 1 && !TryParseHex(parts[1], out to)) continue;
                if (to < from) continue;

                for (int cp = from; cp <= to && budget > 0; cp++, budget--)
                    yield return cp;
            }
        }

        private static bool TryParseHex(string text, out int value) =>
            int.TryParse(text.Trim().Replace("U+", string.Empty).Replace("u+", string.Empty),
                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        private static IEnumerable<int> CodePoints(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    yield return char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                }
                else if (!char.IsSurrogate(c))
                {
                    yield return c;
                }
            }
        }

        // ── 출력 ───────────────────────────────────────────────────────────

        private void Export(List<int> codePoints)
        {
            if (!Directory.Exists(OutputFolder)) Directory.CreateDirectory(OutputFolder);

            var builder = new StringBuilder(codePoints.Count);
            foreach (int cp in codePoints) builder.Append(char.ConvertFromUtf32(cp));

            string path = $"{OutputFolder}/{_outputName}.txt";
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            string locales = string.Join(", ", _selected.Where(kv => kv.Value).Select(kv => kv.Key));
            Debug.Log($"[CharSet] {codePoints.Count}자 → {path}\n  포함 로케일: {locales}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        }
    }

    /// <summary>
    /// .ttf/.otf/.ttc의 cmap 테이블을 직접 읽어 지원 코드포인트를 구한다.
    /// TMP_FontAsset.HasCharacter는 이미 구워진 아틀라스만 보므로 굽기 전 검증에는 못 쓴다.
    /// </summary>
    internal static class FontCmapReader
    {
        public static HashSet<int> Read(string assetPath)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return result;

            byte[] data;
            try { data = File.ReadAllBytes(assetPath); }
            catch (IOException) { return result; }

            if (data.Length < 12) return result;

            // TrueType Collection이면 첫 번째 폰트만 본다.
            int origin = Tag(data, 0) == "ttcf" ? (int)U32(data, 12) : 0;
            if (origin + 12 > data.Length) return result;

            int tableCount = U16(data, origin + 4);
            int cmap = 0;
            for (int i = 0; i < tableCount; i++)
            {
                int record = origin + 12 + i * 16;
                if (record + 16 > data.Length) return result;
                if (Tag(data, record) != "cmap") continue;

                cmap = (int)U32(data, record + 8);
                break;
            }

            if (cmap == 0 || cmap + 4 > data.Length) return result;

            int subtableCount = U16(data, cmap + 2);
            for (int i = 0; i < subtableCount; i++)
            {
                int record = cmap + 4 + i * 8;
                if (record + 8 > data.Length) break;

                int subtable = cmap + (int)U32(data, record + 4);
                if (subtable + 4 > data.Length) continue;

                switch (U16(data, subtable))
                {
                    case 4: ReadFormat4(data, subtable, result); break;
                    case 12: ReadFormat12(data, subtable, result); break;
                }
            }

            return result;
        }

        private static void ReadFormat4(byte[] data, int subtable, HashSet<int> result)
        {
            int segCountX2 = U16(data, subtable + 6);
            int endOffset = subtable + 14;
            int startOffset = endOffset + segCountX2 + 2;
            if (startOffset + segCountX2 > data.Length) return;

            for (int s = 0; s < segCountX2 / 2; s++)
            {
                int end = U16(data, endOffset + s * 2);
                int start = U16(data, startOffset + s * 2);
                if (start == 0xFFFF) continue;

                for (int cp = start; cp <= end; cp++) result.Add(cp);
            }
        }

        private static void ReadFormat12(byte[] data, int subtable, HashSet<int> result)
        {
            if (subtable + 16 > data.Length) return;

            uint groupCount = U32(data, subtable + 12);
            for (uint g = 0; g < groupCount; g++)
            {
                int offset = subtable + 16 + (int)g * 12;
                if (offset + 12 > data.Length) return;

                uint start = U32(data, offset);
                uint end = U32(data, offset + 4);
                if (end < start || end - start > 100000) continue;

                for (uint cp = start; cp <= end; cp++) result.Add((int)cp);
            }
        }

        private static string Tag(byte[] data, int offset) =>
            Encoding.ASCII.GetString(data, offset, 4);

        private static int U16(byte[] data, int offset) =>
            (data[offset] << 8) | data[offset + 1];

        private static uint U32(byte[] data, int offset) =>
            ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
            ((uint)data[offset + 2] << 8) | data[offset + 3];
    }
}
#endif
