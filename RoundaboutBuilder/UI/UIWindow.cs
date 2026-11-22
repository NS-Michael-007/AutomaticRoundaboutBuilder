using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using System;
using UnityEngine;

/* Version RELEASE 1.4.0+ */

/* By Strad, 2019 */

/* Credit to SamsamTS and T__S from whom I copied big chunks of code */

/* Welcome to hell on earth */

namespace RoundaboutBuilder.UI
{
    public class UIWindow : UIPanel
    {
        public static UIWindow instance;

        /* Panel width. Non-English translations (Chinese, for example) have longer
         * road names, so the panel is a bit wider in those languages. Width only -
         * the height calculations in SwitchWindow are untouched. */
        public const int WIDTH_ENGLISH = 204;
        private const int WIDTH_NON_ENGLISH = 244;

        public static int PanelWidth
        {
            get
            {
                try
                {
                    return string.Equals(Loc.CurrentLanguageCode, "en", System.StringComparison.OrdinalIgnoreCase)
                        ? WIDTH_ENGLISH
                        : WIDTH_NON_ENGLISH;
                }
                catch
                {
                    return WIDTH_ENGLISH;
                }
            }
        }

        private static readonly SavedBool SavedKeepOpen = new SavedBool("savedKeepOpen", RoundAboutBuilder.settingsFileName, false, true);
        public static readonly SavedBool SavedSetupTmpe = new SavedBool("savedSetupTMPE", RoundAboutBuilder.settingsFileName, true, true);
        public static readonly SavedBool SavedFollowTerrain = new SavedBool("savedFollowTerrain", RoundAboutBuilder.settingsFileName, false, true);

        public ToolBaseExtended toolOnUI;
        public AbstractPanel m_panelOnUI;
        private AbstractPanel m_lastStandardPanel;

        public RoundAboutPanel P_RoundAboutPanel;
        public EllipsePanel_1 P_EllipsePanel_1;
        public EllipsePanel_2 P_EllipsePanel_2;
        public EllipsePanel_3 P_EllipsePanel_3;
        public TmpeSetupPanel P_TmpeSetupPanel;
        public FreeToolPanel P_FreeToolPanel;
        public HelixPanel P_HelixPanel;

        public HoveringLabel m_hoveringLabel;

        private UIPanel m_topSection;
        private UIPanel m_bottomSection;
        private UIPanel m_setupTmpeSection;
        private UIPanel m_followTerrainSection;
        private UIButton backButton;
        private UIButton closeButton;
        public UIButton undoButton;

        public UINetInfoDropDown dropDown;

        public UIWindow()
        {
            instance = this;
        }

        public override void Start()
        {
            CreateOptionPanel();
        }

        protected override void OnMouseDown(UIMouseEventParameter p)
        {
            base.OnMouseDown(p);
            if (toolOnUI != null && !m_panelOnUI.IsSpecialWindow)
                toolOnUI.enabled = true; // Reenable the tool when user clicks in the window
        }

