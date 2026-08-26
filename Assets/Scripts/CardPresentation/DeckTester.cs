using FurrySocialCard.CardData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class DeckTester : MonoBehaviour
    {
        [SerializeField] private CardDeckController deckController;
        [SerializeField] private CardObject cardPrefab;
        [SerializeField] private RectTransform cardDisplayPosition;
        [SerializeField] private Button dealButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private bool replaceDisplayedCard = true;

        private CardObject displayedCard;

        private void Awake()
        {
            if (dealButton != null)
            {
                dealButton.onClick.AddListener(DealNextCard);
            }
        }

        private void OnEnable()
        {
            if (deckController == null)
            {
                return;
            }

            deckController.Ready += HandleDeckReady;
            deckController.RemainingCountChanged += UpdateStatus;
            UpdateStatus(deckController.RemainingCount);
        }

        private void OnDisable()
        {
            if (deckController == null)
            {
                return;
            }

            deckController.Ready -= HandleDeckReady;
            deckController.RemainingCountChanged -= UpdateStatus;
        }

        private void OnDestroy()
        {
            if (dealButton != null)
            {
                dealButton.onClick.RemoveListener(DealNextCard);
            }
        }

        public void DealNextCard()
        {
            if (deckController == null || cardPrefab == null || cardDisplayPosition == null)
            {
                Debug.LogError("DeckTester requires Deck Controller, Card Prefab, and Card Display Position.", this);
                return;
            }

            if (!deckController.TryDraw(out CardDefinition card))
            {
                UpdateStatus(0);
                return;
            }

            if (replaceDisplayedCard && displayedCard != null)
            {
                Destroy(displayedCard.gameObject);
            }

            displayedCard = Instantiate(cardPrefab, cardDisplayPosition);
            RectTransform cardRect = displayedCard.transform as RectTransform;
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.localRotation = Quaternion.identity;
                cardRect.localScale = Vector3.one;
            }

            displayedCard.Bind(card);
        }

        [ContextMenu("Shuffle And Clear")]
        public void ShuffleAndClear()
        {
            if (deckController != null)
            {
                deckController.Shuffle();
            }

            if (displayedCard != null)
            {
                Destroy(displayedCard.gameObject);
                displayedCard = null;
            }
        }

        private void HandleDeckReady()
        {
            UpdateStatus(deckController.RemainingCount);
        }

        private void UpdateStatus(int remaining)
        {
            if (dealButton != null)
            {
                dealButton.interactable = deckController != null && deckController.IsReady && remaining > 0;
            }

            if (statusText != null)
            {
                statusText.text = deckController != null && deckController.IsReady
                    ? $"剩餘 {remaining} 張"
                    : "牌組載入中";
            }
        }
    }
}
