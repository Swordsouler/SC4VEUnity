using Newtonsoft.Json;
using Sven.Content;
using System;

namespace Sven.Command
{
    [Serializable]
    public class PositionParameter : IBaseParameter
    {
        private RelativeDirection _direction = RelativeDirection.Forward;
        public RelativeDirection Direction
        {
            get => _direction;
            set => _direction = value;
        }

        private SemantizationCore _semantizationCore;
        [JsonIgnore]
        public SemantizationCore SemantizationCore
        {
            get => _semantizationCore;
            set => _semantizationCore = value;
        }
    }
}