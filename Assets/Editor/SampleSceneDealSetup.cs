using FurrySocialCard.CardData;
using FurrySocialCard.CardPresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FurrySocialCard.Editor
{
    public static class SampleSceneDealSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CardPrefabPath = "Assets/Prefabs/CardGroup.prefab";

        [MenuItem("Tools/Furry Social Card/Setup Sample Scene Deal Flow")]
        public static void Setup()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RectTransform deckDisplay = RequireObject("Deck").transform as RectTransform;
            RectTransform battlefield = RequireObject("bafuda").transform.Find("Layout") as RectTransform;
            RectTransform hand = RequireObject("tefuda").transform as RectTransform;
            RectTransform resource = FindOrCreateResource();
            Button startButton = RequireObject("StartBtn").GetComponent<Button>();
            Button drawButton = RequireObject("DrawBtn").GetComponent<Button>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            CardObject cardPrefab = prefab != null ? prefab.GetComponent<CardObject>() : null;

            if (deckDisplay == null || battlefield == null || hand == null || resource == null || startButton == null || drawButton == null || cardPrefab == null)
            {
                throw new MissingReferenceException("SampleScene deal-flow references are incomplete.");
            }

            Image clickSurface = GetOrAdd<Image>(battlefield.gameObject);
            clickSurface.color = new Color(1f, 1f, 1f, 0f);
            clickSurface.raycastTarget = true;
            GetOrAdd<BattlefieldClickArea>(battlefield.gameObject);

            GameObject systems = GameObject.Find("Card Game Flow");
            if (systems == null)
            {
                systems = new GameObject("Card Game Flow");
            }

            CardCatalogLoader loader = GetOrAdd<CardCatalogLoader>(systems);
            CardDeckController deck = GetOrAdd<CardDeckController>(systems);
            PlayerTurnDealController flow = GetOrAdd<PlayerTurnDealController>(systems);
            ResourceExchangeController exchange = GetOrAdd<ResourceExchangeController>(systems);

            var deckSerialized = new SerializedObject(deck);
            deckSerialized.FindProperty("catalogLoader").objectReferenceValue = loader;
            deckSerialized.FindProperty("initializeOnStart").boolValue = false;
            deckSerialized.FindProperty("shuffleOnInitialize").boolValue = true;
            deckSerialized.ApplyModifiedPropertiesWithoutUndo();

            var flowSerialized = new SerializedObject(flow);
            flowSerialized.FindProperty("deckController").objectReferenceValue = deck;
            flowSerialized.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            flowSerialized.FindProperty("deckDisplayParent").objectReferenceValue = deckDisplay;
            flowSerialized.FindProperty("battlefieldParent").objectReferenceValue = battlefield;
            flowSerialized.FindProperty("playerHandParent").objectReferenceValue = hand;
            flowSerialized.FindProperty("resourceParent").objectReferenceValue = resource;
            flowSerialized.FindProperty("startButton").objectReferenceValue = startButton;
            flowSerialized.FindProperty("drawButton").objectReferenceValue = drawButton;
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();

            var exchangeSerialized = new SerializedObject(exchange);
            exchangeSerialized.FindProperty("gameFlow").objectReferenceValue = flow;
            exchangeSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("SampleScene deal and resource-exchange flow configured.");
        }

        public static void SetupFromCommandLine()
        {
            Setup();
        }

        private static RectTransform FindOrCreateResource()
        {
            GameObject existing = GameObject.Find("Resource");
            if (existing != null)
            {
                GridLayoutGroup existingLayout = existing.GetComponent<GridLayoutGroup>();
                if (existingLayout != null)
                {
                    Object.DestroyImmediate(existingLayout);
                }

                RectTransform existingRect = existing.transform as RectTransform;
                existingRect.sizeDelta = new Vector2(720f, 250f);
                return existingRect;
            }

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                throw new MissingReferenceException("Canvas not found.");
            }

            var gameObject = new GameObject("Resource", typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(650f, -260f);
            rect.sizeDelta = new Vector2(720f, 250f);

            Image image = gameObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.12f);
            image.raycastTarget = false;

            return rect;
        }

        private static GameObject RequireObject(string name)
        {
            GameObject result = GameObject.Find(name);
            if (result == null)
            {
                throw new MissingReferenceException($"GameObject not found: {name}");
            }

            return result;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}

