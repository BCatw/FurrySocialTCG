using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FurrySocialCard.CardData
{
    [RequireComponent(typeof(CardCatalogLoader))]
    public sealed class CardDeckController : MonoBehaviour
    {
        [SerializeField] private CardCatalogLoader catalogLoader;
        [SerializeField] private string deckId = "FSC-INITIAL";
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField] private bool shuffleOnInitialize = true;
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int shuffleSeed = 12345;

        private readonly List<CardDefinition> drawPile = new List<CardDefinition>();
        private int nextCardIndex;

        public bool IsReady { get; private set; }
        public int RemainingCount => Mathf.Max(0, drawPile.Count - nextCardIndex);
        public string DeckId => deckId;
        public event Action Ready;
        public event Action<int> RemainingCountChanged;

        private void Reset() => catalogLoader = GetComponent<CardCatalogLoader>();

        private IEnumerator Start()
        {
            if (initializeOnStart)
            {
                yield return Initialize();
            }
        }

        public IEnumerator Initialize()
        {
            IsReady = false;
            catalogLoader ??= GetComponent<CardCatalogLoader>();
            CardCatalog catalog = null;
            string loadError = null;
            yield return catalogLoader.Load(value => catalog = value, error => loadError = error);

            if (catalog == null)
            {
                Debug.LogError($"Cannot initialize deck {deckId}: {loadError}", this);
                yield break;
            }

            if (!catalog.TryGetDeck(deckId, out DeckDefinition deck))
            {
                Debug.LogError($"Deck not found: {deckId}", this);
                yield break;
            }

            drawPile.Clear();
            foreach (string cardId in deck.cardIds)
            {
                if (!catalog.TryGetCard(cardId, out CardDefinition card))
                {
                    Debug.LogError($"Deck {deckId} references missing card {cardId}.", this);
                    drawPile.Clear();
                    yield break;
                }

                drawPile.Add(card);
            }

            nextCardIndex = 0;
            if (shuffleOnInitialize)
            {
                Shuffle();
            }

            IsReady = true;
            RemainingCountChanged?.Invoke(RemainingCount);
            Ready?.Invoke();
        }

        public bool TryDraw(out CardDefinition card)
        {
            if (!IsReady || nextCardIndex >= drawPile.Count)
            {
                card = null;
                return false;
            }

            card = drawPile[nextCardIndex++];
            RemainingCountChanged?.Invoke(RemainingCount);
            return true;
        }

        [ContextMenu("Reset Deck")]
        public void ResetDeck()
        {
            nextCardIndex = 0;
            RemainingCountChanged?.Invoke(RemainingCount);
        }

        [ContextMenu("Shuffle Deck")]
        public void Shuffle()
        {
            int seed = useRandomSeed ? Environment.TickCount : shuffleSeed;
            var random = new System.Random(seed);
            for (int index = drawPile.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                CardDefinition temporary = drawPile[index];
                drawPile[index] = drawPile[swapIndex];
                drawPile[swapIndex] = temporary;
            }

            nextCardIndex = 0;
            RemainingCountChanged?.Invoke(RemainingCount);
        }
    }
}
