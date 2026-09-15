using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class AttackSelectionController : MonoBehaviour
    {
        [SerializeField] private PlayerTurnDealController gameFlow;
        [SerializeField] private CharacterBattleController characterBattle;
        [SerializeField] private GameObject cardGameGroup;
        [SerializeField] private Transform playerCharacterGroups;
        [SerializeField] private Transform enemyCharacterGroups;
        [SerializeField] private Button attackEndButton;
        [SerializeField] private Button selectDoneButton;
        [SerializeField, Min(1f)] private float lineWidth = 12f;
        [SerializeField] private Color lineColor = new Color(1f, 0.25f, 0.3f, 0.9f);
        [SerializeField, Min(0f)] private float attackPerformanceSeconds = 0.5f;
        [Header("Character Assignment Glow")]
        [SerializeField] private Color unassignedAttackColor = new Color32(0, 255, 200, 255);
        [SerializeField] private Color assignedAttackColor = new Color32(255, 155, 0, 255);
        [Header("Character Assignment Movement")]
        [SerializeField, Min(0f)] private float assignedMoveDistance = 40f;
        [SerializeField, Min(0f)] private float assignedMoveDuration = 0.2f;

        private readonly List<CharacterAttackTarget> allies = new List<CharacterAttackTarget>();
        private readonly List<CharacterAttackTarget> enemies = new List<CharacterAttackTarget>();
        private readonly Dictionary<CharacterAttackTarget, CharacterAttackTarget> targets = new Dictionary<CharacterAttackTarget, CharacterAttackTarget>();
        private readonly List<CharacterAttackTarget> attackOrder = new List<CharacterAttackTarget>();
        private readonly Dictionary<CharacterAttackTarget, RectTransform> lines = new Dictionary<CharacterAttackTarget, RectTransform>();
        private RectTransform lineParent;
        private CharacterAttackTarget selectedAlly;
        private readonly HashSet<CardObject> selectedConsumeCards = new HashSet<CardObject>();
        private List<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>> pendingAssignments;
        private ResourceActionPlan pendingResourcePlan;
        private Coroutine buttonBindingRoutine;

        private void Awake()
        {
            FindReferences();
            ConfigureCharacters(playerCharacterGroups, true, allies);
            ConfigureCharacters(enemyCharacterGroups, false, enemies);
            attackEndButton?.onClick.AddListener(CompleteAttackSelection);
            TryBindSelectDoneButton();
            if (selectDoneButton == null) buttonBindingRoutine = StartCoroutine(BindSelectDoneButtonWhenAvailable());
            CardObject.Clicked += HandleResourceCardClicked;
            if (gameFlow != null)
            {
                gameFlow.PhaseChanged += HandlePhaseChanged;
                HandlePhaseChanged(gameFlow.CurrentPhase);
            }
        }

        private void OnDestroy()
        {
            attackEndButton?.onClick.RemoveListener(CompleteAttackSelection);
            selectDoneButton?.onClick.RemoveListener(CompleteResourceSelection);
            CardObject.Clicked -= HandleResourceCardClicked;
            if (gameFlow != null) gameFlow.PhaseChanged -= HandlePhaseChanged;
            Unsubscribe(allies);
            Unsubscribe(enemies);
        }

        private void LateUpdate()
        {
            if (gameFlow == null || gameFlow.CurrentPhase != PlayerTurnDealController.Phase.AttackSelection) return;
            foreach (KeyValuePair<CharacterAttackTarget, CharacterAttackTarget> pair in targets)
            {
                if (lines.TryGetValue(pair.Key, out RectTransform line))
                {
                    UpdateLine(line, pair.Key.ActiveLinkPoint, pair.Value.ActiveLinkPoint);
                }
            }
        }

        private void HandlePhaseChanged(PlayerTurnDealController.Phase phase)
        {
            bool isAttackSelection = phase == PlayerTurnDealController.Phase.AttackSelection;
            bool isAttackPerformance = phase == PlayerTurnDealController.Phase.AttackPerformance;
            bool isResourcePayment = phase == PlayerTurnDealController.Phase.ResourcePaymentSelection;
            bool isEnemyAttack = phase == PlayerTurnDealController.Phase.EnemyAttackSelection
                || phase == PlayerTurnDealController.Phase.EnemyAttackPerformance;
            if (cardGameGroup != null)
            {
                bool showCards = isResourcePayment || (!isAttackSelection && !isAttackPerformance && !isEnemyAttack);
                cardGameGroup.SetActive(showCards);
            }
            if (attackEndButton != null) attackEndButton.gameObject.SetActive(isAttackSelection);
            if (selectDoneButton != null) selectDoneButton.gameObject.SetActive(isResourcePayment);
            if (isAttackSelection) RefreshAttackGlows();
            else if (!isResourcePayment) ClearSelection();
        }

        private void HandleCharacterClicked(CharacterAttackTarget character)
        {
            if (gameFlow == null || gameFlow.CurrentPhase != PlayerTurnDealController.Phase.AttackSelection) return;
            if (character.IsAlly)
            {
                selectedAlly = selectedAlly == character ? null : character;
                RefreshAttackGlows();
                return;
            }
            if (selectedAlly == null) return;

            if (targets.TryGetValue(selectedAlly, out CharacterAttackTarget currentTarget) && currentTarget == character)
            {
                CharacterAttackTarget cancelledAlly = selectedAlly;
                selectedAlly = null;
                RemoveTarget(cancelledAlly);
            }
            else
            {
                if (!targets.ContainsKey(selectedAlly)) attackOrder.Add(selectedAlly);
                targets[selectedAlly] = character;
                EnsureLine(selectedAlly);
                RefreshAttackGlows();
                RefreshAttackOrderLabels();
                RefreshAttackPreview();
            }
        }

        private void CompleteAttackSelection()
        {
            if (gameFlow == null || gameFlow.CurrentPhase != PlayerTurnDealController.Phase.AttackSelection) return;
            List<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>> assignments = CreateOrderedAssignments();
            ResourceActionPlan plan = characterBattle?.RefreshAttackPreview(assignments, true);
            if (plan != null && plan.RequiresConsumeSelection && plan.RequiredConsumeCount > 0)
            {
                pendingAssignments = assignments;
                pendingResourcePlan = plan;
                selectedConsumeCards.Clear();
                gameFlow.BeginResourcePaymentSelection();
                RefreshSelectableResourceCards();
                return;
            }
            StartCoroutine(CompleteAttackSelectionRoutine(assignments, plan));
        }

        private IEnumerator CompleteAttackSelectionRoutine(
            List<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>> assignments,
            ResourceActionPlan resourcePlan)
        {
            if (attackEndButton != null) attackEndButton.interactable = false;
            gameFlow.BeginAttackPerformance();
            if (characterBattle != null)
            {
                yield return characterBattle.PlayAttackSequence(assignments, true, resourcePlan);
            }
            else if (attackPerformanceSeconds > 0f)
            {
                yield return new WaitForSeconds(attackPerformanceSeconds);
            }
            if (gameFlow.CurrentPhase == PlayerTurnDealController.Phase.AttackPerformance)
            {
                gameFlow.CompleteAttackSelection();
            }
            if (attackEndButton != null) attackEndButton.interactable = true;
        }

        private void HandleResourceCardClicked(CardObject card, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (gameFlow == null || gameFlow.CurrentPhase != PlayerTurnDealController.Phase.ResourcePaymentSelection
                || card == null || pendingResourcePlan?.SelectableConsumeCards == null
                || !ContainsCard(pendingResourcePlan.SelectableConsumeCards, card))
                return;

            if (!selectedConsumeCards.Remove(card))
            {
                if (selectedConsumeCards.Count >= pendingResourcePlan.RequiredConsumeCount) return;
                selectedConsumeCards.Add(card);
            }
            RefreshSelectableResourceCards();
            pendingResourcePlan = characterBattle?.RefreshAttackPreview(
                pendingAssignments, true, selectedConsumeCards) ?? pendingResourcePlan;
        }

        private void CompleteResourceSelection()
        {
            if (gameFlow == null || gameFlow.CurrentPhase != PlayerTurnDealController.Phase.ResourcePaymentSelection)
                return;
            pendingResourcePlan = characterBattle?.RefreshAttackPreview(
                pendingAssignments, true, selectedConsumeCards) ?? pendingResourcePlan;
            ClearSelectableResourceCards();
            StartCoroutine(CompleteAttackSelectionRoutine(pendingAssignments, pendingResourcePlan));
        }

        private void RefreshSelectableResourceCards()
        {
            if (pendingResourcePlan?.SelectableConsumeCards == null) return;
            foreach (CardObject card in pendingResourcePlan.SelectableConsumeCards)
            {
                if (card == null) continue;
                card.SetMatchHint(true);
                card.SetSelected(selectedConsumeCards.Contains(card));
            }
        }

        private void ClearSelectableResourceCards()
        {
            if (pendingResourcePlan?.SelectableConsumeCards != null)
                foreach (CardObject card in pendingResourcePlan.SelectableConsumeCards)
                    if (card != null) { card.SetSelected(false); card.SetMatchHint(false); }
            selectedConsumeCards.Clear();
        }

        private static bool ContainsCard(IReadOnlyList<CardObject> cards, CardObject target)
        {
            foreach (CardObject card in cards) if (card == target) return true;
            return false;
        }

        private IEnumerator BindSelectDoneButtonWhenAvailable()
        {
            while (isActiveAndEnabled && selectDoneButton == null)
            {
                TryBindSelectDoneButton();
                if (selectDoneButton == null) yield return null;
            }
            buttonBindingRoutine = null;
        }

        private void TryBindSelectDoneButton()
        {
            if (selectDoneButton != null) return;
            selectDoneButton = FindObjectAcrossLoadedScenes("SelectDoneBtn")?.GetComponent<Button>();
            if (selectDoneButton == null) return;
            selectDoneButton.onClick.RemoveListener(CompleteResourceSelection);
            selectDoneButton.onClick.AddListener(CompleteResourceSelection);
            selectDoneButton.gameObject.SetActive(gameFlow != null
                && gameFlow.CurrentPhase == PlayerTurnDealController.Phase.ResourcePaymentSelection);
        }

        private List<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>> CreateOrderedAssignments()
        {
            var result = new List<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>>();
            foreach (CharacterAttackTarget attacker in attackOrder)
            {
                if (attacker != null && targets.TryGetValue(attacker, out CharacterAttackTarget target) && target != null)
                {
                    result.Add(new KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>(attacker, target));
                }
            }
            return result;
        }

        private void ConfigureCharacters(Transform group, bool isAlly, List<CharacterAttackTarget> destination)
        {
            if (group == null) return;
            for (int index = 0; index < group.childCount; index++)
            {
                Transform child = CharacterSlotUtility.ResolveCharacter(group.GetChild(index));
                if (child == null) continue;
                CharacterAttackTarget target = child.GetComponent<CharacterAttackTarget>();
                if (target == null) target = child.gameObject.AddComponent<CharacterAttackTarget>();
                target.Configure(isAlly);
                target.Clicked += HandleCharacterClicked;
                destination.Add(target);
            }
        }

        private void EnsureLine(CharacterAttackTarget ally)
        {
            if (lines.ContainsKey(ally) || lineParent == null) return;
            GameObject lineObject = new GameObject($"AttackLine_{ally.name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform line = lineObject.GetComponent<RectTransform>();
            line.SetParent(lineParent, false);
            line.SetSiblingIndex(Mathf.Max(0, playerCharacterGroups.GetSiblingIndex()));
            Image image = lineObject.GetComponent<Image>();
            image.color = lineColor;
            image.raycastTarget = false;
            lines.Add(ally, line);
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

        private void RemoveTarget(CharacterAttackTarget ally)
        {
            targets.Remove(ally);
            attackOrder.Remove(ally);
            if (lines.TryGetValue(ally, out RectTransform line))
            {
                lines.Remove(ally);
                if (line != null) Destroy(line.gameObject);
            }
            RefreshAttackGlows();
            RefreshAttackOrderLabels();
            RefreshAttackPreview();
        }

        private void RefreshAttackGlows()
        {
            foreach (CharacterAttackTarget ally in allies)
            {
                if (ally == null) continue;
                bool isAssigned = targets.ContainsKey(ally);
                bool isSelected = selectedAlly == ally;
                ally.SetAttackGlow(true, isAssigned || isSelected ? assignedAttackColor : unassignedAttackColor);
                ally.SetAttackOffset(isAssigned, assignedMoveDistance, assignedMoveDuration);
            }
        }

        private void RefreshAttackOrderLabels()
        {
            foreach (CharacterAttackTarget character in allies) character?.SetAttackOrderText(null);
            foreach (CharacterAttackTarget character in enemies) character?.SetAttackOrderText(null);

            var targetOrders = new Dictionary<CharacterAttackTarget, List<string>>();
            for (int index = 0; index < attackOrder.Count; index++)
            {
                CharacterAttackTarget attacker = attackOrder[index];
                if (attacker == null || !targets.TryGetValue(attacker, out CharacterAttackTarget target) || target == null) continue;
                string orderText = (index + 1).ToString();
                attacker.SetAttackOrderText(orderText);
                if (!targetOrders.TryGetValue(target, out List<string> orders))
                {
                    orders = new List<string>();
                    targetOrders.Add(target, orders);
                }
                orders.Add(orderText);
            }

            foreach (KeyValuePair<CharacterAttackTarget, List<string>> pair in targetOrders)
            {
                pair.Key.SetAttackOrderText(string.Join(", ", pair.Value));
            }
        }
        public void SetEnemyAttackMovement(IEnumerable<CharacterAttackTarget> attackers, bool attacking)
        {
            var activeAttackers = attackers != null
                ? new HashSet<CharacterAttackTarget>(attackers)
                : new HashSet<CharacterAttackTarget>();
            foreach (CharacterAttackTarget enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.SetAttackOffset(attacking && activeAttackers.Contains(enemy), assignedMoveDistance, assignedMoveDuration);
                }
            }
        }

        private void ClearAttackGlows()
        {
            foreach (CharacterAttackTarget ally in allies)
            {
                if (ally == null) continue;
                ally.SetAttackGlow(false, unassignedAttackColor);
                ally.SetAttackOffset(false, assignedMoveDistance, assignedMoveDuration);
            }
        }

        private void ClearSelection()
        {
            ClearSelectableResourceCards();
            pendingAssignments = null;
            pendingResourcePlan = null;
            characterBattle?.ClearAttackPreview();
            selectedAlly = null;
            targets.Clear();
            attackOrder.Clear();
            RefreshAttackOrderLabels();
            ClearAttackGlows();
            foreach (RectTransform line in lines.Values)
            {
                if (line != null) Destroy(line.gameObject);
            }
            lines.Clear();
        }

        private void RefreshAttackPreview()
        {
            characterBattle?.RefreshAttackPreview(CreateOrderedAssignments(), true);
        }

        private void FindReferences()
        {
            if (gameFlow == null) gameFlow = GetComponent<PlayerTurnDealController>();
            if (characterBattle == null) characterBattle = GetComponent<CharacterBattleController>();
            if (cardGameGroup == null) cardGameGroup = FindSceneObject("CardGameGroup");
            if (playerCharacterGroups == null) playerCharacterGroups = FindSceneObject("PlayerCharacterGroups")?.transform;
            if (enemyCharacterGroups == null) enemyCharacterGroups = FindSceneObject("EnemyCharacterGroups")?.transform;
            if (attackEndButton == null) attackEndButton = FindSceneObject("AtkEndBtn")?.GetComponent<Button>();
            Canvas canvas = playerCharacterGroups != null ? playerCharacterGroups.GetComponentInParent<Canvas>() : null;
            lineParent = canvas != null ? canvas.transform as RectTransform : null;
            if (gameFlow == null || cardGameGroup == null || playerCharacterGroups == null || enemyCharacterGroups == null || attackEndButton == null || lineParent == null)
            {
                Debug.LogError("Attack selection references are incomplete. Check CardGameGroup, character groups, AtkEndBtn, and Canvas.", this);
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

        private static GameObject FindObjectAcrossLoadedScenes(string objectName)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (child.name == objectName) return child.gameObject;
            }
            return null;
        }

        private void Unsubscribe(List<CharacterAttackTarget> characters)
        {
            foreach (CharacterAttackTarget character in characters)
            {
                if (character != null) character.Clicked -= HandleCharacterClicked;
            }
        }
    }
}
