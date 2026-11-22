using ColossalFramework;
using ColossalFramework.Plugins;
using RoundaboutBuilder.UI;
using SharedEnvironment;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/* Optional Network Skins Continued integration.

 * When Network Skins is installed, enabled and the player ticked the option in
 * the mod settings, every segment of a freshly built spiral ramp gets a
 * Network Skins "skin" whose bridge pillars and street lights are set to
 * nothing. The mod never touches the prefab itself: the values only live in
 * Network Skins' per segment tables (SegmentSkins / NodeSkins), which the game
 * reads exclusively through Network Skins' own Harmony patches.

 * Everything is reached by reflection on purpose - NetworkSkins.dll is not a
 * compile time dependency, so this mod keeps loading (and behaves exactly as
 * before) when Network Skins is absent, disabled or renamed.
 */
namespace RoundaboutBuilder.Tools
{
    public static class NetworkSkinsHook
    {
        private const string AssemblyName = "NetworkSkins";

        /* PillarType.Bridge and PillarType.Middle are both cleared: Network Skins
         * shows a separate pillar list per structure, and clearing both covers
         * elevated and bridge prefabs without guessing. */
        private const float LightRepeatDistance = 40f;

        private static bool s_resolved;
        private static bool s_available;

        private static Type tManager;
        private static Type tSkin;
        private static Type tModifier;
        private static Type tPillarModifier;
        private static Type tLightModifier;
        private static Type tPillarType;
        private static Type tStreetLightUtils;

        private static PropertyInfo pInstance;
        private static FieldInfo fSegmentSkins;
        private static FieldInfo fNodeSkins;
        private static FieldInfo fAppliedSkins;
        private static MethodInfo mUsageAdded;
        private static MethodInfo mUsageRemoved;
        private static MethodInfo mGetMatchingSkin;
        private static MethodInfo mIsStreetLightProp;

        /* True when NetworkSkins.dll is loaded and every member we need exists. */
        public static bool IsAvailable()
        {
            Resolve();
            return s_available;
        }

        private static void Resolve()
        {
            if (s_resolved) return;
            s_resolved = true;

            try
            {
                Assembly assembly = FindAssembly();
                if (assembly == null)
                {
                    Debug.Log("[RoundaboutBuilder] Network Skins not loaded - pillar/light hiding is off.");
                    return;
                }

                tManager = assembly.GetType("NetworkSkins.Skins.NetworkSkinManager");
                tSkin = assembly.GetType("NetworkSkins.Skins.NetworkSkin");
                /* NetworkSkinModifier sits in NetworkSkins.Skins, not in .Modifiers -
                 * the modifiers themselves are the ones in .Modifiers. */
                tModifier = assembly.GetType("NetworkSkins.Skins.NetworkSkinModifier");
                tPillarModifier = assembly.GetType("NetworkSkins.Skins.Modifiers.PillarModifier");
                tLightModifier = assembly.GetType("NetworkSkins.Skins.Modifiers.StreetLightModifier");
                tPillarType = assembly.GetType("NetworkSkins.Net.PillarType");
                tStreetLightUtils = assembly.GetType("NetworkSkins.Net.StreetLightUtils");

                if (tManager == null || tSkin == null || tModifier == null || tPillarModifier == null
                    || tLightModifier == null || tPillarType == null || tStreetLightUtils == null)
                {
                    Debug.LogWarning("[RoundaboutBuilder] Network Skins API changed - pillar/light hiding is off. Missing: "
                        + MissingNames(
                            tManager == null ? "NetworkSkinManager" : null,
                            tSkin == null ? "NetworkSkin" : null,
                            tModifier == null ? "NetworkSkinModifier" : null,
                            tPillarModifier == null ? "PillarModifier" : null,
                            tLightModifier == null ? "StreetLightModifier" : null,
                            tPillarType == null ? "PillarType" : null,
                            tStreetLightUtils == null ? "StreetLightUtils" : null));
                    return;
                }

                pInstance = tManager.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                fSegmentSkins = tManager.GetField("SegmentSkins", BindingFlags.Public | BindingFlags.Static);
                fNodeSkins = tManager.GetField("NodeSkins", BindingFlags.Public | BindingFlags.Static);
                fAppliedSkins = tManager.GetField("AppliedSkins", BindingFlags.Public | BindingFlags.Instance);
                mUsageAdded = tManager.GetMethod("UsageAdded", BindingFlags.Public | BindingFlags.Instance);
                mUsageRemoved = tManager.GetMethod("UsageRemoved", BindingFlags.Public | BindingFlags.Instance);
                mGetMatchingSkin = tSkin.GetMethod("GetMatchingSkinFromList", BindingFlags.Public | BindingFlags.Static);
                mIsStreetLightProp = tStreetLightUtils.GetMethod("IsStreetLightProp", BindingFlags.Public | BindingFlags.Static);

                if (pInstance == null || fSegmentSkins == null || fNodeSkins == null || fAppliedSkins == null
                    || mUsageAdded == null || mUsageRemoved == null || mGetMatchingSkin == null
                    || mIsStreetLightProp == null)
                {
                    Debug.LogWarning("[RoundaboutBuilder] Network Skins API is incomplete - pillar/light hiding is off. Missing: "
                        + MissingNames(
                            pInstance == null ? "instance" : null,
                            fSegmentSkins == null ? "SegmentSkins" : null,
                            fNodeSkins == null ? "NodeSkins" : null,
                            fAppliedSkins == null ? "AppliedSkins" : null,
                            mUsageAdded == null ? "UsageAdded" : null,
                            mUsageRemoved == null ? "UsageRemoved" : null,
                            mGetMatchingSkin == null ? "GetMatchingSkinFromList" : null)
                        + (mIsStreetLightProp == null ? " IsStreetLightProp" : ""));
                    return;
                }

                s_available = true;
                Debug.Log("[RoundaboutBuilder] Network Skins found - pillar/light hiding available.");
            }
            catch (Exception e)
            {
                s_available = false;
                Debug.LogWarning("[RoundaboutBuilder] Network Skins lookup failed.");
                Debug.LogWarning(e);
            }
        }