        private void CreateOptionPanel()
        {
            Debug.Log("[RoundaboutBuilder] UI window build: lang=" + Loc.CurrentLanguageCode + ", width=" + PanelWidth);
            name = "RAB_ToolOptionsPanel";
            atlas = ResourceLoader.GetAtlas("Ingame");
            backgroundSprite = "SubcategoriesPanel";
            size = new Vector2(PanelWidth, 180);
            absolutePosition = new Vector3(RoundAboutBuilder.savedWindowX.value, RoundAboutBuilder.savedWindowY.value);

            isVisible = false;

            //DebugUtils.Log("absolutePosition: " + absolutePosition);

            eventPositionChanged += (c, p) =>
            {
                if (absolutePosition.x < 0)
                    absolutePosition = RoundAboutBuilder.defWindowPosition;

                Vector2 resolution = GetUIView().GetScreenResolution();

                absolutePosition = new Vector2(
                    Mathf.Clamp(absolutePosition.x, 0, resolution.x - width),
                    Mathf.Clamp(absolutePosition.y, 0, resolution.y - height));

                RoundAboutBuilder.savedWindowX.value = (int)absolutePosition.x;
                RoundAboutBuilder.savedWindowY.value = (int)absolutePosition.y;
            };

            UIDragHandle dragHandle = AddUIComponent<UIDragHandle>();
            dragHandle.width = width;
            dragHandle.relativePosition = Vector3.zero;
            dragHandle.target = parent;

            P_TmpeSetupPanel = AddUIComponent<TmpeSetupPanel>();
            InitPanels();

            // From Elektrix's Road Tools
            UIButton openDescription = AddUIComponent<UIButton>();
            openDescription.relativePosition = new Vector3(width - 24f, 8f);
            openDescription.size = new Vector3(15f, 15f);
            openDescription.normalFgSprite = "ToolbarIconHelp";
            openDescription.name = "RAB_workshopButton";
            openDescription.tooltip = Loc.L("RAB_TIP_WORKSHOP", "Roundabout Builder [{0}] by Strad\nOpen in Steam Workshop", RoundAboutBuilder.VERSION);
            UIUtil.SetupButtonStateSprites(ref openDescription, "OptionBase", true);
            if (!PlatformService.IsOverlayEnabled())
            {
                openDescription.isVisible = false;
                openDescription.isEnabled = false;
            }
            openDescription.eventClicked += delegate (UIComponent component, UIMouseEventParameter click)
            {
                if (PlatformService.IsOverlayEnabled() && RoundAboutBuilder.WORKSHOP_FILE_ID != null)
                {
                    PlatformService.ActivateGameOverlayToWorkshopItem(RoundAboutBuilder.WORKSHOP_FILE_ID);
                }
                openDescription.Unfocus();
            };
            // -- Elektrix

            float cummulativeHeight = 8;

            /* Top section */
            m_topSection = AddUIComponent<UIPanel>();
            m_topSection.relativePosition = new Vector2(0, 0);
            m_topSection.SendToBack();

            UILabel label = m_topSection.AddUIComponent<UILabel>();
            label.textScale = 0.9f;
            label.text = Loc.L("RAB_WIN_TITLE", "Roundabout Builder");
            label.relativePosition = new Vector2(8, cummulativeHeight);
            label.SendToBack();
            cummulativeHeight += label.height + 8;

            m_topSection.height = cummulativeHeight;
            m_topSection.width = width;
            dragHandle.height = cummulativeHeight;

            dropDown = AddUIComponent<UINetInfoDropDown>();
            //dropDown.relativePosition = new Vector2(8, cummulativeHeight);
            dropDown.width = width - 16;
            //cummulativeHeight += dropDown.height + 8;


            /* Bottom section */

            m_followTerrainSection = AddUIComponent<UIPanel>();
            m_followTerrainSection.width = PanelWidth;
            m_followTerrainSection.clipChildren = true;
            var chbFollowTerrain = UIUtil.CreateCheckBox(m_followTerrainSection);
            chbFollowTerrain.name = "RAB_followTerrain";
            chbFollowTerrain.label.text = Loc.L("RAB_CHK_LEVEL_TERRAIN", "Level terrain");
            chbFollowTerrain.tooltip = Loc.L("RAB_TIP_LEVEL_TERRAIN", "Make the roundabout the same height everywhere");
            chbFollowTerrain.isChecked = !SavedFollowTerrain;
            chbFollowTerrain.relativePosition = new Vector3(8, 0);
            chbFollowTerrain.eventCheckChanged += (c, state) =>
            {
                // Let's make it confusing - the variable and the chackbox will have opposite values
                SavedFollowTerrain.value = !state;
            };
            m_followTerrainSection.height = chbFollowTerrain.height + 8;

            /* Height calculations below are deliberately unchanged. */

            m_setupTmpeSection = AddUIComponent<UIPanel>();
            m_setupTmpeSection.width = PanelWidth;
            m_setupTmpeSection.clipChildren = true;
            var setupTmpe = UIUtil.CreateCheckBox(m_setupTmpeSection);
            setupTmpe.name = "RAB_setupTmpe";
            setupTmpe.label.text = Loc.L("RAB_CHK_SETUP_TMPE", "Set up TMPE");
            setupTmpe.tooltip = Loc.L("RAB_TIP_SETUP_TMPE", "Apply TMPE policies to the roundabout");
            setupTmpe.isChecked = SavedSetupTmpe;
            setupTmpe.relativePosition = new Vector3(8, 0);
            setupTmpe.eventCheckChanged += (c, state) =>
            {
                SavedSetupTmpe.value = state;
            };
            var tmpeButton = UIUtil.CreateButton(m_setupTmpeSection);
            tmpeButton.text = "...";
            tmpeButton.tooltip = Loc.L("RAB_TIP_TMPE_SETTINGS", "TMPE settings");
            tmpeButton.height = setupTmpe.height;
            tmpeButton.width = 30;
            tmpeButton.relativePosition = new Vector2(width - tmpeButton.width - 8, 0);
            tmpeButton.eventClick += (c, p) =>
            {
                toolOnUI.enabled = false;
                SwitchWindow(P_TmpeSetupPanel);
            };
            m_setupTmpeSection.height = setupTmpe.height + 8;
            //cummulativeHeight += keepOpen.height + 8;

            cummulativeHeight = 0;
            m_bottomSection = AddUIComponent<UIPanel>();

            var keepOpen = UIUtil.CreateCheckBox(m_bottomSection);
            keepOpen.name = "RAB_keepOpen";
            keepOpen.label.text = Loc.L("RAB_CHK_KEEP_OPEN", "Keep open");
            keepOpen.tooltip = Loc.L("RAB_TIP_KEEP_OPEN", "Keep the window open after roundabout is built");
            keepOpen.isChecked = SavedKeepOpen.value;
            keepOpen.relativePosition = new Vector3(8, cummulativeHeight);
            keepOpen.width = PanelWidth - 8; // width - padding
            keepOpen.eventCheckChanged += (c, state) =>
            {
                SavedKeepOpen.value = state;
            };
            cummulativeHeight += keepOpen.height + 8;

            // Back button
            backButton = UIUtil.CreateButton(m_bottomSection);
            backButton.text = Loc.L("RAB_BTN_BACK", "Back");
            backButton.relativePosition = new Vector2(8, cummulativeHeight);
            backButton.width = width - 16;
            backButton.eventClick += (c, p) =>
            {
                if (backButton.isVisible)
                {
                    if (m_panelOnUI.IsSpecialWindow)
                    {
                        SwitchWindow(m_lastStandardPanel);
                        toolOnUI.enabled = true;
                    }
                    else
                    {
                        toolOnUI.GoToFirstStage();
                        SwitchTool(RoundaboutTool.Instance);
                    }
                }
            };

            cummulativeHeight += backButton.height + 8;

            closeButton = UIUtil.CreateButton(m_bottomSection);
            closeButton.text = Loc.L("RAB_BTN_CLOSE", "Close");
            closeButton.relativePosition = new Vector2(8, cummulativeHeight);
            closeButton.eventClick += (c, p) =>
            {
                enabled = false;
                if (toolOnUI != null)
                {
                    toolOnUI.enabled = false;
                }
            };

            undoButton = UIUtil.CreateButton(m_bottomSection);
            undoButton.text = Loc.L("RAB_BTN_UNDO", "Undo");
            undoButton.tooltip = Loc.L("RAB_TIP_UNDO", "Remove last built roundabout (CTRL+Z). Warning: Use only right after the roundabout has been built");
            undoButton.relativePosition = new Vector2(16 + closeButton.width, cummulativeHeight);
            undoButton.isEnabled = false;
            undoButton.eventClick += (c, p) =>
            {
                ModThreading.UndoAction();
            };
            
            cummulativeHeight += closeButton.height + 8;

            m_bottomSection.height = cummulativeHeight;
            m_bottomSection.width = width;

            m_hoveringLabel = AddUIComponent<HoveringLabel>();
            m_hoveringLabel.isVisible = false;

            /* Enable roundabout tool as default */
            SwitchTool(RoundaboutTool.Instance);

            // Is done by modthreading from 1.5.3 on
            /*try
            {
                if(RoundAboutBuilder.ShowUIButton)
                   UIPanelButton.CreateButton();
            }
            catch(Exception e)
            {
                Debug.LogWarning("Failed to create UI button.");
                Debug.LogWarning(e);
            }*/
            

            enabled = false;
        }

