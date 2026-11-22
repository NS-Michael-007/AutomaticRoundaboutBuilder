using ColossalFramework.UI;
using RoundaboutBuilder.Tools;
using UnityEngine;

/* Panel of the spiral ramp tool: radius, total height, turns, direction, structure. */
namespace RoundaboutBuilder.UI
{
    public class HelixPanel : AbstractPanel
    {
        public override bool ShowDropDown => true;
        public override bool ShowFollowTerrain => false;
        public override bool ShowTmpeSetup => false;
        public override bool ShowBackButton => true;
        public override bool IsSpecialWindow => false;

        public static readonly int RADIUS_DEF = 40;
        public static readonly int HEIGHT_DEF = 12;
        public static readonly int TURNS_DEF = 1;
        public static readonly int TAIL_DEF = 0;

        public NumericTextField RadiusField { get; private set; }
        public NumericTextField HeightField { get; private set; }
        public NumericTextField TurnsField { get; private set; }
        public NumericTextField TailField { get; private set; }
        public UICheckBox ReverseCheckBox { get; private set; }
        public UISpiralStructureDropDown StructureDropDown { get; private set; }

        /* Ground / elevated / bridge, resolved later by StructureVariants. */
        public RampStructure Structure
        {
            get
            {
                int index = StructureDropDown != null ? StructureDropDown.selectedIndex : 0;
                if (index < 0 || index > 2) index = 0;
                return (RampStructure)index;
            }
        }

        public override void Start()
        {
            Debug.Log("[RoundaboutBuilder] HelixPanel.Start begin, width=" + width);
            float cumulativeHeight = 0;

            UILabel labelRadius = AddUIComponent<UILabel>();
            labelRadius.textScale = 0.9f;
            labelRadius.text = Loc.L("RAB_LBL_SPIRAL_RADIUS", "Radius:");
            labelRadius.relativePosition = new Vector2(8, cumulativeHeight);
            labelRadius.tooltip = Loc.L("RAB_TIP_SPIRAL_RADIUS", "Radius of the ramp (centre line)");
            labelRadius.SendToBack();

            RadiusField = AddUIComponent<NumericTextField>();
            RadiusField.relativePosition = new Vector2(width - RadiusField.width - 8, cumulativeHeight);
            RadiusField.tooltip = labelRadius.tooltip;
            RadiusField.MinVal = 8;
            RadiusField.MaxVal = RoundAboutBuilder.UnlimitedRadius.value ? RADIUS_MAX_VAL_UNLIMITED : RADIUS_MAX_VAL;
            RadiusField.Increment = 8;
            RadiusField.DefaultVal = RADIUS_DEF;
            RadiusField.text = RADIUS_DEF.ToString();
            cumulativeHeight += RadiusField.height + 8;

            UILabel labelHeight = AddUIComponent<UILabel>();
            labelHeight.textScale = 0.9f;
            labelHeight.text = Loc.L("RAB_LBL_SPIRAL_HEIGHT", "Height:");
            labelHeight.relativePosition = new Vector2(8, cumulativeHeight);
            labelHeight.tooltip = Loc.L("RAB_TIP_SPIRAL_HEIGHT", "Total height gained over the whole ramp");
            labelHeight.SendToBack();

            HeightField = AddUIComponent<NumericTextField>();
            HeightField.relativePosition = new Vector2(width - HeightField.width - 8, cumulativeHeight);
            HeightField.tooltip = labelHeight.tooltip;
            HeightField.MinVal = 1;
            HeightField.MaxVal = 300;
            HeightField.Increment = 3; /* matches NetUtil.DEFAULT_ELEVATTION_STEP */
            HeightField.DefaultVal = HEIGHT_DEF;
            HeightField.text = HEIGHT_DEF.ToString();
            cumulativeHeight += HeightField.height + 8;

            UILabel labelTurns = AddUIComponent<UILabel>();
            labelTurns.textScale = 0.9f;
            labelTurns.text = Loc.L("RAB_LBL_SPIRAL_TURNS", "Turns:");
            labelTurns.relativePosition = new Vector2(8, cumulativeHeight);
            labelTurns.tooltip = Loc.L("RAB_TIP_SPIRAL_TURNS", "How many full turns the ramp makes");
            labelTurns.SendToBack();

            TurnsField = AddUIComponent<NumericTextField>();
            TurnsField.relativePosition = new Vector2(width - TurnsField.width - 8, cumulativeHeight);
            TurnsField.tooltip = labelTurns.tooltip;
            TurnsField.MinVal = 1;
            TurnsField.MaxVal = 6;
            TurnsField.Increment = 1;
            TurnsField.DefaultVal = TURNS_DEF;
            TurnsField.text = TURNS_DEF.ToString();
            cumulativeHeight += TurnsField.height + 8;

            UILabel labelTail = AddUIComponent<UILabel>();
            labelTail.textScale = 0.9f;
            labelTail.text = Loc.L("RAB_LBL_SPIRAL_TAIL", "End offset:");
            labelTail.relativePosition = new Vector2(8, cumulativeHeight);
            labelTail.tooltip = Loc.L("RAB_TIP_SPIRAL_TAIL",
                "Extra degrees past the last full turn. 180 puts the end point on the opposite side of the start");
            labelTail.SendToBack();

            TailField = AddUIComponent<NumericTextField>();
            TailField.relativePosition = new Vector2(width - TailField.width - 8, cumulativeHeight);
            TailField.tooltip = labelTail.tooltip;
            TailField.MinVal = 0;
            TailField.MaxVal = 345;
            TailField.Increment = 45;
            TailField.DefaultVal = TAIL_DEF;
            TailField.text = TAIL_DEF.ToString();
            cumulativeHeight += TailField.height + 8;

            UILabel labelStructure = AddUIComponent<UILabel>();
            labelStructure.textScale = 0.9f;
            labelStructure.text = Loc.L("RAB_LBL_SPIRAL_STRUCT", "Structure:");
            labelStructure.relativePosition = new Vector2(8, cumulativeHeight);
            labelStructure.tooltip = Loc.L("RAB_TIP_SPIRAL_STRUCT",
                "Ground, elevated or bridge. Auto follows the mode selected in Network Anarchy");
            labelStructure.SendToBack();

            StructureDropDown = AddUIComponent<UISpiralStructureDropDown>();
            StructureDropDown.relativePosition = new Vector2(width - StructureDropDown.width - 8, cumulativeHeight);
            StructureDropDown.tooltip = labelStructure.tooltip;
            StructureDropDown.items = new string[]
            {
                Loc.L("RAB_STRUCT_AUTO", "Auto"),
                Loc.L("RAB_STRUCT_ELEVATED", "Elevated"),
                Loc.L("RAB_STRUCT_BRIDGE", "Bridge")
            };
            StructureDropDown.selectedIndex = 0;
            cumulativeHeight += StructureDropDown.height + 8;

            UIButton buildButton = UIUtil.CreateButton(this);
            buildButton.text = Loc.L("RAB_BTN_BUILD", "Build");
            buildButton.width = width - 16;
            buildButton.relativePosition = new Vector2(8, cumulativeHeight);
            buildButton.playAudioEvents = false;
            buildButton.eventClick += (c, p) =>
            {
                HelixTool.Instance.BuildRamp();
            };
            cumulativeHeight += buildButton.height + 8;

            ReverseCheckBox = UIUtil.CreateCheckBox(this);
            ReverseCheckBox.name = "RAB_spiralReverse";
            ReverseCheckBox.label.text = Loc.L("RAB_CHK_SPIRAL_REVERSE", "Reverse direction");
            ReverseCheckBox.tooltip = Loc.L("RAB_TIP_SPIRAL_REVERSE", "Build the ramp the other way around");
            ReverseCheckBox.isChecked = false;
            ReverseCheckBox.relativePosition = new Vector3(8, cumulativeHeight);
            cumulativeHeight += ReverseCheckBox.height + 8;

            UILabel warning = AddUIComponent<UILabel>();
            /* autoSize off first: the wordWrap setter calls Invalidate(), which re-measures against
             * (size - padding) and writes the smaller size back -> set_size -> OnSizeChanged -> Invalidate
             * -> stack overflow. */
            warning.autoSize = false;
            warning.width = width - 16;
            warning.height = 32;
            warning.wordWrap = true;
            warning.textScale = 0.9f;
            warning.text = Loc.L("RAB_SPIRAL_WARN", "Both ends stay open - connect them to your roads");
            warning.relativePosition = new Vector2(8, cumulativeHeight);
            warning.SendToBack();
            cumulativeHeight += warning.height;

            height = cumulativeHeight;
            Debug.Log("[RoundaboutBuilder] HelixPanel.Start done, height=" + height);
        }
    }

