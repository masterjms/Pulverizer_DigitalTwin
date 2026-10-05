using UnityEngine;
using UnityEngine.UI;

namespace CoalPulverizer.UI
{
    // 미분기 단면 도면을 Canvas UI 요소로 그리고, 선택된 구역을 하이라이트함
    public sealed class BlueprintPanel : MonoBehaviour
    {
        private RectTransform[] _zones;
        private Image[] _zoneImages;
        private int _activeZone = -1;

        // 도면 구역 색상 (비활성 / 활성)
        private static readonly Color ZoneBase    = new Color(0.08f, 0.15f, 0.32f, 0.85f);
        private static readonly Color ZoneHover   = new Color(0.20f, 0.55f, 1.00f, 1.00f);
        private static readonly Color ZoneBg      = new Color(0.02f, 0.05f, 0.12f, 1.00f);
        private static readonly Color GridLine     = new Color(0.10f, 0.25f, 0.45f, 0.50f);

        public void Build(RectTransform parent)
        {
            // 배경
            var bg = CreateImage(parent, "BP_Bg", ZoneBg,
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            AddGridLines(bg.rectTransform, 5, 8);

            // 총 15개 구역 (BlueprintZone 인덱스와 동일)
            // 레이아웃: 단면도를 세로로 표현 (하단 = 기어, 상단 = 분급기)
            _zones      = new RectTransform[15];
            _zoneImages = new Image[15];

            // Zone 0: 기반 (Foundation)
            CreateZone(bg.rectTransform, 0,  new Vector2(0.05f,0.01f), new Vector2(0.95f,0.07f), "기반");
            // Zone 1: 기어 감속기
            CreateZone(bg.rectTransform, 1,  new Vector2(0.30f,0.07f), new Vector2(0.70f,0.20f), "기어");
            // Zone 2: 볼 테이블
            CreateZone(bg.rectTransform, 2,  new Vector2(0.20f,0.20f), new Vector2(0.80f,0.30f), "볼 테이블");
            // Zone 3: 불 링
            CreateZone(bg.rectTransform, 3,  new Vector2(0.18f,0.27f), new Vector2(0.82f,0.32f), "불 링");
            // Zone 4: 롤 타이어 (좌우)
            CreateZoneDouble(bg.rectTransform, 4,
                new Vector2(0.04f,0.28f), new Vector2(0.24f,0.46f),
                new Vector2(0.76f,0.28f), new Vector2(0.96f,0.46f), "롤");
            // Zone 5: (예약)
            CreateZone(bg.rectTransform, 5,  new Vector2(0.38f,0.28f), new Vector2(0.62f,0.34f), "");
            // Zone 6: 1차 공기 덕트 (좌측)
            CreateZone(bg.rectTransform, 6,  new Vector2(0.00f,0.36f), new Vector2(0.12f,0.44f), "1차 공기");
            // Zone 7: 하우징 (외벽)
            CreateZoneRing(bg.rectTransform, 7);
            // Zone 8: 피드 스커트
            CreateZone(bg.rectTransform, 8,  new Vector2(0.36f,0.42f), new Vector2(0.64f,0.52f), "스커트");
            // Zone 9: 분급기 콘
            CreateZone(bg.rectTransform, 9,  new Vector2(0.28f,0.54f), new Vector2(0.72f,0.72f), "분급기");
            // Zone 10: 분급기 케이지
            CreateZone(bg.rectTransform, 10, new Vector2(0.22f,0.68f), new Vector2(0.78f,0.76f), "케이지");
            // Zone 11: 원료탄 투입관
            CreateZone(bg.rectTransform, 11, new Vector2(0.42f,0.72f), new Vector2(0.58f,0.95f), "투입관");
            // Zone 12: 배출 파이프 (좌우)
            CreateZoneDouble(bg.rectTransform, 12,
                new Vector2(0.04f,0.78f), new Vector2(0.24f,0.88f),
                new Vector2(0.76f,0.78f), new Vector2(0.96f,0.88f), "배출");
            // Zone 13: 유압 가압 (외부)
            CreateZoneDouble(bg.rectTransform, 13,
                new Vector2(0.00f,0.44f), new Vector2(0.06f,0.62f),
                new Vector2(0.94f,0.44f), new Vector2(1.00f,0.62f), "유압");
            // Zone 14: 파이라이트 배출 박스
            CreateZone(bg.rectTransform, 14, new Vector2(0.70f,0.07f), new Vector2(0.95f,0.16f), "배출");
        }

        public void Highlight(int zone)
        {
            if (_activeZone == zone) return;
            _activeZone = zone;
            for (int i = 0; i < _zoneImages.Length; i++)
            {
                if (_zoneImages[i] == null) continue;
                _zoneImages[i].color = (i == zone) ? ZoneHover : ZoneBase;
            }
        }

        public void ResetHighlight()
        {
            _activeZone = -1;
            for (int i = 0; i < _zoneImages.Length; i++)
            {
                if (_zoneImages[i] != null) _zoneImages[i].color = ZoneBase;
            }
        }

        // ─── helpers ─────────────────────────────────────────────

        private void CreateZone(RectTransform parent, int zoneIdx,
            Vector2 anchorMin, Vector2 anchorMax, string label)
        {
            var img = CreateImage(parent, "Z" + zoneIdx, ZoneBase, anchorMin, anchorMax,
                Vector2.zero, Vector2.zero);
            AddOutline(img.rectTransform);
            AddLabel(img.rectTransform, label);
            if (_zones[zoneIdx] == null)
            {
                _zones[zoneIdx]      = img.rectTransform;
                _zoneImages[zoneIdx] = img;
            }
        }

        // Zone을 좌/우 두 개로 분리 표현 (롤 타이어, 배출관 등)
        private void CreateZoneDouble(RectTransform parent, int zoneIdx,
            Vector2 amin1, Vector2 amax1, Vector2 amin2, Vector2 amax2, string label)
        {
            var img1 = CreateImage(parent, "Z" + zoneIdx + "L", ZoneBase, amin1, amax1, Vector2.zero, Vector2.zero);
            var img2 = CreateImage(parent, "Z" + zoneIdx + "R", ZoneBase, amin2, amax2, Vector2.zero, Vector2.zero);
            AddOutline(img1.rectTransform);
            AddOutline(img2.rectTransform);
            AddLabel(img1.rectTransform, label);

            if (_zones[zoneIdx] == null)
            {
                _zones[zoneIdx]      = img1.rectTransform;
                _zoneImages[zoneIdx] = img1;
            }
            // 두 번째 이미지도 같이 색 변경하도록 별도 처리
            var syncIdx = zoneIdx;
            var img2Ref = img2;
            // 동기화: Highlight() 에서 img2도 바꾸도록 추가 등록
            RegisterSecondary(syncIdx, img2Ref);
        }

        // 하우징은 얇은 링 형태로
        private void CreateZoneRing(RectTransform parent, int zoneIdx = 7)
        {
            var left  = CreateImage(parent, "Z7L", ZoneBase, new Vector2(0,0.20f), new Vector2(0.07f,0.80f), Vector2.zero, Vector2.zero);
            var right = CreateImage(parent, "Z7R", ZoneBase, new Vector2(0.93f,0.20f), new Vector2(1,0.80f), Vector2.zero, Vector2.zero);
            _zones[zoneIdx]      = left.rectTransform;
            _zoneImages[zoneIdx] = left;
            RegisterSecondary(zoneIdx, right);
        }

        private readonly System.Collections.Generic.List<(int, Image)> _secondary = new();

        private void RegisterSecondary(int zone, Image img)
        {
            _secondary.Add((zone, img));
        }

        // Highlight을 오버라이드해서 secondary도 처리
        // (Start에서 호출하면 Build 이후이므로 안전)
        private void LateUpdate()
        {
            if (_activeZone < 0) return;
            foreach (var (zone, img) in _secondary)
            {
                if (img == null) continue;
                img.color = (zone == _activeZone) ? ZoneHover : ZoneBase;
            }
        }

        private static Image CreateImage(RectTransform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin  = anchorMin;
            rt.anchorMax  = anchorMax;
            rt.offsetMin  = offsetMin;
            rt.offsetMax  = offsetMax;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void AddOutline(RectTransform rt)
        {
            var outlineGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var ort = outlineGo.GetComponent<RectTransform>();
            ort.SetParent(rt, false);
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;
            var img = outlineGo.GetComponent<Image>();
            img.color = GridLine;
            // 테두리만 남기기 위해 중앙을 투명하게 (Outline 컴포넌트 대신 단순 테두리 이미지 사용)
            img.type = Image.Type.Sliced;
        }

        private static void AddLabel(RectTransform parent, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // TextMesh Pro가 없을 경우 기본 Text 사용
            var textComp = go.AddComponent<UnityEngine.UI.Text>();
            textComp.text = text;
            textComp.fontSize = 9;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = new Color(0.6f, 0.8f, 1f, 0.8f);
            textComp.font = UIFonts.Korean;
        }

        private static void AddGridLines(RectTransform parent, int cols, int rows)
        {
            for (int i = 1; i < cols; i++)
            {
                float x = i / (float)cols;
                CreateImage(parent, "GL_V" + i, GridLine,
                    new Vector2(x - 0.002f, 0), new Vector2(x + 0.002f, 1),
                    Vector2.zero, Vector2.zero);
            }
            for (int i = 1; i < rows; i++)
            {
                float y = i / (float)rows;
                CreateImage(parent, "GL_H" + i, GridLine,
                    new Vector2(0, y - 0.001f), new Vector2(1, y + 0.001f),
                    Vector2.zero, Vector2.zero);
            }
        }
    }
}
