using ColossalFramework.Math;
using ColossalFramework.UI;
using RoundaboutBuilder.Tools;
using RoundaboutBuilder.UI;
using SharedEnvironment;
using System;
using UnityEngine;

/* By Strad, 01/2019 */

/* Version RELEASE 1.4.0+ */

namespace RoundaboutBuilder
{
    /* Tool for building elliptic roundabouts */

    /* Adjust mode: the ellipse is previewed live and a single left click builds it.
     * The main axis direction comes from the "Angle" field, both radii from the
     * "Main axis" / "Minor axis" fields.
     *
     * Two center modes:
     *   snap (default) - the center is an existing intersection, the ellipse replaces it;
     *   free           - the center is wherever the mouse is, a stand-alone ring is built. */

    class EllipseTool : ToolBaseExtended
    {
        public static EllipseTool Instance;

        private static readonly int DISTANCE_PADDING = 15;
        private static readonly int RADIUS1_DEF = 60;
        private static readonly int RADIUS2_DEF = 30;
        private static readonly int ANGLE_STEP = 15;

        ushort centralNode;

        /* Which center mode produced the current preview. */
        bool centerIsNode;
        Vector3 centerPosition;

        // Saving the values from the last frame so that we don't have to recalculate the ellipse every time
        float prevRadius1 = 0;
        float prevRadius2 = 0;
        float prevAngle = -1;
        Vector3 prevCenter;
        Ellipse ellipse;

        public bool ControlVertices = true;

        /* The ellipse panels only exist when the elliptic roundabout mode is switched on */
        private static EllipsePanel_3 Panel
        {
            get { return UIWindow.instance != null ? UIWindow.instance.P_EllipsePanel_3 : null; }
        }

        private static bool SnapCenter
        {
            get { return Panel != null && Panel.SnapCenterCheckBox != null ? Panel.SnapCenterCheckBox.isChecked : true; }
        }

        /* Returns radii */
        private bool Radius(out float radius1, out float radius2)
        {
            radius1 = radius2 = -1;
            if (Panel == null) return false;
            float? radius1Q = Panel.Radius1tf.Value;
            float? radius2Q = Panel.Radius2tf.Value;
            if (radius1Q == null || radius2Q == null)
            {
                return false;
            }

            radius1 = (float)radius1Q;
            radius2 = (float)radius2Q;

            if (radius2 > radius1)
            {
                return false;
            }
            return true;
        }

        /* Returns the main axis direction angle in degrees (0 - 359) */
        private bool Angle(out float angle)
        {
            angle = -1;
            if (Panel == null) return false;
            float? angleQ = Panel.AngleField.Value;
            if (angleQ == null)
            {
                return false;
            }

            angle = (float)angleQ;
            return true;
        }

        /* 0 degrees points along +X (to the right of the map), 90 degrees along +Z */
        private static Vector3 AxisDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        }

        /* Recalculates the ellipse from the panel values, only when something changed */
        private void RefreshEllipse(Vector3 center)
        {
            if (!Radius(out float radius1, out float radius2) || !Angle(out float angle))
            {
                ellipse = null;
                return;
            }

            if (ellipse != null && radius1 == prevRadius1 && radius2 == prevRadius2
                && angle == prevAngle && (prevCenter - center).sqrMagnitude < 0.01f)
            {
                return;
            }

            prevRadius1 = radius1;
            prevRadius2 = radius2;
            prevAngle = angle;
            prevCenter = center;
            ellipse = new Ellipse(center, AxisDirection(angle), radius1, radius2);
        }

