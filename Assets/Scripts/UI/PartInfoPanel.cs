using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CoalPulverizer.UI
{
    // 우측에 슬라이드인되는 부품 정보 패널 전체를 런타임에 생성하고 제어
    public sealed class PartInfoPanel : MonoBehaviour
    {
        private RectTransform _panel;
        private Text _nameKr;
        private Text _nameEn;
        private Text _description;
        private Text _specText;
        private BlueprintPanel _blueprint;
        private WearPanel _wearPanel;
        private bool _isVisible;

        private const float PanelWidth  = 340f;
        private const float SlideTime   = 0.28f;

        // ─── 빌드 ────────────────────────────────────────────────

        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>(true);
            Debug.Log($"[PartInfoPanel] Awake — canvas={canvas != null}");
            if (canvas != null)
                Build(canvas);
        }

        public void Build(Canvas canvas)
        {
            var canvasRt = canvas.GetComponent<RectTransform>();

            // 패널 루트 (우측에서 슬라이드인)
            var panelGo = new GameObject("PartInfoPanel", typeof(RectTransform), typeof(Image));
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.SetParent(canvasRt, false);
            _panel.anchorMin  = new Vector2(1, 0);
            _panel.anchorMax  = new Vector2(1, 1);
            _panel.pivot      = new Vector2(1, 0.5f);
            _panel.sizeDelta  = new Vector2(PanelWidth, 0);
            _panel.anchoredPosition = new Vector2(PanelWidth, 0); // 처음엔 오른쪽으로 숨김

            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.16f, 0.97f);

            // 닫기 버튼
            CreateCloseButton(_panel);

            // 콘텐츠 스크롤 영역
            float topPad    = 44f;
            float bpHeight  = 200f;
            float padding   = 14f;

            // ── 도면 영역 ──
            var bpGo = new GameObject("Blueprint", typeof(RectTransform));
            var bpRt = bpGo.GetComponent<RectTransform>();
            bpRt.SetParent(_panel, false);
            bpRt.anchorMin  = new Vector2(0, 1);
            bpRt.anchorMax  = new Vector2(1, 1);
            bpRt.pivot      = new Vector2(0.5f, 1);
            bpRt.anchoredPosition = new Vector2(0, -(topPad + padding));
            bpRt.sizeDelta  = new Vector2(-padding * 2, bpHeight);

            _blueprint = bpGo.AddComponent<BlueprintPanel>();
            _blueprint.Build(bpRt);

            // ── 구분선 ──
            float divY = -(topPad + padding + bpHeight + 12f);
            CreateDivider(_panel, divY);

            // ── 부품명 (한국어) ──
            float textTop = divY - 20f;
            _nameKr = CreateText(_panel, "NameKr", "",
                new Vector2(padding, textTop), new Vector2(-padding, textTop - 28f),
                22, FontStyle.Bold, new Color(0.3f, 0.85f, 1f));

            // ── 부품명 (영어) ──
            _nameEn = CreateText(_panel, "NameEn", "",
                new Vector2(padding, textTop - 30f), new Vector2(-padding, textTop - 48f),
                11, FontStyle.Normal, new Color(0.5f, 0.65f, 0.75f));

            // ── 구분선 ──
            CreateDivider(_panel, textTop - 54f);

            // ── 설명 ──
            _description = CreateText(_panel, "Desc", "",
                new Vector2(padding, textTop - 66f), new Vector2(-padding, textTop - 142f),
                12, FontStyle.Normal, new Color(0.78f, 0.88f, 0.96f));

            // ── 제원 헤더 ──
            CreateText(_panel, "SpecHeader", "▪ 주요 제원",
                new Vector2(padding, textTop - 148f), new Vector2(-padding, textTop - 166f),
                11, FontStyle.Bold, new Color(0.3f, 0.85f, 1f));

            // ── 제원 목록 ──
            _specText = CreateText(_panel, "Specs", "",
                new Vector2(padding + 4f, textTop - 170f), new Vector2(-padding, textTop - 310f),
                11, FontStyle.Normal, new Color(0.72f, 0.85f, 0.92f));

            // ── 마모 예측 패널 (롤 타이어 전용) ──
            float wearTop = textTop - 320f;
            var wearGo = new GameObject("WearPanel", typeof(RectTransform));
            var wearRt = wearGo.GetComponent<RectTransform>();
            wearRt.SetParent(_panel, false);
            wearRt.anchorMin        = new Vector2(0, 1);
            wearRt.anchorMax        = new Vector2(1, 1);
            wearRt.pivot            = new Vector2(0.5f, 1);
            wearRt.anchoredPosition = new Vector2(0, wearTop);
            wearRt.sizeDelta        = new Vector2(0, 200f);
            _wearPanel = wearGo.AddComponent<WearPanel>();
            _wearPanel.Build(wearRt);

            Hide(instant: true);
        }

        // ─── 공개 API ─────────────────────────────────────────────

        public void Show(PartData data)
        {
            Debug.Log($"[PartInfoPanel] Show called — data={data?.KoreanName ?? "NULL"}, _panel={_panel != null}");
            if (data == null) { Hide(); return; }

            _nameKr.text      = data.KoreanName;
            _nameEn.text      = data.EnglishName;
            _description.text = data.Description;
            _specText.text    = data.Specs != null ? "• " + string.Join("\n• ", data.Specs) : "";
            _blueprint.Highlight(data.BlueprintZone);
            _wearPanel.Show(data.WearData);

            if (!_isVisible) SlideIn();
        }

        public void Hide(bool instant = false)
        {
            _blueprint.ResetHighlight();
            if (!_isVisible && !instant) return;
            if (instant)
            {
                _panel.anchoredPosition = new Vector2(PanelWidth, 0);
                _isVisible = false;
            }
            else
            {
                SlideOut();
            }
        }

        // ─── 슬라이드 애니메이션 ──────────────────────────────────

        private void SlideIn()
        {
            _isVisible = true;
            StopAllCoroutines();
            StartCoroutine(AnimateX(PanelWidth, 0f));
        }

        private void SlideOut()
        {
            _isVisible = false;
            StopAllCoroutines();
            StartCoroutine(AnimateX(0f, PanelWidth));
        }

        private IEnumerator AnimateX(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < SlideTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / SlideTime);
                _panel.anchoredPosition = new Vector2(Mathf.Lerp(from, to, t), 0f);
                yield return null;
            }
            _panel.anchoredPosition = new Vector2(to, 0f);
        }

        // ─── UI 헬퍼 ─────────────────────────────────────────────

        private void CreateCloseButton(RectTransform parent)
        {
            var go = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            rt.sizeDelta = new Vector2(32f, 32f);

            go.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.5f, 0.7f);

            var label = new GameObject("X", typeof(RectTransform));
            var lrt = label.GetComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var t = label.AddComponent<Text>();
            t.text      = "✕";
            t.fontSize  = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = new Color(0.6f, 0.8f, 1f);
            t.font      = UIFonts.Korean;

            go.GetComponent<Button>().onClick.AddListener(() => Hide());
        }

        private static void CreateDivider(RectTransform parent, float yAnchoredPos)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, yAnchoredPos);
            rt.sizeDelta = new Vector2(-24f, 1f);
            go.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.65f, 0.6f);
        }

        private static Text CreateText(RectTransform parent, string name, string text,
            Vector2 offsetMin, Vector2 offsetMax,
            int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(offsetMin.x, offsetMax.y); // bottom-left in anchor space
            rt.offsetMax = new Vector2(offsetMax.x, offsetMin.y); // top-right
            // 앵커 기반 절대 좌표로 변환
            rt.anchoredPosition = new Vector2(0, (offsetMin.y + offsetMax.y) * 0.5f);
            rt.sizeDelta        = new Vector2(0, Mathf.Abs(offsetMax.y - offsetMin.y));

            // 다시 단순하게
            rt.anchorMin        = new Vector2(0, 1);
            rt.anchorMax        = new Vector2(1, 1);
            rt.pivot            = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, offsetMin.y);
            rt.sizeDelta        = new Vector2(0, Mathf.Abs(offsetMax.y - offsetMin.y));

            var comp = go.AddComponent<Text>();
            comp.text       = text;
            comp.fontSize   = fontSize;
            comp.fontStyle  = style;
            comp.color      = color;
            comp.alignment  = TextAnchor.UpperLeft;
            comp.verticalOverflow   = VerticalWrapMode.Overflow;
            comp.horizontalOverflow = HorizontalWrapMode.Wrap;
            comp.font = UIFonts.Korean;
            return comp;
        }
    }
}
