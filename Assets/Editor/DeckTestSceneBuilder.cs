using FurrySocialCard.CardData;
using FurrySocialCard.CardPresentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FurrySocialCard.Editor
{
    public static class DeckTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/DeckTestScene.unity";
        private const string CardPrefabPath = "Assets/Prefabs/CardGroup.prefab";

        [MenuItem("Tools/Furry Social Card/Build Deck Test Scene")]
        public static void BuildDeckTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject systems = new GameObject("Deck Test Systems");
            systems.AddComponent<CardCatalogLoader>();
            CardDeckController deckController = systems.AddComponent<CardDeckController>();

            Canvas canvas = CreateCanvas();
            CreateEventSystem();
            RectTransform displayPosition = CreateRect("Card Display Position", canvas.transform, new Vector2(0, 70), new Vector2(260, 360));
            Button dealButton = CreateButton(canvas.transform);
            TMP_Text statusText = CreateStatusText(canvas.transform);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            CardObject cardPrefab = prefab != null ? prefab.GetComponent<CardObject>() : null;
            if (cardPrefab == null)
            {
                throw new MissingReferenceException($"CardObject is missing on {CardPrefabPath}.");
            }

            DeckTester tester = systems.AddComponent<DeckTester>();
            var serializedTester = new SerializedObject(tester);
            serializedTester.FindProperty("deckController").objectReferenceValue = deckController;
            serializedTester.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            serializedTester.FindProperty("cardDisplayPosition").objectReferenceValue = displayPosition;
            serializedTester.FindProperty("dealButton").objectReferenceValue = dealButton;
            serializedTester.FindProperty("statusText").objectReferenceValue = statusText;
            serializedTester.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Deck test scene created: {ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            BuildDeckTestScene();
        }

        private static Canvas CreateCanvas()
        {
            GameObject gameObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Button CreateButton(Transform parent)
        {
            RectTransform rect = CreateRect("Deal Button", parent, new Vector2(0, -350), new Vector2(260, 80));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color32(47, 107, 180, 255);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            RectTransform labelRect = CreateRect("Label", rect, Vector2.zero, rect.sizeDelta);
            TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "發一張牌";
            label.fontSize = 32;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            return button;
        }

        private static TMP_Text CreateStatusText(Transform parent)
        {
            RectTransform rect = CreateRect("Deck Status", parent, new Vector2(0, 330), new Vector2(400, 70));
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "牌組載入中";
            text.fontSize = 30;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }
    }
}
