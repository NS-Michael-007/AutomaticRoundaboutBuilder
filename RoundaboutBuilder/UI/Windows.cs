using ColossalFramework;
using ColossalFramework.UI;
using RoundaboutBuilder.Tools;
using System.Reflection.Emit;
using UnityEngine;

/* Version RELEASE 1.4.0+ */

namespace RoundaboutBuilder.UI
{
    public abstract class AbstractPanel : UIPanel
    {
        public const int RADIUS_MAX_VAL = 2000;
        public const int RADIUS_MAX_VAL_UNLIMITED = 20000;

        public abstract bool ShowDropDown { get; }
        public abstract bool ShowFollowTerrain { get; }
        public abstract bool ShowTmpeSetup { get; }
        public abstract bool ShowBackButton { get; }
        public abstract bool IsSpecialWindow { get; }

        public AbstractPanel()
        {
            isVisible = false;
            width = UIWindow.PanelWidth;
            height = 218;
            clipChildren = true;
        }
    }

    public class RoundAboutPanel : AbstractPanel
    {
        public override bool ShowDropDown => true;
        public override bool ShowFollowTerrain => true;
        public override bool ShowTmpeSetup => true;
        public override bool ShowBackButton => false;
        public override bool IsSpecialWindow => false;

        public NumericTextField RadiusField { get; private set; }
        public UILabel label { get; private set; }

        public override void Start()
        {
            float cumulativeHeight = 0;
            UILabel labelRadius = AddUIComponent<UILabel>();
            labelRadius.textScale = 0.9f;
            labelRadius.text = Loc.L("RAB_LBL_RADIUS", "Radius:");
            labelRadius.relativePosition = new Vector2(8, cumulativeHeight);
            labelRadius.tooltip = Loc.L("RAB_TIP_PLUS_MINUS", "Press +/- to adjust");
            labelRadius.SendToBack();

            RadiusField = AddUIComponent<NumericTextField>();
            RadiusField.relativePosition = new Vector2(width - RadiusField.width - 8, cumulativeHeight);
            RadiusField.tooltip = Loc.L("RAB_TIP_PLUS_MINUS", "Press +/- to adjust");
            RadiusField.MaxVal = RoundAboutBuilder.UnlimitedRadius.value ? RADIUS_MAX_VAL_UNLIMITED : RADIUS_MAX_VAL;
            cumulativeHeight += RadiusField.height + 8;

            UIButton button = UIUtil.CreateButton(this);
            button.text = Loc.L("RAB_BTN_FREE_CURSOR", "Free Cursor Mode...");
            button.tooltip = Loc.L("RAB_TIP_FREE_CURSOR", "Create roundabouts anywhere (Warning! Roads won't be removed or connected)");
            button.relativePosition = new Vector2(8, cumulativeHeight);
            button.width = width - 16;
            button.eventClick += (c, p) =>
            {
                UIWindow.instance.SwitchTool(FreeCursorTool.Instance);
            };
            cumulativeHeight += button.height + 8;

            if (RoundAboutBuilder.EnableSpiralRamp.value)
            {
                button = UIUtil.CreateButton(this);
                button.text = Loc.L("RAB_BTN_SPIRAL_RAMP", "Spiral ramp...");
                button.tooltip = Loc.L("RAB_TIP_SPIRAL_RAMP", "Build a ramp that circles while it rises (you connect both ends yourself)");
                button.relativePosition = new Vector2(8, cumulativeHeight);
                button.width = width - 16;
                button.eventClick += (c, p) =>
                {
                    UIWindow.instance.SwitchTool(HelixTool.Instance);
                };
                cumulativeHeight += button.height + 8;
            }

            if (RoundAboutBuilder.LegacyEllipticRoundabouts.value)
            {
                button = UIUtil.CreateButton(this);
                button.text = Loc.L("RAB_BTN_ELLIPSE", "Elliptic Roundabout...");
                button.relativePosition = new Vector2(8, cumulativeHeight);
                button.width = width - 16;
                button.eventClick += (c, p) =>
                {
                    UIWindow.instance.SwitchTool(EllipseTool.Instance);
                };
                cumulativeHeight += button.height + 8;
            }

            label = AddUIComponent<UILabel>();
            /* autoSize must be switched off BEFORE wordWrap: UILabel defaults to autoSize = true and the
             * wordWrap setter calls Invalidate(), which re-measures against (size - padding) and writes the
             * smaller size back -> set_size -> OnSizeChanged -> Invalidate -> stack overflow. */
            label.autoSize = false;
            label.width = width - 16;
            label.height = 48;
            label.wordWrap = true;
            label.textScale = 0.9f;
            label.text = Loc.L("RAB_TIP_NETWORK_ANARCHY", "Tip: Use Network Anarchy for elevated roads");
            label.relativePosition = new Vector2(8, cumulativeHeight);
            label.SendToBack();
            cumulativeHeight += label.height;

            height = cumulativeHeight;

            UIWindow.instance.SwitchWindow(this);
        }
    }

