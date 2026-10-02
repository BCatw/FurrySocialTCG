using System;
using System.Collections.Generic;

namespace FurrySocialCard.CharacterData
{
    // Shared by execution and preview. A climax is an event, not a persistent full bar.
    public sealed class CharacterBattleState
    {
        public int Climax { get; private set; }
        public int Stamina { get; private set; }
        public int RestOwnerTurns { get; private set; }
        public int Limit { get; private set; }
        public bool IsSaint => Stamina <= 0;
        public bool CanAct => !IsSaint && RestOwnerTurns == 0;
        public CharacterBattleState(int limit, int stamina)
        { Limit = Math.Max(1, limit); Stamina = Math.Max(1, stamina); }
        public CharacterBattleState Copy() => (CharacterBattleState)MemberwiseClone();
        public void CompleteOwnerTurn() { RestOwnerTurns = Math.Max(0, RestOwnerTurns - 1); }
        public bool Apply(int delta, bool force, bool ownTurn)
        {
            if (!CanAct) return false;
            Climax = force ? Limit : (int)Math.Max(0L, Math.Min(Limit, (long)Climax + delta));
            if (Climax < Limit) return false;
            Climax = 0;
            Stamina--;
            RestOwnerTurns = ownTurn ? 2 : 1;
            return true;
        }
    }

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
        public int stamina = 1;
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
