using System;
using System.Collections.Generic;

namespace FurrySocialCard.CardData
{
    [Serializable]
    public sealed class CardCatalogDocument
    {
        public int schemaVersion;
        public string dataVersion;
        public List<CardDefinition> cards = new List<CardDefinition>();
        public List<DeckDefinition> decks = new List<DeckDefinition>();
    }
}
