using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class ResourceTableController : MonoBehaviour
    {
        private static readonly string[] TierOrder = { "獸徵", "法術", "器具", "動作" };
        private static readonly string[] AttributeOrder = { "束縛", "撫摸", "震動", "濕潤", "衝擊", "侵入" };

        [SerializeField] private PlayerTurnDealController gameFlow;
        [SerializeField] private CharacterBattleController characterBattle;
        [SerializeField] private RectTransform table;
        [SerializeField] private RectTransform tableTargetPosition;
        [SerializeField] private RectTransform resourceGrid;
        [SerializeField] private Button tabButton;
        [SerializeField, Min(0f)] private float moveDurationSeconds = 0.25f;
        [Header("Action Preview")]
        [SerializeField] private Color actionGlowColor = Color.white;
        [SerializeField] private Color actionChangedColor = new Color32(255, 80, 80, 255);
        [SerializeField] private Color actionUnchangedColor = new Color32(90, 220, 120, 255);
        [SerializeField, Range(0f, 2f)] private float categoryGlowIntensity = 0.45f;
        [SerializeField, Range(0f, 2f)] private float consumedCellGlowIntensity = 1f;
        [Header("Skill Requirement Preview")]
        [SerializeField] private Color skillGlowColor = new Color32(255, 220, 40, 255);
        [SerializeField] private Color skillSurplusColor = new Color32(90, 220, 120, 255);
        [SerializeField] private Color skillExactColor = new Color32(255, 220, 40, 255);
        [SerializeField] private Color skillMissingColor = new Color32(145, 145, 145, 255);
        [SerializeField] private Color skillInvalidColor = new Color32(255, 80, 80, 255);

        private readonly Dictionary<ResourceCellKey, int> counts = new Dictionary<ResourceCellKey, int>();
        private readonly Dictionary<ResourceCellKey, ResourceCellView> cells = new Dictionary<ResourceCellKey, ResourceCellView>();
        private readonly Dictionary<string, ResourceCellView> mediumSummaryCells = new Dictionary<string, ResourceCellView>();
        private readonly Dictionary<string, ResourceCellView> interactionSummaryCells = new Dictionary<string, ResourceCellView>();
        private Vector2 closedPosition;
        private bool isOpen;
        private bool boardLocked;
        private bool sourcesBound;
        private Coroutine bindRoutine;
        private ResourceActionPlan actionPlan;
        private SkillRequirementPreview skillPreview;

        public RectTransform ResourceGrid => resourceGrid;

        private void Awake()
        {
            FindReferences();
            if (table != null)
            {
                closedPosition = table.anchoredPosition;
            }
            Refresh();
        }

        private void OnEnable()
        {
            FindReferences();
            tabButton?.onClick.AddListener(Toggle);
            TryBindSources();
            if (!sourcesBound) bindRoutine = StartCoroutine(BindWhenAvailable());
            Refresh();
        }

        private void OnDisable()
        {
            tabButton?.onClick.RemoveListener(Toggle);
            if (bindRoutine != null) StopCoroutine(bindRoutine);
            bindRoutine = null;
            UnbindSources();
            table?.DOKill();
        }

        public void Toggle()
        {
            if (!boardLocked) SetOpen(!isOpen);
        }
        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        public void SetOpen(bool open)
        {
            if (table == null || tableTargetPosition == null)
            {
                return;
            }

            isOpen = open;
            Vector2 destination = isOpen ? tableTargetPosition.anchoredPosition : closedPosition;
            table.DOKill();
            DOTween.To(
                    () => table.anchoredPosition,
                    position => table.anchoredPosition = position,
                    destination,
                    moveDurationSeconds)
                .SetEase(Ease.InOutQuad)
                .SetLink(table.gameObject);
        }

        public void Refresh()
        {
            FindReferences();
            counts.Clear();
            if (gameFlow != null)
            {
                foreach (CardObject card in gameFlow.ResourceCards)
                {
                    if (card?.Definition == null)
                    {
                        continue;
                    }

                    var key = new ResourceCellKey(card.Definition.Tier, card.Definition.Attribute);
                    counts.TryGetValue(key, out int count);
                    counts[key] = count + 1;
                }
            }

            if (resourceGrid == null)
            {
                return;
            }

            cells.Clear();
            int cellIndex = 0;
            for (int tierIndex = 0; tierIndex < TierOrder.Length; tierIndex++)
            {
                for (int attributeIndex = 0; attributeIndex < AttributeOrder.Length; attributeIndex++)
                {
                    if (cellIndex >= resourceGrid.childCount)
                    {
                        Debug.LogWarning("ResourceGrid needs 24 ResourceCell children.", this);
                        return;
                    }

                    Transform child = resourceGrid.GetChild(cellIndex++);
                    ResourceCellView cell = child.GetComponent<ResourceCellView>();
                    if (cell == null)
                    {
                        cell = child.gameObject.AddComponent<ResourceCellView>();
                    }

                    var key = new ResourceCellKey(TierOrder[tierIndex], AttributeOrder[attributeIndex]);
                    counts.TryGetValue(key, out int count);
                    cell.Bind(key.Medium, key.InteractionType, count);
                    cells[key] = cell;
                }
            }
            CacheSummaryCells();
            BindSummaryCells();
            ApplyPreview();
        }

        public void SetPatternMode(bool active)
        {
            if (resourceGrid == null)
            {
                return;
            }

            for (int index = 0; index < resourceGrid.childCount; index++)
            {
                ResourceCellView cell = resourceGrid.GetChild(index).GetComponent<ResourceCellView>();
                cell?.SetPatternMode(active);
            }

            if (!active)
            {
                Refresh();
            }
        }

        private void FindReferences()
        {
            if (table == null)
            {
                table = transform as RectTransform;
            }
            if (resourceGrid == null && table != null)
            {
                resourceGrid = table.Find("ResourceGrid") as RectTransform;
            }
            if (gameFlow == null)
            {
                gameFlow = FindObjectOfType<PlayerTurnDealController>();
            }
            if (characterBattle == null) characterBattle = FindObjectOfType<CharacterBattleController>();

            if (tableTargetPosition == null && table != null)
            {
                tableTargetPosition = table.parent?.Find("TableTargetPosition") as RectTransform;
            }
            if (tabButton == null && table != null)
            {
                Transform tab = table.Find("Text (TMP)");
                if (tab != null)
                {
                    tabButton = tab.GetComponent<Button>();
                    if (tabButton == null)
                    {
                        tabButton = tab.gameObject.AddComponent<Button>();
                        tabButton.targetGraphic = tab.GetComponent<Graphic>();
                    }
                }
            }
        }

        private IEnumerator BindWhenAvailable()
        {
            while (isActiveAndEnabled && !sourcesBound)
            {
                FindReferences();
                TryBindSources();
                if (!sourcesBound) yield return null;
            }
            bindRoutine = null;
        }

        private void TryBindSources()
        {
            if (sourcesBound || gameFlow == null || characterBattle == null) return;
            gameFlow.ResourceCardsChanged += Refresh;
            gameFlow.PhaseChanged += HandlePhaseChanged;
            characterBattle.ResourceActionPreviewChanged += HandleActionPreviewChanged;
            characterBattle.SkillRequirementPreviewChanged += HandleSkillPreviewChanged;
            sourcesBound = true;
            HandlePhaseChanged(gameFlow.CurrentPhase);
        }

        private void UnbindSources()
        {
            if (!sourcesBound) return;
            if (gameFlow != null)
            {
                gameFlow.ResourceCardsChanged -= Refresh;
                gameFlow.PhaseChanged -= HandlePhaseChanged;
            }
            if (characterBattle != null)
            {
                characterBattle.ResourceActionPreviewChanged -= HandleActionPreviewChanged;
                characterBattle.SkillRequirementPreviewChanged -= HandleSkillPreviewChanged;
            }
            sourcesBound = false;
        }

        private void HandlePhaseChanged(PlayerTurnDealController.Phase phase)
        {
            if (phase == PlayerTurnDealController.Phase.AttackSelection)
            {
                boardLocked = false;
                Open();
                Refresh();
                return;
            }
            if (phase == PlayerTurnDealController.Phase.ResourcePaymentSelection)
            {
                boardLocked = true;
                Open();
                Refresh();
                return;
            }
            boardLocked = false;
            actionPlan = null;
            skillPreview = null;
            Close();
            Refresh();
        }

        private void HandleActionPreviewChanged(ResourceActionPlan plan) { actionPlan = plan; Refresh(); }
        private void HandleSkillPreviewChanged(SkillRequirementPreview preview) { skillPreview = preview; Refresh(); }

        private void ApplyPreview()
        {
            if (skillPreview != null) { ApplySkillPreview(); return; }
            if (actionPlan?.CellChanges == null) return;
            foreach (KeyValuePair<ResourceCellKey, ResourceCellView> pair in cells)
                if (actionPlan.CellChanges.TryGetValue(pair.Key, out ResourceCellChange change))
                    pair.Value.ShowActionPreview(change.BeforeAvailable, change.AfterAvailable, change.IsRelevant,
                        actionGlowColor, actionChangedColor, actionUnchangedColor,
                        change.BeforeAvailable != change.AfterAvailable ? consumedCellGlowIntensity : categoryGlowIntensity);
            ApplyActionSummaryPreview();
        }

        private void ApplySkillPreview()
        {
            foreach (KeyValuePair<ResourceCellKey, ResourceCellView> pair in cells)
            {
                var lines = new StringBuilder();
                Color selectedColor = skillSurplusColor;
                int severity = -1;
                foreach (PatternRequirement requirement in skillPreview.Requirements)
                {
                    if (!RequirementTouchesCell(requirement, pair.Key, skillPreview.AvailableResources)) continue;
                    int actual = CountMatching(requirement, skillPreview.AvailableResources);
                    FormatRequirement(requirement, actual, out string text, out Color color, out int itemSeverity);
                    if (lines.Length > 0) lines.AppendLine();
                    lines.Append(text);
                    if (itemSeverity > severity) { severity = itemSeverity; selectedColor = color; }
                }
                if (lines.Length > 0) pair.Value.ShowSkillRequirement(lines.ToString(), selectedColor, skillGlowColor);
            }
            ApplySkillSummaryPreview();
        }

        private void CacheSummaryCells()
        {
            mediumSummaryCells.Clear();
            interactionSummaryCells.Clear();
            Transform mediumRoot = FindDescendant(transform.root, "ResourceMediumSum");
            Transform interactionRoot = FindDescendant(transform.root, "InteractionTypeSum");
            CacheSummaryGroup(mediumRoot, TierOrder, mediumSummaryCells);
            CacheSummaryGroup(interactionRoot, AttributeOrder, interactionSummaryCells);
        }

        private static void CacheSummaryGroup(Transform root, IReadOnlyList<string> order,
            IDictionary<string, ResourceCellView> destination)
        {
            if (root == null) return;
            ResourceCellView[] views = root.GetComponentsInChildren<ResourceCellView>(true);
            for (int index = 0; index < views.Length && index < order.Count; index++)
                destination[order[index]] = views[index];
        }

        private void BindSummaryCells()
        {
            foreach (string medium in TierOrder)
            {
                int total = 0;
                foreach (string interaction in AttributeOrder)
                    if (counts.TryGetValue(new ResourceCellKey(medium, interaction), out int value)) total += value;
                if (mediumSummaryCells.TryGetValue(medium, out ResourceCellView cell))
                    cell.BindSummary(PatternScope.ResourceMedium, medium, total);
            }
            foreach (string interaction in AttributeOrder)
            {
                int total = 0;
                foreach (string medium in TierOrder)
                    if (counts.TryGetValue(new ResourceCellKey(medium, interaction), out int value)) total += value;
                if (interactionSummaryCells.TryGetValue(interaction, out ResourceCellView cell))
                    cell.BindSummary(PatternScope.InteractionType, interaction, total);
            }
        }

        private void ApplyActionSummaryPreview()
        {
            foreach (string medium in TierOrder)
                ApplySummaryAction(mediumSummaryCells, medium, true);
            foreach (string interaction in AttributeOrder)
                ApplySummaryAction(interactionSummaryCells, interaction, false);
        }

        private void ApplySummaryAction(IReadOnlyDictionary<string, ResourceCellView> summaries,
            string category, bool byMedium)
        {
            if (!summaries.TryGetValue(category, out ResourceCellView cell)) return;
            int before = 0, after = 0;
            bool relevant = false;
            foreach (KeyValuePair<ResourceCellKey, ResourceCellChange> pair in actionPlan.CellChanges)
            {
                if ((byMedium ? pair.Key.Medium : pair.Key.InteractionType) != category) continue;
                before += pair.Value.BeforeAvailable;
                after += pair.Value.AfterAvailable;
                relevant |= pair.Value.IsRelevant;
            }
            cell.ShowActionPreview(before, after, relevant, actionGlowColor,
                actionChangedColor, actionUnchangedColor, categoryGlowIntensity);
        }

        private void ApplySkillSummaryPreview()
        {
            foreach (PatternRequirement requirement in skillPreview.Requirements)
            {
                IReadOnlyDictionary<string, ResourceCellView> summaries = null;
                string category = null;
                if (requirement.Scope == PatternScope.ResourceMedium)
                { summaries = mediumSummaryCells; category = requirement.Medium; }
                else if (requirement.Scope == PatternScope.InteractionType)
                { summaries = interactionSummaryCells; category = requirement.InteractionType; }
                if (summaries == null || !summaries.TryGetValue(category, out ResourceCellView cell)) continue;
                int actual = CountMatching(requirement, skillPreview.AvailableResources);
                FormatRequirement(requirement, actual, out string label, out Color color, out _);
                cell.ShowSkillRequirement(label, color, skillGlowColor);
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child;
            return null;
        }

        private void FormatRequirement(PatternRequirement requirement, int actual, out string text, out Color color, out int severity)
        {
            int minimum = requirement.Comparison == PatternComparison.GreaterThan ? requirement.Count + 1 : requirement.Count;
            if (requirement.Comparison == PatternComparison.Equal && actual > requirement.Count)
            { text = $"{actual}（+{actual - requirement.Count}）"; color = skillInvalidColor; severity = 3; }
            else if (actual < minimum)
            { text = $"{actual}（-{minimum - actual}）"; color = skillMissingColor; severity = 2; }
            else if (actual == minimum)
            { text = actual.ToString(); color = skillExactColor; severity = 1; }
            else
            { text = $"{actual}（+{actual - minimum}）"; color = skillSurplusColor; severity = 0; }
        }

        private static bool RequirementTouchesCell(PatternRequirement requirement, ResourceCellKey key, IReadOnlyList<CardObject> resources)
        {
            switch (requirement.Scope)
            {
                case PatternScope.ResourceMedium: return key.Medium == requirement.Medium;
                case PatternScope.InteractionType: return key.InteractionType == requirement.InteractionType;
                case PatternScope.MediumAndInteraction: return key.Medium == requirement.Medium && key.InteractionType == requirement.InteractionType;
                case PatternScope.SpecificCard:
                    if (resources == null) return false;
                    foreach (CardObject card in resources)
                        if (card?.Definition != null && card.Definition.id == requirement.CardId)
                            return key.Medium == card.Definition.Tier && key.InteractionType == card.Definition.Attribute;
                    return false;
                default: return false;
            }
        }

        private static int CountMatching(PatternRequirement requirement, IReadOnlyList<CardObject> resources)
        {
            int count = 0;
            if (resources == null) return count;
            foreach (CardObject card in resources)
            {
                if (card?.Definition == null) continue;
                bool matches;
                switch (requirement.Scope)
                {
                    case PatternScope.ResourceMedium: matches = card.Definition.Tier == requirement.Medium; break;
                    case PatternScope.InteractionType: matches = card.Definition.Attribute == requirement.InteractionType; break;
                    case PatternScope.MediumAndInteraction: matches = card.Definition.Tier == requirement.Medium && card.Definition.Attribute == requirement.InteractionType; break;
                    default: matches = card.Definition.id == requirement.CardId; break;
                }
                if (matches) count++;
            }
            return count;
        }
    }
}
