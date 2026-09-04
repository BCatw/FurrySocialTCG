using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FurrySocialCard.CardData;
using TMPro;
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
        [Tooltip("Optional. If empty, an enemy resource row is created opposite the player Resource row at runtime.")]
        [SerializeField] private RectTransform enemyResourceParent;
        [SerializeField] private Button startButton;
        [SerializeField] private Button drawButton;
        [Header("Turn Information")]
        [SerializeField] private GameObject turnInfoObject;
        [SerializeField] private TMP_Text turnInfoText;
        [SerializeField, Min(0f)] private float turnInfoSeconds = 1.5f;
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
        private readonly HashSet<CardObject> tappedResourceCards = new HashSet<CardObject>();
        private readonly List<CardObject> enemyResourceCards = new List<CardObject>();
        private readonly HashSet<CardObject> tappedEnemyResourceCards = new HashSet<CardObject>();
        private Coroutine activeRoutine;

        public Phase CurrentPhase { get; private set; } = Phase.WaitingForStart;
        public IReadOnlyList<CardObject> BattlefieldCards => battlefieldCards;
        public IReadOnlyList<CardObject> HandCards => handCards;
        public IReadOnlyList<CardObject> ResourceCards => resourceCards;
        public IReadOnlyList<CardObject> EnemyResourceCards => enemyResourceCards;
        public bool IsAnimating { get; private set; }
        public event Action<Phase> PhaseChanged;
        public event Action ResourceCardsChanged;
        public event Action EnemyResourceCardsChanged;

        private void Awake()
        {
            startButton?.onClick.AddListener(StartNewGame);
            drawButton?.onClick.AddListener(DrawForPlayer);
            SetDrawButtonEnabled(false);
            if (turnInfoObject != null) turnInfoObject.SetActive(false);
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
            if (CurrentPhase != Phase.ResourceExchange || IsAnimating || activeRoutine != null)
            {
                return;
            }

            SetDrawButtonEnabled(false);
            activeRoutine = StartCoroutine(TestDrawForPlayerRoutine());
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

            UntapAllResources();
            SetPhase(Phase.EnemyDraw);
        }

        public void BeginEnemyResourceExchange() => SetPhase(Phase.EnemyResourceExchange);
        public void BeginEnemyAttack() => SetPhase(Phase.EnemyAttack);

        public void CompleteEnemyTurn()
        {
            if (CurrentPhase != Phase.EnemyAttack) return;
            UntapAllEnemyResources();
            SetDrawButtonEnabled(false);
            activeRoutine = StartCoroutine(BeginPlayerTurnRoutine());
        }

        public IEnumerator ShowTurnInfo(string message)
        {
            if (turnInfoObject == null) yield break;
            if (turnInfoText != null) turnInfoText.text = message;
            turnInfoObject.SetActive(true);
            if (turnInfoSeconds > 0f) yield return new WaitForSeconds(turnInfoSeconds);
            turnInfoObject.SetActive(false);
        }

        public bool TryDrawDefinition(out CardDefinition definition)
        {
            definition = null;
            return deckController != null && deckController.TryDraw(out definition);
        }

        public List<CardObject> GetAvailableResourceCardsSnapshot()
        {
            var result = new List<CardObject>();
            foreach (CardObject card in resourceCards)
            {
                if (card != null && !tappedResourceCards.Contains(card)) result.Add(card);
            }
            return result;
        }

        public List<CardObject> GetAvailableEnemyResourceCardsSnapshot()
        {
            var result = new List<CardObject>();
            foreach (CardObject card in enemyResourceCards)
            {
                if (card != null && !tappedEnemyResourceCards.Contains(card)) result.Add(card);
            }
            return result;
        }

        public void TapResources(IEnumerable<CardObject> cards)
        {
            if (cards == null) return;
            foreach (CardObject card in cards)
            {
                if (card != null && resourceCards.Contains(card))
                {
                    tappedResourceCards.Add(card);
                    card.SetDimmed(true);
                }
            }
            ResourceCardsChanged?.Invoke();
        }

        public void ConsumeResources(IEnumerable<CardObject> cards)
        {
            if (cards == null) return;
            bool changed = false;
            foreach (CardObject card in new List<CardObject>(cards))
            {
                if (card == null || !resourceCards.Remove(card)) continue;
                tappedResourceCards.Remove(card);
                spawnedCards.Remove(card);
                card.transform.DOKill();
                Destroy(card.gameObject);
                changed = true;
            }
            if (changed)
            {
                ReflowResource();
                ResourceCardsChanged?.Invoke();
            }
        }

        public void TapEnemyResources(IEnumerable<CardObject> cards)
        {
            if (cards == null) return;
            foreach (CardObject card in cards)
            {
                if (card != null && enemyResourceCards.Contains(card))
                {
                    tappedEnemyResourceCards.Add(card);
                    card.SetDimmed(true);
                }
            }
            EnemyResourceCardsChanged?.Invoke();
        }

        public void ConsumeEnemyResources(IEnumerable<CardObject> cards)
        {
            if (cards == null) return;
            bool changed = false;
            foreach (CardObject card in new List<CardObject>(cards))
            {
                if (card == null || !enemyResourceCards.Remove(card)) continue;
                tappedEnemyResourceCards.Remove(card);
                spawnedCards.Remove(card);
                card.transform.DOKill();
                Destroy(card.gameObject);
                changed = true;
            }
            if (changed)
            {
                ReflowEnemyResource();
                EnemyResourceCardsChanged?.Invoke();
            }
        }

        private void UntapAllResources()
        {
            foreach (CardObject card in tappedResourceCards) card?.SetDimmed(false);
            tappedResourceCards.Clear();
            ResourceCardsChanged?.Invoke();
        }

        private void UntapAllEnemyResources()
        {
            foreach (CardObject card in tappedEnemyResourceCards) card?.SetDimmed(false);
            tappedEnemyResourceCards.Clear();
            EnemyResourceCardsChanged?.Invoke();
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

        public IEnumerator RevealEnemyCardToBattlefield(CardDefinition definition, Action<CardObject> completed)
        {
            CardObject card = CreateCardAtDeck(definition);
            if (card == null) { completed?.Invoke(null); yield break; }
            battlefieldCards.Add(card);
            IsAnimating = true;
            if (deckRevealSeconds > 0f) yield return new WaitForSeconds(deckRevealSeconds);
            yield return AnimateExistingCardToParent(card, battlefieldParent, Zone.Battlefield);
            IsAnimating = false;
            completed?.Invoke(card);
        }

        public IEnumerator RevealEnemyCardToResource(CardDefinition definition, CardObject eaten, Action<CardObject> completed)
        {
            CardObject card = CreateCardAtDeck(definition);
            if (card == null || eaten == null) { completed?.Invoke(null); yield break; }
            IsAnimating = true;
            if (deckRevealSeconds > 0f) yield return new WaitForSeconds(deckRevealSeconds);
            yield return MoveCardsToEnemyResourceAnimated(card, eaten);
            IsAnimating = false;
            completed?.Invoke(card);
        }

        public IEnumerator MoveCardsToEnemyResourceAnimated(CardObject first, CardObject second)
        {
            if (first == null || second == null) yield break;
            EnsureEnemyResourceParent();
            Vector3 firstStart = first.transform.position;
            Vector3 secondStart = second.transform.position;
            RegisterAsEnemyResource(first);
            RegisterAsEnemyResource(second);
            RectTransform firstRect = PrepareEnemyDestination(first, out Vector3 firstTarget);
            RectTransform secondRect = PrepareEnemyDestination(second, out Vector3 secondTarget);
            ReflowEnemyResource();
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
            if (first != null) FinishAtParent(first, enemyResourceParent);
            if (second != null) FinishAtParent(second, enemyResourceParent);
            ReflowEnemyResource();
            IsAnimating = false;
            EnemyResourceCardsChanged?.Invoke();
        }

        private IEnumerator BeginPlayerTurnRoutine()
        {
            SetPhase(Phase.PlayerDraw);
            yield return ShowTurnInfo("玩家回合");
            yield return DrawCardToZone(playerHandParent, Zone.Hand, null);
            SetPhase(Phase.ResourceExchange);
            activeRoutine = null;
            SetDrawButtonEnabled(deckController != null && deckController.RemainingCount > 0);
        }

        private IEnumerator TestDrawForPlayerRoutine()
        {
            yield return DrawCardToZone(playerHandParent, Zone.Hand, null);
            activeRoutine = null;
            SetPhase(Phase.ResourceExchange);
            SetDrawButtonEnabled(deckController != null && deckController.RemainingCount > 0);
        }

        private IEnumerator InitializeAndDeal()
        {
            yield return ShowTurnInfo("遊戲開始");
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

            SetPhase(Phase.DealingEnemyHand);
            yield return null;
            yield return BeginPlayerTurnRoutine();
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

            return CreateCardAtDeck(definition);
        }

        private CardObject CreateCardAtDeck(CardDefinition definition)
        {
            if (definition == null || cardPrefab == null || deckDisplayParent == null) return null;
            CardObject card = Instantiate(cardPrefab, deckDisplayParent);
            FinishAtParent(card, deckDisplayParent);
            card.Bind(definition);
            spawnedCards.Add(card);
            return card;
        }

        private RectTransform PrepareEnemyDestination(CardObject card, out Vector3 targetPosition)
        {
            RectTransform rect = card.transform as RectTransform;
            rect.SetParent(enemyResourceParent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            ReflowEnemyResource();
            Canvas.ForceUpdateCanvases();
            targetPosition = rect.position;
            return rect;
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

        private void RegisterAsEnemyResource(CardObject card)
        {
            battlefieldCards.Remove(card);
            if (!enemyResourceCards.Contains(card)) enemyResourceCards.Add(card);
            card.SetSelected(false);
            card.SetDimmed(false);
        }

        private void EnsureEnemyResourceParent()
        {
            if (enemyResourceParent != null || resourceParent == null) return;
            var holder = new GameObject("EnemyResource", typeof(RectTransform));
            enemyResourceParent = holder.GetComponent<RectTransform>();
            enemyResourceParent.SetParent(resourceParent.parent, false);
            enemyResourceParent.anchorMin = resourceParent.anchorMin;
            enemyResourceParent.anchorMax = resourceParent.anchorMax;
            enemyResourceParent.pivot = resourceParent.pivot;
            enemyResourceParent.sizeDelta = resourceParent.sizeDelta;
            enemyResourceParent.anchoredPosition = new Vector2(resourceParent.anchoredPosition.x, -resourceParent.anchoredPosition.y);
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

        private void ReflowEnemyResource()
        {
            ReflowCards(enemyResourceCards, resourceCardSpacing);
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
            tappedResourceCards.Clear();
            enemyResourceCards.Clear();
            tappedEnemyResourceCards.Clear();
            ResourceCardsChanged?.Invoke();
            EnemyResourceCardsChanged?.Invoke();
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
            DealingEnemyHand,
            PlayerDraw,
            ResourceExchange,
            AttackSelection,
            EnemyDraw,
            EnemyResourceExchange,
            EnemyAttack
        }
    }
}

