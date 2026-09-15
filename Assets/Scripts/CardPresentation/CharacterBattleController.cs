using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using FurrySocialCard.CardData;
using FurrySocialCard.CharacterData;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FurrySocialCard.CardPresentation
{
    public sealed class CharacterBattleController : MonoBehaviour
    {
        [Serializable]
        private sealed class CharacterSpriteEntry
        {
            public string characterId;
            public Sprite portraitA;
        }

        [Header("Data")]
        [SerializeField] private TextAsset characterData;
        [SerializeField] private CharacterSpriteEntry[] characterSprites;
        [Header("Team Members (Character IDs)")]
        [SerializeField] private string[] playerTeam = { "char_T000", "char_T001", "char_T002" };
        [SerializeField] private string[] enemyTeam = { "char_T000", "char_T001", "char_T002" };
        [Header("Scene References")]
        [SerializeField] private PlayerTurnDealController gameFlow;
        [SerializeField] private AttackSelectionController attackSelection;
        [SerializeField] private Transform playerCharacterGroups;
        [SerializeField] private Transform enemyCharacterGroups;

        [Header("Attack Performance")]
        [SerializeField] private GameObject skillGroup;
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField, Min(0.01f)] private float singleAttackSeconds = 1f;
        [SerializeField, Min(0f)] private float attackIntervalSeconds = 0.5f;
        [SerializeField, Min(0f)] private float attackLungeDistance = 120f;
        [SerializeField, Min(0f)] private float hitShakeStrength = 24f;
        [SerializeField, Min(2)] private int hitShakeVibrato = 8;
        [SerializeField, Min(0f)] private float skillTransitionSeconds = 0.2f;
        [SerializeField, Min(0f)] private float skillHoldSeconds = 0.5f;
        [SerializeField, Min(0.1f)] private float maximumSkillPerformanceSeconds = 1.5f;
        private readonly Dictionary<string, CharacterDefinition> characters = new Dictionary<string, CharacterDefinition>();
        private readonly Dictionary<string, SkillDefinition> skills = new Dictionary<string, SkillDefinition>();
        private readonly Dictionary<string, EffectDefinition> effects = new Dictionary<string, EffectDefinition>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<CharacterAttackTarget, CharacterCombatantView> combatants = new Dictionary<CharacterAttackTarget, CharacterCombatantView>();
        private readonly Dictionary<string, List<PatternRequirement>> parsedPatterns = new Dictionary<string, List<PatternRequirement>>();
        public event Action<ResourceActionPlan> ResourceActionPreviewChanged;
        public event Action<SkillRequirementPreview> SkillRequirementPreviewChanged;

        private void Awake()
        {
            FindReferences();
            if (!LoadData()) return;
            BindTeam(playerCharacterGroups, playerTeam, true);
            BindTeam(enemyCharacterGroups, enemyTeam, false);
            if (gameFlow != null)
            {
                gameFlow.ResourceCardsChanged += RefreshSkillAvailability;
                gameFlow.EnemyResourceCardsChanged += RefreshSkillAvailability;
            }
            RefreshSkillAvailability();
        }

        private void OnDestroy()
        {
            if (gameFlow != null)
            {
                gameFlow.ResourceCardsChanged -= RefreshSkillAvailability;
                gameFlow.EnemyResourceCardsChanged -= RefreshSkillAvailability;
            }
            foreach (CharacterCombatantView view in combatants.Values)
                if (view != null) view.SkillHoverChanged -= HandleSkillHoverChanged;
        }

        private bool LoadData()
        {
            TextAsset source = characterData != null ? characterData : Resources.Load<TextAsset>("CharacterData/fsc_characters");
            if (source == null)
            {
                Debug.LogError("Character data JSON is missing.", this);
                return false;
            }

            CharacterDataDocument document;
            try { document = JsonUtility.FromJson<CharacterDataDocument>(source.text); }
            catch (Exception exception)
            {
                Debug.LogError($"Character data JSON could not be parsed: {exception.Message}", this);
                return false;
            }
            if (document == null) return false;
            AddById(document.characters, value => value.id, characters, "character");
            AddById(document.skills, value => value.id, skills, "skill");
            AddById(document.effects, value => value.id, effects, "effect");
            if (characterSprites != null)
            {
                foreach (CharacterSpriteEntry entry in characterSprites)
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.characterId)) sprites[entry.characterId] = entry.portraitA;
                }
            }
            return characters.Count > 0;
        }

        private void BindTeam(Transform group, string[] teamIds, bool isAlly)
        {
            if (group == null) return;
            for (int index = 0; index < group.childCount; index++)
            {
                Transform slot = group.GetChild(index);
                Transform child = CharacterSlotUtility.ResolveCharacter(slot);
                bool hasSlot = teamIds != null && index < teamIds.Length && !string.IsNullOrWhiteSpace(teamIds[index]);
                if (!hasSlot || child == null || !characters.TryGetValue(teamIds[index], out CharacterDefinition definition))
                {
                    slot.gameObject.SetActive(false);
                    if (hasSlot) Debug.LogWarning($"Character '{teamIds[index]}' was not found; slot {index + 1} is disabled.", this);
                    continue;
                }

                slot.gameObject.SetActive(true);
                child.gameObject.SetActive(true);
                var skillDefinitions = new SkillDefinition[3];
                for (int skillIndex = 0; skillIndex < 3; skillIndex++) skills.TryGetValue(definition.GetActiveSkillId(skillIndex), out skillDefinitions[skillIndex]);
                CharacterCombatantView view = child.GetComponent<CharacterCombatantView>();
                if (view == null) view = child.gameObject.AddComponent<CharacterCombatantView>();
                sprites.TryGetValue(definition.id, out Sprite portrait);
                view.Bind(definition, skillDefinitions, portrait, isAlly);
                view.SkillHoverChanged -= HandleSkillHoverChanged;
                view.SkillHoverChanged += HandleSkillHoverChanged;
                CharacterAttackTarget attackTarget = child.GetComponent<CharacterAttackTarget>();
                if (attackTarget == null)
                {
                    attackTarget = child.gameObject.AddComponent<CharacterAttackTarget>();
                    attackTarget.Configure(isAlly);
                }
                combatants[attackTarget] = view;
            }
        }

        private void RefreshSkillAvailability()
        {
            if (gameFlow == null) return;
            List<CardObject> playerResources = gameFlow.GetAvailableResourceCardsSnapshot();
            List<CardObject> enemyResources = gameFlow.GetAvailableEnemyResourceCardsSnapshot();
            foreach (KeyValuePair<CharacterAttackTarget, CharacterCombatantView> pair in combatants)
            {
                CharacterCombatantView view = pair.Value;
                List<CardObject> available = view.IsAlly ? playerResources : enemyResources;
                for (int index = 0; index < 3; index++)
                {
                    bool usable = TryGetSkill(view.Definition, index, out SkillDefinition skill) && IsUsable(skill, available);
                    view.SetSkillAvailable(index, usable);
                }
            }
        }

        public ResourceActionPlan RefreshAttackPreview(
            IReadOnlyList<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>> assignments,
            bool isPlayer,
            IReadOnlyCollection<CardObject> preferredConsumeCards = null)
        {
            ClearAttackPreview();
            if (gameFlow == null || assignments == null || assignments.Count == 0) return null;

            List<CardObject> resources = isPlayer
                ? gameFlow.GetAvailableResourceCardsSnapshot()
                : gameFlow.GetAvailableEnemyResourceCardsSnapshot();
            ResourceActionPlan resourcePlan = BuildResourceActionPlan(assignments, resources, preferredConsumeCards);
            ResourceActionPreviewChanged?.Invoke(resourcePlan);
            var projected = new Dictionary<CharacterCombatantView, int>();
            var participants = new HashSet<CharacterCombatantView>();
            foreach (CharacterCombatantView view in combatants.Values) projected[view] = view.CurrentClimax;

            foreach (SkillResourcePlan skillPlan in resourcePlan.Skills)
            {
                CharacterCombatantView attacker = skillPlan.Attacker;
                CharacterCombatantView target = skillPlan.Target;
                participants.Add(attacker);
                participants.Add(target);
                int selfDelta = 0;
                int targetDelta = 0;
                foreach (string effectId in skillPlan.Skill.effectIds)
                {
                    if (!effects.TryGetValue(effectId, out EffectDefinition effect)) continue;
                    CharacterCombatantView recipient = ResolvePreviewTarget(effect.target, attacker, target);
                    if (recipient == null || !projected.TryGetValue(recipient, out int before)) continue;
                    int after;
                    if (effect.effectType == "force_climax")
                        after = Mathf.Max(1, recipient.Definition.climaxLimit);
                    else if (effect.effectType == "climax_delta")
                        after = Mathf.Clamp(before + EvaluateValue(effect.value, resources), 0,
                            Mathf.Max(1, recipient.Definition.climaxLimit));
                    else
                        continue;
                    int effectiveDelta = after - before;
                    projected[recipient] = after;
                    if (recipient == attacker) selfDelta += effectiveDelta;
                    if (recipient == target) targetDelta += effectiveDelta;
                }
                attacker.SetSkillClimaxPreview(skillPlan.SkillIndex, selfDelta, targetDelta, true);
            }

            foreach (CharacterCombatantView participant in participants)
                participant.SetClimaxPreview(projected[participant], true);
            return resourcePlan;
        }

        public void ClearAttackPreview()
        {
            foreach (CharacterCombatantView view in combatants.Values) view?.ClearAttackPreview();
            ResourceActionPreviewChanged?.Invoke(null);
        }

        private static CharacterCombatantView ResolvePreviewTarget(string target,
            CharacterCombatantView attacker, CharacterCombatantView attackTarget)
        {
            return target == "self" ? attacker : target == "enemy_single" ? attackTarget : null;
        }

        public IEnumerator PlayAttackSequence(
            IReadOnlyList<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>> assignments,
            bool isPlayer,
            ResourceActionPlan preparedPlan = null)
        {
            if (gameFlow == null || assignments == null || assignments.Count == 0) yield break;

            List<CardObject> resourceSnapshot = isPlayer
                ? gameFlow.GetAvailableResourceCardsSnapshot()
                : gameFlow.GetAvailableEnemyResourceCardsSnapshot();
            ResourceActionPlan resourcePlan = preparedPlan ?? BuildResourceActionPlan(assignments, resourceSnapshot, null);
            var plans = new List<AttackPlan>();

            foreach (KeyValuePair<CharacterAttackTarget, CharacterAttackTarget> assignment in assignments)
            {
                if (assignment.Key == null || assignment.Value == null) continue;
                if (!combatants.TryGetValue(assignment.Key, out CharacterCombatantView attackerView)
                    || !combatants.TryGetValue(assignment.Value, out CharacterCombatantView targetView)) continue;

                var availableSkills = new List<SkillResourcePlan>();
                foreach (SkillResourcePlan skillPlan in resourcePlan.Skills)
                    if (skillPlan.Attacker == attackerView && skillPlan.Target == targetView)
                        availableSkills.Add(skillPlan);
                plans.Add(new AttackPlan(assignment.Key, assignment.Value, attackerView, targetView, availableSkills));
            }

            for (int planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                AttackPlan plan = plans[planIndex];
                foreach (SkillResourcePlan skill in plan.Skills)
                {
                    yield return ShowSkillPerformance(skill.Skill.displayName);
                }

                Tween attackTween = plan.AttackerTarget.CreateAttackTween(
                    plan.TargetTarget,
                    attackLungeDistance,
                    singleAttackSeconds,
                    hitShakeStrength,
                    hitShakeVibrato,
                    () =>
                    {
                        foreach (SkillResourcePlan skill in plan.Skills)
                        {
                            ExecuteSkill(new SkillExecution(plan.AttackerView, plan.TargetView, skill.Skill), skill.PaymentCards, resourceSnapshot, isPlayer);
                        }
                        RefreshSkillAvailability();
                    });
                if (attackTween != null) yield return attackTween.WaitForCompletion();

                if (planIndex < plans.Count - 1 && attackIntervalSeconds > 0f)
                {
                    yield return new WaitForSeconds(attackIntervalSeconds);
                }
            }
        }

        private IEnumerator ShowSkillPerformance(string skillName)
        {
            if (skillGroup == null) yield break;
            if (skillNameText != null) skillNameText.text = skillName ?? string.Empty;

            CanvasGroup canvasGroup = skillGroup.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = skillGroup.AddComponent<CanvasGroup>();
            RectTransform rect = skillGroup.transform as RectTransform;
            Vector3 baseScale = rect != null ? rect.localScale : Vector3.one;

            float maximum = Mathf.Max(0.1f, maximumSkillPerformanceSeconds);
            float hold = Mathf.Min(Mathf.Max(0f, skillHoldSeconds), maximum);
            float transition = Mathf.Min(Mathf.Max(0f, skillTransitionSeconds), Mathf.Max(0f, (maximum - hold) * 0.5f));

            skillGroup.SetActive(true);
            DOTween.Kill(skillGroup);
            canvasGroup.alpha = 0f;
            if (rect != null) rect.localScale = baseScale * 0.9f;

            Sequence sequence = DOTween.Sequence().SetLink(skillGroup);
            sequence.Append(TweenCanvasAlpha(canvasGroup, 1f, transition, Ease.OutQuad));
            if (rect != null) sequence.Join(TweenScale(rect, baseScale, transition, Ease.OutBack));
            if (hold > 0f) sequence.AppendInterval(hold);
            sequence.Append(TweenCanvasAlpha(canvasGroup, 0f, transition, Ease.InQuad));
            if (rect != null) sequence.Join(TweenScale(rect, baseScale * 1.05f, transition, Ease.InQuad));
            yield return sequence.WaitForCompletion();

            canvasGroup.alpha = 1f;
            if (rect != null) rect.localScale = baseScale;
            skillGroup.SetActive(false);
        }
        private static Tween TweenCanvasAlpha(CanvasGroup target, float destination, float duration, Ease ease)
        {
            return DOTween.To(() => target.alpha, value => target.alpha = value, destination, Mathf.Max(0f, duration))
                .SetEase(ease);
        }

        private static Tween TweenScale(RectTransform target, Vector3 destination, float duration, Ease ease)
        {
            return DOTween.To(() => target.localScale, value => target.localScale = value, destination, Mathf.Max(0f, duration))
                .SetEase(ease);
        }
        private void ExecuteSkill(SkillExecution execution, IReadOnlyList<CardObject> paymentCards,
            List<CardObject> resourceSnapshot, bool isPlayer)
        {
            foreach (string effectId in execution.Skill.effectIds)
            {
                if (!effects.TryGetValue(effectId, out EffectDefinition effect))
                {
                    Debug.LogWarning($"Skill '{execution.Skill.id}' references missing effect '{effectId}'.", this);
                    continue;
                }
                if (effect.effectType == "force_climax")
                {
                    ResolveTarget(effect.target, execution)?.ForceClimax();
                }
                else if (effect.effectType == "climax_delta")
                {
                    int value = EvaluateValue(effect.value, resourceSnapshot);
                    ResolveTarget(effect.target, execution)?.AddClimax(value);
                }
                else
                {
                    Debug.Log($"Prototype skipped non-Climax effect '{effect.id}' ({effect.effectType}).", this);
                }
            }

            if (string.Equals(execution.Skill.resourceBehavior, "Tap", StringComparison.OrdinalIgnoreCase))
            {
                if (isPlayer) gameFlow.TapResources(paymentCards);
                else gameFlow.TapEnemyResources(paymentCards);
            }
            else if (string.Equals(execution.Skill.resourceBehavior, "Consume", StringComparison.OrdinalIgnoreCase))
            {
                if (isPlayer) gameFlow.ConsumeResources(paymentCards);
                else gameFlow.ConsumeEnemyResources(paymentCards);
            }
        }

        private bool IsUsable(SkillDefinition skill, List<CardObject> resources)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.pattern) || skill.effectIds == null || skill.effectIds.Count == 0) return false;
            return TryGetRequirements(skill, out List<PatternRequirement> requirements)
                && PatternRequirementMatcher.AreAllSatisfied(requirements, requirement => Count(requirement, resources));
        }

        private bool TryGetRequirements(SkillDefinition skill, out List<PatternRequirement> requirements)
        {
            if (parsedPatterns.TryGetValue(skill.id, out requirements)) return true;
            if (!PatternExpressionParser.TryParse(skill.pattern, out requirements, out string error))
            {
                Debug.LogWarning($"Skill '{skill.id}' has an invalid Pattern: {error}", this);
                return false;
            }
            parsedPatterns[skill.id] = requirements;
            return true;
        }

        private static int Count(PatternRequirement requirement, IEnumerable<CardObject> cards)
        {
            int count = 0;
            foreach (CardObject card in cards) if (Matches(requirement, card)) count++;
            return count;
        }

        private static bool Matches(PatternRequirement requirement, CardObject card)
        {
            if (card?.Definition == null) return false;
            switch (requirement.Scope)
            {
                case PatternScope.ResourceMedium: return card.Definition.Tier == requirement.Medium;
                case PatternScope.InteractionType: return card.Definition.Attribute == requirement.InteractionType;
                case PatternScope.MediumAndInteraction: return card.Definition.Tier == requirement.Medium && card.Definition.Attribute == requirement.InteractionType;
                default: return card.Definition.id == requirement.CardId;
            }
        }

        private ResourceActionPlan BuildResourceActionPlan(
            IReadOnlyList<KeyValuePair<CharacterAttackTarget, CharacterAttackTarget>> assignments,
            List<CardObject> resources,
            IReadOnlyCollection<CardObject> preferredConsumeCards)
        {
            var skillPlans = new List<SkillResourcePlan>();
            var reserved = new HashSet<CardObject>();
            var changes = new Dictionary<ResourceCellKey, ResourceCellChange>();
            var selectableConsumeCards = new HashSet<CardObject>();
            int requiredConsumeCount = 0;
            bool requiresConsumeSelection = false;
            foreach (CardObject card in resources)
            {
                if (card?.Definition == null) continue;
                ResourceCellKey key = Key(card);
                if (!changes.TryGetValue(key, out ResourceCellChange change))
                {
                    change = new ResourceCellChange();
                    changes.Add(key, change);
                }
                change.BeforeAvailable++;
                change.AfterAvailable++;
            }

            if (assignments != null)
            foreach (KeyValuePair<CharacterAttackTarget, CharacterAttackTarget> assignment in assignments)
            {
                if (assignment.Key == null || assignment.Value == null) continue;
                if (!combatants.TryGetValue(assignment.Key, out CharacterCombatantView attacker) ||
                    !combatants.TryGetValue(assignment.Value, out CharacterCombatantView target)) continue;
                for (int skillIndex = 0; skillIndex < 3; skillIndex++)
                {
                    if (!TryGetSkill(attacker.Definition, skillIndex, out SkillDefinition skill) ||
                        !IsUsable(skill, resources) ||
                        !TryGetRequirements(skill, out List<PatternRequirement> requirements)) continue;

                    foreach (PatternRequirement requirement in requirements)
                        foreach (CardObject card in resources)
                            if (Matches(requirement, card) && changes.TryGetValue(Key(card), out ResourceCellChange relevant))
                                relevant.IsRelevant = true;

                    bool consumes = string.Equals(skill.resourceBehavior, "Consume", StringComparison.OrdinalIgnoreCase);
                    if (consumes)
                    {
                        foreach (PatternRequirement requirement in requirements)
                        {
                            int needed = RequiredPaymentCount(requirement);
                            int candidateCount = CandidateCount(requirement, resources, reserved);
                            if (candidateCount > needed)
                            {
                                requiresConsumeSelection = true;
                                foreach (CardObject card in resources)
                                    if (card != null && !reserved.Contains(card) && Matches(requirement, card))
                                        selectableConsumeCards.Add(card);
                            }
                        }
                    }

                    List<CardObject> payment = IsPaymentBehavior(skill.resourceBehavior)
                        ? SelectPaymentCards(requirements, resources, reserved,
                            consumes ? preferredConsumeCards : null)
                        : new List<CardObject>();
                    if (consumes) requiredConsumeCount += payment.Count;
                    foreach (CardObject card in payment)
                    {
                        reserved.Add(card);
                        ResourceCellChange change = changes[Key(card)];
                        change.AfterAvailable = Math.Max(0, change.AfterAvailable - 1);
                        if (string.Equals(skill.resourceBehavior, "Tap", StringComparison.OrdinalIgnoreCase))
                            change.TapCount++;
                        else
                            change.ConsumeCount++;
                    }
                    skillPlans.Add(new SkillResourcePlan
                    {
                        Attacker = attacker,
                        Target = target,
                        SkillIndex = skillIndex,
                        Skill = skill,
                        Requirements = requirements,
                        PaymentCards = payment
                    });
                }
            }
            return new ResourceActionPlan
            {
                Skills = skillPlans,
                CellChanges = changes,
                SelectableConsumeCards = new List<CardObject>(selectableConsumeCards),
                RequiredConsumeCount = requiredConsumeCount,
                RequiresConsumeSelection = requiresConsumeSelection
            };
        }

        private static bool IsPaymentBehavior(string behavior) =>
            string.Equals(behavior, "Tap", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(behavior, "Consume", StringComparison.OrdinalIgnoreCase);

        private static List<CardObject> SelectPaymentCards(IReadOnlyList<PatternRequirement> requirements,
            IReadOnlyList<CardObject> resources, ISet<CardObject> reserved,
            IReadOnlyCollection<CardObject> preferredCards)
        {
            var selected = new List<CardObject>();
            var selectedSet = new HashSet<CardObject>();
            var orderedRequirements = new List<PatternRequirement>(requirements);
            orderedRequirements.Sort((left, right) =>
                CandidateCount(left, resources, reserved).CompareTo(CandidateCount(right, resources, reserved)));
            foreach (PatternRequirement requirement in orderedRequirements)
            {
                int needed = RequiredPaymentCount(requirement);
                if (preferredCards != null)
                {
                    foreach (CardObject card in preferredCards)
                    {
                        if (needed <= 0) break;
                        if (!TrySelectPaymentCard(card, requirement, reserved, selectedSet, selected)) continue;
                        needed--;
                    }
                }
                foreach (CardObject card in resources)
                {
                    if (needed <= 0) break;
                    if (!TrySelectPaymentCard(card, requirement, reserved, selectedSet, selected)) continue;
                    needed--;
                }
            }
            return selected;
        }

        private static int RequiredPaymentCount(PatternRequirement requirement) =>
            requirement.Comparison == PatternComparison.GreaterThan ? requirement.Count + 1 : requirement.Count;

        private static bool TrySelectPaymentCard(CardObject card, PatternRequirement requirement,
            ISet<CardObject> reserved, ISet<CardObject> selectedSet, ICollection<CardObject> selected)
        {
            if (card == null || reserved.Contains(card) || selectedSet.Contains(card) || !Matches(requirement, card))
                return false;
            selected.Add(card);
            selectedSet.Add(card);
            return true;
        }

        private static int CandidateCount(PatternRequirement requirement, IReadOnlyList<CardObject> resources,
            ISet<CardObject> reserved)
        {
            int count = 0;
            foreach (CardObject card in resources)
                if (card != null && !reserved.Contains(card) && Matches(requirement, card)) count++;
            return count;
        }

        private static ResourceCellKey Key(CardObject card) =>
            new ResourceCellKey(card.Definition.Tier, card.Definition.Attribute);

        private void HandleSkillHoverChanged(CharacterCombatantView view, int skillIndex, bool active)
        {
            if (!active || view == null || !view.IsAlly || gameFlow == null ||
                !TryGetSkill(view.Definition, skillIndex, out SkillDefinition skill) ||
                !TryGetRequirements(skill, out List<PatternRequirement> requirements))
            {
                SkillRequirementPreviewChanged?.Invoke(null);
                return;
            }
            SkillRequirementPreviewChanged?.Invoke(new SkillRequirementPreview
            {
                Requirements = requirements,
                AvailableResources = gameFlow.GetAvailableResourceCardsSnapshot()
            });
        }

        private int EvaluateValue(string expression, List<CardObject> resources)
        {
            if (string.IsNullOrWhiteSpace(expression)) return 0;
            string expanded = Regex.Replace(expression, @"(?:〈([^〉]+)〉|<([^>]+)>)", match =>
            {
                string medium = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                var requirement = new PatternRequirement(PatternScope.ResourceMedium, medium, null, PatternComparison.GreaterThanOrEqual, 0);
                return Count(requirement, resources).ToString(CultureInfo.InvariantCulture);
            });
            return ArithmeticParser.TryEvaluate(expanded, out double value) ? Mathf.RoundToInt((float)value) : 0;
        }

        private CharacterCombatantView ResolveTarget(string target, SkillExecution execution)
        {
            return target == "self" ? execution.Attacker : target == "enemy_single" ? execution.Target : null;
        }

        private bool TryGetSkill(CharacterDefinition character, int index, out SkillDefinition skill)
        {
            skill = null;
            return character != null && skills.TryGetValue(character.GetActiveSkillId(index), out skill);
        }

        private void FindReferences()
        {
            if (gameFlow == null) gameFlow = GetComponent<PlayerTurnDealController>();
            if (attackSelection == null) attackSelection = GetComponent<AttackSelectionController>();
            if (playerCharacterGroups == null) playerCharacterGroups = GameObject.Find("PlayerCharacterGroups")?.transform;
            if (enemyCharacterGroups == null) enemyCharacterGroups = GameObject.Find("EnemyCharacterGroups")?.transform;
            if (skillGroup == null) skillGroup = FindSkillPerformanceGroup();
            if (skillNameText == null && skillGroup != null)
            {
                Transform skillName = skillGroup.transform.Find("SkillName");
                skillNameText = skillName != null ? skillName.GetComponentInChildren<TMP_Text>(true) : null;
            }
            if (skillGroup != null) skillGroup.SetActive(false);
        }

        private static GameObject FindSkillPerformanceGroup()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "SkillGroup" && child.Find("SkillName") != null) return child.gameObject;
                }
            }
            return null;
        }
        private static void AddById<T>(IEnumerable<T> source, Func<T, string> id, Dictionary<string, T> destination, string label) where T : class
        {
            if (source == null) return;
            foreach (T value in source)
            {
                string key = value != null ? id(value) : null;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (destination.ContainsKey(key)) Debug.LogWarning($"Duplicate {label} ID '{key}' was ignored.");
                else destination.Add(key, value);
            }
        }

        private readonly struct AttackPlan
        {
            public readonly CharacterAttackTarget AttackerTarget;
            public readonly CharacterAttackTarget TargetTarget;
            public readonly CharacterCombatantView AttackerView;
            public readonly CharacterCombatantView TargetView;
            public readonly IReadOnlyList<SkillResourcePlan> Skills;

            public AttackPlan(
                CharacterAttackTarget attackerTarget,
                CharacterAttackTarget targetTarget,
                CharacterCombatantView attackerView,
                CharacterCombatantView targetView,
                IReadOnlyList<SkillResourcePlan> skills)
            {
                AttackerTarget = attackerTarget;
                TargetTarget = targetTarget;
                AttackerView = attackerView;
                TargetView = targetView;
                Skills = skills;
            }
        }
        private readonly struct SkillExecution
        {
            public readonly CharacterCombatantView Attacker;
            public readonly CharacterCombatantView Target;
            public readonly SkillDefinition Skill;
            public SkillExecution(CharacterCombatantView attacker, CharacterCombatantView target, SkillDefinition skill)
            {
                Attacker = attacker; Target = target; Skill = skill;
            }
        }

        private sealed class ArithmeticParser
        {
            private readonly string text;
            private int index;
            private ArithmeticParser(string text) { this.text = text; }
            public static bool TryEvaluate(string text, out double value)
            {
                try
                {
                    var parser = new ArithmeticParser(text);
                    value = parser.ParseExpression();
                    parser.SkipSpaces();
                    return parser.index == text.Length;
                }
                catch { value = 0; return false; }
            }
            private double ParseExpression()
            {
                double value = ParseTerm();
                while (true)
                {
                    SkipSpaces();
                    if (Take('+')) value += ParseTerm();
                    else if (Take('-')) value -= ParseTerm();
                    else return value;
                }
            }
            private double ParseTerm()
            {
                double value = ParseFactor();
                while (true)
                {
                    SkipSpaces();
                    if (Take('*')) value *= ParseFactor();
                    else if (Take('/')) value /= ParseFactor();
                    else return value;
                }
            }
            private double ParseFactor()
            {
                SkipSpaces();
                if (Take('-')) return -ParseFactor();
                if (Take('('))
                {
                    double value = ParseExpression();
                    if (!Take(')')) throw new FormatException();
                    return value;
                }
                int start = index;
                while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.')) index++;
                if (start == index) throw new FormatException();
                return double.Parse(text.Substring(start, index - start), CultureInfo.InvariantCulture);
            }
            private bool Take(char value)
            {
                SkipSpaces();
                if (index >= text.Length || text[index] != value) return false;
                index++;
                return true;
            }
            private void SkipSpaces() { while (index < text.Length && char.IsWhiteSpace(text[index])) index++; }
        }
    }
}
