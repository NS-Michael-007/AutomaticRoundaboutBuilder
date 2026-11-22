using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using RoundaboutBuilder.Tools;
using RoundaboutBuilder.UI;
using SharedEnvironment;
using System;
using UnityEngine;

/* Standalone helical (spiral) ramp.
 *
 * It circles while it rises and both ends are left open - the player connects
 * them to the road network. Nothing else in the mod is touched: this tool is
 * independent of RoundaboutTool / EllipseTool / FreeCursorTool.
 */
namespace RoundaboutBuilder
{
    class HelixTool : ToolBaseExtended
    {
        public static HelixTool Instance;

        /* Chord between two nodes. 30 m means six segments per turn at radius 30
         * (2 * PI * 30 / 6 = 31.4), and the count then follows the radius by plain
         * division: 20 -> 4, 30 -> 6, 40 -> 8, 60 -> 12 segments per turn. */
        private const float TARGET_CHORD = 30f;

        /* Conservative limits. They are pre-checks only - they stop us from
         * building half a ramp and failing in the middle. Raise them here if the
         * game turns out to accept steeper / shorter geometry. */
        private const float MIN_CHORD = 6f;
        private const float MAX_GRADE = 0.15f;

        private static HelixPanel Panel
        {
            get { return UIWindow.instance != null ? UIWindow.instance.P_HelixPanel : null; }
        }

        private static bool ReadPanel(HelixPanel panel, out float radius, out float height, out float turns, out float tail, out bool reverse)
        {
            radius = height = turns = tail = 0f;
            reverse = false;

            if (panel == null) return false;

            int? radiusQ = panel.RadiusField.Value;
            int? heightQ = panel.HeightField.Value;
            int? turnsQ = panel.TurnsField.Value;
            if (radiusQ == null || heightQ == null || turnsQ == null) return false;

            int? tailQ = panel.TailField != null ? panel.TailField.Value : null;

            radius = (float)radiusQ;
            height = (float)heightQ;
            turns = (float)turnsQ;
            tail = tailQ != null ? (float)tailQ : 0f;
            reverse = panel.ReverseCheckBox != null && panel.ReverseCheckBox.isChecked;
            return true;
        }

        /* Start direction in degrees. Right click turns the whole ramp by 15 degrees. */
        private float m_startAngleDeg;

        private float StartAngleRadians
        {
            get { return m_startAngleDeg * (float)Math.PI / 180f; }
        }

        protected override void OnRightClick()
        {
            base.OnRightClick();
            m_startAngleDeg = (m_startAngleDeg + 15f) % 360f;
            Debug.Log("[RoundaboutBuilder] Spiral start direction: " + m_startAngleDeg + " degrees");
        }

        /* Turns passed to HelixPath: whole turns plus the extra end offset. */
        private static float PathTurns(float turns, float tail)
        {
            return turns + tail / 360f;
        }

        public void BuildRamp()
        {
            HelixPanel panel = Panel;
            if (panel == null) return;

            if (!ReadPanel(panel, out float radius, out float height, out float turns, out float tail, out bool reverse))
            {
                UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_SPIRAL_FIELDS", "Fill in the radius, height and turns fields"));
                return;
            }

            HelixPath path = new HelixPath(HoverPosition, radius, height, PathTurns(turns, tail), reverse, StartAngleRadians);
            int segments = HelixPath.SegmentCount(path.Radius, path.TotalAngle, TARGET_CHORD);
            float chord = HelixPath.ChordLength(path.Radius, path.TotalAngle / segments);

            if (chord < MIN_CHORD)
            {
                UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_SPIRAL_TOO_SMALL",
                    "Radius is too small for this segment length (chord {0}). Increase the radius", (int)chord));
                return;
            }

