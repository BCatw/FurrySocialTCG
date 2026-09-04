using System;
using System.Collections;
using System.Collections.Generic;
using FurrySocialCard.CardData;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class EnemyTurnController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerTurnDealController gameFlow;
        [SerializeField] private ResourceExchangeController resourceExchange;
        [SerializeField] private CharacterBattleController characterBattle;
        [SerializeField] private Transform playerCharacterGroups;
        [SerializeField] private Transform enemyCharacterGroups;

        [Header("Prototype AI")]
        [SerializeField, Min(0)] private int initialHandCards = 8;
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int randomSeed = 24680;
        [SerializeField, Min(0f)] private float decisionDelaySeconds = 0.25f;
        [SerializeField, Min(0f)] private float attackPreviewSeconds = 0.7f;
        [SerializeField, Min(1f)] private float lineWidth = 12f;
        [SerializeField] private Color lineColor = new Color(0.35f, 0.65f, 1f, 0.9f);

        private readonly List<CardDefinition> hand = new List<CardDefinition>();
        private readonly List<CardObject> candidates = new List<CardObject>();
        private readonly Dictionary<CharacterAttackTarget, CharacterAttackTarget> attackTargets =
            new Dictionary<CharacterAttackTarget, CharacterAttackTarget>();
        private readonly List<RectTransform> attackLines = new List<RectTransform>();

        private System.Random random;
        private Coroutine turnRoutine;
        private RectTransform lineParent;
        private bool initialHandReady;

        private void Awake()
        {
            FindReferences();
            ResetRandom();
            if (gameFlow != null) gameFlow.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDestroy()
        {
            if (gameFlow != null) gameFlow.PhaseChanged -= HandlePhaseChanged;
            ClearLines();
        }

        private void HandlePhaseChanged(PlayerTurnDealController.Phase phase)
        {
            if (phase == PlayerTurnDealController.Phase.Initializing)
            {
                if (turnRoutine != null) StopCoroutine(turnRoutine);
                turnRoutine = null;
                hand.Clear();
                initialHandReady = false;
                attackTargets.Clear();
                ClearLines();
                ResetRandom();
                return;
            }

            if (phase == PlayerTurnDealController.Phase.PlayerDraw && !initialHandReady)
            {
                DealInitialHand();
                initialHandReady = true;
                return;
            }

            if (phase == PlayerTurnDealController.Phase.EnemyDraw && turnRoutine == null)
            {
                turnRoutine = StartCoroutine(RunTurn());
            }
        }

        private void DealInitialHand()
        {
            for (int index = 0; index < initialHandCards; index++)
            {
                if (!gameFlow.TryDrawDefinition(out CardDefinition definition)) break;
                hand.Add(definition);
            }
        }

        private IEnumerator RunTurn()
        {
            if (gameFlow.TryDrawDefinition(out CardDefinition drawn)) hand.Add(drawn);
            if (decisionDelaySeconds > 0f) yield return new WaitForSeconds(decisionDelaySeconds);

            gameFlow.BeginEnemyResourceExchange();
            if (hand.Count > 0)
            {
                int handIndex = random.Next(hand.Count);
                CardDefinition played = hand[handIndex];
                hand.RemoveAt(handIndex);
                FindCandidates(played, null);

                if (candidates.Count == 0)
                {
                    yield return gameFlow.RevealEnemyCardToBattlefield(played, null);
                }
                else
                {
                    CardObject eaten = candidates[random.Next(candidates.Count)];
                    yield return gameFlow.RevealEnemyCardToResource(played, eaten, null);
                    yield return RunChainDraws();
                }
            }

            if (decisionDelaySeconds > 0f) yield return new WaitForSeconds(decisionDelaySeconds);
            gameFlow.BeginEnemyAttack();
            BuildRandomAttacks();
            CreateLines();

            float elapsed = 0f;
            while (elapsed < attackPreviewSeconds)
            {
                UpdateLines();
                elapsed += Time.deltaTime;
                yield return null;
            }

            characterBattle?.ResolveEnemyAttacks(attackTargets);
            attackTargets.Clear();
            ClearLines();
            if (decisionDelaySeconds > 0f) yield return new WaitForSeconds(decisionDelaySeconds);
            gameFlow.CompleteEnemyTurn();
            turnRoutine = null;
        }

        private IEnumerator RunChainDraws()
        {
            int limit = resourceExchange != null ? resourceExchange.MaxChainDrawsPerTurn : 0;
            float interval = resourceExchange != null ? resourceExchange.ChainDrawIntervalSeconds : 0f;
            for (int index = 0; index < limit; index++)
            {
                if (interval > 0f) yield return new WaitForSeconds(interval);
                CardObject drawnCard = null;
                yield return gameFlow.DrawToBattlefield(card => drawnCard = card);
                if (drawnCard == null) yield break;

                FindCandidates(drawnCard.Definition, drawnCard);
                if (candidates.Count == 0) yield break;
                CardObject eaten = candidates[random.Next(candidates.Count)];
                yield return gameFlow.MoveCardsToEnemyResourceAnimated(drawnCard, eaten);
            }
        }

        private void FindCandidates(CardDefinition source, CardObject excluded)
        {
            candidates.Clear();
            ResourceExchangeController.MatchRule rule = resourceExchange != null
                ? resourceExchange.CurrentMatchRule
                : ResourceExchangeController.MatchRule.SameTierAndAttribute;

            foreach (CardObject fieldCard in gameFlow.BattlefieldCards)
            {
                if (fieldCard != null && fieldCard != excluded &&
                    ResourceExchangeController.CanEat(source, fieldCard.Definition, rule))
                {
                    candidates.Add(fieldCard);
                }
            }
        }

        private void BuildRandomAttacks()
        {
            attackTargets.Clear();
            var players = new List<CharacterAttackTarget>();
            CollectActiveTargets(playerCharacterGroups, players);

            if (enemyCharacterGroups == null || players.Count == 0) return;
            for (int index = 0; index < enemyCharacterGroups.childCount; index++)
            {
                Transform child = enemyCharacterGroups.GetChild(index);
                if (!child.gameObject.activeInHierarchy) continue;
                CharacterAttackTarget enemy = child.GetComponent<CharacterAttackTarget>();
                if (enemy != null) attackTargets[enemy] = players[random.Next(players.Count)];
            }
        }

        private static void CollectActiveTargets(Transform group, List<CharacterAttackTarget> destination)
        {
            if (group == null) return;
            for (int index = 0; index < group.childCount; index++)
            {
                Transform child = group.GetChild(index);
                if (!child.gameObject.activeInHierarchy) continue;
                CharacterAttackTarget target = child.GetComponent<CharacterAttackTarget>();
                if (target != null) destination.Add(target);
            }
        }

        private void CreateLines()
        {
            ClearLines();
            if (lineParent == null) return;
            foreach (KeyValuePair<CharacterAttackTarget, CharacterAttackTarget> pair in attackTargets)
            {
                var lineObject = new GameObject("EnemyAttackLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform line = lineObject.GetComponent<RectTransform>();
                line.SetParent(lineParent, false);
                Image image = lineObject.GetComponent<Image>();
                image.color = lineColor;
                image.raycastTarget = false;
                attackLines.Add(line);
            }
            UpdateLines();
        }

        private void UpdateLines()
        {
            int index = 0;
            foreach (KeyValuePair<CharacterAttackTarget, CharacterAttackTarget> pair in attackTargets)
            {
                if (index >= attackLines.Count) break;
                UpdateLine(attackLines[index], pair.Key.ActiveLinkPoint, pair.Value.ActiveLinkPoint);
                index++;
            }
        }

        private void UpdateLine(RectTransform line, RectTransform from, RectTransform to)
        {
            if (line == null || from == null || to == null || lineParent == null) return;
            Vector2 start = lineParent.InverseTransformPoint(from.position);
            Vector2 end = lineParent.InverseTransformPoint(to.position);
            Vector2 difference = end - start;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = start;
            line.sizeDelta = new Vector2(difference.magnitude, lineWidth);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
        }

        private void ClearLines()
        {
            foreach (RectTransform line in attackLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            attackLines.Clear();
        }

        private void ResetRandom()
        {
            random = useRandomSeed ? new System.Random(randomSeed) : new System.Random();
        }

        private void FindReferences()
        {
            if (gameFlow == null) gameFlow = GetComponent<PlayerTurnDealController>();
            if (resourceExchange == null) resourceExchange = GetComponent<ResourceExchangeController>();
            if (characterBattle == null) characterBattle = GetComponent<CharacterBattleController>();
            if (playerCharacterGroups == null) playerCharacterGroups = GameObject.Find("PlayerCharacterGroups")?.transform;
            if (enemyCharacterGroups == null) enemyCharacterGroups = GameObject.Find("EnemyCharacterGroups")?.transform;
            Canvas canvas = playerCharacterGroups != null ? playerCharacterGroups.GetComponentInParent<Canvas>() : null;
            lineParent = canvas != null ? canvas.transform as RectTransform : null;

            if (gameFlow == null || resourceExchange == null || characterBattle == null ||
                playerCharacterGroups == null || enemyCharacterGroups == null || lineParent == null)
            {
                Debug.LogError("Enemy turn references are incomplete.", this);
            }
        }
    }
}
