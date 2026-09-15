using System;
using System.Collections.Generic;
using FurrySocialCard.CharacterData;

namespace FurrySocialCard.CardPresentation
{
    public readonly struct ResourceCellKey : IEquatable<ResourceCellKey>
    {
        public readonly string Medium;
        public readonly string InteractionType;

        public ResourceCellKey(string medium, string interactionType)
        {
            Medium = medium;
            InteractionType = interactionType;
        }

        public bool Equals(ResourceCellKey other) => Medium == other.Medium && InteractionType == other.InteractionType;
        public override bool Equals(object obj) => obj is ResourceCellKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((Medium != null ? Medium.GetHashCode() : 0) * 397) ^ (InteractionType != null ? InteractionType.GetHashCode() : 0); }
        }
    }

    public sealed class ResourceCellChange
    {
        public int BeforeAvailable;
        public int AfterAvailable;
        public int TapCount;
        public int ConsumeCount;
        public bool IsRelevant;
    }

    public sealed class SkillResourcePlan
    {
        public CharacterCombatantView Attacker;
        public CharacterCombatantView Target;
        public int SkillIndex;
        public SkillDefinition Skill;
        public IReadOnlyList<PatternRequirement> Requirements;
        public IReadOnlyList<CardObject> PaymentCards;
    }

    public sealed class ResourceActionPlan
    {
        public IReadOnlyList<SkillResourcePlan> Skills;
        public IReadOnlyDictionary<ResourceCellKey, ResourceCellChange> CellChanges;
        public IReadOnlyList<CardObject> SelectableConsumeCards;
        public int RequiredConsumeCount;
        public bool RequiresConsumeSelection;
    }

    public sealed class SkillRequirementPreview
    {
        public IReadOnlyList<PatternRequirement> Requirements;
        public IReadOnlyList<CardObject> AvailableResources;
    }
}