        internal void GoBack()
        {
            if (toolOnUI != null)
            {
                toolOnUI.GoToFirstStage();
                toolOnUI.enabled = false;
            }      
            SwitchTool( RoundaboutTool.Instance );
        }

        public void SwitchTool(ToolBaseExtended tool)
        {
            if (tool == toolOnUI) return;

            toolOnUI = tool;

            if (tool is EllipseTool)
            {
                SwitchWindow( P_EllipsePanel_3 != null ? (AbstractPanel)P_EllipsePanel_3 : P_RoundAboutPanel );
            }
            else if(tool is HelixTool)
            {
                SwitchWindow(P_HelixPanel != null ? (AbstractPanel)P_HelixPanel : P_RoundAboutPanel);
            }
            else if(tool is RoundaboutTool)
            {
                SwitchWindow( P_RoundAboutPanel );
            }else
            {
                SwitchWindow( P_FreeToolPanel );
            }

            tool.enabled = true;
        }

        public void InitPanels()
        {
            Debug.Log("[RoundaboutBuilder] InitPanels: spiral=" + RoundAboutBuilder.EnableSpiralRamp.value
                + ", legacyEllipse=" + RoundAboutBuilder.LegacyEllipticRoundabouts.value
                + ", width=" + PanelWidth);
            //bool enabledS = this.enabled;
            //enabled = false;
            if(P_RoundAboutPanel != null)
            {
                RemoveUIComponent(P_RoundAboutPanel);
                RemoveUIComponent(P_FreeToolPanel);
                if (P_HelixPanel != null) RemoveUIComponent(P_HelixPanel);
            }
            if(P_EllipsePanel_1 != null)
            {
                RemoveUIComponent(P_EllipsePanel_1);
                RemoveUIComponent(P_EllipsePanel_2);
                RemoveUIComponent(P_EllipsePanel_3);
            }
            P_RoundAboutPanel = AddUIComponent<RoundAboutPanel>();
            Debug.Log("[RoundaboutBuilder] InitPanels: RoundAboutPanel built.");
            P_FreeToolPanel = AddUIComponent<FreeToolPanel>();
            Debug.Log("[RoundaboutBuilder] InitPanels: FreeToolPanel built.");
            if(RoundAboutBuilder.EnableSpiralRamp.value)
            {
                P_HelixPanel = AddUIComponent<HelixPanel>();
                Debug.Log("[RoundaboutBuilder] InitPanels: HelixPanel built.");
            }
            if(RoundAboutBuilder.LegacyEllipticRoundabouts.value)
            {
                P_EllipsePanel_1 = AddUIComponent<EllipsePanel_1>();
                P_EllipsePanel_2 = AddUIComponent<EllipsePanel_2>();
                P_EllipsePanel_3 = AddUIComponent<EllipsePanel_3>();
            }
            Debug.Log("[RoundaboutBuilder] InitPanels: done.");
           // this.enabled = enabledS;
        }

