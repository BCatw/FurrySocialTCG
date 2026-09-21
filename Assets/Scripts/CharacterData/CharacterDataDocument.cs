using System;
using System.Collections.Generic;

namespace FurrySocialCard.CharacterData
{
    [Serializable]
    public sealed class CharacterDataDocument
    {
        public int schemaVersion = 1;
        public string dataVersion;
        public List<CharacterDefinition> characters = new List<CharacterDefinition>();
        public List<SkillDefinition> skills = new List<SkillDefinition>();
        public List<EffectDefinition> effects = new List<EffectDefinition>();
    }

    [Serializable]
    public sealed class CharacterDefinition
    {
        public string id;
        public string displayName;
        public string tags;
        public int climaxLimit;
        public string activeSkill1Id;
        public string activeSkill2Id;
        public string activeSkill3Id;
        public string passiveSkillId;

        public string GetActiveSkillId(int index)
        {
            if (index == 0) return activeSkill1Id;
            if (index == 1) return activeSkill2Id;
            return activeSkill3Id;
        }
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string id;
        public string displayName;
        public string skillType;
        public string pattern;
        public string resourceBehavior;
        public List<string> effectIds = new List<string>();
        public string designStatus;
    }

    [Serializable]
    public sealed class EffectDefinition
    {
        public string id;
        public string effectType;
        public string target;
        public string value;
        public int durationTurns;
    }
}
