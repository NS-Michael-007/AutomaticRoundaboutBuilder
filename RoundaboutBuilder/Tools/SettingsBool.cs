using ColossalFramework;
using ColossalFramework.UI;
using ICities;

namespace RoundaboutBuilder.Tools
{
    /* A SavedBool that keeps translation keys instead of final text, so the
     * settings page follows the language chosen at runtime. */
    public class SettingsBool : SavedBool
    {
        private readonly string m_descriptionKey;
        private readonly string m_descriptionFallback;
        private readonly string m_tooltipKey;
        private readonly string m_tooltipFallback;

        public SettingsBool(string descriptionKey, string descriptionFallback, string tooltipKey, string tooltipFallback, string name, bool defaultValue)
            : base(name, RoundAboutBuilder.settingsFileName, defaultValue, true)
        {
            m_descriptionKey = descriptionKey;
            m_descriptionFallback = descriptionFallback;
            m_tooltipKey = tooltipKey;
            m_tooltipFallback = tooltipFallback;
        }

        public string Description
        {
            get { return Loc.L(m_descriptionKey, m_descriptionFallback); }
        }

        public string Tooltip
        {
            get { return Loc.L(m_tooltipKey, m_tooltipFallback); }
        }

        public UICheckBox Draw(UIHelper group, OnCheckChanged callback = null)
        {
            UICheckBox chb;
            chb = (UICheckBox)group.AddCheckbox(Description, this.value, (b) =>
            {
                this.value = b;
                if (callback != null) callback.Invoke(b);
            });
            chb.tooltip = Tooltip;
            return chb;
        }
    }
}
