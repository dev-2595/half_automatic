using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HalfAutomatic.UI
{
    /// <summary>uGUI 요소를 코드로 만드는 헬퍼. 씬/프리팹 파일 없이 UI를 구성하기 위한 용도.</summary>
    public static class UiFactory
    {
        // 기본 폰트에는 한글 글리프가 없으므로 OS 폰트를 쓴다.
        // 모바일 빌드 시에는 한글 폰트 에셋을 프로젝트에 포함해야 한다.
        private static readonly string[] KoreanFontNames =
        {
            "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Arial",
        };

        private static Font _font;

        public static readonly Color Background = new Color(0.10f, 0.12f, 0.16f);
        public static readonly Color Panel = new Color(0.17f, 0.20f, 0.26f);
        public static readonly Color Accent = new Color(0.95f, 0.62f, 0.15f);
        public static readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f);
        public static readonly Color ErrorColor = new Color(1.0f, 0.45f, 0.45f);

        private static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(KoreanFontNames, 32);
                }
                return _font;
            }
        }

        /// <summary>세로 1080x1920 기준으로 스케일되는 캔버스와, 없으면 EventSystem을 만든다.</summary>
        public static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem(parent);
            return canvas;
        }

        /// <summary>화면 전체를 덮는 배경 패널 + 세로 중앙 정렬 레이아웃을 가진 화면 루트.</summary>
        public static RectTransform CreateScreenRoot(string name, Transform canvas)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(canvas, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            go.GetComponent<Image>().color = Background;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 32f;
            layout.padding = new RectOffset(96, 96, 96, 96);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static Text CreateLabel(Transform parent, string text, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            ConfigureText(label, text, fontSize, color, alignment);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button CreateButton(Transform parent, string text, Color fill)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = fill;
            go.GetComponent<Button>().targetGraphic = image;
            go.GetComponent<LayoutElement>().minHeight = 140f;

            Text label = CreateChildText(go.transform, "Text", text, 44, Background, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return go.GetComponent<Button>();
        }

        public static InputField CreateInputField(Transform parent, string placeholder, int characterLimit)
        {
            var go = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = Panel;
            go.GetComponent<LayoutElement>().minHeight = 140f;

            Text text = CreateChildText(go.transform, "Text", string.Empty, 44, TextColor, TextAnchor.MiddleLeft);
            Text hint = CreateChildText(go.transform, "Placeholder", placeholder, 44,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.4f), TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 32f);
            Stretch(hint.rectTransform, 32f);

            var input = go.GetComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = characterLimit;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private static Text CreateChildText(Transform parent, string name, string value, int size, Color color,
            TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            ConfigureText(text, value, size, color, alignment);
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureText(Text text, string value, int size, Color color, TextAnchor alignment)
        {
            text.font = DefaultFont;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, 0f);
            rect.offsetMax = new Vector2(-padding, 0f);
        }

        private static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(parent, false);

            // 프로젝트에 Input System 패키지가 있으면 그 입력 모듈을, 없으면 기본(legacy) 모듈을 쓴다.
            System.Type inputSystemModule = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                go.AddComponent(inputSystemModule);
            }
            else
            {
                go.AddComponent<StandaloneInputModule>();
            }
        }
    }
}
