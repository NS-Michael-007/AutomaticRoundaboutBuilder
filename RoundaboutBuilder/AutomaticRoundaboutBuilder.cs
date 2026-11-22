using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using ICities;
using RoundaboutBuilder.Tools;
using RoundaboutBuilder.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

/* By Strad, 02/2019 - 04/2020 */

/* Version RELEASE 1.9.8 - localization support */

/* Warning: I am lazy thus the version labels across the files may not be updated */

/* There was time when I kept the code more tidy than now. If you are learing how to mod, I suggest you to look at older versions of this mod. (Mayebe even the first one.)
 * Everything is available on Github. */

namespace RoundaboutBuilder
{
    public class RoundAboutBuilder : IUserMod
    {
        public static readonly string VERSION = "RELEASE 1.9.8";
        public static PublishedFileId WORKSHOP_FILE_ID;

        public const string settingsFileName = "RoundaboutBuilder";

        public static readonly SavedInputKey ModShortcut = new SavedInputKey("modShortcut", settingsFileName, SavedInputKey.Encode(KeyCode.O, true, false, false), true);
        public static readonly SavedInputKey IncreaseShortcut = new SavedInputKey("increaseShortcut", settingsFileName, SavedInputKey.Encode(KeyCode.Equals, false, false, false), true);
        public static readonly SavedInputKey DecreaseShortcut = new SavedInputKey("decreaseShortcut", settingsFileName, SavedInputKey.Encode(KeyCode.Minus, false, false, false), true);

        public static readonly Vector2 defWindowPosition = new Vector2(85, 10);

        public static readonly SettingsBool SelectTwoWayRoads = new SettingsBool(
            "RAB_OPT_TWO_WAY", "Allow selection of two-way roads",
            "RAB_OPT_TWO_WAY_DESC", "You can select two-way roads for your roundabouts through the roads menu (if that option is enabled)",
            "selectTwoWayRoads", false);
        public static readonly SettingsBool DoNotRemoveAnyRoads = new SettingsBool(
            "RAB_OPT_NO_ROADS", "Do not remove or connect any roads (experimental)",
            "RAB_OPT_NO_ROADS_DESC", "No roads will be removed or connected when the roundabout is built",
            "dontRemoveRoads", false);
        public static readonly SettingsBool FollowRoadToolSelection = new SettingsBool(
            "RAB_OPT_FOLLOW_ROAD", "Use the selected road in roads menu as the roundabout road",
            "RAB_OPT_FOLLOW_ROAD_DESC", "Your selected road for the roundabout will change as you browse through the roads menu",
            "followRoadToolSelection", true);
        public static readonly SettingsBool ShowUIButton = new SettingsBool(
            "RAB_OPT_TOOLBAR_BUTTON", "Show mod icon in toolbar (needs reload)",
            "RAB_OPT_TOOLBAR_BUTTON_DESC", "Show the Roundabout Builder icon in road tools panel (You can always use CTRL+O to open the mod menu)",
            "showUIButton", true);
        public static readonly SettingsBool DockToUnifiedUI = new SettingsBool(
            "RAB_OPT_DOCK_UUI", "Dock to Unified UI",
            "RAB_OPT_DOCK_UUI_DESC", "Show the roundabout button in the Unified UI toolbar (requires the Unified UI mod). Turn off to go back to the vanilla toolbar button only",
            "dockToUnifiedUI", true);
        public static readonly SettingsBool UseExtraKeys = new SettingsBool(
            "RAB_OPT_EXTRA_KEYS", "Use secondary increase / decrease radius keys",
            "RAB_OPT_EXTRA_KEYS_DESC", "If checked, you can use bound keys from the list below to increase / decrease radius (besides the ones on numpad)",
            "useExtraPlusMinusKeys", true);
        public static readonly SettingsBool UseOldSnappingAlgorithm = new SettingsBool(
            "RAB_OPT_OLD_SNAP", "Legacy: Use old snapping algorithm",
            "RAB_OPT_OLD_SNAP_DESC", "Old snapping algorithm connects roads at 90° angle, but distorts their geometry",
            "useOldSnappingAlgorithm", false);
        public static readonly SettingsBool DoNotFilterPrefabs = new SettingsBool(
            "RAB_OPT_NO_PREFAB_FILTER", "Do not filter prefabs (include all networks in the menu)",
            "RAB_OPT_NO_PREFAB_FILTER_DESC", "The dropdown menu will include all prefabs available, not only one-way roads",
            "doNotFilterPrefabs", false);
        public static readonly SettingsBool NeedMoney = new SettingsBool(
            "RAB_OPT_NEED_MONEY", "Require money",
            "RAB_OPT_NEED_MONEY_DESC", "Building a roundabout will cost you money",
            "needMoney", true);
        public static readonly SettingsBool CtrlToReverseDirection = new SettingsBool(
            "RAB_OPT_CTRL_REVERSE", "Hold CTRL to build in opposite direction",
            "RAB_OPT_CTRL_REVERSE_DESC", "The roundabout will be built in opposite direction if you hold CTRL",
            "ctrlToOppositeDir", false);
        public static readonly SettingsBool UnlimitedRadius = new SettingsBool(
            "RAB_OPT_UNLIMITED_RADIUS", "Unlimited radius",
            "RAB_OPT_UNLIMITED_RADIUS_DESC", "(Almost) Unlimited roundabout radius - up to 20 000 units",
            "unlimitedRadius", false);
        public static readonly SettingsBool LegacyEllipticRoundabouts = new SettingsBool(
            "RAB_OPT_LEGACY_ELLIPSE", "Legacy: Elliptic roundabouts",
            "RAB_OPT_LEGACY_ELLIPSE_DESC", "Enable elliptic roundabouts. This feature is no longer maintained",
            "legacyEllipticRoundabouts", false);
        public static readonly SettingsBool EnableSpiralRamp = new SettingsBool(
            "RAB_OPT_SPIRAL_RAMP", "Enable spiral ramps (experimental)",
            "RAB_OPT_SPIRAL_RAMP_DESC", "Adds a spiral ramp tool to the main panel. Both ends of the ramp stay open, you connect them yourself",
            "enableSpiralRamp", false);
        public static readonly SettingsBool HidePillarsAndLights = new SettingsBool(
            "RAB_OPT_NS_HIDE", "Hide bridge pillars and street lights (Network Skins)",
            "RAB_OPT_NS_HIDE_DESC", "Spiral ramps built with a bridge or elevated prefab get no pillars and no street lights. Requires Network Skins Continued; without it, or when a lane prop is not recognised as a street light, only a line is written to the log",
            "hidePillarsAndLights", false);

