using ColossalFramework;
using ColossalFramework.Math;
using SharedEnvironment;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/* By Strad, 01/2019 */

/* Version RELEASE 1.2.0+ */

namespace RoundaboutBuilder.Tools
{
    /* This class takes the nodes obtained from EdgeIntersections2 and connects them together to create the final roundabout. */

    /* This is all such garbage with old algortithms mixing with new and other stuff... Not the nicest piece of code. */

    public class FinalConnector
    {
        private static readonly int DISTANCE_MIN = 20; // Min distance from other nodes when inserting controlling node

        private List<RoundaboutNode> intersections;
        private bool leftHandTraffic;
        private Ellipse ellipse;
        private NetInfo centerNodeNetInfo;
        /* When the ring is built elevated or as a bridge, every node and segment uses
         * this prefab instead of the one selected in the road dropdown. Null = ground. */
        private NetInfo m_prefabOverride;
        private double m_maxAngDistance;

        public bool m_followTerrain { get; private set; }
        public bool m_reverseDirection { get; private set; }

        private ActionGroup actionGroupTMPE;
        private ActionGroup actionGroupRoads;
        private WrappersDictionary wrappersDictionary;

        /* Every segment this connector adds, so callers can post-process them once they exist
         * (Network Skins pillar / street light hiding needs the segment ids). */
        public readonly List<WrappedSegment> BuiltSegments = new List<WrappedSegment>();

        public int Cost => actionGroupRoads != null ? actionGroupRoads.DoCost() : 0;

        // Time to time something goes wrong. Let's make sure that we don't get stuck in infinite recursion.
        // Didn't happen to me since I degbugged it, but one never knows for sure.
        private int pleasenoinfiniterecursion;

        public FinalConnector(NetInfo centerNodeNetInfo, EdgeIntersections2 edgeIntersections, Ellipse ellipse, bool insertControllingVertices, bool followTerrain, bool reverseDirection, NetInfo prefabOverride = null)
        {
            intersections = edgeIntersections?.Intersections ?? new List<RoundaboutNode>();
            actionGroupTMPE = edgeIntersections?.ActionGroupTMPE ?? new ActionGroup("Set up TMPE");
            actionGroupRoads = edgeIntersections?.ActionGroupRoads ?? new ActionGroup("Build roundabout");
            wrappersDictionary = edgeIntersections?.networkDictionary ?? new WrappersDictionary();

            this.ellipse = ellipse;
            pleasenoinfiniterecursion = 0;
            this.centerNodeNetInfo = centerNodeNetInfo;
            m_prefabOverride = prefabOverride;
            leftHandTraffic = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic ==
                                    SimulationMetaData.MetaBool.True;
            m_followTerrain = followTerrain;
            m_reverseDirection = reverseDirection;

            // We ensure that the segments are not too long. The ellipse segment count is the
            // authoritative limit (one segment per 5 m of radius, e.g. 18 segments at r=90);
            // 25 m of chord is kept as a second cap so the ring never looks polygonal.
            m_maxAngDistance = Math.Min(Math.Min(Math.PI * 25 / ellipse.RadiusMain, Math.PI / 2 + 0.1d),
                2 * Math.PI / Math.Max(ellipse.Density, Ellipse.DENSITY_MIN));

            bool isCircle = ellipse.IsCircle();
            if (!isCircle && insertControllingVertices)
            {
                /* See doc in the method below */
                InsertIntermediateNodes();
            }

            /* If there is nothing to connect to, we start the ring from one point on the ellipse.
             * Circles always did this; free-standing ellipses (center placed anywhere) need it too. */
            if (intersections.Count == 0)
            {
                Vector3 defaultIntersection = ellipse.VectorAtAngle(0);
                defaultIntersection = new Vector3(defaultIntersection.x, m_followTerrain ? NetUtil.TerrainHeight(defaultIntersection) : defaultIntersection.y, defaultIntersection.z);
                //ushort newNodeId = NetAccess.CreateNode(centerNodeNetInfo, defaultIntersection);

                WrappedNode newNodeW = new WrappedNode();
                newNodeW.Position = defaultIntersection;
                newNodeW.NetInfo = m_prefabOverride != null ? m_prefabOverride : centerNodeNetInfo;
                RoundaboutNode raNode = new RoundaboutNode(newNodeW);
                raNode.Create(actionGroupRoads);
                intersections.Add(raNode);
            }

            int count = intersections.Count;
            foreach (RoundaboutNode item in intersections)
            {
                item.angle = Ellipse.VectorsAngle(item.wrappedNode.Position - ellipse.Center);
            }

            /* We sort the nodes according to their angles */
            intersections.Sort();

            /* Goes over all the nodes and conntets each of them to the angulary closest neighbour. (In a given direction) */
            
            for (int i = 0; i < count; i++)
            {
                /* Both circles and ellipses are subdivided so the built ring uses the same
                 * segment count as the preview (one segment per 5 m of mean radius). */
                RoundaboutNode prevNode = CheckAngularDistance(intersections[i], intersections[(i + 1) % count]);
                ConnectNodes(intersections[(i + 1) % count], prevNode);
            }

            // Charge player
            actionGroupRoads.ItemClass = centerNodeNetInfo.m_class;
        }

