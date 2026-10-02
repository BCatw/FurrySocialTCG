using System;
using FurrySocialCard.CharacterData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class CharacterCombatantView : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private Slider climaxBar;
        [SerializeField] private Slider climaxPreviewBar;
        [SerializeField] private TMP_Text climaxValueText;
        [SerializeField] private TMP_Text battleStatusText;
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private TMP_Text[] skillTexts = new TMP_Text[3];
        [SerializeField] private TMP_Text[] skillSelfClimaxTexts = new TMP_Text[3];
        [SerializeField] private TMP_Text[] skillTargetClimaxTexts = new TMP_Text[3];
        [SerializeField] private Color usableSkillColor = new Color32(255, 215, 0, 255);

        private readonly Color[] normalSkillColors = new Color[3];
        public event Action<CharacterCombatantView, int, bool> SkillHoverChanged;
        public CharacterDefinition Definition { get; private set; }
        public CharacterBattleState BattleState { get; private set; }
        public int CurrentClimax => BattleState?.Climax ?? 0;
        public bool IsAlly { get; private set; }
        public int RemainingStamina => BattleState?.Stamina ?? 0;
        public int RestOwnerTurns => BattleState?.RestOwnerTurns ?? 0;
        public bool IsSaint => RemainingStamina <= 0;
        public bool CanAct => !IsSaint && RestOwnerTurns == 0;
        public void ResetBattleState()
        {
            BattleState = new CharacterBattleState(Definition.climaxLimit, Definition.stamina);
            ClearAttackPreview();
        }
        public void CompleteOwnerTurn()
        {
            BattleState.CompleteOwnerTurn();
            RefreshClimaxBar();
        }
        public bool ApplyClimax(int delta, bool force, bool ownTurn)
        {
            bool reached = BattleState.Apply(delta, force, ownTurn);
            RefreshClimaxBar();
            return reached;
        }

        public void Bind(CharacterDefinition definition, SkillDefinition[] skills, Sprite portrait, bool isAlly)
        {
            FindReferences();
            Definition = definition;
            IsAlly = isAlly;
            BattleState = new CharacterBattleState(definition.climaxLimit, definition.stamina);
            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = portrait != null;
                portraitImage.preserveAspect = true;
            }
            for (int index = 0; index < skillTexts.Length; index++)
            {
                if (skillTexts[index] == null) continue;
                skillTexts[index].text = skills != null && index < skills.Length && skills[index] != null
                    ? skills[index].displayName
                    : string.Empty;
                normalSkillColors[index] = skillTexts[index].color;
            }
            ClearAttackPreview();
            RefreshClimaxBar();
        }

        public void SetSkillAvailable(int index, bool available)
        {
            if (index < 0 || index >= skillTexts.Length || skillTexts[index] == null) return;
            skillTexts[index].color = available ? usableSkillColor : normalSkillColors[index];
        }

        public void SetClimaxPreview(int projectedClimax, bool visible)
        {
            if (Definition == null) return;
            int maximum = Mathf.Max(1, Definition.climaxLimit);
            int projected = Mathf.Clamp(projectedClimax, 0, maximum);
            int delta = projected - CurrentClimax;
            if (climaxPreviewBar != null)
            {
                climaxPreviewBar.gameObject.SetActive(visible);
                climaxPreviewBar.minValue = 0f;
                climaxPreviewBar.maxValue = maximum;
                climaxPreviewBar.value = projected;
            }
            if (climaxValueText != null)
                climaxValueText.text = visible
                    ? $"{CurrentClimax} ({delta:+#;-#;0}) / {maximum}"
                    : $"{CurrentClimax} / {maximum}";
        }

        public void SetStatePreview(CharacterBattleState projected)
        {
            bool changed = projected.Climax != CurrentClimax || projected.Stamina != RemainingStamina;
            SetClimaxPreview(projected.Climax, changed);
            if (battleStatusText != null && projected.Stamina != RemainingStamina)
                battleStatusText.text = $"Stamina {RemainingStamina} → {projected.Stamina} | 預計{(projected.IsSaint ? "聖人" : "休息")}（Token +1）";
        }

        public void SetActionCancelledPreview(string reason)
        {
            if (battleStatusText != null) battleStatusText.text = reason;
        }

        public void SetSkillClimaxPreview(int index, int selfDelta, int targetDelta, bool visible)
        {
            SetSignedValue(skillSelfClimaxTexts, index, selfDelta, visible && selfDelta != 0);
            SetSignedValue(skillTargetClimaxTexts, index, targetDelta, visible && targetDelta != 0);
        }

        public void ClearAttackPreview()
        {
            if (climaxPreviewBar != null) climaxPreviewBar.gameObject.SetActive(false);
            for (int index = 0; index < 3; index++)
            {
                SetSignedValue(skillSelfClimaxTexts, index, 0, false);
                SetSignedValue(skillTargetClimaxTexts, index, 0, false);
            }
            if (Definition != null) RefreshClimaxBar();
        }

        private void FindReferences()
        {
            if (portraitImage == null) portraitImage = transform.Find("Mask/Image")?.GetComponent<Image>();
            if (climaxBar == null) climaxBar = transform.Find("ClimaxBar")?.GetComponent<Slider>();
            if (climaxPreviewBar == null) climaxPreviewBar = transform.Find("ClimaxBar_Preview")?.GetComponent<Slider>();
            if (climaxValueText == null) climaxValueText = transform.Find("ClimaxBar/Value")?.GetComponentInChildren<TMP_Text>(true);
            Transform staminaRoot = transform.Find("Stamina");
            if (staminaText == null) staminaText = staminaRoot?.GetComponentInChildren<TMP_Text>(true);
            if (staminaRoot != null)
                foreach (UnityEngine.UI.Graphic graphic in staminaRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    graphic.raycastTarget = false;
            if (staminaText != null) staminaText.raycastTarget = false;
            if (battleStatusText == null && climaxValueText != null)
            {
                var status = new GameObject("BattleStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
                status.transform.SetParent(transform, false);
                battleStatusText = status.GetComponent<TextMeshProUGUI>();
                battleStatusText.font = climaxValueText.font;
                battleStatusText.fontSize = 22f;
                battleStatusText.alignment = TextAlignmentOptions.Center;
                battleStatusText.raycastTarget = false;
                var rect = (RectTransform)status.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(0f, 60f);
                rect.anchoredPosition = new Vector2(0f, 4f);
            }
            for (int index = 0; index < skillTexts.Length; index++)
            {
                Transform skillRoot = transform.Find($"SkillGroup/Skill_{index + 1}");
                if (skillTexts[index] == null) skillTexts[index] = skillRoot?.Find("Name")?.GetComponentInChildren<TMP_Text>(true);
                if (skillSelfClimaxTexts[index] == null) skillSelfClimaxTexts[index] = skillRoot?.Find("climax_self")?.GetComponentInChildren<TMP_Text>(true);
                if (skillTargetClimaxTexts[index] == null) skillTargetClimaxTexts[index] = skillRoot?.Find("climax_target")?.GetComponentInChildren<TMP_Text>(true);
                if (skillRoot != null)
                {
                    SkillHoverTarget hover = skillRoot.GetComponent<SkillHoverTarget>();
                    if (hover == null) hover = skillRoot.gameObject.AddComponent<SkillHoverTarget>();
                    int capturedIndex = index;
                    hover.Configure(active => SkillHoverChanged?.Invoke(this, capturedIndex, active));
                }
            }
        }

        private void RefreshClimaxBar()
        {
            if (Definition == null) return;
            if (climaxBar != null)
            {
                climaxBar.minValue = 0f;
                climaxBar.maxValue = Mathf.Max(1, Definition.climaxLimit);
                climaxBar.value = CurrentClimax;
            }
            if (climaxValueText != null)
                climaxValueText.text = $"{CurrentClimax} / {Mathf.Max(1, Definition.climaxLimit)}";
            if (battleStatusText != null)
                battleStatusText.text = IsSaint ? "聖人" : RestOwnerTurns > 0 ? "休息" : "可行動";
            if (staminaText != null) staminaText.text = RemainingStamina.ToString();
        }

        private static void SetSignedValue(TMP_Text[] labels, int index, int value, bool visible)
        {
            if (labels == null || index < 0 || index >= labels.Length || labels[index] == null) return;
            labels[index].gameObject.SetActive(visible);
            if (visible) labels[index].text = value.ToString("+#;-#;0");
        }
    }
}