        //public static readonly SavedBool SeenUpdateMsg = new SavedBool("seenUpdateMsg170", RoundAboutBuilder.settingsFileName, false, true);
        public static readonly SavedInt savedWindowX = new SavedInt("windowX", settingsFileName, (int)defWindowPosition.x, true);
        public static readonly SavedInt savedWindowY = new SavedInt("windowY", settingsFileName, (int)defWindowPosition.y, true);
        public static readonly SavedInt TotalRoundaboutsBuilt = new SavedInt("totalRoundaboutsBuilt", settingsFileName, 0, true);

        //public static readonly SavedBool ShowUndoItAd = new SavedBool("showUndoItAd", RoundAboutBuilder.settingsFileName, true, true);

        public static bool _settingsFailed = false;

        public RoundAboutBuilder()
        {
            try
            {
                // Creating setting file - from SamsamTS
                if (GameSettings.FindSettingsFileByName(settingsFileName) == null)
                {
                    GameSettings.AddSettingsFile(new SettingsFile[] { new SettingsFile() { fileName = settingsFileName } });
                }
            }
            catch (Exception e)
            {
                _settingsFailed = true;
                Debug.Log("Couldn't load/create the setting file.");
                Debug.LogException(e);
            }
        }

        public string Name
        {
            get { return Loc.L("RAB_MOD_NAME", "Roundabout Builder"); }
        }

        public string Description
        {
            get { return Loc.L("RAB_MOD_DESC", "Press CTRL+O to open mod menu") + " [" + VERSION + "]"; }
        }