            if (path.Grade > MAX_GRADE)
            {
                UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_SPIRAL_TOO_STEEP",
                    "Grade {0}% is too steep. Raise the radius, lower the height or add a turn", (int)(path.Grade * 100f)));
                return;
            }

            NetInfo groundInfo = UIWindow.instance.dropDown.Value;
            if (groundInfo == null)
            {
                UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_SPIRAL_NO_NET", "No road selected"));
                return;
            }

            /* Which prefab this ramp is made of: ground, elevated or bridge. */
            string missing;
            NetInfo netInfo = StructureVariants.Resolve(groundInfo, panel.Structure, out missing);
            if (netInfo == null)
            {
                UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_SPIRAL_NO_VARIANT",
                    "{0} is not available for this road. Pick another structure or another road", missing));
                return;
            }

            /* Safety net: a prefab whose AI reports no elevation room would make
             * NetUtil.GetElevation return 0 and the whole ramp would drop to the terrain. */
            if (height > 0f && netInfo != groundInfo && !StructureVariants.CanBeRaised(netInfo))
            {
                UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_SPIRAL_NO_VARIANT",
                    "{0} is not available for this road. Pick another structure or another road",
                    panel.Structure.ToString()));
                return;
            }

            Debug.Log("[RoundaboutBuilder] Spiral structure=" + panel.Structure + ", prefab=" + netInfo.name
                + ", ground prefab=" + groundInfo.name);

            bool leftHandTraffic = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;

            ActionGroup roads = new ActionGroup(Loc.L("RAB_SPIRAL_ACTION_NAME", "Build spiral ramp"));
            roads.ItemClass = netInfo.m_class;

            /* Collect the segments so Network Skins can be applied once they exist. */
            System.Collections.Generic.List<WrappedSegment> builtSegments = new System.Collections.Generic.List<WrappedSegment>();

            WrappedNode previous = null;
            for (int i = 0; i <= segments; i++)
            {
                float angle = path.TotalAngle * i / segments;

                WrappedNode node = new WrappedNode();
                node.Position = path.PositionAt(angle);
                node.NetInfo = netInfo;
                roads.Actions.Add(node);

                if (previous != null)
                {
                    float previousAngle = path.TotalAngle * (i - 1) / segments;

                    WrappedSegment segment = new WrappedSegment();
                    segment.StartNode = previous;
                    segment.EndNode = node;
                    segment.StartDirection = path.TangentAt(previousAngle);
                    /* The end direction points from the end node into the segment,
                     * hence the minus sign (same convention as FinalConnector). */
                    segment.EndDirection = -path.TangentAt(angle);
                    segment.NetInfo = netInfo;
                    segment.Invert = leftHandTraffic;
                    segment.SwitchStartAndEnd = leftHandTraffic;
                    segment.DeployPlacementEffects = true;
                    roads.Actions.Add(segment);
                    builtSegments.Add(segment);
                }

                previous = node;
            }

            /* Optional Network Skins pass: no pillars, no street lights. Skipped
             * silently when Network Skins is missing or the option is off. */
            if (RoundAboutBuilder.HidePillarsAndLights.value && NetworkSkinsHook.IsAvailable())
            {
                NetworkSkinsHook.ReportSupport(netInfo);
                roads.Actions.Add(new NetworkSkinsAction(builtSegments, netInfo));
            }

            UIWindow.instance.LostFocus();
            ModThreading.PushAction(roads, new ActionGroup("Set up TMPE"));
        }

        protected override void OnClick()
        {
            base.OnClick();
            BuildRamp();
        }

        protected override void RenderOverlayExtended(RenderManager.CameraInfo cameraInfo)
        {
            if (insideUI || !Cursor.visible) return;

            try
            {
                HelixPanel panel = Panel;
                if (panel == null) return;

                if (!ReadPanel(panel, out float radius, out float height, out float turns, out float tail, out bool reverse))
                {
                    RenderHoveringLabel(Loc.L("RAB_ERR_SPIRAL_FIELDS", "Fill in the radius, height and turns fields"));
                    return;
                }

                HelixPath path = new HelixPath(HoverPosition, radius, height, PathTurns(turns, tail), reverse, StartAngleRadians);
                int segments = HelixPath.SegmentCount(path.Radius, path.TotalAngle, TARGET_CHORD);
                float chord = HelixPath.ChordLength(path.Radius, path.TotalAngle / segments);

                /* Base marker */
                RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, Color.black, HoverPosition, 15f,
                    HoverPosition.y - 1f, HoverPosition.y + 1f, true, true);

                float minY = HoverPosition.y - 2f;
                float maxY = HoverPosition.y + height + 2f;

                for (int i = 0; i < segments; i++)
                {
                    float angle0 = path.TotalAngle * i / segments;
                    float angle1 = path.TotalAngle * (i + 1) / segments;

                    Vector3 start = path.PositionAt(angle0);
                    Vector3 end = path.PositionAt(angle1);

                    Bezier3 bezier = new Bezier3();
                    bezier.a = start;
                    bezier.d = end;
                    NetSegment.CalculateMiddlePoints(start, path.TangentAt(angle0), end, -path.TangentAt(angle1),
                        false, false, out bezier.b, out bezier.c);

                    RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, Color.red, bezier, 1f, 0f, 0f,
                        minY, maxY, true, true);
                }

                /* Start (green) and end (yellow) of the ramp */
                Vector3 rampStart = path.PositionAt(0f);
                Vector3 rampEnd = path.PositionAt(path.TotalAngle);
                RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, Color.green, rampStart, 4f,
                    rampStart.y - 1f, rampStart.y + 1f, true, true);
                RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, Color.yellow, rampEnd, 4f,
                    rampEnd.y - 1f, rampEnd.y + 1f, true, true);

                if (chord < MIN_CHORD)
                {
                    RenderHoveringLabel(Loc.L("RAB_ERR_SPIRAL_TOO_SMALL",
                        "Radius is too small for this segment length (chord {0}). Increase the radius", (int)chord));
                }
                else if (path.Grade > MAX_GRADE)
                {
                    RenderHoveringLabel(Loc.L("RAB_ERR_SPIRAL_TOO_STEEP",
                        "Grade {0}% is too steep. Raise the radius, lower the height or add a turn", (int)(path.Grade * 100f)));
                }
                else
                {
                    RenderHoveringLabel(Loc.L("RAB_SPIRAL_INFO", "{0} segments, grade {1}%\nClick to build",
                        segments, (int)(path.Grade * 100f)));
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public override void IncreaseButton()
        {
            HelixPanel panel = Panel;
            if (panel != null) panel.RadiusField.Increase();
        }

        public override void DecreaseButton()
        {
            HelixPanel panel = Panel;
            if (panel != null) panel.RadiusField.Decrease();
        }
    }
}
