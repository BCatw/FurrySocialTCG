using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FurrySocialCard.CardData;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class PlayerTurnDealController : MonoBehaviour
    {
        [SerializeField] private CardDeckController deckController;
        [SerializeField] private CardObject cardPrefab;
        [SerializeField] private RectTransform deckDisplayParent;
        [SerializeField] private RectTransform battlefieldParent;
        [SerializeField] private RectTransform playerHandParent;
        [SerializeField] private RectTransform resourceParent;
        [SerializeField] private Button startButton;
        [SerializeField] private Button drawButton;
        [SerializeField, Min(0f)] private float deckRevealSeconds = 0.5f;
        [SerializeField, Min(0f)] private float cardMoveDurationSeconds = 0.2f;
        [SerializeField, Min(0f)] private float dealIntervalSeconds = 0.12f;
        [SerializeField, Min(1)] private int initialBattlefieldCards = 8;
        [SerializeField, Min(1)] private int initialHandCards = 8;
        [SerializeField, Min(1f)] private float handCardSpacing = 115f;
        [SerializeField, Min(1f)] private float resourceCardSpacing = 115f;

        private readonly List<CardObject> spawnedCards = new List<CardObject>();
        private readonly List<CardObject> battlefieldCards = new List<CardObject>();
        private readonly List<CardObject> handCards = new List<CardObject>();
        private readonly List<CardObject> resourceCards = new List<CardObject>();
        private Coroutine activeRoutine;

        public Phase CurrentPhase { get; private set; } = Phase.WaitingForStart;
        public IReadOnlyList<CardObject> BattlefieldCards => battlefieldCards;
        public IReadOnlyList<CardObject> HandCards => handCards;
        public IReadOnlyList<CardObject> ResourceCards => resourceCards;
        public bool IsAnimating { get; private set; }
        public event Action<Phase> PhaseChanged;
        public event Action ResourceCardsChanged;

        private void Awake()
        {
            startButton?.onClick.AddListener(StartNewGame);
            drawButton?.onClick.AddListener(DrawForPlayer);
            SetDrawButtonEnabled(false);
        }

        private void OnDestroy()
        {
            DOTween.Kill(gameObject);
            startButton?.onClick.RemoveListener(StartNewGame);
            drawButton?.onClick.RemoveListener(DrawForPlayer);
        }

        public void StartNewGame()
        {
            StopAllCoroutines();
            DOTween.Kill(gameObject);
            activeRoutine = null;
            IsAnimating = false;
            ClearDealtCards();
            SetDrawButtonEnabled(false);
            SetPhase(Phase.Initializing);
            activeRoutine = StartCoroutine(InitializeAndDeal());
        }

        public void DrawForPlayer()
        {
            if (CurrentPhase != Phase.PlayerDraw || IsAnimating || activeRoutine != null)
            {
                return;
            }

            SetDrawButtonEnabled(false);
            activeRoutine = StartCoroutine(DrawForPlayerRoutine());
        }

        public void CompleteResourceExchange()
        {
            if (CurrentPhase != Phase.ResourceExchange || IsAnimating)
            {
                return;
            }

            SetDrawButtonEnabled(false);
            SetPhase(Phase.AttackSelection);
        }

        public void CompleteAttackSelection()
        {
            if (CurrentPhase != Phase.AttackSelection)
            {
                return;
            }

            SetPhase(Phase.PlayerDraw);
            SetDrawButtonEnabled(deckController != null && deckController.RemainingCount > 0);
        }

        public IEnumerator DrawToBattlefield(Action<CardObject> completed)
        {
            CardObject drawnCard = null;
            yield return DrawCardToZone(battlefieldParent, Zone.Battlefield, card => drawnCard = card);
            completed?.Invoke(drawnCard);
        }

        public IEnumerator MoveHandCardToBattlefieldAnimated(CardObject card)
        {
            if (card == null || !handCards.Remove(card))
            {
                yield break;
            }

            battlefieldCards.Add(card);
            ReflowHand();
            yield return AnimateExistingCardToParent(card, battlefieldParent, Zone.Battlefield);
        }

        public IEnumerator MoveCardsToResourceAnimated(CardObject first, CardObject second)
        {
            if (first == null || second == null)
            {
                yield break;
            }

            Vector3 firstStart = first.transform.position;
            Vector3 secondStart = second.transform.position;
            RegisterAsResource(first);
            RegisterAsResource(second);

            RectTransform firstRect = PrepareDestination(first, resourceParent, Zone.Resource, out Vector3 firstTarget);
            RectTransform secondRect = PrepareDestination(second, resourceParent, Zone.Resource, out Vector3 secondTarget);
            ReflowResource();
            Canvas.ForceUpdateCanvases();
            firstTarget = firstRect.position;
            secondTarget = secondRect.position;

            RectTransform animationParent = GetAnimationParent();
            firstRect.SetParent(animationParent, true);
            secondRect.SetParent(animationParent, true);
            firstRect.position = firstStart;
            secondRect.position = secondStart;

            IsAnimating = true;
            Sequence sequence = DOTween.Sequence().SetLink(gameObject);
            sequence.Join(firstRect.DOMove(firstTarget, cardMoveDurationSeconds).SetEase(Ease.InOutQuad));
            sequence.Join(secondRect.DOMove(secondTarget, cardMoveDurationSeconds).SetEase(Ease.InOutQuad));
            yield return sequence.WaitForCompletion();

            if (first != null)
            {
                FinishAtParent(first, resourceParent);
            }
            if (second != null)
            {
                FinishAtParent(second, resourceParent);
            }
            ReflowResource();
            IsAnimating = false;
            ResourceCardsChanged?.Invoke();
        }

        private IEnumerator DrawForPlayerRoutine()
        {
            yield return DrawCardToZone(playerHandParent, Zone.Hand, null);
            SetPhase(Phase.ResourceExchange);
            activeRoutine = null;
        }

        private IEnumerator InitializeAndDeal()
        {
            if (deckController == null)
            {
                Debug.LogError("Deck Controller is missing.", this);
                SetPhase(Phase.WaitingForStart);
                activeRoutine = null;
                yield break;
            }

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
                SetPhase(Phase.WaitingForStart);
                activeRoutine = null;
                yield break;
            }

            SetPhase(Phase.DealingBattlefield);
            yield return DealCards(initialBattlefieldCards, battlefieldParent, Zone.Battlefield);

            SetPhase(Phase.DealingPlayerHand);
            yield return DealCards(initialHandCards, playerHandParent, Zone.Hand);

            SetPhase(Phase.PlayerDraw);
            SetDrawButtonEnabled(deckController.RemainingCount > 0);
            activeRoutine = null;
        }

        private IEnumerator DealCards(int count, RectTransform parent, Zone zone)
        {
            for (int index = 0; index < count; index++)
            {
                bool drewCard = false;
                yield return DrawCardToZone(parent, zone, card => drewCard = card != null);
                if (!drewCard)
                {
                    yield break;
                }

                if (dealIntervalSeconds > 0f)
                {
                    yield return new WaitForSeconds(dealIntervalSeconds);
                }
            }
        }

        private IEnumerator DrawCardToZone(RectTransform destination, Zone zone, Action<CardObject> completed)
        {
            CardObject card = CreateCardAtDeck();
            if (card == null)
            {
                completed?.Invoke(null);
                yield break;
            }

            AddToZone(card, zone);
            IsAnimating = true;
            if (deckRevealSeconds > 0f)
            {
                yield return new WaitForSeconds(deckRevealSeconds);
            }

            yield return AnimateExistingCardToParent(card, destination, zone);
            IsAnimating = false;
            completed?.Invoke(card);
        }

        private CardObject CreateCardAtDeck()
        {
            if (deckController == null || cardPrefab == null || deckDisplayParent == null)
            {
                Debug.LogError("Deal controller references are incomplete, including Deck display.", this);
                return null;
            }

            if (!deckController.TryDraw(out CardDefinition definition))
            {
                SetDrawButtonEnabled(false);
                return null;
            }

            CardObject card = Instantiate(cardPrefab, deckDisplayParent);
            FinishAtParent(card, deckDisplayParent);
            card.Bind(definition);
            spawnedCards.Add(card);
            return card;
        }

        private IEnumerator AnimateExistingCardToParent(CardObject card, RectTransform destination, Zone zone)
        {
            if (card == null || destination == null)
            {
                yield break;
            }

            Vector3 startPosition = card.transform.position;
            RectTransform rect = PrepareDestination(card, destination, zone, out Vector3 targetPosition);
            RectTransform animationParent = GetAnimationParent();
            rect.SetParent(animationParent, true);
            rect.position = startPosition;

            IsAnimating = true;
            Tween tween = rect.DOMove(targetPosition, cardMoveDurationSeconds)
                .SetEase(Ease.InOutQuad)
                .SetLink(card.gameObject);
            yield return tween.WaitForCompletion();

            if (card != null)
            {
                FinishAtParent(card, destination);
                ReflowZone(zone);
            }
            IsAnimating = false;
        }

        private RectTransform PrepareDestination(CardObject card, RectTransform destination, Zone zone, out Vector3 targetPosition)
        {
            RectTransform rect = card.transform as RectTransform;
            rect.SetParent(destination, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            ReflowZone(zone);
            Canvas.ForceUpdateCanvases();
            targetPosition = rect.position;
            return rect;
        }

        private void AddToZone(CardObject card, Zone zone)
        {
            switch (zone)
            {
                case Zone.Hand:
                    handCards.Add(card);
                    break;
                case Zone.Battlefield:
                    battlefieldCards.Add(card);
                    break;
                case Zone.Resource:
                    RegisterAsResource(card);
                    break;
            }
        }

        private void RegisterAsResource(CardObject card)
        {
            handCards.Remove(card);
            battlefieldCards.Remove(card);
            if (!resourceCards.Contains(card))
            {
                resourceCards.Add(card);
            }
            card.SetSelected(false);
            card.SetDimmed(false);
            ReflowHand();
        }

        private void ReflowZone(Zone zone)
        {
            if (zone == Zone.Hand)
            {
                ReflowHand();
            }
            else if (zone == Zone.Resource)
            {
                ReflowResource();
            }
        }

        private RectTransform GetAnimationParent()
        {
            Canvas canvas = deckDisplayParent.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : deckDisplayParent.parent as RectTransform;
        }

        private static void FinishAtParent(CardObject card, RectTransform parent)
        {
            RectTransform rect = card.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = Vector2.zero;
        }

        private void ReflowHand()
        {
            ReflowCards(handCards, handCardSpacing);
        }

        private void ReflowResource()
        {
            ReflowCards(resourceCards, resourceCardSpacing);
        }

        private static void ReflowCards(IReadOnlyList<CardObject> cards, float spacing)
        {
            float left = (cards.Count - 1) * spacing * -0.5f;
            for (int index = 0; index < cards.Count; index++)
            {
                if (cards[index] != null && cards[index].transform is RectTransform rect && rect.parent != null)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(left + index * spacing, 0f);
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
                    card.transform.DOKill();
                    card.gameObject.SetActive(false);
                    Destroy(card.gameObject);
                }
            }

            spawnedCards.Clear();
            battlefieldCards.Clear();
            handCards.Clear();
            resourceCards.Clear();
            ResourceCardsChanged?.Invoke();
        }

        private void SetDrawButtonEnabled(bool enabled)
        {
            if (drawButton != null)
            {
                drawButton.interactable = enabled;
            }
        }

        private void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private enum Zone
        {
            Battlefield,
            Hand,
            Resource
        }

        public enum Phase
        {
            WaitingForStart,
            Initializing,
            DealingBattlefield,
            DealingPlayerHand,
            PlayerDraw,
            ResourceExchange,
            AttackSelection
        }
    }
}

