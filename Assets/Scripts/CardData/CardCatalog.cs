using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace FurrySocialCard.CardData
{
    public sealed class CardCatalog
    {
        private readonly ReadOnlyCollection<CardDefinition> cards;
        private readonly Dictionary<string, CardDefinition> cardsById;
        private readonly ReadOnlyCollection<DeckDefinition> decks;
        private readonly Dictionary<string, DeckDefinition> decksById;

        public int SchemaVersion { get; }
        public string DataVersion { get; }
        public IReadOnlyList<CardDefinition> Cards => cards;
        public IReadOnlyList<DeckDefinition> Decks => decks;

        private CardCatalog(CardCatalogDocument document)
        {
            SchemaVersion = document.schemaVersion;
            DataVersion = document.dataVersion;
            cards = document.cards.AsReadOnly();
            cardsById = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
            decks = document.decks.AsReadOnly();
            decksById = new Dictionary<string, DeckDefinition>(StringComparer.Ordinal);

            foreach (CardDefinition card in document.cards)
            {
                cardsById.Add(card.id, card);
            }

            foreach (DeckDefinition deck in document.decks)
            {
                decksById.Add(deck.id, deck);
            }
        }

        public bool TryGetCard(string cardId, out CardDefinition card)
        {
            return cardsById.TryGetValue(cardId, out card);
        }

        public bool TryGetDeck(string deckId, out DeckDefinition deck)
        {
            return decksById.TryGetValue(deckId, out deck);
        }

        public static bool TryParse(string json, out CardCatalog catalog, out string error)
        {
            catalog = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Card catalog JSON is empty.";
                return false;
            }

            CardCatalogDocument document;
            try
            {
                document = JsonUtility.FromJson<CardCatalogDocument>(json);
            }
            catch (Exception exception)
            {
                error = $"Card catalog JSON cannot be parsed: {exception.Message}";
                return false;
            }

            if (!TryValidate(document, out error))
            {
                return false;
            }

            catalog = new CardCatalog(document);
            return true;
        }

        private static bool TryValidate(CardCatalogDocument document, out string error)
        {
            if (document == null)
            {
                error = "Card catalog document is missing.";
                return false;
            }

            if (document.schemaVersion != 1)
            {
                error = $"Unsupported schemaVersion: {document.schemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(document.dataVersion))
            {
                error = "dataVersion is required.";
                return false;
            }

            if (document.cards == null || document.cards.Count == 0)
            {
                error = "At least one card is required.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var serialNumbers = new HashSet<int>();

            for (int index = 0; index < document.cards.Count; index++)
            {
                CardDefinition card = document.cards[index];
                if (card == null)
                {
                    error = $"cards[{index}] is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(card.id))
                {
                    error = $"cards[{index}].id is required.";
                    return false;
                }

                if (!ids.Add(card.id))
                {
                    error = $"Duplicate card id: {card.id}.";
                    return false;
                }

                if (card.serialNumber <= 0 || !serialNumbers.Add(card.serialNumber))
                {
                    error = $"serialNumber must be positive and unique: {card.serialNumber}.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(card.color) || string.IsNullOrWhiteSpace(card.shape))
                {
                    error = $"Card {card.id} requires color and shape.";
                    return false;
                }

                if (card.points <= 0)
                {
                    error = $"Card {card.id} points must be positive.";
                    return false;
                }
            }

            if (document.decks == null || document.decks.Count == 0)
            {
                error = "At least one deck is required.";
                return false;
            }

            var deckIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DeckDefinition deck in document.decks)
            {
                if (deck == null || string.IsNullOrWhiteSpace(deck.id) || !deckIds.Add(deck.id))
                {
                    error = "Deck ids are required and must be unique.";
                    return false;
                }

                if (deck.cardIds == null || deck.cardIds.Count == 0)
                {
                    error = $"Deck {deck.id} must contain at least one card.";
                    return false;
                }

                var cardsInDeck = new HashSet<string>(StringComparer.Ordinal);
                foreach (string cardId in deck.cardIds)
                {
                    if (!ids.Contains(cardId))
                    {
                        error = $"Deck {deck.id} references unknown card: {cardId}.";
                        return false;
                    }

                    if (!cardsInDeck.Add(cardId))
                    {
                        error = $"Deck {deck.id} contains duplicate card: {cardId}.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}