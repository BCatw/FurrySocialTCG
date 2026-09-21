using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class PatternCreatorController : MonoBehaviour
    {
        private static readonly string[] MediumOrder = { "獸徵", "法術", "器具", "動作" };
        private static readonly string[] InteractionOrder = { "束縛", "撫摸", "震動", "濕潤", "衝擊", "侵入" };

        [SerializeField] private ResourceTableController resourceTable;
        [SerializeField] private GameObject patternStringCreator;
        [SerializeField] private Button openPatternCreatorButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private RectTransform interactionTypeEditor;
        [SerializeField] private RectTransform resourceMediumEditor;
        [SerializeField] private TMP_InputField patternInputField;
        [SerializeField] private Button loadPatternButton;

        private readonly List<ResourceCellView> mediumCells = new List<ResourceCellView>();
        private readonly List<ResourceCellView> interactionCells = new List<ResourceCellView>();
        private readonly List<ResourceCellView> combinedCells = new List<ResourceCellView>();
        private bool isOpen;

        private void Awake()
        {
            ConfigureCells();
            SetOpen(false);
        }

        private void OnEnable()
        {
            openPatternCreatorButton?.onClick.AddListener(Toggle);
            copyButton?.onClick.AddListener(CopyPatternString);
            loadPatternButton?.onClick.AddListener(LoadPatternString);
        }

        private void OnDisable()
        {
            openPatternCreatorButton?.onClick.RemoveListener(Toggle);
            copyButton?.onClick.RemoveListener(CopyPatternString);
            loadPatternButton?.onClick.RemoveListener(LoadPatternString);
        }

        public void Toggle() => SetOpen(!isOpen);

        public void CopyPatternString()
        {
            GUIUtility.systemCopyBuffer = BuildPatternString();
        }

        public string BuildPatternString()
        {
            var requirements = new List<PatternRequirement>();
            AddRequirements(requirements, mediumCells);
            AddRequirements(requirements, interactionCells);
            AddRequirements(requirements, combinedCells);
            return PatternExpressionBuilder.Build(requirements);
        }

        public void LoadPatternString()
        {
            string input = patternInputField != null ? patternInputField.text : null;
            if (!PatternExpressionParser.TryParse(input, out List<PatternRequirement> requirements, out string error))
            {
                Debug.LogWarning($"Pattern was not loaded: {error}", this);
                return;
            }

            var assignments = new List<CellAssignment>();
            foreach (PatternRequirement requirement in requirements)
            {
                if (requirement.Scope == PatternScope.SpecificCard)
                {
                    Debug.LogWarning("Pattern was not loaded: specific-card conditions are not supported by the current Pattern Creator UI.", this);
                    return;
                }

                ResourceCellView cell = FindCell(requirement);
                if (cell == null)
                {
                    Debug.LogWarning($"Pattern was not loaded: no editor cell exists for '{requirement.ToExpression()}'.", this);
                    return;
                }
                assignments.Add(new CellAssignment(cell, requirement));
            }

            ClearAllRequirements();
            foreach (CellAssignment assignment in assignments)
            {
                assignment.Cell.ApplyPatternRequirement(assignment.Requirement);
            }
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            patternStringCreator?.SetActive(open);
            resourceTable?.SetPatternMode(open);

            foreach (ResourceCellView cell in mediumCells)
            {
                cell.SetPatternMode(open);
            }
            foreach (ResourceCellView cell in interactionCells)
            {
                cell.SetPatternMode(open);
            }
        }

        private void ConfigureCells()
        {
            mediumCells.Clear();
            interactionCells.Clear();
            combinedCells.Clear();

            if (resourceMediumEditor != null)
            {
                for (int index = 0; index < Mathf.Min(MediumOrder.Length, resourceMediumEditor.childCount); index++)
                {
                    ResourceCellView cell = RequireCell(resourceMediumEditor.GetChild(index));
                    cell.ConfigurePattern(PatternScope.ResourceMedium, MediumOrder[index], null, true, false);
                    mediumCells.Add(cell);
                }
            }

            if (interactionTypeEditor != null)
            {
                for (int index = 0; index < Mathf.Min(InteractionOrder.Length, interactionTypeEditor.childCount); index++)
                {
                    ResourceCellView cell = RequireCell(interactionTypeEditor.GetChild(index));
                    cell.ConfigurePattern(PatternScope.InteractionType, null, InteractionOrder[index], false, true);
                    interactionCells.Add(cell);
                }
            }

            RectTransform grid = resourceTable != null ? resourceTable.ResourceGrid : null;
            if (grid == null)
            {
                return;
            }

            int cellIndex = 0;
            for (int mediumIndex = 0; mediumIndex < MediumOrder.Length; mediumIndex++)
            {
                for (int interactionIndex = 0; interactionIndex < InteractionOrder.Length; interactionIndex++)
                {
                    if (cellIndex >= grid.childCount)
                    {
                        return;
                    }
                    ResourceCellView cell = RequireCell(grid.GetChild(cellIndex++));
                    cell.ConfigurePattern(
                        PatternScope.MediumAndInteraction,
                        MediumOrder[mediumIndex],
                        InteractionOrder[interactionIndex],
                        true,
                        true);
                    combinedCells.Add(cell);
                }
            }
        }

        private static ResourceCellView RequireCell(Transform transform)
        {
            ResourceCellView cell = transform.GetComponent<ResourceCellView>();
            return cell != null ? cell : transform.gameObject.AddComponent<ResourceCellView>();
        }

        private static void AddRequirements(List<PatternRequirement> target, List<ResourceCellView> cells)
        {
            foreach (ResourceCellView cell in cells)
            {
                target.Add(cell.GetPatternRequirement());
            }
        }

        private ResourceCellView FindCell(PatternRequirement requirement)
        {
            List<ResourceCellView> source = requirement.Scope == PatternScope.ResourceMedium
                ? mediumCells
                : requirement.Scope == PatternScope.InteractionType
                    ? interactionCells
                    : combinedCells;

            foreach (ResourceCellView cell in source)
            {
                if (cell.PatternScope == requirement.Scope
                    && cell.Tier == requirement.Medium
                    && cell.Attribute == requirement.InteractionType)
                {
                    return cell;
                }
            }
            return null;
        }

        private void ClearAllRequirements()
        {
            ClearRequirements(mediumCells);
            ClearRequirements(interactionCells);
            ClearRequirements(combinedCells);
        }

        private static void ClearRequirements(List<ResourceCellView> cells)
        {
            foreach (ResourceCellView cell in cells)
            {
                cell.ClearPatternRequirement();
            }
        }

        private readonly struct CellAssignment
        {
            public readonly ResourceCellView Cell;
            public readonly PatternRequirement Requirement;

            public CellAssignment(ResourceCellView cell, PatternRequirement requirement)
            {
                Cell = cell;
                Requirement = requirement;
            }
        }
    }
}