    public class FreeToolPanel : AbstractPanel
    {
        public override bool ShowDropDown => true;
        public override bool ShowFollowTerrain => true;
        public override bool ShowTmpeSetup => false;
        public override bool ShowBackButton => true;
        public override bool IsSpecialWindow => false;

        public NumericTextField RadiusField { get; private set; }
        public NumericTextField ElevationField { get; private set; }
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

        private int previousElevation_;

        public override void Start()
        {
            float cumulativeHeight = 0;
            UILabel labelRadius = AddUIComponent<UILabel>();
            labelRadius.textScale = 0.9f;
            labelRadius.text = Loc.L("RAB_LBL_RADIUS", "Radius:");
            labelRadius.relativePosition = new Vector2(8, cumulativeHeight);
            labelRadius.tooltip = Loc.L("RAB_TIP_PLUS_MINUS", "Press +/- to adjust");
            labelRadius.SendToBack();

            RadiusField = AddUIComponent<NumericTextField>();
            RadiusField.relativePosition = new Vector2(width - RadiusField.width - 8, cumulativeHeight);
            RadiusField.tooltip = Loc.L("RAB_TIP_PLUS_MINUS", "Press +/- to adjust");
            RadiusField.MaxVal = RoundAboutBuilder.UnlimitedRadius.value ? RADIUS_MAX_VAL_UNLIMITED : RADIUS_MAX_VAL;
            cumulativeHeight += RadiusField.height + 8;

            UILabel labelElevation = AddUIComponent<UILabel>();
            labelElevation.textScale = 0.9f;
            labelElevation.text = Loc.L("RAB_LBL_ELEVATION", "Elevation:");
            labelElevation.relativePosition = new Vector2(8, cumulativeHeight);
            labelElevation.tooltip = Loc.L("RAB_TIP_ELEVATION", "Press PgUp/PgDn to adjust");
            labelElevation.SendToBack();

            ElevationField = AddUIComponent<NumericTextField>();
            ElevationField.relativePosition = new Vector2(width - ElevationField.width - 8, cumulativeHeight);
            ElevationField.tooltip = Loc.L("RAB_TIP_ELEVATION_FIELD", "Press PgUp/PgDn/Home to adjust - set elevation in Road Tool");
            ElevationField.MinVal = -500;
            ElevationField.MaxVal = 1000;
            ElevationField.Increment = NetUtil.DEFAULT_ELEVATTION_STEP;
            ElevationField.DefaultVal = 0;
            ElevationField.text = "0";
            ElevationField.eventVisibilityChanged += (c, state) =>
            {
                if (state)
                {
                    RefreshElevation();
                }
            };

            cumulativeHeight += ElevationField.height + 8;

            UILabel labelStructure = AddUIComponent<UILabel>();
            labelStructure.textScale = 0.9f;
            labelStructure.text = Loc.L("RAB_LBL_ELLIPSE_STRUCT", "Structure:");
            labelStructure.relativePosition = new Vector2(8, cumulativeHeight);
            labelStructure.tooltip = Loc.L("RAB_TIP_ELLIPSE_STRUCT",
                "Ground, elevated or bridge. Elevated and bridge use the Network Anarchy variant of the selected road");
            labelStructure.SendToBack();

            StructureDropDown = AddUIComponent<UISpiralStructureDropDown>();
            StructureDropDown.relativePosition = new Vector2(width - StructureDropDown.width - 8, cumulativeHeight);
            StructureDropDown.tooltip = labelStructure.tooltip;
            StructureDropDown.items = new string[]
            {
                Loc.L("RAB_STRUCT_GROUND", "Ground"),
                Loc.L("RAB_STRUCT_ELEVATED", "Elevated"),
                Loc.L("RAB_STRUCT_BRIDGE", "Bridge")
            };
            StructureDropDown.selectedIndex = 0;
            cumulativeHeight += StructureDropDown.height + 8;

            UILabel label = AddUIComponent<UILabel>();
            /* autoSize off first - see the note in RoundAboutPanel. */
            label.autoSize = false;
            label.width = width - 16;
            label.height = 96;
            label.wordWrap = true;
            label.textScale = 0.9f;
            label.text = Loc.L("RAB_WARN_FREE_TOOL", "Roads won't be removed or connected\nUse Network Anarchy for elevated roads");
            label.relativePosition = new Vector2(8, cumulativeHeight);
            label.SendToBack();
            cumulativeHeight += label.height;

            var absoluteElevation = UIUtil.CreateCheckBox(this);
            absoluteElevation.name = "RAB_absoluteElevation";
            absoluteElevation.label.text = Loc.L("RAB_CHK_ABSOLUTE_ELEVATION", "Absolute elevation");
            absoluteElevation.tooltip = Loc.L("RAB_TIP_ABSOLUTE_ELEVATION", "Elevation will be measured from zero level instead of terrain level");
            absoluteElevation.relativePosition = new Vector3(8, cumulativeHeight);
            absoluteElevation.isChecked = false;
            absoluteElevation.eventCheckChanged += (c, state) =>
            {
                if (state)
                {
                    previousElevation_ = ElevationField.Value ?? 0;
                    var center = new Vector3(Screen.width / 2, Screen.height / 2, 0);
                    center = Camera.main.ScreenToWorldPoint(center);
                    ElevationField.Value = previousElevation_ + (int)NetUtil.TerrainHeight(center);
                }
                else
                {
                    ElevationField.Value = previousElevation_;
                }
                FreeCursorTool.Instance.AbsoluteElevation = state;
            };
            cumulativeHeight += absoluteElevation.height + 8;

            height = cumulativeHeight;
        }

