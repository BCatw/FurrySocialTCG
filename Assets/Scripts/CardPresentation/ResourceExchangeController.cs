using System.Collections;
using System.Collections.Generic;
using FurrySocialCard.CardData;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FurrySocialCard.CardPresentation
{
    public sealed class ResourceExchangeController : MonoBehaviour
    {
        [SerializeField] private PlayerTurnDealController gameFlow;
        [SerializeField] private MatchRule matchRule = MatchRule.SameTierAndAttribute;
        [SerializeField, Min(0f)] private float chainDrawIntervalSeconds = 0.18f;
        [Tooltip("每次手牌成功吃牌後，最多可以連續補幾張牌；設為 0 時不補牌。")]
        [SerializeField, Min(0)] private int maxChainDrawsPerTurn = 3;

        public MatchRule CurrentMatchRule => matchRule;
        public int MaxChainDrawsPerTurn => maxChainDrawsPerTurn;
        public float ChainDrawIntervalSeconds => chainDrawIntervalSeconds;

        private readonly List<CardObject> candidates = new List<CardObject>();
        private CardObject selectedHandCard;
        private CardObject pendingDrawnCard;
        private CardObject selectedDrawCandidate;
        private Coroutine exchangeRoutine;

        private void OnEnable()
        {
            CardObject.Clicked += HandleCardClicked;
            BattlefieldClickArea.Clicked += HandleBattlefieldClicked;
            if (gameFlow != null)
            {
                gameFlow.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void OnDisable()
        {
            CardObject.Clicked -= HandleCardClicked;
            BattlefieldClickArea.Clicked -= HandleBattlefieldClicked;
            if (gameFlow != null)
            {
                gameFlow.PhaseChanged -= HandlePhaseChanged;
            }
            ClearAllMatchHints();
        }

        private void HandlePhaseChanged(PlayerTurnDealController.Phase phase)
        {
            if (phase == PlayerTurnDealController.Phase.ResourceExchange)
            {
                RefreshHandMatchHints();
                return;
            }

            ClearAllMatchHints();

            if (exchangeRoutine != null)
            {
                StopCoroutine(exchangeRoutine);
                exchangeRoutine = null;
            }
            ClearSelection();
        }

        private void HandleCardClicked(CardObject card, PointerEventData eventData)
        {
            if (card == null)
            {
                return;
            }

            if (pendingDrawnCard != null)
            {
                if (candidates.Contains(card))
                {
                    selectedDrawCandidate = card;
                }
                return;
            }

            if (!CanInteract())
            {
                return;
            }

            if (Contains(gameFlow.HandCards, card))
            {
                SelectOrToggle(card);
                return;
            }

            if (selectedHandCard != null && Contains(gameFlow.BattlefieldCards, card))
            {
                StartPlay(eventData.position, eventData.pressEventCamera);
            }
        }

        private void HandleBattlefieldClicked(PointerEventData eventData)
        {
            if (CanInteract() && selectedHandCard != null)
            {
                StartPlay(eventData.position, eventData.pressEventCamera);
            }
        }

        private bool CanInteract()
        {
            return gameFlow != null
                && gameFlow.CurrentPhase == PlayerTurnDealController.Phase.ResourceExchange
                && exchangeRoutine == null
                && !gameFlow.IsAnimating;
        }

        private void SelectOrToggle(CardObject card)
        {
            if (selectedHandCard == card)
            {
                ClearSelection();
                return;
            }

            selectedHandCard?.SetSelected(false);
            selectedHandCard = card;
            selectedHandCard.SetSelected(true);
            RefreshBattlefieldDimming();
        }

        private void StartPlay(Vector2 clickScreenPosition, Camera eventCamera)
        {
            exchangeRoutine = StartCoroutine(ResolvePlay(clickScreenPosition, eventCamera));
        }

        private IEnumerator ResolvePlay(Vector2 clickScreenPosition, Camera eventCamera)
        {
            CardObject playedCard = selectedHandCard;
            playedCard?.SetMatchHint(false);
            FindCandidates(playedCard, playedCard, candidates);
            CardObject eatenCard = ChooseCandidate(candidates, clickScreenPosition, eventCamera);
            ClearSelection();

            if (eatenCard == null)
            {
                yield return gameFlow.MoveHandCardToBattlefieldAnimated(playedCard);
                exchangeRoutine = null;
                gameFlow.CompleteResourceExchange();
                yield break;
            }

            yield return gameFlow.MoveCardsToResourceAnimated(playedCard, eatenCard);
            yield return ResolveChainDraws();
            exchangeRoutine = null;
            gameFlow.CompleteResourceExchange();
        }

        private IEnumerator ResolveChainDraws()
        {
            int chainDrawCount = 0;
            while (chainDrawCount < maxChainDrawsPerTurn)
            {
                CardObject drawnCard = null;
                yield return gameFlow.DrawToBattlefield(card => drawnCard = card);
                if (drawnCard == null)
                {
                    yield break;
                }
                chainDrawCount++;

                if (chainDrawIntervalSeconds > 0f)
                {
                    yield return new WaitForSeconds(chainDrawIntervalSeconds);
                }

                FindCandidates(drawnCard, drawnCard, candidates);
                if (candidates.Count == 0)
                {
                    yield break;
                }

                CardObject eatenCard;
                if (candidates.Count == 1)
                {
                    eatenCard = candidates[0];
                }
                else
                {
                    pendingDrawnCard = drawnCard;
                    selectedDrawCandidate = null;
                    RefreshDrawCandidateDimming();
                    yield return new WaitUntil(() => selectedDrawCandidate != null);
                    eatenCard = selectedDrawCandidate;
                    ClearDrawCandidateSelection();
                }

                yield return gameFlow.MoveCardsToResourceAnimated(drawnCard, eatenCard);
            }
        }

        private void RefreshDrawCandidateDimming()
        {
            if (gameFlow == null || pendingDrawnCard == null)
            {
                return;
            }

            pendingDrawnCard.SetSelected(true);
            foreach (CardObject fieldCard in gameFlow.BattlefieldCards)
            {
                if (fieldCard != null)
                {
                    bool canBeChosen = fieldCard == pendingDrawnCard || candidates.Contains(fieldCard);
                    fieldCard.SetDimmed(!canBeChosen);
                }
            }
        }

        private void ClearDrawCandidateSelection()
        {
            pendingDrawnCard?.SetSelected(false);
            pendingDrawnCard = null;
            selectedDrawCandidate = null;

            if (gameFlow == null)
            {
                return;
            }

            foreach (CardObject fieldCard in gameFlow.BattlefieldCards)
            {
                fieldCard?.SetDimmed(false);
            }
        }

        private void RefreshBattlefieldDimming()
        {
            foreach (CardObject fieldCard in gameFlow.BattlefieldCards)
            {
                fieldCard?.SetDimmed(selectedHandCard != null && !CanEat(selectedHandCard.Definition, fieldCard.Definition, matchRule));
            }
        }

        private void ClearSelection()
        {
            ClearDrawCandidateSelection();

            if (selectedHandCard != null)
            {
                selectedHandCard.SetSelected(false);
                selectedHandCard = null;
            }

            if (gameFlow == null)
            {
                return;
            }

            foreach (CardObject fieldCard in gameFlow.BattlefieldCards)
            {
                fieldCard?.SetDimmed(false);
            }
        }

        private void RefreshHandMatchHints()
        {
            if (gameFlow == null) return;
            foreach (CardObject handCard in gameFlow.HandCards)
            {
                bool hasMatch = false;
                if (handCard != null)
                {
                    foreach (CardObject fieldCard in gameFlow.BattlefieldCards)
                    {
                        if (fieldCard != null && CanEat(handCard.Definition, fieldCard.Definition, matchRule))
                        {
                            hasMatch = true;
                            break;
                        }
                    }
                    handCard.SetMatchHint(hasMatch);
                }
            }
        }

        private void ClearAllMatchHints()
        {
            if (gameFlow == null) return;
            foreach (CardObject handCard in gameFlow.HandCards) handCard?.SetMatchHint(false);
            foreach (CardObject fieldCard in gameFlow.BattlefieldCards) fieldCard?.SetMatchHint(false);
        }

        private void FindCandidates(CardObject source, CardObject excluded, List<CardObject> results)
        {
            results.Clear();
            foreach (CardObject fieldCard in gameFlow.BattlefieldCards)
            {
                if (fieldCard != null && fieldCard != excluded && CanEat(source.Definition, fieldCard.Definition, matchRule))
                {
                    results.Add(fieldCard);
                }
            }
        }

        private static CardObject ChooseCandidate(List<CardObject> available, Vector2 clickPosition, Camera eventCamera)
        {
            if (available.Count == 0)
            {
                return null;
            }
            if (available.Count == 1)
            {
                return available[0];
            }

            CardObject nearest = available[0];
            float nearestDistance = float.MaxValue;
            foreach (CardObject card in available)
            {
                Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, card.transform.position);
                float distance = (screenPosition - clickPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = card;
                }
            }
            return nearest;
        }

        public static bool CanEat(CardDefinition source, CardDefinition target, MatchRule rule)
        {
            if (source == null || target == null)
            {
                return false;
            }

            bool sameTier = source.Tier == target.Tier;
            bool sameAttribute = source.Attribute == target.Attribute;
            switch (rule)
            {
                case MatchRule.SameTier: return sameTier;
                case MatchRule.SameAttribute: return sameAttribute;
                case MatchRule.SameTierOrAttribute: return sameTier || sameAttribute;
                default: return sameTier && sameAttribute;
            }
        }

        private static bool Contains(IReadOnlyList<CardObject> cards, CardObject target)
        {
            for (int index = 0; index < cards.Count; index++)
            {
                if (cards[index] == target)
                {
                    return true;
                }
            }
            return false;
        }

        public enum MatchRule
        {
            SameTierAndAttribute,
            SameTier,
            SameAttribute,
            SameTierOrAttribute
        }
    }
}

