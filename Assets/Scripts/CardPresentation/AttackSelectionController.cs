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
        [SerializeField] private GameObject cardGameGroup;
        [SerializeField] private Transform playerCharacterGroups;
        [SerializeField] private Transform enemyCharacterGroups;
        [SerializeField] private Button attackEndButton;
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
        private readonly Dictionary<CharacterAttackTarget, RectTransform> lines = new Dictionary<CharacterAttackTarget, RectTransform>();
        private RectTransform lineParent;
        private CharacterAttackTarget selectedAlly;

        public event Action<IReadOnlyDictionary<CharacterAttackTarget, CharacterAttackTarget>> AttackConfirmed;

        private void Awake()
        {
            FindReferences();
            ConfigureCharacters(playerCharacterGroups, true, allies);
            ConfigureCharacters(enemyCharacterGroups, false, enemies);
            attackEndButton?.onClick.AddListener(CompleteAttackSelection);
            if (gameFlow != null)
            {
                gameFlow.PhaseChanged += HandlePhaseChanged;
                HandlePhaseChanged(gameFlow.CurrentPhase);
            }
        }

        private void OnDestroy()
        {
            attackEndButton?.onClick.RemoveListener(CompleteAttackSelection);
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
            bool isEnemyAttack = phase == PlayerTurnDealController.Phase.EnemyAttackSelection
                || phase == PlayerTurnDealController.Phase.EnemyAttackPerformance;
            if (cardGameGroup != null)
            {
                bool showCards = !isAttackSelection && !isAttackPerformance && !isEnemyAttack;
                cardGameGroup.SetActive(showCards);
            }
            if (attackEndButton != null) attackEndButton.gameObject.SetActive(isAttackSelection);
            if (isAttackSelection) RefreshAttackGlows();
            else ClearSelection();
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
                targets[selectedAlly] = character;
                EnsureLine(selectedAlly);
                RefreshAttackGlows();
            }
        }

        private void CompleteAttackSelection()
        {
            if (gameFlow == null || gameFlow.CurrentPhase != PlayerTurnDealController.Phase.AttackSelection) return;
            StartCoroutine(CompleteAttackSelectionRoutine());
        }

        private IEnumerator CompleteAttackSelectionRoutine()
        {
            if (attackEndButton != null) attackEndButton.interactable = false;
            AttackConfirmed?.Invoke(targets);
            ClearSelection();
            gameFlow.BeginAttackPerformance();
            if (attackPerformanceSeconds > 0f) yield return new WaitForSeconds(attackPerformanceSeconds);
            if (gameFlow.CurrentPhase == PlayerTurnDealController.Phase.AttackPerformance)
            {
                gameFlow.CompleteAttackSelection();
            }
            if (attackEndButton != null) attackEndButton.interactable = true;
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
            if (lines.TryGetValue(ally, out RectTransform line))
            {
                lines.Remove(ally);
                if (line != null) Destroy(line.gameObject);
            }
            RefreshAttackGlows();
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
            selectedAlly = null;
            targets.Clear();
            ClearAttackGlows();
            foreach (RectTransform line in lines.Values)
            {
                if (line != null) Destroy(line.gameObject);
            }
            lines.Clear();
        }

        private void FindReferences()
        {
            if (gameFlow == null) gameFlow = GetComponent<PlayerTurnDealController>();
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

        private void Unsubscribe(List<CharacterAttackTarget> characters)
        {
            foreach (CharacterAttackTarget character in characters)
            {
                if (character != null) character.Clicked -= HandleCharacterClicked;
            }
        }
    }
}