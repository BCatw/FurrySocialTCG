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

        private readonly List<CardObject> candidates = new List<CardObject>();
        private CardObject selectedHandCard;
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
        }

        private void HandlePhaseChanged(PlayerTurnDealController.Phase phase)
        {
            if (phase == PlayerTurnDealController.Phase.ResourceExchange)
            {
                return;
            }

            if (exchangeRoutine != null)
            {
                StopCoroutine(exchangeRoutine);
                exchangeRoutine = null;
            }
            ClearSelection();
        }

        private void HandleCardClicked(CardObject card, PointerEventData eventData)
        {
            if (!CanInteract() || card == null)
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
            FindCandidates(playedCard, playedCard, candidates);
            CardObject eatenCard = ChooseCandidate(candidates, clickScreenPosition, eventCamera);
            ClearSelection();

            if (eatenCard == null)
            {
                yield return gameFlow.MoveHandCardToBattlefieldAnimated(playedCard);
                exchangeRoutine = null;
                yield break;
            }

            yield return gameFlow.MoveCardsToResourceAnimated(playedCard, eatenCard);
            yield return ResolveChainDraws();
            exchangeRoutine = null;
        }

        private IEnumerator ResolveChainDraws()
        {
            while (true)
            {
                CardObject drawnCard = null;
                yield return gameFlow.DrawToBattlefield(card => drawnCard = card);
                if (drawnCard == null)
                {
                    yield break;
                }

                if (chainDrawIntervalSeconds > 0f)
                {
                    yield return new WaitForSeconds(chainDrawIntervalSeconds);
                }

                FindCandidates(drawnCard, drawnCard, candidates);
                if (candidates.Count == 0)
                {
                    yield break;
                }

                Vector2 drawnPosition = RectTransformUtility.WorldToScreenPoint(null, drawnCard.transform.position);
                CardObject eatenCard = ChooseCandidate(candidates, drawnPosition, null);
                yield return gameFlow.MoveCardsToResourceAnimated(drawnCard, eatenCard);
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

