using System;

namespace Sven.Command
{
    [Serializable]
    public abstract class BaseSettingsGUI
    {
        public abstract void OnGUI(S4MSettingsWindow window);
    }
}