        public void OnEnabled()
        {
            try
            {
                Loc.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Localization initialization failed.");
                Debug.LogWarning(e);
            }

            // Probably useless
            try
            {
                WORKSHOP_FILE_ID = new PublishedFileId(1625704117uL);
            }
            catch
            {
                Debug.Log("Error when assigning Workshop File ID");
            }

            try
            {
                UUIIntegration.RegisterIfEnabled();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Unified UI registration failed.");
                Debug.LogWarning(e);
            }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                Loc.Initialize();

                UIHelper group = helper.AddGroup(Name) as UIHelper;
                UIPanel panel = group.self as UIPanel;

                AddLanguageDropdown(group);
                group.AddSpace(10);

                FollowRoadToolSelection.Draw(group);
                NeedMoney.Draw(group);
                CtrlToReverseDirection.Draw(group);
                UnlimitedRadius.Draw(group, (b) =>
                {
                    if (UIWindow.instance != null) UIWindow.instance.InitPanels();
                });
                SelectTwoWayRoads.Draw(group, (b) =>
                {
                    if (UIWindow.instance != null) UIWindow.instance.dropDown.Populate(); // Reload dropdown menu
                });
                DoNotFilterPrefabs.Draw(group, (b) =>
                {
                    if (UIWindow.instance != null) UIWindow.instance.dropDown.Populate(); // Reload dropdown menu
                });
                DoNotRemoveAnyRoads.Draw(group);

                group.AddSpace(10);

                ShowUIButton.Draw(group);
                DockToUnifiedUI.Draw(group, (b) =>
                {
                    UUIIntegration.Apply(b);
                });
                UseExtraKeys.Draw(group);
                UseOldSnappingAlgorithm.Draw(group);
                LegacyEllipticRoundabouts.Draw(group, (b) =>
                {
                    if (UIWindow.instance != null) UIWindow.instance.InitPanels();
                });
                EnableSpiralRamp.Draw(group, (b) =>
                {
                    if (UIWindow.instance != null) UIWindow.instance.InitPanels();
                });
                HidePillarsAndLights.Draw(group);

                group.AddSpace(10);

                panel.gameObject.AddComponent<OptionsKeymapping>();

                group.AddSpace(10);

                group.AddButton(Loc.L("RAB_BTN_RESET_POS", "Reset tool window position"), () =>
                {
                    savedWindowX.Delete();
                    savedWindowY.Delete();

                    if (UIWindow.instance)
                        UIWindow.instance.absolutePosition = defWindowPosition;
                });

                group.AddSpace(10);

                group.AddButton(Loc.L("RAB_BTN_REMOVE_GLITCHED", "Remove glitched roads (Save game inbefore)"), () =>
                {
                    Tools.GlitchedRoadsCheck.RemoveGlitchedRoads();
                });

            }
            catch (Exception e)
            {
                Debug.Log("OnSettingsUI failed");
                Debug.LogException(e);
            }
        }

        /* Language selector - only shown when at least one translation file is present. */
        private static void AddLanguageDropdown(UIHelper group)
        {
            try
            {
                List<LanguageEntry> languages = Loc.AvailableLanguages;
                if (languages == null || languages.Count == 0)
                    return;

                string[] options = new string[languages.Count + 1];
                options[0] = Loc.L("RAB_LANGUAGE_AUTO", "Auto (system language)");

                string saved = Loc.GetSavedLanguageCode();
                int selected = 0;

                for (int i = 0; i < languages.Count; i++)
                {
                    options[i + 1] = languages[i].NativeName;
                    if (!string.IsNullOrEmpty(saved) && languages[i].Code == saved)
                        selected = i + 1;
                }

                group.AddDropdown(Loc.L("RAB_LANGUAGE_LABEL", "Language"), options, selected, (index) =>
                {
                    string code = string.Empty;
                    if (index > 0 && index <= languages.Count)
                        code = languages[index - 1].Code;

                    Loc.SetLanguage(code);

                    /* The window was built with the previous language, rebuild it. */
                    UIWindow.Rebuild();
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Could not create the language dropdown.");
                Debug.LogWarning(e);
            }
        }

        // Easter egg
        public static void EasterEggToggle()
        {
            try
            {
                int requiredCount = 1024;
                if (TotalRoundaboutsBuilt.value < int.MaxValue)
                    TotalRoundaboutsBuilt.value++;
                if (TotalRoundaboutsBuilt.value == requiredCount)
                {
                    UIWindow.instance.ThrowErrorMsg(Loc.L("RAB_EASTER_EGG",
                        "Congratulations! You've built {0} roundabouts in total! Do you live in France?\n" +
                        "Fun fact: There is more than 32000 roundabouts in France, which is about one roundabout every 30 km. But the country with the highest number of roundabouts" +
                        " per km is the United Kingdom (25000 in total/one every 15 km). Thanks to Scott Batson from Quora for this summary (2015)",
                        requiredCount));
                }
            }
            catch { }
        }
    }
}
