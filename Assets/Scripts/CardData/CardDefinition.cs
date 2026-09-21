using System;

namespace FurrySocialCard.CardData
{
    [Serializable]
    public sealed class CardDefinition
    {
        public string id;
        public int serialNumber;

        // Schema v2 fields.
        public string attribute;
        public string tier;
        public string text;

        // Schema v1 compatibility fields. Kept so the legacy catalog remains usable.
        public string color;
        public string shape;
        public int points;

        public string Attribute => !string.IsNullOrWhiteSpace(attribute) ? attribute : shape;
        public string Tier => !string.IsNullOrWhiteSpace(tier) ? tier : color;
        public string DisplayText => !string.IsNullOrWhiteSpace(text) ? text : points > 0 ? points.ToString("00") : string.Empty;
    }
}
