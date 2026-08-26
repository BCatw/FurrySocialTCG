using System.Collections;
using System.Collections.Generic;
using FurrySocialCard.CardData;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class PlayerTurnDealController : MonoBehaviour
    {
        [SerializeField] private CardDeckController deckController;
        [SerializeField] private CardObject cardPrefab;
        [SerializeField] private RectTransform battlefieldParent;
        [SerializeField] private RectTransform playerHandParent;
        [SerializeField] private Button startButton;
        [SerializeField] private Button drawButton;
        [SerializeField, Min(0f)] private float dealIntervalSeconds = 0.12f;
        [SerializeField, Min(1)] private int initialBattlefieldCards = 8;
        [SerializeField, Min(1)] private int initialHandCards = 8;
        [SerializeField, Min(1f)] private float handCardSpacing = 115f;

        private readonly List<CardObject> spawnedCards = new List<CardObject>();
        private readonly List<CardObject> handCards = new List<CardObject>();
        private Coroutine dealingRoutine;

        public Phase CurrentPhase { get; private set; } = Phase.WaitingForStart;

        private void Awake()
        {
            startButton?.onClick.AddListener(StartNewGame);
            drawButton?.onClick.AddListener(DrawForPlayer);
            SetDrawButtonEnabled(false);
        }

        private void OnDestroy()
        {
            startButton?.onClick.RemoveListener(StartNewGame);
            drawButton?.onClick.RemoveListener(DrawForPlayer);
        }

        public void StartNewGame()
        {
            if (dealingRoutine != null)
            {
                StopCoroutine(dealingRoutine);
            }

            ClearDealtCards();
            SetDrawButtonEnabled(false);
            CurrentPhase = Phase.Initializing;
            dealingRoutine = StartCoroutine(InitializeAndDeal());
        }

        public void DrawForPlayer()
        {
            if (CurrentPhase != Phase.PlayerDraw || !TryDealCard(playerHandParent, out CardObject card))
            {
                return;
            }

            handCards.Add(card);
            ReflowHand();
            SetDrawButtonEnabled(deckController.RemainingCount > 0);
        }

        private IEnumerator InitializeAndDeal()
        {
            if (!deckController.IsReady)
            {
                yield return deckController.Initialize();
            }
            else
            {
                deckController.Shuffle();
            }

            if (!deckController.IsReady)
            {
                Debug.LogError("Deck could not be initialized.", this);
                CurrentPhase = Phase.WaitingForStart;
                dealingRoutine = null;
                yield break;
            }

            CurrentPhase = Phase.DealingBattlefield;
            yield return DealCards(initialBattlefieldCards, battlefieldParent, false);

            CurrentPhase = Phase.DealingPlayerHand;
            yield return DealCards(initialHandCards, playerHandParent, true);

            CurrentPhase = Phase.PlayerDraw;
            SetDrawButtonEnabled(deckController.RemainingCount > 0);
            dealingRoutine = null;
        }

        private IEnumerator DealCards(int count, RectTransform parent, bool addToHand)
        {
            for (int index = 0; index < count; index++)
            {
                if (!TryDealCard(parent, out CardObject card))
                {
                    yield break;
                }

                if (addToHand)
                {
                    handCards.Add(card);
                    ReflowHand();
                }

                if (dealIntervalSeconds > 0f)
                {
                    yield return new WaitForSeconds(dealIntervalSeconds);
                }
                else
                {
                    yield return null;
                }
            }
        }

        private bool TryDealCard(RectTransform parent, out CardObject cardObject)
        {
            cardObject = null;
            if (deckController == null || cardPrefab == null || parent == null)
            {
                Debug.LogError("Deal controller references are incomplete.", this);
                return false;
            }

            if (!deckController.TryDraw(out CardDefinition card))
            {
                SetDrawButtonEnabled(false);
                return false;
            }

            cardObject = Instantiate(cardPrefab, parent);
            RectTransform rect = cardObject.transform as RectTransform;
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.anchoredPosition = Vector2.zero;
            }

            cardObject.Bind(card);
            spawnedCards.Add(cardObject);
            return true;
        }

        private void ReflowHand()
        {
            float left = (handCards.Count - 1) * handCardSpacing * -0.5f;
            for (int index = 0; index < handCards.Count; index++)
            {
                if (handCards[index] != null && handCards[index].transform is RectTransform rect)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(left + index * handCardSpacing, 0f);
                    rect.SetSiblingIndex(index);
                }
            }
        }

        private void ClearDealtCards()
        {
            foreach (CardObject card in spawnedCards)
            {
                if (card != null)
                {
                    card.gameObject.SetActive(false);
                    Destroy(card.gameObject);
                }
            }

            spawnedCards.Clear();
            handCards.Clear();
        }

        private void SetDrawButtonEnabled(bool enabled)
        {
            if (drawButton != null)
            {
                drawButton.interactable = enabled;
            }
        }

        public enum Phase
        {
            WaitingForStart,
            Initializing,
            DealingBattlefield,
            DealingPlayerHand,
            PlayerDraw
        }
    }
}
