using Sven.Content;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Command
{
    public class MoveCommand : ActionCommand<Vector3>, IBaseCommand<object>
    {
        public object Execute(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            throw new System.NotImplementedException();
        }
    }
}