        public void RefreshElevation()
        {
            int elevation = NetUtil.GetElevation();
            if (FreeCursorTool.Instance.AbsoluteElevation)
            {
                int terrainY = (int)FreeCursorTool.Instance.HoverPosition.y;
                UIWindow.instance.P_FreeToolPanel.ElevationField.Value = terrainY + elevation;
            }
            else
            {
                UIWindow.instance.P_FreeToolPanel.ElevationField.Value = elevation;
            }
        }
    }

    public class EllipsePanel_1 : AbstractPanel
    {
        public override bool ShowDropDown => true;
        public override bool ShowFollowTerrain => false;
        public override bool ShowTmpeSetup => false;
        public override bool ShowBackButton => true;
        public override bool IsSpecialWindow => false;

        public override void Start()
        {
            UILabel label = AddUIComponent<UILabel>();
            /* autoSize off first - see the note in RoundAboutPanel. */
            label.autoSize = false;
            label.width = width - 16;
            label.height = 130;
            label.wordWrap = true;
            label.textScale = 0.9f;
            label.text = Loc.L("RAB_PANEL_STEP1", "Step 1/3:\nSelect center of the elliptic roundabout (Nothing will be built yet)");
            label.relativePosition = new Vector2(8, 0);
            label.SendToBack();
            height = 150;
        }
    }

    public class EllipsePanel_2 : AbstractPanel
    {
        public override bool ShowDropDown => true;
        public override bool ShowFollowTerrain => false;
        public override bool ShowTmpeSetup => false;
        public override bool ShowBackButton => true;
        public override bool IsSpecialWindow => false;

        public override void Start()
        {
            UILabel label = AddUIComponent<UILabel>();
            /* autoSize off first - see the note in RoundAboutPanel. */
            label.autoSize = false;
            label.width = width - 16;
            label.height = 130;
            label.wordWrap = true;
            label.textScale = 0.9f;
            label.text = Loc.L("RAB_PANEL_STEP2", "Step 2/3:\nSelect any intersection on the main axis of the roundabout (Nothing will be built yet)");
            label.relativePosition = new Vector2(8, 0);
            label.SendToBack();
            height = 150;
        }
    }

    public class EllipsePanel_3 : AbstractPanel
    {
        public override bool ShowDropDown => true;
        public override bool ShowFollowTerrain => true;
        public override bool ShowTmpeSetup => true;
        public override bool ShowBackButton => true;
        public override bool IsSpecialWindow => false;

        public static readonly int RADIUS1_DEF = 60;
        public static readonly int RADIUS2_DEF = 40;
        public static readonly int ANGLE_DEF = 0;

