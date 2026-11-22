using ColossalFramework.UI;
using System;
using UnityEngine;
using UnifiedUI.Helpers;

/* Optional Unified UI integration.

 * Puts the mod's roundabout button into the Unified UI toolbar. Everything here
 * is optional: when Unified UI is not installed (or not enabled, or the
 * "Dock to Unified UI" setting is off) the vanilla toolbar button keeps working
 * exactly as before and nothing else changes.
 */
namespace RoundaboutBuilder.UI
{
    public static class UUIIntegration
    {
        private const string ButtonName = "RoundaboutBuilder";
        private const string IconFileName = "uui_roundabout_builder.png";

        private static UUICustomButton s_button;
        private static bool s_registered;

        /* Called from OnEnabled and again when a level is loaded. The second call
         * covers the case where Unified UI is loaded after this mod (the library
         * has no queue for late registrations). */
        public static void RegisterIfEnabled()
        {
            if (s_registered) return;
            if (!RoundAboutBuilder.DockToUnifiedUI.value)
            {
                Debug.Log("[RoundaboutBuilder] Unified UI: skipped (Dock to Unified UI is off).");
                return;
            }

            Register();
        }

        public static void Register()
        {
            if (s_registered) return;

            try
            {
                if (!IsUUIPluginEnabled())
                {
                    Debug.Log("[RoundaboutBuilder] Unified UI plugin not found or not enabled - keeping the vanilla toolbar button.");
                    return;
                }

                string iconPath = UUIHelpers.GetFullPath<RoundAboutBuilder>(new string[] { IconFileName });
                Texture2D icon = UUIHelpers.LoadTexture(iconPath);
                if (icon == null)
                {
                    Debug.LogWarning("[RoundaboutBuilder] UUI icon not found: " + iconPath);
                    return;
                }

                s_button = UUIHelpers.RegisterCustomButton(
                    ButtonName,
                    null, /* null = the default Unified UI group */
                    Loc.L("RAB_UUI_TOOLTIP", "Roundabout Builder"),
                    icon,
                    new Action<bool>(OnToggle),
                    new Action<ToolBase>(OnToolChanged),
                    new UUIHotKeys()); /* no hotkey: CTRL+O is still handled by ModThreading */

                s_registered = s_button != null;
                Debug.Log("[RoundaboutBuilder] Unified UI button registered.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Unified UI registration skipped.");
                Debug.LogWarning(e);
            }
        }

        /* Unified UI 2.2 keeps UnifiedUI.API.UUIAPI inside UnifiedUILib.dll, while
         * UUIHelpers.IsUUIEnabled() looks for that type inside the *mod* assembly -
         * so it reports false for every 2.2 user. All we actually need to know is
         * whether an enabled plugin brings UnifiedUIMod / UnifiedUILib along. */
        private static bool IsUUIPluginEnabled()
        {
            try
            {
                foreach (ColossalFramework.Plugins.PluginManager.PluginInfo plugin in
                    ColossalFramework.Plugins.PluginManager.instance.GetPluginsInfo())
                {
                    if (plugin == null || !plugin.isEnabled) continue;

                    System.Collections.Generic.List<System.Reflection.Assembly> assemblies = plugin.GetAssemblies();
                    if (assemblies == null) continue;

                    foreach (System.Reflection.Assembly assembly in assemblies)
                    {
                        if (assembly == null) continue;

                        string assemblyName = assembly.GetName().Name;
                        if (assemblyName == "UnifiedUIMod" || assemblyName == "UnifiedUILib") return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Could not list plugins while looking for Unified UI.");
                Debug.LogWarning(e);
            }

            return false;
        }

        /* Called when the "Dock to Unified UI" checkbox is changed. */
        public static void Apply(bool docked)
        {
            if (docked)
                RegisterIfEnabled();
            else
                Unregister();
        }

        public static void Unregister()
        {
            try
            {
                if (s_button != null && s_button.Button != null)
                    UUIHelpers.Destroy(s_button.Button);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Could not remove the Unified UI button.");
                Debug.LogWarning(e);
            }

            s_button = null;
            s_registered = false;
        }

        private static void OnToggle(bool isOn)
        {
            UIWindow window = UIWindow.instance;
            if (window == null) return;

            if (isOn != window.enabled)
                window.Toggle();
        }

        private static void OnToolChanged(ToolBase tool)
        {
        }

        /* Keeps the Unified UI button in sync when the window is opened or closed
         * somewhere else (window button, CTRL+O, Close button). */
        public static void SyncButtonState(bool visible)
        {
            if (s_button == null || s_button.Button == null) return;

            try
            {
                s_button.IsPressed = visible;
            }
            catch
            {
            }
        }
    }
}