        public void SwitchWindow(AbstractPanel panel)
        {
            if (m_panelOnUI != null)
            {
                m_panelOnUI.isVisible = false;
                if (!m_panelOnUI.IsSpecialWindow) m_lastStandardPanel = m_panelOnUI;
            }           

            panel.isVisible = true;
            m_panelOnUI = panel;

            float cumulativeHeight = m_topSection.height;

            if(panel.ShowDropDown)
            {
                dropDown.relativePosition = new Vector2(8, cumulativeHeight);
                cumulativeHeight += dropDown.height + 8;
                dropDown.isVisible = true;
            }
            else
            {
                dropDown.isVisible = false;
            }
            dropDown.Populate(true);

            panel.relativePosition = new Vector2(0, cumulativeHeight);
            cumulativeHeight += panel.height;

            if (panel.ShowFollowTerrain)
            {
                m_followTerrainSection.relativePosition = new Vector2(0, cumulativeHeight);
                cumulativeHeight += m_followTerrainSection.height;
                m_followTerrainSection.isVisible = true;
            }
            else
            {
                m_followTerrainSection.isVisible = false;
            }

            if (ModLoadingExtension.tmpeDetected && panel.ShowTmpeSetup)
            {
                m_setupTmpeSection.relativePosition = new Vector2(0, cumulativeHeight);
                cumulativeHeight += m_setupTmpeSection.height;
                m_setupTmpeSection.isVisible = true;
            }
            else
            {
                m_setupTmpeSection.isVisible = false;
            }

            // THIS IS HELL ON EARTH
            // Adjust position of other buttons and height of the panel if the back button is visible
            if(panel.ShowBackButton)
            {
                if(!backButton.enabled)
                {
                    m_bottomSection.height += backButton.height + 8;
                    closeButton.relativePosition = new Vector2(closeButton.relativePosition.x, closeButton.relativePosition.y + backButton.height + 8);
                    undoButton.relativePosition = new Vector2(undoButton.relativePosition.x, undoButton.relativePosition.y + backButton.height + 8);
                }
                backButton.enabled = true;
            }
            else
            {
                if (backButton.enabled)
                {
                    m_bottomSection.height -= backButton.height + 8;
                    closeButton.relativePosition = new Vector2(closeButton.relativePosition.x, closeButton.relativePosition.y - backButton.height - 8);
                    undoButton.relativePosition = new Vector2(undoButton.relativePosition.x, undoButton.relativePosition.y - backButton.height - 8);
                }
                backButton.enabled = false;
            }

            m_bottomSection.relativePosition = new Vector2(0, cumulativeHeight);
            cumulativeHeight += m_bottomSection.height;

            height = cumulativeHeight;
        }