        public void BuildEllipse()
        {
            if (ellipse == null || (centerIsNode && centralNode == 0))
            {
                UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_NO_ELLIPSE", "Move the mouse to where the ellipse should go first!"));
                return;
            }

            ushort centerNode = centralNode;
            Vector3 center = centerPosition;
            bool snap = centerIsNode;
            Ellipse toBeBuiltEllipse = ellipse;
            Ellipse ellipseWithPadding = ellipse;
            /* When the old snapping algorithm is enabled, we create secondary (bigger) ellipse, so the newly connected roads obtained by the 
             * graph traveller are not too short. They will be at least as long as the padding. */
            if (RoundAboutBuilder.UseOldSnappingAlgorithm.value)
            {
                ellipseWithPadding = new Ellipse(center, AxisDirection(prevAngle), prevRadius1 + DISTANCE_PADDING, prevRadius2 + DISTANCE_PADDING);
            }

            UIWindow.instance.LostFocus();

            try
            {
                NetInfo groundInfo = UIWindow.instance.dropDown.Value;
                if (groundInfo == null)
                {
                    UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_NO_NET", "No road selected"));
                    return;
                }

                /* Ground / elevated / bridge. "Ground" keeps the historic behaviour: the road
                 * picked in the dropdown is used as is. */
                NetInfo buildInfo = groundInfo;
                RampStructure structure = Panel != null ? Panel.Structure : RampStructure.Auto;
                if (structure != RampStructure.Auto)
                {
                    string missing;
                    buildInfo = StructureVariants.Resolve(groundInfo, structure, out missing);
                    if (buildInfo == null)
                    {
                        UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_ELLIPSE_NO_VARIANT",
                            "{0} is not available for this road. Pick another structure or another road", missing));
                        return;
                    }

                    if (!StructureVariants.CanBeRaised(buildInfo))
                    {
                        UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_ERR_ELLIPSE_NO_VARIANT",
                            "{0} is not available for this road. Pick another structure or another road",
                            structure.ToString()));
                        return;
                    }
                }

                Debug.Log("[RoundaboutBuilder] Ellipse structure=" + structure + ", mode=" + (snap ? "snap" : "free")
                    + ", prefab=" + (buildInfo != null ? buildInfo.name : "null")
                    + ", ground prefab=" + groundInfo.name + ", segments=" + toBeBuiltEllipse.Density);

                bool reverseDirection = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && RoundAboutBuilder.CtrlToReverseDirection.value;
                NetInfo prefabOverride = structure != RampStructure.Auto ? buildInfo : null;

                FinalConnector finalConnector;
                if (snap)
                {
                    GraphTraveller2 traveller = new GraphTraveller2(centerNode, ellipseWithPadding);
                    EdgeIntersections2 intersections = new EdgeIntersections2(traveller, centerNode, toBeBuiltEllipse, GetFollowTerrain());
                    finalConnector = new FinalConnector(NetUtil.Node(centerNode).Info, intersections, toBeBuiltEllipse, ControlVertices, GetFollowTerrain(), reverseDirection, prefabOverride);
                }
                else
                {
                    /* Free mode: nothing is snapped to, so no intersections are collected - the
                     * ring is just laid out around the mouse position. */
                    finalConnector = new FinalConnector(groundInfo, null, toBeBuiltEllipse, ControlVertices, GetFollowTerrain(), reverseDirection, prefabOverride);
                }

                /* Optional Network Skins pass, same as the spiral and the free tool. */
                GameAction nsAction = null;
                NetInfo skinsPrefab = prefabOverride != null ? prefabOverride : groundInfo;
                if (RoundAboutBuilder.HidePillarsAndLights.value && NetworkSkinsHook.IsAvailable() && skinsPrefab != null)
                {
                    NetworkSkinsHook.ReportSupport(skinsPrefab);
                    nsAction = new NetworkSkinsAction(finalConnector.BuiltSegments, skinsPrefab);
                }

                finalConnector.Build(nsAction);

                // Easter egg
                RoundAboutBuilder.EasterEggToggle();
            }
            catch (ActionException e)
            {
                UIWindow.instance.ThrowErrorMsg(e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                UIWindow.instance.ThrowErrorMsg(e.ToString(), true);
            }
            finally
            {
                ClearPreview();
            }
        }

        private void ClearPreview()
        {
            ellipse = null;
            centralNode = 0;
            centerIsNode = false;
            prevRadius1 = 0;
            prevRadius2 = 0;
            prevAngle = -1;
        }

        protected override void OnClick()
        {
            base.OnClick();
            if (ellipse != null)
            {
                BuildEllipse();
            }
        }

        /* Right click turns the main axis by 15 degrees, same as the spiral's start direction. */
        protected override void OnRightClick()
        {
            base.OnRightClick();
            if (Panel == null) return;

            StepAngle(ANGLE_STEP);
            Debug.Log("[RoundaboutBuilder] Ellipse main axis angle: " + Panel.AngleField.text + " degrees");
        }

        public override void IncreaseButton()
        {
            if (Panel == null) return;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                StepAngle(ANGLE_STEP);
            }
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Panel.Radius1tf.Increase();
            }
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                Panel.Radius2tf.Increase();
            }
            else
            {

                int newRadius1 = 0;
                int newRadius2 = 0;
                if (!Radius(out float radius1, out float radius2))
                {
                    Panel.Radius1tf.text = RADIUS1_DEF.ToString();
                    Panel.Radius2tf.text = RADIUS2_DEF.ToString();
                    return;
                }
                else
                {
                    double ratio = (double)radius2 / (double)radius1;
                    newRadius1 = Convert.ToInt32(Math.Ceiling(new decimal(radius1 + 1) / new decimal(5))) * 5;
                    newRadius2 = Convert.ToInt32(ratio * newRadius1);
                }
                if (Panel.Radius1tf.IsValid(newRadius1) && Panel.Radius2tf.IsValid(newRadius2) && newRadius1 >= newRadius2)
                {
                    Panel.Radius1tf.text = newRadius1.ToString();
                    Panel.Radius2tf.text = newRadius2.ToString();
                }

            }
        }

        public override void DecreaseButton()
        {
            if (Panel == null) return;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                StepAngle(-ANGLE_STEP);
            }
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Panel.Radius1tf.Decrease();
            }
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                Panel.Radius2tf.Decrease();
            }
            else
            {

                int newRadius1 = 0;
                int newRadius2 = 0;
                if (!Radius(out float radius1, out float radius2))
                {
                    Panel.Radius1tf.text = RADIUS1_DEF.ToString();
                    Panel.Radius2tf.text = RADIUS2_DEF.ToString();
                    return;
                }
                else
                {
                    double ratio = (double)radius2 / (double)radius1;
                    newRadius1 = Convert.ToInt32(Math.Floor(new decimal(radius1 - 1) / new decimal(5))) * 5;
                    newRadius2 = Convert.ToInt32(ratio * newRadius1);
                }
                if (Panel.Radius1tf.IsValid(newRadius1) && Panel.Radius2tf.IsValid(newRadius2) && newRadius1 >= newRadius2)
                {
                    Panel.Radius1tf.text = newRadius1.ToString();
                    Panel.Radius2tf.text = newRadius2.ToString();
                }

            }
        }

        /* Rotates the main axis by the given step, wrapping around 0 - 359 */
        private void StepAngle(int step)
        {
            if (Panel == null) return;
            NumericTextField angleField = Panel.AngleField;
            int? current = angleField.Value;
            int newAngle = ((current ?? 0) + step) % 360;
            if (newAngle < 0) newAngle += 360;
            if (angleField.IsValid(newAngle)) angleField.text = newAngle.ToString();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearPreview();
        }

        public override void GoToFirstStage()
        {
            ClearPreview();
            if (UIWindow.instance != null && Panel != null
                && UIWindow.instance.m_panelOnUI != Panel)
            {
                UIWindow.instance.SwitchWindow(Panel);
            }
        }

        private void RenderClickLabel()
        {
            if (ellipse != null)
            {
                RenderHoveringLabel(Loc.L("RAB_HOVER_ELLIPSE_CLICK", "{0} segments\nClick to build, adjust in the panel", ellipse.Density));
            }
            else
            {
                RenderHoveringLabel(Loc.L("RAB_HOVER_ELLIPSE_INVALID", "Invalid radii or angle\nCheck the values in the panel"));
            }
        }

        /* This draws UI shapes on the map. */
        protected override void RenderOverlayExtended(RenderManager.CameraInfo cameraInfo)
        {
            try
            {
                bool snap = SnapCenter;

                if (!insideUI)
                {
                    if (snap)
                    {
                        if (m_hoverNode != 0)
                        {
                            NetNode hoveredNode = NetUtil.Node(m_hoverNode);
                            RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, Color.black, hoveredNode.m_position, 15f, hoveredNode.m_position.y - 1f, hoveredNode.m_position.y + 1f, true, true);
                            centralNode = m_hoverNode;
                            centerIsNode = true;
                            centerPosition = hoveredNode.m_position;
                            RefreshEllipse(hoveredNode.m_position);
                            RenderClickLabel();
                        }
                        else
                        {
                            ClearPreview();
                            RenderMousePositionCircle(cameraInfo);
                            RenderHoveringLabel(Loc.L("RAB_HOVER_ELLIPSE_CENTER", "Hover the mouse over the center intersection"));
                        }
                    }
                    else
                    {
                        /* Free mode: the mouse position is the center of the ellipse. */
                        centralNode = 0;
                        centerIsNode = false;
                        centerPosition = HoverPosition;
                        RefreshEllipse(HoverPosition);
                        RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, Color.black, HoverPosition, 15f, HoverPosition.y - 1f, HoverPosition.y + 1f, true, true);
                        RenderClickLabel();
                    }
                }
                else
                {
                    /* Mouse is over the panel: keep the preview in sync with the fields, just hide the label */
                    if (UIWindow.instance != null && UIWindow.instance.m_hoveringLabel != null)
                    {
                        UIWindow.instance.m_hoveringLabel.isVisible = false;
                    }
                    if (centerIsNode && centralNode != 0)
                    {
                        RefreshEllipse(NetUtil.Node(centralNode).m_position);
                    }
                }

                if (ellipse != null)
                {
                    if (centerIsNode && centralNode != 0)
                    {
                        NetNode centralNodeDraw = NetUtil.Node(centralNode);
                        RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, Color.red, centralNodeDraw.m_position, 15f, centralNodeDraw.m_position.y - 1f, centralNodeDraw.m_position.y + 1f, true, true);
                    }
                    else if (!centerIsNode)
                    {
                        RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, Color.red, centerPosition, 15f, centerPosition.y - 1f, centerPosition.y + 1f, true, true);
                    }
                    DrawEllipse(cameraInfo);
                }
            }
            catch(Exception e)
            {
                Debug.Log("Catched exception while rendering ellipse UI.");
                Debug.Log(e);
                ClearPreview();
            }
        }

        private void DrawEllipse(RenderManager.CameraInfo cameraInfo)
        {
            foreach(Bezier2 bezier in ellipse.Beziers)
            {
                RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, Color.red, ShiftTo3D(bezier), 0.1f, 0, 0, -1f, 1280f, false, true);
            }
        }

        public static Bezier3 ShiftTo3D(Bezier2 bezier)
        {
            Vector3 v1 = new Vector3(bezier.a.x, 0, bezier.a.y);
            Vector3 v2 = new Vector3(bezier.b.x, 0, bezier.b.y);
            Vector3 v3 = new Vector3(bezier.c.x, 0, bezier.c.y);
            Vector3 v4 = new Vector3(bezier.d.x, 0, bezier.d.y);
            return new Bezier3(v1, v2, v3, v4);
        }
    }    
}