        public void Build(GameAction extraAction = null)
        {
            /* Optional tail action, e.g. the Network Skins pass. It runs after all segments
             * of this ring exist, inside the same action group. */
            if (extraAction != null)
            {
                actionGroupRoads.Actions.Add(extraAction);
            }
            ModThreading.PushAction(actionGroupRoads, actionGroupTMPE);
        }

        /* For circles only. */
        private RoundaboutNode CheckAngularDistance(RoundaboutNode p1, RoundaboutNode p2)
        {
            RoundaboutNode prevNode = p1;
            double angDif = NormalizeAngle(prevNode.angle - p2.angle);
            if (p1 == p2)
                angDif = 2 * Math.PI;

            /* If the distance between two nodes is too great, we put in an intermediate node inbetween */
            while ( angDif > m_maxAngDistance )
            {
                recursionGuard();

                double angle = NormalizeAngle(prevNode.angle - angDif / Math.Ceiling(angDif / m_maxAngDistance));
                Vector3 vector = ellipse.VectorAtAbsoluteAngle(angle);

                //ushort newNodeId = NetAccess.CreateNode(centerNodeNetInfo, vector);
                WrappedNode newNodeW = new WrappedNode();
                newNodeW.Position = new Vector3(vector.x, m_followTerrain ? NetUtil.TerrainHeight(vector) : vector.y, vector.z); ;
                newNodeW.NetInfo = centerNodeNetInfo;
                actionGroupRoads.Actions.Add(newNodeW);

                RoundaboutNode newNode = new RoundaboutNode(newNodeW);
                newNode.angle = angle;

                ConnectNodes(newNode, prevNode);

                prevNode = newNode;
                angDif = NormalizeAngle(prevNode.angle - p2.angle);
            }

            return prevNode;
        }

        /* Ellipse only. Adds nodes where the ellipse intersects its axes to keep it in shape. User can turn this off. Kepp in mind that since we can only
         * approximate the ellipse (maybe I am wrong), every node on its circumference changes its actual shape. */
        private void InsertIntermediateNodes()
        {
            List<RoundaboutNode> newNodes = new List<RoundaboutNode>();
            /* Originally I planned to pair every node on the ellipse to make it symmetric... Didn't work, I gave up on making it work. */
            /*foreach(VectorNodeStruct intersection in intersections.ToArray())
            {
                double angle = getAbsoluteAngle(intersection.vector);
                VectorNodeStruct newNode = new VectorNodeStruct(ellipse.VectorAtAbsoluteAngle(getConjugateAngle(angle)));
                intersections.Add( newNode );
                EllipseTool.Instance.debugDrawPositions.Add(newNode.vector);
            }*/
            var vec = ellipse.VectorAtAngle(0);
            newNodes.Add(new RoundaboutNode(new Vector3(vec.x, m_followTerrain ? NetUtil.TerrainHeight(vec) : vec.y,vec.z)));
            vec = ellipse.VectorAtAngle(Math.PI / 2);
            newNodes.Add(new RoundaboutNode(new Vector3(vec.x, m_followTerrain ? NetUtil.TerrainHeight(vec) : vec.y, vec.z)));
            vec = ellipse.VectorAtAngle(Math.PI);
            newNodes.Add(new RoundaboutNode(new Vector3(vec.x, m_followTerrain ? NetUtil.TerrainHeight(vec) : vec.y, vec.z)));
            vec = ellipse.VectorAtAngle(3 * Math.PI / 2);
            newNodes.Add(new RoundaboutNode(new Vector3(vec.x, m_followTerrain ? NetUtil.TerrainHeight(vec) : vec.y, vec.z)));
            /*EllipseTool.Instance.debugDrawPositions.Add(new VectorNodeStruct(ellipse.VectorAtAngle(0)).vector);
            EllipseTool.Instance.debugDrawPositions.Add(new VectorNodeStruct(ellipse.VectorAtAngle(Math.PI / 2)).vector);
            EllipseTool.Instance.debugDrawPositions.Add(new VectorNodeStruct(ellipse.VectorAtAngle(Math.PI)).vector);
            EllipseTool.Instance.debugDrawPositions.Add(new VectorNodeStruct(ellipse.VectorAtAngle(3 * Math.PI / 2)).vector);*/
            /* Disgusting nested FOR cycle. We don't want to cluster the nodes too close to each other. */
            foreach(RoundaboutNode vectorNode in newNodes.ToArray())
            {
                foreach(RoundaboutNode intersection in intersections)
                {
                    if(VectorDistance(vectorNode.wrappedNode.Position, intersection.wrappedNode.Position) < DISTANCE_MIN)
                    {
                        newNodes.Remove(vectorNode);
                        //Debug.Log("Node too close, removing from list");
                    }
                }
            }
            intersections.AddRange(newNodes);
        }

