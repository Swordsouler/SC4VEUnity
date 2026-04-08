namespace Sven.Command
{
    public abstract class Command<TSettings, T> : Command<TSettings> where TSettings : BaseSettingsGUI
    {
        private T _parameter;
        public T Parameter
        {
            get => _parameter;
            set
            {
                if (_parameter != null && _parameter.Equals(value)) return;
                _parameter = value;
            }
        }
    }
    public abstract class Command<TSettings> : BaseSettings<TSettings> where TSettings : BaseSettingsGUI { }
}