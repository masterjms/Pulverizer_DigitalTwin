using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CoalPulverizer.UI
{
    // 롤 타이어 클릭 시 우측 패널 하단에 표시하는 AMWI 마모 예측 섹션
    public sealed class WearPanel : MonoBehaviour
    {
        private Image _amwiFill;
        private Image _wearFill;
        private Text  _amwiLabel;
        private Text  _severityLabel;
        private Text  _lifeLabel;
        private readonly List<Image> _factorFills  = new();
        private readonly List<Text>  _factorLabels = new();

        private static readonly Color ColNormal  = new Color(0.25f, 0.90f, 0.55f);
        private static readonly Color ColWarning = new Color(1.00f, 0.78f, 0.10f);
        private static readonly Color ColAlert   = new Color(1.00f, 0.28f, 0.28f);
        private static readonly Color ColBg      = new Color(0.05f, 0.12f, 0.25f, 0.90f);
        private static readonly Color ColDiv     = new Color(0.15f, 0.35f, 0.65f, 0.60f);
        private static readonly Color ColAccent  = new Color(0.30f, 0.85f, 1.00f);
        private const float P = 14f;

        // self: WearPanel 자신의 RectTransform (PartInfoPanel이 위치 잡아서 전달)
        public void Build(RectTransform self)
        {
            float y = 0f;

            // ── 구분선 ──
            Div(self, y); y -= 10f;

            // ── 헤더 ──
            MakeText(self, "▪ 마모 예측 (AMWI)", y, 18f, 11, FontStyle.Bold, ColAccent, false);
            y -= 22f;

            // ── AMWI 게이지 바 ──
            _amwiFill = MakeBar(self, y, 12f);
            y -= 16f;

            // ── AMWI 값 (좌) + 심각도 (우) ──
            _amwiLabel    = MakeText(self, "AMWI: —", y, 16f, 11, FontStyle.Normal, Color.white, false);
            _severityLabel = MakeText(self, "—",      y, 16f, 10, FontStyle.Bold,   ColNormal,   true);
            y -= 20f;

            // ── 4 팩터 미니 바 ──
            string[] fNames = { "GP", "CT", "BI", "RL" };
            for (int i = 0; i < 4; i++)
            {
                var (fill, lbl) = MakeFactor(self, fNames[i], i, y);
                _factorFills.Add(fill);
                _factorLabels.Add(lbl);
            }
            y -= 36f;

            // ── 구분선 ──
            Div(self, y); y -= 10f;

            // ── 누적 마모 헤더 ──
            MakeText(self, "▪ 누적 마모량", y, 16f, 10, FontStyle.Bold, ColAccent, false);
            y -= 18f;

            // ── 마모 게이지 바 ──
            _wearFill = MakeBar(self, y, 10f);
            _wearFill.color = new Color(1f, 0.55f, 0.2f);
            y -= 14f;

            // ── 잔여 수명 텍스트 ──
            _lifeLabel = MakeText(self, "—", y, 36f, 10, FontStyle.Normal,
                new Color(0.72f, 0.88f, 0.96f), false);

            gameObject.SetActive(false);
        }

        public void Show(WearData d)
        {
            if (d == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);

            Color c = d.Severity == WearSeverity.Alert   ? ColAlert
                    : d.Severity == WearSeverity.Warning ? ColWarning
                    : ColNormal;

            // AMWI 게이지
            _amwiFill.fillAmount = Mathf.Clamp01(d.AMWI);
            _amwiFill.color = c;
            _amwiLabel.text = $"AMWI: {d.AMWI:F3}";

            // 심각도 배지
            _severityLabel.text  = d.Severity == WearSeverity.Alert   ? "● 경보"
                                 : d.Severity == WearSeverity.Warning ? "● 주의" : "● 정상";
            _severityLabel.color = c;

            // 팩터 바 (GP / CT / BI / RL)
            float[] vals  = { d.GP, d.CT, d.BI, d.RL };
            string[] keys = { "GP", "CT", "BI", "RL" };
            for (int i = 0; i < 4; i++)
            {
                _factorFills[i].fillAmount = vals[i];
                _factorFills[i].color = c;
                _factorLabels[i].text = $"{keys[i]}\n{vals[i]:F2}";
            }

            // 누적 마모 게이지
            _wearFill.fillAmount = d.WearPercent;

            // 잔여 수명
            int days = d.RemainingDays;
            string lifeStr = days < 9999
                ? $"잔여 ≈ {days}일 ({days / 30}개월)  •  {d.DailyWearRateMm:F3} mm/day"
                : "";
            _lifeLabel.text = $"{d.WornMm:F1} / {d.WearLimitMm:F0} mm  ({d.WearPercent * 100f:F0}%)\n{lifeStr}";
        }

        public void Hide() => gameObject.SetActive(false);

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private Image MakeBar(RectTransform parent, float y, float h)
        {
            // 배경
            var bg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.SetParent(parent, false);
            bgRt.anchorMin = new Vector2(0, 1); bgRt.anchorMax = new Vector2(1, 1);
            bgRt.pivot = new Vector2(0.5f, 1);
            bgRt.anchoredPosition = new Vector2(0, y);
            bgRt.sizeDelta = new Vector2(-P * 2, h);
            bg.GetComponent<Image>().color = ColBg;

            // Fill (Filled Image)
            var fill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.SetParent(bgRt, false);
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f); fillRt.offsetMax = new Vector2(-2f, -2f);
            var img = fill.GetComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 0f;
            img.color = ColNormal;
            return img;
        }

        private static (Image fill, Text lbl) MakeFactor(RectTransform parent,
            string name, int idx, float y)
        {
            float colW    = 0.25f;
            float xMin    = idx * colW;
            float xMax    = xMin + colW;
            float textH   = 22f;
            float barH    = 10f;
            float inner   = 2f;

            // 라벨 텍스트
            var lblGo = new GameObject("FL_" + name, typeof(RectTransform));
            var lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.SetParent(parent, false);
            lblRt.anchorMin = new Vector2(xMin, 1); lblRt.anchorMax = new Vector2(xMax, 1);
            lblRt.pivot = new Vector2(0.5f, 1);
            lblRt.anchoredPosition = new Vector2(0, y);
            lblRt.sizeDelta = new Vector2(-inner * 2, textH);
            var lbl = lblGo.AddComponent<Text>();
            lbl.text = name + "\n—";
            lbl.fontSize = 8;
            lbl.alignment = TextAnchor.UpperCenter;
            lbl.color = new Color(0.72f, 0.85f, 0.92f);
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            lbl.font = UIFonts.Korean;

            // 미니 바 배경
            var bgGo = new GameObject("FB_" + name, typeof(RectTransform), typeof(Image));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(parent, false);
            bgRt.anchorMin = new Vector2(xMin, 1); bgRt.anchorMax = new Vector2(xMax, 1);
            bgRt.pivot = new Vector2(0.5f, 1);
            bgRt.anchoredPosition = new Vector2(0, y - textH);
            bgRt.sizeDelta = new Vector2(-inner * 2, barH);
            bgGo.GetComponent<Image>().color = ColBg;

            // 미니 바 Fill
            var fillGo = new GameObject("FF_" + name, typeof(RectTransform), typeof(Image));
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.SetParent(bgRt, false);
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(1f, 1f); fillRt.offsetMax = new Vector2(-1f, -1f);
            var fill = fillGo.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.color = ColNormal;

            return (fill, lbl);
        }

        private static Text MakeText(RectTransform parent, string text, float y, float h,
            int size, FontStyle style, Color col, bool rightAlign)
        {
            var go = new GameObject("T_" + text.Substring(0, Mathf.Min(8, text.Length)),
                typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(rightAlign ? -P * 0.5f : 0f, y);
            rt.sizeDelta = new Vector2(rightAlign ? -P : 0f, h);
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = col;
            t.alignment = rightAlign ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.font = UIFonts.Korean;
            return t;
        }

        private static void Div(RectTransform parent, float y)
        {
            var go = new GameObject("Div", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(-24f, 1f);
            go.GetComponent<Image>().color = ColDiv;
        }
    }
}
