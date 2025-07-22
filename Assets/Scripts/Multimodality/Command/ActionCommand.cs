namespace Sven.Command
{
    public abstract class ActionCommand<TSettings, T> : BaseCommand<TSettings> where TSettings : BaseCommandSettings
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
}