        public void LostFocus()
        {
            if (!SavedKeepOpen)
            {
                enabled = false;
            }
        }

        // Toggles the tool (when clicking the mod button or pressing the shortcut)
        public void Toggle()
        {
            if (instance.toolOnUI != null && (instance.toolOnUI.enabled || (instance.enabled && m_panelOnUI.IsSpecialWindow)))
            {
                instance.enabled = false;
            }
            else if (instance.toolOnUI != null && instance.enabled)
            {
                instance.toolOnUI.enabled = true;
            }
            else
            {
                instance.enabled = true;
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (m_panelOnUI != null && m_lastStandardPanel != null && m_panelOnUI.IsSpecialWindow)
            {
                SwitchWindow(m_lastStandardPanel);
            }
            isVisible = true;
            if (toolOnUI != null)
                toolOnUI.enabled = true;
            UUIIntegration.SyncButtonState(true);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            isVisible = false;
            if (toolOnUI != null)
                toolOnUI.enabled = false;
            UUIIntegration.SyncButtonState(false);
        }

        /* Recreates the tool window so it picks up a newly selected language. */
        public static void Rebuild()
        {
            try
            {
                UIWindow oldWindow = instance;
                if (oldWindow == null) return;

                UnityEngine.Object.Destroy(oldWindow.gameObject);
                UIView.GetAView().AddUIComponent(typeof(UIWindow));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoundaboutBuilder] Could not rebuild the tool window.");
                Debug.LogWarning(e);
            }
        }

        public void ThrowErrorMsg(string content, bool error = false)
        {
            ExceptionPanel panel = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
            panel.SetMessage(Loc.L("RAB_ERR_TITLE", "Roundabout builder"), content, error);
        }
    }
}