    /* Plain three item dropdown, skinned like UINetInfoDropDown so it looks native. */
    public class UISpiralStructureDropDown : UIDropDown
    {
        public UISpiralStructureDropDown()
        {
            atlas = ResourceLoader.GetAtlas("Ingame");
            size = new Vector2(110f, 30f);
            listBackground = "GenericPanelLight";
            itemHeight = 25;
            itemHover = "ListItemHover";
            itemHighlight = "ListItemHighlight";
            normalBgSprite = "ButtonMenu";
            disabledBgSprite = "ButtonMenuDisabled";
            hoveredBgSprite = "ButtonMenuHovered";
            focusedBgSprite = "ButtonMenu";
            listWidth = 140;
            listHeight = 200;
            listPosition = UIDropDown.PopupListPosition.Below;
            clampListToScreen = true;
            builtinKeyNavigation = true;
            foregroundSpriteMode = UIForegroundSpriteMode.Stretch;
            popupColor = new Color32(45, 52, 61, 255);
            popupTextColor = new Color32(170, 170, 170, 255);
            zOrder = 1;
            textScale = 0.8f;
            verticalAlignment = UIVerticalAlignment.Middle;
            horizontalAlignment = UIHorizontalAlignment.Left;
            textFieldPadding = new RectOffset(8, 0, 8, 0);
            itemPadding = new RectOffset(14, 0, 8, 0);

            UIButton button = AddUIComponent<UIButton>();
            triggerButton = button;
            button.atlas = ResourceLoader.GetAtlas("Ingame");
            button.text = "";
            button.size = size;
            button.relativePosition = new Vector3(0f, 0f);
            button.textVerticalAlignment = UIVerticalAlignment.Middle;
            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
            button.normalFgSprite = "IconDownArrow";
            button.hoveredFgSprite = "IconDownArrowHovered";
            button.pressedFgSprite = "IconDownArrowPressed";
            button.focusedFgSprite = "IconDownArrowFocused";
            button.disabledFgSprite = "IconDownArrowDisabled";
            button.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
            button.horizontalAlignment = UIHorizontalAlignment.Right;
            button.verticalAlignment = UIVerticalAlignment.Middle;
            button.zOrder = 0;
            button.textScale = 0.8f;
        }
    }
}