        /* Only an enabled plugin counts, same rule as the Unified UI integration. */
        private static Assembly FindAssembly()
        {
            try
            {
                foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo())
                {
                    if (plugin == null || !plugin.isEnabled) continue;

                    List<Assembly> assemblies = plugin.GetAssemblies();
                    if (assemblies == null) continue;

                    foreach (Assembly assembly in assemblies)
                    {
                        if (assembly == null) continue;
                        if (assembly.GetName().Name == AssemblyName) return assembly;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Could not list plugins while looking for Network Skins.");
                Debug.LogWarning(e);
            }

            /* Fallback: the assembly may be loaded without an enabled plugin owning it. */
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly != null && assembly.GetName().Name == AssemblyName) return assembly;
            }

            return null;
        }

        private static object ManagerInstance()
        {
            try
            {
                return pInstance.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static string MissingNames(string a, string b, string c, string d, string e, string f, string g)
        {
            string result = "";
            result += a != null ? a + " " : "";
            result += b != null ? b + " " : "";
            result += c != null ? c + " " : "";
            result += d != null ? d + " " : "";
            result += e != null ? e + " " : "";
            result += f != null ? f + " " : "";
            result += g != null ? g + " " : "";
            return result.Trim();
        }

        /* Pillars are node buildings, and the game only builds them for AIs that
         * implement GetNodeBuilding - the same list Network Skins patches. */
        public static bool SupportsPillars(NetInfo prefab)
        {
            if (prefab == null || prefab.m_netAI == null) return false;

            string aiName = prefab.m_netAI.GetType().Name;
            return aiName == "RoadBridgeAI" || aiName == "TrainTrackBridgeAI" || aiName == "PedestrianBridgeAI"
                || aiName == "MonorailTrackAI" || aiName == "MetroTrackBridgeAI";
        }

        /* Number of lane props that Network Skins itself would treat as street
         * lights. When that is 0 the light part is skipped and only logged. */
        public static int CountStreetLights(NetInfo prefab, out int totalProps)
        {
            totalProps = 0;
            if (prefab == null || prefab.m_lanes == null || mIsStreetLightProp == null) return 0;

            int lights = 0;
            for (int l = 0; l < prefab.m_lanes.Length; l++)
            {
                NetInfo.Lane lane = prefab.m_lanes[l];
                if (lane == null || lane.m_laneProps == null || lane.m_laneProps.m_props == null) continue;

                for (int p = 0; p < lane.m_laneProps.m_props.Length; p++)
                {
                    NetLaneProps.Prop prop = lane.m_laneProps.m_props[p];
                    if (prop == null) continue;

                    totalProps++;

                    PropInfo info = prop.m_finalProp;
                    if (info == null) info = prop.m_prop;

                    try
                    {
                        object result = mIsStreetLightProp.Invoke(null, new object[] { info });
                        if (result is bool && (bool)result) lights++;
                    }
                    catch
                    {
                    }
                }
            }

            return lights;
        }

        /* Write one log line per ramp so the player can tell what happened. */
        public static void ReportSupport(NetInfo prefab)
        {
            if (!IsAvailable() || prefab == null) return;

            int totalProps;
            int lights = CountStreetLights(prefab, out totalProps);

            Debug.Log("[RoundaboutBuilder] Network Skins: prefab=" + prefab.name
                + ", AI=" + (prefab.m_netAI != null ? prefab.m_netAI.GetType().Name : "none")
                + ", pillar support=" + SupportsPillars(prefab)
                + ", street lights " + lights + "/" + totalProps + " props");

            if (!SupportsPillars(prefab))
            {
                Debug.Log("[RoundaboutBuilder] Network Skins: this prefab has no pillar support - only street lights will be touched.");
            }

            if (lights == 0)
            {
                Debug.Log("[RoundaboutBuilder] Network Skins: no lane prop was recognised as a street light - lights are left as they are.");
            }
        }

        /* Modifier list: no bridge pillar, no middle pillar, no street light. */
        public static IList BuildModifiers(NetInfo prefab, out bool lightsIncluded)
        {
            lightsIncluded = false;

            Type listType = typeof(List<>).MakeGenericType(tModifier);
            IList modifiers = (IList)Activator.CreateInstance(listType);

            object bridgeType = Enum.Parse(tPillarType, "Bridge");
            modifiers.Add(Activator.CreateInstance(tPillarModifier, new object[] { bridgeType, null }));

            object middleType = Enum.Parse(tPillarType, "Middle");
            modifiers.Add(Activator.CreateInstance(tPillarModifier, new object[] { middleType, null }));

            int totalProps;
            if (CountStreetLights(prefab, out totalProps) > 0)
            {
                modifiers.Add(Activator.CreateInstance(tLightModifier, new object[] { null, LightRepeatDistance }));
                lightsIncluded = true;
            }

            return modifiers;
        }

        /* Give one segment the skin. Nodes are set too: bridge pillars are node
         * buildings, and Network Skins only substitutes them when NodeSkins[] is
         * filled - pasting a segment skin alone leaves the pillars standing. */
        public static bool Apply(ushort segmentId, NetInfo prefab, IList modifiers)
        {
            if (!s_available || prefab == null) return false;

            object manager = ManagerInstance();
            if (manager == null)
            {
                Debug.Log("[RoundaboutBuilder] Network Skins manager is not ready - skipping.");
                return false;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            NetSegment segment = netManager.m_segments.m_buffer[segmentId];

            if (segment.m_flags == NetSegment.Flags.None) return false;

            if (segment.Info != prefab)
            {
                Debug.LogWarning("[RoundaboutBuilder] Network Skins: segment prefab does not match the skin prefab, skipped.");
                return false;
            }

            object skin = GetOrCreateSkin(manager, prefab, modifiers);
            if (skin == null) return false;

            IList segmentSkins = (IList)fSegmentSkins.GetValue(null);
            IList nodeSkins = (IList)fNodeSkins.GetValue(null);
            if (segmentSkins == null || nodeSkins == null) return false;

            segmentSkins[segmentId] = skin;
            nodeSkins[segment.m_startNode] = skin;
            nodeSkins[segment.m_endNode] = skin;

            /* Segment plus both nodes: this is exactly the reference count that
             * Network Skins' own OnSegmentPlaced uses. */
            mUsageAdded.Invoke(manager, new object[] { skin, 3 });

            Refresh(netManager, segmentId, segment);
            return true;
        }

        /* Undo: leave the tables clean and give the references back. */
        public static void Revert(ushort segmentId)
        {
            if (!s_available) return;

            object manager = ManagerInstance();
            if (manager == null) return;

            IList segmentSkins = (IList)fSegmentSkins.GetValue(null);
            IList nodeSkins = (IList)fNodeSkins.GetValue(null);
            if (segmentSkins == null || nodeSkins == null) return;

            NetManager netManager = Singleton<NetManager>.instance;
            NetSegment segment = netManager.m_segments.m_buffer[segmentId];

            object skin = segmentSkins[segmentId];
            if (skin == null) return;

            int removed = 0;

            segmentSkins[segmentId] = null;
            removed++;

            if (segment.m_flags != NetSegment.Flags.None)
            {
                if (nodeSkins[segment.m_startNode] == skin)
                {
                    nodeSkins[segment.m_startNode] = null;
                    removed++;
                }

                if (nodeSkins[segment.m_endNode] == skin)
                {
                    nodeSkins[segment.m_endNode] = null;
                    removed++;
                }

                Refresh(netManager, segmentId, segment);
            }

            for (int i = 0; i < removed; i++)
            {
                try { mUsageRemoved.Invoke(manager, new object[] { skin }); }
                catch { }
            }
        }

        private static object GetOrCreateSkin(object manager, NetInfo prefab, IList modifiers)
        {
            try
            {
                /* Reuse an identical skin the player already applied, so the same
                 * combination does not pile up in the save file. */
                IList applied = (IList)fAppliedSkins.GetValue(manager);
                if (applied != null)
                {
                    object match = mGetMatchingSkin.Invoke(null, new object[] { applied, prefab, modifiers });
                    if (match != null) return match;
                }

                /* The constructor copies the prefab's lanes and calls Recalculate. */
                return Activator.CreateInstance(tSkin, new object[] { prefab, modifiers });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Network Skins: could not create the skin.");
                Debug.LogWarning(e);
                return null;
            }
        }

        /* Make the change visible without waiting for a simulation step, and mark
         * the nodes so the pillar buildings are rebuilt on the next update. */
        private static void Refresh(NetManager netManager, ushort segmentId, NetSegment segment)
        {
            try
            {
                netManager.UpdateNodeColors(segment.m_startNode);
                netManager.UpdateNodeColors(segment.m_endNode);
                netManager.UpdateSegmentRenderer(segmentId, true);
                netManager.UpdateNodeRenderer(segment.m_startNode, true);
                netManager.UpdateNodeRenderer(segment.m_endNode, true);
                netManager.UpdateNode(segment.m_startNode);
                netManager.UpdateNode(segment.m_endNode);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }
        }
    }

    /* Runs after every node and segment of the ramp has been created, so the
     * segment ids are valid. Undo runs before the wrappers release the network. */
    public class NetworkSkinsAction : GameAction
    {
        private readonly List<WrappedSegment> m_segments;
        private readonly NetInfo m_prefab;
        private IList m_modifiers;

        public NetworkSkinsAction(List<WrappedSegment> segments, NetInfo prefab)
        {
            m_segments = segments;
            m_prefab = prefab;
        }

        public override void Do()
        {
            if (!NetworkSkinsHook.IsAvailable()) return;

            if (m_modifiers == null)
            {
                bool lightsIncluded;
                m_modifiers = NetworkSkinsHook.BuildModifiers(m_prefab, out lightsIncluded);
            }

            foreach (WrappedSegment segment in m_segments)
            {
                try
                {
                    NetworkSkinsHook.Apply(segment.Id, m_prefab, m_modifiers);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[RoundaboutBuilder] Network Skins: could not skin a segment.");
                    Debug.LogWarning(e);
                }
            }
        }

        public override void Undo()
        {
            for (int i = m_segments.Count - 1; i >= 0; i--)
            {
                try
                {
                    NetworkSkinsHook.Revert(m_segments[i].Id);
                }
                catch
                {
                }
            }
        }

        public override void Redo()
        {
            Do();
        }
    }
}