        public NumericTextField Radius1tf { get; private set; }
        public NumericTextField Radius2tf { get; private set; }
        public NumericTextField AngleField { get; private set; }
        public UISpiralStructureDropDown StructureDropDown { get; private set; }
        public UICheckBox SnapCenterCheckBox { get; private set; }

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
            float cumulativeHeight = 0;

            UILabel labelRadius = AddUIComponent<UILabel>();
            labelRadius.textScale = 0.9f;
            labelRadius.text = Loc.L("RAB_LBL_MAIN_AXIS", "Main axis:");
            labelRadius.relativePosition = new Vector2(8, cumulativeHeight);
            labelRadius.tooltip = Loc.L("RAB_TIP_SHIFT_PLUS_MINUS", "Press SHIFT +/- to adjust");
            labelRadius.SendToBack();

            Radius1tf = AddUIComponent<NumericTextField>();
            Radius1tf.relativePosition = new Vector2(width - Radius1tf.width - 8, cumulativeHeight);
            Radius1tf.tooltip = Loc.L("RAB_TIP_SHIFT_PLUS_MINUS", "Press SHIFT +/- to adjust");
            Radius1tf.DefaultVal = RADIUS1_DEF;
            Radius1tf.MaxVal = RoundAboutBuilder.UnlimitedRadius.value ? RADIUS_MAX_VAL_UNLIMITED : RADIUS_MAX_VAL;
            Radius1tf.text = RADIUS1_DEF.ToString();
            cumulativeHeight += Radius1tf.height + 8;

            labelRadius = AddUIComponent<UILabel>();
            labelRadius.textScale = 0.9f;
            labelRadius.text = Loc.L("RAB_LBL_MINOR_AXIS", "Minor axis:");
            labelRadius.relativePosition = new Vector2(8, cumulativeHeight);
            labelRadius.tooltip = Loc.L("RAB_TIP_CTRL_PLUS_MINUS", "Press CTRL +/- to adjust");
            labelRadius.SendToBack();

            Radius2tf = AddUIComponent<NumericTextField>();
            Radius2tf.relativePosition = new Vector2(width - Radius1tf.width - 8, cumulativeHeight);
            Radius2tf.tooltip = Loc.L("RAB_TIP_CTRL_PLUS_MINUS", "Press CTRL +/- to adjust");
            Radius2tf.DefaultVal = RADIUS2_DEF;
            Radius2tf.MaxVal = RoundAboutBuilder.UnlimitedRadius.value ? RADIUS_MAX_VAL_UNLIMITED : RADIUS_MAX_VAL;
            Radius2tf.text = RADIUS2_DEF.ToString();
            cumulativeHeight += Radius2tf.height + 8;

            UILabel labelAngle = AddUIComponent<UILabel>();
            labelAngle.textScale = 0.9f;
            labelAngle.text = Loc.L("RAB_LBL_ANGLE", "Angle:");
            labelAngle.relativePosition = new Vector2(8, cumulativeHeight);
            labelAngle.tooltip = Loc.L("RAB_TIP_ALT_PLUS_MINUS", "Press ALT +/- to rotate by 15 degrees");
            labelAngle.SendToBack();

            AngleField = AddUIComponent<NumericTextField>();
            AngleField.relativePosition = new Vector2(width - Radius1tf.width - 8, cumulativeHeight);
            AngleField.tooltip = Loc.L("RAB_TIP_ALT_PLUS_MINUS", "Press ALT +/- to rotate by 15 degrees");
            AngleField.MinVal = 0;
            AngleField.MaxVal = 359;
            AngleField.Increment = 15;
            AngleField.DefaultVal = 0;
            AngleField.text = ANGLE_DEF.ToString();
            cumulativeHeight += AngleField.height + 8;

            UILabel labelStructure = AddUIComponent<UILabel>();
            labelStructure.textScale = 0.9f;
            labelStructure.text = Loc.L("RAB_LBL_ELLIPSE_STRUCT", "Structure:");
            labelStructure.relativePosition = new Vector2(8, cumulativeHeight);
            labelStructure.tooltip = Loc.L("RAB_TIP_ELLIPSE_STRUCT",
                "Ground, elevated or bridge. Elevated and bridge use the Network Anarchy variant of the selected road");
            labelStructure.SendToBack();

