using System;
using System.Reflection;
using UnityEngine;

/* Chooses which prefab a spiral segment is built with: ground, elevated or bridge.
 *
 * The game keeps the whole "family" of a road in its AI (m_elevatedInfo, m_bridgeInfo,
 * m_slopeInfo, m_tunnelInfo), and Network Anarchy exposes the same thing as
 * NetworkAnarchy.NetAIWrapper.Elevated / .Bridge. We ask NAR first (that is its public
 * API) and fall back to reading the AI fields directly, which is exactly what NAR does
 * internally. NAR is reached by reflection because its enum is called Mode in some
 * releases and Modes in others, so a hard compile-time reference would break on the
 * other version.
 */
namespace RoundaboutBuilder.Tools
{
    public enum RampStructure
    {
        Auto = 0,
        Elevated = 1,
        Bridge = 2
    }

    internal static class StructureVariants
    {
        private static bool s_narSearched;
        private static Assembly s_narAssembly;

        /* Returns the prefab to build with, or null when the road has no such variant.
         * 'missing' then carries the localised name of the variant that was asked for. */
        public static NetInfo Resolve(NetInfo prefab, RampStructure structure, out string missing)
        {
            missing = null;
            if (prefab == null) return null;

            NetInfo elevated = Variant(prefab, "Elevated", "m_elevatedInfo");
            NetInfo bridge = Variant(prefab, "Bridge", "m_bridgeInfo");

            if (structure == RampStructure.Elevated)
            {
                if (elevated == null)
                    missing = Loc.L("RAB_STRUCT_ELEVATED", "elevated");
                return elevated;
            }

            if (structure == RampStructure.Bridge)
            {
                if (bridge == null)
                    missing = Loc.L("RAB_STRUCT_BRIDGE", "bridge");
                return bridge;
            }

            /* Auto: follow the mode the player picked in Network Anarchy. Without NAR,
             * or with NAR in its neutral mode, an elevated ramp is what people expect. */
            string narMode = NetworkAnarchyMode();
            if (narMode == "Bridge" && bridge != null) return bridge;
            if (narMode == "Elevated" && elevated != null) return elevated;
            if (narMode == "Ground" || narMode == "Tunnel") return prefab;

            return elevated != null ? elevated : prefab;
        }

        /* True when the prefab can actually be raised - used as a safety net before we
         * hand it to NetUtil.GetElevation, which would otherwise flatten the ramp. */
        public static bool CanBeRaised(NetInfo prefab)
        {
            if (prefab == null || prefab.m_netAI == null) return false;

            int min, max;
            prefab.m_netAI.GetElevationLimits(out min, out max);
            return max > 0;
        }

        private static NetInfo Variant(NetInfo prefab, string narProperty, string fieldName)
        {
            try
            {
                Assembly nar = NarAssembly();
                if (nar != null)
                {
                    Type wrapperType = nar.GetType("NetworkAnarchy.NetAIWrapper");
                    if (wrapperType != null && prefab.m_netAI != null)
                    {
                        object wrapper = Activator.CreateInstance(wrapperType, new object[] { prefab.m_netAI });
                        PropertyInfo property = wrapperType.GetProperty(narProperty);
                        if (property != null)
                        {
                            NetInfo result = property.GetValue(wrapper, null) as NetInfo;
                            if (result != null) return result;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Network Anarchy variant lookup failed for " + narProperty + ".");
                Debug.LogWarning(e);
            }

            return FieldVariant(prefab.m_netAI, fieldName);
        }

        /* Reads m_elevatedInfo / m_bridgeInfo the way NAR itself does, walking up the
         * AI hierarchy in case the field lives on a base class. */
        private static NetInfo FieldVariant(NetAI ai, string fieldName)
        {
            if (ai == null) return null;

            Type type = ai.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(ai) as NetInfo;
                type = type.BaseType;
            }

            return null;
        }

        private static Assembly NarAssembly()
        {
            if (s_narSearched) return s_narAssembly;

            s_narSearched = true;
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly == null) continue;
                    if (assembly.GetType("NetworkAnarchy.NetworkAnarchy", false) != null)
                    {
                        s_narAssembly = assembly;
                        break;
                    }
                }
            }
            catch
            {
            }

            return s_narAssembly;
        }

        /* Name of NetworkAnarchy.instance.mode, e.g. "Elevated". Null when NAR is absent. */
        private static string NetworkAnarchyMode()
        {
            try
            {
                Assembly nar = NarAssembly();
                if (nar == null) return null;

                Type main = nar.GetType("NetworkAnarchy.NetworkAnarchy", false);
                if (main == null) return null;

                object instance = MemberValue(main, null, "instance");
                if (instance == null) return null;

                object mode = MemberValue(main, instance, "mode");
                return mode != null ? Enum.GetName(mode.GetType(), mode) : null;
            }
            catch
            {
                return null;
            }
        }

        private static object MemberValue(Type type, object instance, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            if (property != null) return property.GetValue(instance, null);

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            return field != null ? field.GetValue(instance) : null;
        }
    }
}
