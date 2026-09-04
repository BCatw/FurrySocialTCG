using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class TurnPhaseDisplayController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerTurnDealController gameFlow;
        [SerializeField] private ResourceExchangeController resourceExchange;
        [SerializeField] private GameObject turnPhasePrefab;
        [SerializeField] private GameObject playerTurnPhase;
        [SerializeField] private GameObject enemyTurnPhase;

        [Header("Colors")]
        [SerializeField] private Color playerBlue = new Color(0.25f, 0.55f, 1f, 1f);
        [SerializeField] private Color refillGreen = new Color(0.25f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color selectionYellow = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color performanceRed = new Color(0.85f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color inactiveGray = new Color(0.4f, 0.4f, 0.4f, 1f);

        private PhaseView playerView;
        private PhaseView enemyView;

        private void Awake()
        {
            FindReferences();
            PrepareViews();
            HideBoth();

            if (gameFlow != null)
            {
                gameFlow.PhaseChanged += HandlePhaseChanged;
                HandlePhaseChanged(gameFlow.CurrentPhase);
            }
            if (resourceExchange != null)
            {
                resourceExchange.RefillStarted += HandleRefillStarted;
                resourceExchange.RefillCompleted += HandleRefillCompleted;
            }
        }

        private void OnDestroy()
        {
            if (gameFlow != null) gameFlow.PhaseChanged -= HandlePhaseChanged;
            if (resourceExchange != null)
            {
                resourceExchange.RefillStarted -= HandleRefillStarted;
                resourceExchange.RefillCompleted -= HandleRefillCompleted;
            }
        }

        private void HandlePhaseChanged(PlayerTurnDealController.Phase phase)
        {
            switch (phase)
            {
                case PlayerTurnDealController.Phase.PlayerDraw:
                    ShowPlayer("TURN", playerBlue);
                    break;
                case PlayerTurnDealController.Phase.ResourceExchange:
                    ShowPlayer("PLAY", playerBlue);
                    break;
                case PlayerTurnDealController.Phase.AttackSelection:
                    ShowPlayer("SELECT", selectionYellow);
                    break;
                case PlayerTurnDealController.Phase.AttackPerformance:
                    ShowPlayer("ATK", performanceRed);
                    break;
                case PlayerTurnDealController.Phase.EnemyDraw:
                    ShowEnemy("DRAW", playerBlue);
                    break;
                case PlayerTurnDealController.Phase.EnemyResourceExchange:
                    ShowEnemy("PLAY", playerBlue);
                    break;
                case PlayerTurnDealController.Phase.EnemyRefill:
                    ShowEnemy("DRAW", refillGreen);
                    break;
                case PlayerTurnDealController.Phase.EnemyAttackSelection:
                    ShowEnemy("SELECT", selectionYellow);
                    break;
                case PlayerTurnDealController.Phase.EnemyAttackPerformance:
                    ShowEnemy("ATK", performanceRed);
                    break;
                default:
                    HideBoth();
                    break;
            }
        }

        private void HandleRefillStarted()
        {
            if (gameFlow != null && gameFlow.CurrentPhase == PlayerTurnDealController.Phase.ResourceExchange)
            {
                ShowPlayer("DRAW", refillGreen);
            }
        }

        private void HandleRefillCompleted()
        {
            if (gameFlow != null && gameFlow.CurrentPhase == PlayerTurnDealController.Phase.ResourceExchange)
            {
                ShowPlayer("PLAY", playerBlue);
            }
        }

        private void ShowPlayer(string label, Color color)
        {
            playerView.Set(true, label, color);
            enemyView.Set(true, "WAIT", inactiveGray);
        }

        private void ShowEnemy(string label, Color color)
        {
            playerView.Set(true, "WAIT", inactiveGray);
            enemyView.Set(true, label, color);
        }

        private void HideBoth()
        {
            playerView.Set(false, string.Empty, inactiveGray);
            enemyView.Set(false, string.Empty, inactiveGray);
        }

        private void PrepareViews()
        {
            if (playerTurnPhase != null && playerTurnPhase.GetComponentInChildren<Image>(true) == null)
            {
                CreateViewUnder(playerTurnPhase.transform);
            }
            if (enemyTurnPhase != null && enemyTurnPhase.GetComponentInChildren<Image>(true) == null)
            {
                CreateViewUnder(enemyTurnPhase.transform);
            }

            playerView = new PhaseView(playerTurnPhase);
            enemyView = new PhaseView(enemyTurnPhase);
        }

        private void CreateViewUnder(Transform parent)
        {
            if (turnPhasePrefab == null || parent == null) return;
            GameObject instance = Instantiate(turnPhasePrefab, parent);
            instance.name = "TurnPhaseView";
            if (instance.transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        private void FindReferences()
        {
            if (gameFlow == null) gameFlow = GetComponent<PlayerTurnDealController>();
            if (resourceExchange == null) resourceExchange = GetComponent<ResourceExchangeController>();
            if (playerTurnPhase == null) playerTurnPhase = FindSceneObject("PlayerTurnPhase");
            if (enemyTurnPhase == null) enemyTurnPhase = FindSceneObject("EnemyTurnPhase");

            if (gameFlow == null || resourceExchange == null || playerTurnPhase == null || enemyTurnPhase == null)
            {
                Debug.LogError("Turn phase display references are incomplete.", this);
            }
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName) return child.gameObject;
                }
            }
            return null;
        }

        private readonly struct PhaseView
        {
            private readonly GameObject root;
            private readonly Image background;
            private readonly TMP_Text label;

            public PhaseView(GameObject root)
            {
                this.root = root;
                background = root != null ? root.GetComponentInChildren<Image>(true) : null;
                label = root != null ? root.GetComponentInChildren<TMP_Text>(true) : null;
            }

            public void Set(bool visible, string text, Color color)
            {
                if (root == null) return;
                root.SetActive(visible);
                if (background != null) background.color = color;
                if (label != null) label.text = text;
            }
        }
    }
}