            StructureDropDown = AddUIComponent<UISpiralStructureDropDown>();
            StructureDropDown.relativePosition = new Vector2(width - StructureDropDown.width - 8, cumulativeHeight);
            StructureDropDown.tooltip = labelStructure.tooltip;
            StructureDropDown.items = new string[]
            {
                Loc.L("RAB_STRUCT_GROUND", "Ground"),
                Loc.L("RAB_STRUCT_ELEVATED", "Elevated"),
                Loc.L("RAB_STRUCT_BRIDGE", "Bridge")
            };
            StructureDropDown.selectedIndex = 0;
            cumulativeHeight += StructureDropDown.height + 8;

            SnapCenterCheckBox = UIUtil.CreateCheckBox(this);
            SnapCenterCheckBox.name = "RAB_snapCenter";
            SnapCenterCheckBox.label.text = Loc.L("RAB_CHK_SNAP_CENTER", "Snap center to intersection");
            SnapCenterCheckBox.tooltip = Loc.L("RAB_TIP_SNAP_CENTER",
                "Checked: the ellipse replaces an existing intersection. Unchecked: the mouse position is the center of a free-standing ellipse");
            SnapCenterCheckBox.isChecked = true;
            SnapCenterCheckBox.relativePosition = new Vector3(8, cumulativeHeight);
            cumulativeHeight += SnapCenterCheckBox.height + 8;

            var buildButton = UIUtil.CreateButton(this);
            buildButton.text = Loc.L("RAB_BTN_BUILD", "Build");
            buildButton.relativePosition = new Vector2(8, cumulativeHeight);
            buildButton.playAudioEvents = false;
            buildButton.eventClick += (c, p) =>
            {
                EllipseTool.Instance.BuildEllipse();
            };
            cumulativeHeight += buildButton.height + 8;

            var controlVertices = UIUtil.CreateCheckBox(this);
            controlVertices.name = "RAB_controlVertices";
            controlVertices.label.text = Loc.L("RAB_CHK_CONTROL_POINTS", "Insert control points");
            controlVertices.tooltip = Loc.L("RAB_TIP_CONTROL_POINTS", "Control points are inserted on main axes to keep the ellipse in shape. See workshop page");
            controlVertices.isChecked = EllipseTool.Instance.ControlVertices;
            controlVertices.relativePosition = new Vector3(8, cumulativeHeight);
            controlVertices.eventCheckChanged += (c, state) =>
            {
                EllipseTool.Instance.ControlVertices = state;
            };
            cumulativeHeight += controlVertices.height + 8;