        /* See ellipse class */
        private double getAbsoluteAngle(Vector3 absPosition)
        {
            return Ellipse.VectorsAngle(absPosition - ellipse.Center);
        }


        private void ConnectNodes(RoundaboutNode vectorNode1, RoundaboutNode vectorNode2)
        {
            bool invert = leftHandTraffic ^ m_reverseDirection;

            NetInfo buildInfo = m_prefabOverride != null ? m_prefabOverride : centerNodeNetInfo;
            vectorNode1.Create(actionGroupRoads,buildInfo);
            vectorNode2.Create(actionGroupRoads,buildInfo);

            /* NetNode node1 = GetNode(vectorNode1.nodeId);
             NetNode node2 = GetNode(vectorNode2.nodeId);*/

            double angle1 = getAbsoluteAngle(vectorNode1.wrappedNode.Position);
            double angle2 = getAbsoluteAngle(vectorNode2.wrappedNode.Position);

            Vector3 vec1 = ellipse.TangentAtAbsoluteAngle(angle1);
            Vector3 vec2 = ellipse.TangentAtAbsoluteAngle(angle2);

            vec1.Normalize();
            vec2.Normalize();
            vec2 = -vec2;

            /*EllipseTool.Instance.debugDrawVector(10*vec1, vectorNode1.vector);
            EllipseTool.Instance.debugDrawVector(10*vec2, vectorNode2.vector);*/

            //NetInfo netPrefab = PrefabCollection<NetInfo>.FindLoaded("Oneway Road");
            NetInfo netPrefab = m_prefabOverride != null ? m_prefabOverride : UI.UIWindow.instance.dropDown.Value;
            //ushort newSegmentId = NetAccess.CreateSegment(vectorNode1.nodeId, vectorNode2.nodeId, vec1, vec2, netPrefab, invert, leftHandTraffic, true);
            WrappedSegment newSegment = new WrappedSegment();
            newSegment.StartNode = vectorNode1.wrappedNode;
            newSegment.EndNode = vectorNode2.wrappedNode;
            newSegment.StartDirection = vec1;
            newSegment.EndDirection = vec2;
            newSegment.NetInfo = netPrefab;
            newSegment.Invert = invert;
            newSegment.SwitchStartAndEnd = leftHandTraffic;
            newSegment.DeployPlacementEffects = true;

            actionGroupRoads.Actions.Add(newSegment);
            BuiltSegments.Add(newSegment);


            try
            {
                SetupTMPE(newSegment);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            //Debug.Log(string.Format("Building segment between nodes {0}, {1}, bezier scale {2}", node1, node2, scale));
        }

        private void SetupTMPE(WrappedSegment segment)
        {
            /* None of this below works: */
            /*bool resultPrev1 = TrafficManager.Manager.Impl.JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(segment,false);
            bool resultPrev2 = TrafficManager.Manager.Impl.JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(segment,true);
            bool result1 = TrafficManager.Manager.Impl.JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segment, false, true);
            bool result2 = TrafficManager.Manager.Impl.JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segment, true, true);
            bool resultPost1 = TrafficManager.Manager.Impl.JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(segment, false);
            bool resultPost2 = TrafficManager.Manager.Impl.JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(segment, true);*/
            /*Debug.Log($"Setting up tmpe segment {segment}. Result: {resultPrev1}, {resultPrev2}, {result1}, {result2}, {resultPost1}, {resultPost2}");
            ModThreading.Timer(segment);*/
            actionGroupTMPE.Actions.Add(new EnteringBlockedJunctionAllowedAction( segment, true, false) );
            actionGroupTMPE.Actions.Add(new EnteringBlockedJunctionAllowedAction( segment, false, false) );
            actionGroupTMPE.Actions.Add(new LaneChangingAction(segment, true));
            actionGroupTMPE.Actions.Add(new LaneChangingAction(segment, false));
            actionGroupTMPE.Actions.Add(new NoCrossingsAction(segment, true));
            actionGroupTMPE.Actions.Add(new NoCrossingsAction(segment, false));
            actionGroupTMPE.Actions.Add(new NoParkingAction(segment));
        }

        private void recursionGuard()
        {
            pleasenoinfiniterecursion++;
            if (pleasenoinfiniterecursion > 1000)
            {
                throw new Exception("Something went wrong! Mod got stuck in infinite recursion.");
            }
        }

        /* Utility */

        private static float VectorDistance(Vector3 v1, Vector3 v2) // Without Y coordinate
        {
            return (float)(Math.Sqrt(Math.Pow(v1.x - v2.x, 2) + Math.Pow(v1.z - v2.z, 2)));
        }
        private static double NormalizeAngle(double angle)
        {
            angle = angle % (2 * Math.PI);
            return angle < 0 ? angle + 2 * Math.PI : angle;
        }
    }

}
