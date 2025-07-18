namespace Sven.Command
{
    public abstract class ActionCommand<T> : BaseCommand<CommandSettings>
    {
        private T _parameter;
        public T Parameter
        {
            get => _parameter;
            set
            {
                if (_parameter.Equals(value)) return;
                _parameter = value;
            }
        }
    }
}