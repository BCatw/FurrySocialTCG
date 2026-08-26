using System;

namespace FurrySocialCard.CardData
{
    [Serializable]
    public sealed class CardDefinition
    {
        public string id;
        public int serialNumber;
        public string color;
        public string shape;
        public int points;
    }
}
