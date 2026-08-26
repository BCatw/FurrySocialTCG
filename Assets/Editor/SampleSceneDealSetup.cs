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
            RectTransform battlefield = RequireObject("bafuda").transform.Find("Layout") as RectTransform;
            RectTransform hand = RequireObject("tefuda").transform as RectTransform;
            Button startButton = RequireObject("StartBtn").GetComponent<Button>();
            Button drawButton = RequireObject("DrawBtn").GetComponent<Button>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            CardObject cardPrefab = prefab != null ? prefab.GetComponent<CardObject>() : null;

            if (battlefield == null || hand == null || startButton == null || drawButton == null || cardPrefab == null)
            {
                throw new MissingReferenceException("SampleScene deal-flow references are incomplete.");
            }

            GameObject systems = GameObject.Find("Card Game Flow");
            if (systems == null)
            {
                systems = new GameObject("Card Game Flow");
            }

            CardCatalogLoader loader = GetOrAdd<CardCatalogLoader>(systems);
            CardDeckController deck = GetOrAdd<CardDeckController>(systems);
            PlayerTurnDealController flow = GetOrAdd<PlayerTurnDealController>(systems);

            var deckSerialized = new SerializedObject(deck);
            deckSerialized.FindProperty("catalogLoader").objectReferenceValue = loader;
            deckSerialized.FindProperty("initializeOnStart").boolValue = false;
            deckSerialized.FindProperty("shuffleOnInitialize").boolValue = true;
            deckSerialized.ApplyModifiedPropertiesWithoutUndo();

            var flowSerialized = new SerializedObject(flow);
            flowSerialized.FindProperty("deckController").objectReferenceValue = deck;
            flowSerialized.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            flowSerialized.FindProperty("battlefieldParent").objectReferenceValue = battlefield;
            flowSerialized.FindProperty("playerHandParent").objectReferenceValue = hand;
            flowSerialized.FindProperty("startButton").objectReferenceValue = startButton;
            flowSerialized.FindProperty("drawButton").objectReferenceValue = drawButton;
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("SampleScene deal flow configured.");
        }

        public static void SetupFromCommandLine()
        {
            Setup();
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