            height = cumulativeHeight;
        }
    }

    public class TmpeSetupPanel : AbstractPanel
    {
        public override bool ShowDropDown => false;
        public override bool ShowFollowTerrain => false;
        public override bool ShowTmpeSetup => false;
        public override bool ShowBackButton => true;
        public override bool IsSpecialWindow => true;

        public static readonly SavedBool SavedEnterBlockedYieldingRoad = new SavedBool("tmpeEnterBlockedJunction", RoundAboutBuilder.settingsFileName, true, true);
        public static readonly SavedBool SavedEnterBlockedMainRoad = new SavedBool("tmpeEnterBlockedJunctionMainRoad", RoundAboutBuilder.settingsFileName, true, true);
        public static readonly SavedBool SavedNoParking = new SavedBool("tmpeNoParking", RoundAboutBuilder.settingsFileName, true, true);
        public static readonly SavedBool SavedPrioritySigns = new SavedBool("tmpePrioritySigns", RoundAboutBuilder.settingsFileName, false, true);
        public static readonly SavedBool SavedNoCrossings = new SavedBool("tmpeNoCrossings", RoundAboutBuilder.settingsFileName, false, true);
        public static readonly SavedBool SavedAllowLaneChanging = new SavedBool("tmpeLaneChanging", RoundAboutBuilder.settingsFileName, false, true);

        public override void Start()
        {
            float cumulativeHeight = 8;

            UILabel label = AddUIComponent<UILabel>();
            label.textScale = 0.9f;
            label.width = width;
            label.text = Loc.L("RAB_LBL_TMPE_SETTINGS", "TMPE settings");
            label.relativePosition = new Vector2(8, cumulativeHeight);
            label.SendToBack();
            cumulativeHeight += label.height + 8;

            var checkBox = UIUtil.CreateCheckBox(this);
            checkBox.name = "RAB_enterBlockedJunctionMainRoad";
            checkBox.label.text = Loc.L("RAB_TMPE_ENTER_MAIN", "Enter junct. main r.");
            checkBox.tooltip = Loc.L("RAB_TIP_TMPE_ENTER_MAIN", "Allow vehicles on the roundabout to enter blocked junctions (TMPE policy)");
            checkBox.isChecked = SavedEnterBlockedMainRoad;
            checkBox.relativePosition = new Vector3(8, cumulativeHeight);
            checkBox.eventCheckChanged += (c, state) =>
            {
                SavedEnterBlockedMainRoad.value = state;
            };
            cumulativeHeight += checkBox.height + 8;

            checkBox = UIUtil.CreateCheckBox(this);
            checkBox.name = "RAB_enterBlockedJunctionYieldingRoad";
            checkBox.label.text = Loc.L("RAB_TMPE_ENTER_YIELD", "Enter junct. yield. r.");
            checkBox.tooltip = Loc.L("RAB_TIP_TMPE_ENTER_YIELD", "Allow vehicles entering the roundabout to enter blocked junction (TMPE policy)");
            checkBox.isChecked = SavedEnterBlockedYieldingRoad;
            checkBox.relativePosition = new Vector3(8, cumulativeHeight);
            checkBox.eventCheckChanged += (c, state) =>
            {
                SavedEnterBlockedYieldingRoad.value = state;
            };
            cumulativeHeight += checkBox.height + 8;

            checkBox = UIUtil.CreateCheckBox(this);
            checkBox.name = "RAB_noParking";
            checkBox.label.text = Loc.L("RAB_TMPE_NO_PARKING", "No parking");
            checkBox.tooltip = Loc.L("RAB_TIP_TMPE_NO_PARKING", "Restrict parking on the roundabout (TMPE policy)");
            checkBox.isChecked = SavedNoParking;
            checkBox.relativePosition = new Vector3(8, cumulativeHeight);
            checkBox.eventCheckChanged += (c, state) =>
            {
                SavedNoParking.value = state;
            };
            cumulativeHeight += checkBox.height + 8;

            checkBox = UIUtil.CreateCheckBox(this);
            checkBox.name = "RAB_prioritySigns";
            checkBox.label.text = Loc.L("RAB_TMPE_PRIORITY", "Priority signs");
            checkBox.tooltip = Loc.L("RAB_TIP_TMPE_PRIORITY", "Vehicles on the roundabout will have right-of-way");
            checkBox.isChecked = SavedPrioritySigns;
            checkBox.relativePosition = new Vector3(8, cumulativeHeight);
            checkBox.eventCheckChanged += (c, state) =>
            {
                SavedPrioritySigns.value = state;
            };
            cumulativeHeight += checkBox.height + 8;

            checkBox = UIUtil.CreateCheckBox(this);
            checkBox.name = "RAB_noCrossings";
            checkBox.label.text = Loc.L("RAB_TMPE_NO_CROSSINGS", "Disable crosswalks");
            checkBox.tooltip = Loc.L("RAB_TIP_TMPE_NO_CROSSINGS", "Disallow pedestrians to cross to inner ring of the roundabout (Does not change visual appearance)");
            checkBox.isChecked = SavedNoCrossings;
            checkBox.relativePosition = new Vector3(8, cumulativeHeight);
            checkBox.eventCheckChanged += (c, state) =>
            {
                SavedNoCrossings.value = state;
            };
            cumulativeHeight += checkBox.height + 8;

            checkBox = UIUtil.CreateCheckBox(this);
            checkBox.name = "RAB_laneChanging";
            checkBox.label.text = Loc.L("RAB_TMPE_LANE_CHANGING", "Allow lane changing");
            checkBox.tooltip = Loc.L("RAB_TIP_TMPE_LANE_CHANGING", "Allow vehicles to change lanes at the junction (both on the main and entering roads)");
            checkBox.isChecked = SavedAllowLaneChanging;
            checkBox.relativePosition = new Vector3(8, cumulativeHeight);
            checkBox.eventCheckChanged += (c, state) =>
            {
                SavedAllowLaneChanging.value = state;
            };
            cumulativeHeight += checkBox.height + 8;

            /*label = AddUIComponent<UILabel>();
            label.text = "Would you like to see more TMPE features (eg. automatic lane connector)? Please visit the workshop page and let me know your usual " +
                "roundabout TMPE setup. Please, be as concrete as possible. ( You can include screenshots ;) )\n- Your Strad. PS: I don't promise anything :D";
            label.wordWrap = true;
            label.textScale = 0.65f;
            label.autoSize = false;
            label.width = width - 16;
            label.relativePosition = new Vector2(8, cumulativeHeight);
            label.SendToBack();
            cumulativeHeight += label.height + 8;*/
        }
    }
}
