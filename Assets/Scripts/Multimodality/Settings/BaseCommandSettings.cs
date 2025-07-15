using System;

namespace Sven.Command
{
    [Serializable]
    public abstract class BaseCommandSettings
    {
        public abstract void OnGUI(MultimodalitySettingsWindow window);
    }
}