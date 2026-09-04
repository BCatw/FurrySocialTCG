using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using FurrySocialCard.CardData;
using FurrySocialCard.CharacterData;
using UnityEngine;

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

        private readonly Dictionary<string, CharacterDefinition> characters = new Dictionary<string, CharacterDefinition>();
        private readonly Dictionary<string, SkillDefinition> skills = new Dictionary<string, SkillDefinition>();
        private readonly Dictionary<string, EffectDefinition> effects = new Dictionary<string, EffectDefinition>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<CharacterAttackTarget, CharacterCombatantView> combatants = new Dictionary<CharacterAttackTarget, CharacterCombatantView>();
        private readonly Dictionary<string, List<PatternRequirement>> parsedPatterns = new Dictionary<string, List<PatternRequirement>>();

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
            if (attackSelection != null) attackSelection.AttackConfirmed += ResolveAttacks;
            RefreshSkillAvailability();
        }

        private void OnDestroy()
        {
            if (gameFlow != null)
            {
                gameFlow.ResourceCardsChanged -= RefreshSkillAvailability;
                gameFlow.EnemyResourceCardsChanged -= RefreshSkillAvailability;
            }
            if (attackSelection != null) attackSelection.AttackConfirmed -= ResolveAttacks;
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
                Transform child = group.GetChild(index);
                bool hasSlot = teamIds != null && index < teamIds.Length && !string.IsNullOrWhiteSpace(teamIds[index]);
                if (!hasSlot || !characters.TryGetValue(teamIds[index], out CharacterDefinition definition))
                {
                    child.gameObject.SetActive(false);
                    if (hasSlot) Debug.LogWarning($"Character '{teamIds[index]}' was not found; slot {index + 1} is disabled.", this);
                    continue;
                }

                child.gameObject.SetActive(true);
                var skillDefinitions = new SkillDefinition[3];
                for (int skillIndex = 0; skillIndex < 3; skillIndex++) skills.TryGetValue(definition.GetActiveSkillId(skillIndex), out skillDefinitions[skillIndex]);
                CharacterCombatantView view = child.GetComponent<CharacterCombatantView>();
                if (view == null) view = child.gameObject.AddComponent<CharacterCombatantView>();
                sprites.TryGetValue(definition.id, out Sprite portrait);
                view.Bind(definition, skillDefinitions, portrait, isAlly);
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

        private void ResolveAttacks(IReadOnlyDictionary<CharacterAttackTarget, CharacterAttackTarget> attackTargets)
        {
            ResolveAttacksForSide(playerCharacterGroups, attackTargets, true);
        }

        public void ResolveEnemyAttacks(IReadOnlyDictionary<CharacterAttackTarget, CharacterAttackTarget> attackTargets)
        {
            ResolveAttacksForSide(enemyCharacterGroups, attackTargets, false);
        }

        private void ResolveAttacksForSide(Transform attackerGroup, IReadOnlyDictionary<CharacterAttackTarget, CharacterAttackTarget> attackTargets, bool isPlayer)
        {
            if (gameFlow == null || attackerGroup == null || attackTargets == null) return;
            List<CardObject> resourceSnapshot = isPlayer
                ? gameFlow.GetAvailableResourceCardsSnapshot()
                : gameFlow.GetAvailableEnemyResourceCardsSnapshot();
            var executions = new List<SkillExecution>();

            for (int childIndex = 0; childIndex < attackerGroup.childCount; childIndex++)
            {
                CharacterAttackTarget attacker = attackerGroup.GetChild(childIndex).GetComponent<CharacterAttackTarget>();
                if (attacker == null || !attackTargets.TryGetValue(attacker, out CharacterAttackTarget target)) continue;
                if (!combatants.TryGetValue(attacker, out CharacterCombatantView attackerView) || !combatants.TryGetValue(target, out CharacterCombatantView targetView)) continue;
                for (int skillIndex = 0; skillIndex < 3; skillIndex++)
                {
                    if (TryGetSkill(attackerView.Definition, skillIndex, out SkillDefinition skill) && IsUsable(skill, resourceSnapshot))
                    {
                        executions.Add(new SkillExecution(attackerView, targetView, skill));
                    }
                }
            }

            foreach (SkillExecution execution in executions) ExecuteSkill(execution, resourceSnapshot, isPlayer);
            RefreshSkillAvailability();
        }

        private void ExecuteSkill(SkillExecution execution, List<CardObject> resourceSnapshot, bool isPlayer)
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

            if (!TryGetRequirements(execution.Skill, out List<PatternRequirement> requirements)) return;
            List<CardObject> usedCards = SelectRequiredCards(requirements, resourceSnapshot);
            if (string.Equals(execution.Skill.resourceBehavior, "Tap", StringComparison.OrdinalIgnoreCase))
            {
                if (isPlayer) gameFlow.TapResources(usedCards);
                else gameFlow.TapEnemyResources(usedCards);
            }
            else if (string.Equals(execution.Skill.resourceBehavior, "Consume", StringComparison.OrdinalIgnoreCase))
            {
                if (isPlayer) gameFlow.ConsumeResources(usedCards);
                else gameFlow.ConsumeEnemyResources(usedCards);
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

        private static List<CardObject> SelectRequiredCards(List<PatternRequirement> requirements, List<CardObject> resources)
        {
            var selected = new List<CardObject>();
            var seen = new HashSet<CardObject>();
            foreach (PatternRequirement requirement in requirements)
            {
                int needed = requirement.Comparison == PatternComparison.GreaterThan ? requirement.Count + 1 : requirement.Count;
                foreach (CardObject card in resources)
                {
                    if (needed <= 0) break;
                    if (!Matches(requirement, card)) continue;
                    if (seen.Add(card)) selected.Add(card);
                    needed--;
                }
            }
            return selected;
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
