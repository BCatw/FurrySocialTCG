using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FurrySocialCard.CardPresentation
{
    public enum PatternScope
    {
        ResourceMedium,
        InteractionType,
        MediumAndInteraction,
        SpecificCard
    }

    public enum PatternComparison
    {
        GreaterThan,
        Equal,
        GreaterThanOrEqual
    }

    public readonly struct PatternRequirement
    {
        public readonly PatternScope Scope;
        public readonly string Medium;
        public readonly string InteractionType;
        public readonly string CardId;
        public readonly PatternComparison Comparison;
        public readonly int Count;

        public PatternRequirement(
            PatternScope scope,
            string medium,
            string interactionType,
            PatternComparison comparison,
            int count,
            string cardId = null)
        {
            Scope = scope;
            Medium = medium;
            InteractionType = interactionType;
            CardId = cardId;
            Comparison = comparison;
            Count = Math.Max(0, count);
        }

        public string ToExpression()
        {
            if (Count <= 0)
            {
                return null;
            }

            string target;
            switch (Scope)
            {
                case PatternScope.ResourceMedium:
                    target = $"〈{Medium}〉";
                    break;
                case PatternScope.InteractionType:
                    target = $"[{InteractionType}]";
                    break;
                case PatternScope.SpecificCard:
                    target = $"{{{CardId}}}";
                    break;
                default:
                    target = $"〈{Medium}〉[{InteractionType}]";
                    break;
            }

            return $"{target} {ComparisonSymbol(Comparison)} {Count}";
        }

        public static string ComparisonSymbol(PatternComparison comparison)
        {
            switch (comparison)
            {
                case PatternComparison.Equal: return "=";
                case PatternComparison.GreaterThanOrEqual: return ">=";
                default: return ">";
            }
        }

        public bool IsSatisfiedBy(int actualCount)
        {
            switch (Comparison)
            {
                case PatternComparison.Equal: return actualCount == Count;
                case PatternComparison.GreaterThanOrEqual: return actualCount >= Count;
                default: return actualCount > Count;
            }
        }

        public string IdentityKey()
        {
            return $"{Scope}|{Medium}|{InteractionType}|{CardId}";
        }
    }

    public static class PatternExpressionBuilder
    {
        public static string Build(IEnumerable<PatternRequirement> requirements)
        {
            var expressions = new List<string>();
            if (requirements != null)
            {
                foreach (PatternRequirement requirement in requirements)
                {
                    string expression = requirement.ToExpression();
                    if (!string.IsNullOrEmpty(expression))
                    {
                        expressions.Add(expression);
                    }
                }
            }
            return string.Join(" + ", expressions);
        }
    }

    public static class PatternExpressionParser
    {
        private static readonly Regex RequirementRegex = new Regex(
            @"^(?:〈(?<medium>[^〈〉\[\]{}+]+)〉(?:\[(?<combinedType>[^\[\]{}+]+)\])?|\[(?<interaction>[^\[\]{}+]+)\]|\{(?<card>[^{}+]+)\})\s*(?<operator>>=|>|=)\s*(?<count>\d+)$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> KnownMediums = new HashSet<string>
        {
            "獸徵", "法術", "器具", "動作"
        };

        private static readonly HashSet<string> KnownInteractionTypes = new HashSet<string>
        {
            "束縛", "撫摸", "震動", "濕潤", "衝擊", "侵入"
        };

        public static bool TryParse(string input, out List<PatternRequirement> requirements, out string error)
        {
            requirements = new List<PatternRequirement>();
            error = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Pattern cannot be empty.";
                requirements.Clear();
                return false;
            }

            string[] parts = input.Split('+');
            var identities = new HashSet<string>();
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index].Trim();
                Match match = RequirementRegex.Match(part);
                if (!match.Success)
                {
                    error = $"Condition {index + 1} has invalid syntax: '{part}'.";
                    requirements.Clear();
                    return false;
                }

                if (!int.TryParse(match.Groups["count"].Value, out int count))
                {
                    error = $"Condition {index + 1} has an invalid count.";
                    requirements.Clear();
                    return false;
                }

                PatternComparison comparison = ParseComparison(match.Groups["operator"].Value);
                PatternRequirement requirement;
                if (match.Groups["card"].Success)
                {
                    string cardId = match.Groups["card"].Value.Trim();
                    requirement = new PatternRequirement(PatternScope.SpecificCard, null, null, comparison, count, cardId);
                }
                else if (match.Groups["interaction"].Success)
                {
                    string interaction = match.Groups["interaction"].Value.Trim();
                    if (!KnownInteractionTypes.Contains(interaction))
                    {
                        error = $"Condition {index + 1} uses unknown Interaction Type '{interaction}'.";
                        requirements.Clear();
                        return false;
                    }
                    requirement = new PatternRequirement(PatternScope.InteractionType, null, interaction, comparison, count);
                }
                else
                {
                    string medium = match.Groups["medium"].Value.Trim();
                    if (!KnownMediums.Contains(medium))
                    {
                        error = $"Condition {index + 1} uses unknown Resource Medium '{medium}'.";
                        requirements.Clear();
                        return false;
                    }

                    if (match.Groups["combinedType"].Success)
                    {
                        string interaction = match.Groups["combinedType"].Value.Trim();
                        if (!KnownInteractionTypes.Contains(interaction))
                        {
                            error = $"Condition {index + 1} uses unknown Interaction Type '{interaction}'.";
                            requirements.Clear();
                            return false;
                        }
                        requirement = new PatternRequirement(PatternScope.MediumAndInteraction, medium, interaction, comparison, count);
                    }
                    else
                    {
                        requirement = new PatternRequirement(PatternScope.ResourceMedium, medium, null, comparison, count);
                    }
                }

                if (!identities.Add(requirement.IdentityKey()))
                {
                    error = $"Condition {index + 1} duplicates an earlier target.";
                    requirements.Clear();
                    return false;
                }
                requirements.Add(requirement);
            }
            return true;
        }

        private static PatternComparison ParseComparison(string value)
        {
            if (value == "=") return PatternComparison.Equal;
            if (value == ">=") return PatternComparison.GreaterThanOrEqual;
            return PatternComparison.GreaterThan;
        }
    }

    public static class PatternRequirementMatcher
    {
        public static bool AreAllSatisfied(
            IEnumerable<PatternRequirement> requirements,
            Func<PatternRequirement, int> countProvider)
        {
            if (requirements == null || countProvider == null)
            {
                return false;
            }

            foreach (PatternRequirement requirement in requirements)
            {
                if (!requirement.IsSatisfiedBy(countProvider(requirement)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
