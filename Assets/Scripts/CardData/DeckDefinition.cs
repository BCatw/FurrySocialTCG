using System;
using System.Collections.Generic;

namespace FurrySocialCard.CardData
{
    [Serializable]
    public sealed class DeckDefinition
    {
        public string id;
        public string displayName;
        public List<string> cardIds = new List<string>();
    }
}