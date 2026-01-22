using Microsoft.Office.Core;
using System.IO;
using System.Reflection;

namespace DarkSoulsOutlookToast
{
    public class DarkSoulsRibbon : IRibbonExtensibility
    {
        private IRibbonUI _ribbon;

        public string GetCustomUI(string ribbonID)
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = "DarkSoulsOutlookToast.DarkSoulsRibbon.xml";

            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public void OnLoad(IRibbonUI ribbonUI) => _ribbon = ribbonUI;

        public bool GetPopupsPressed(IRibbonControl control)
            => Properties.Settings.Default.PopupsEnabled;

        public void OnTogglePopups(IRibbonControl control, bool pressed)
        {
            Properties.Settings.Default.PopupsEnabled = pressed;
            Properties.Settings.Default.Save();
        }
    }
}
