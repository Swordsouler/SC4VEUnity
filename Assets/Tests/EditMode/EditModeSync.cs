using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sc4ve.Tests.EditMode
{
    /// <summary>
    /// Attente bloquante d'une tâche asynchrone en EditMode sans interblocage : le
    /// SynchronizationContext Unity est neutralisé le temps de l'appel, si bien que les
    /// continuations post-await s'exécutent sur le pool de threads au lieu d'être postées
    /// vers le thread principal (qui est bloqué par l'attente). La partie AVANT le premier
    /// await s'exécute toujours sur le thread appelant (accès aux API Unity possibles).
    /// </summary>
    internal static class EditModeSync
    {
        public static void RunSync(Func<Task> action)
        {
            SynchronizationContext previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                action().GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        public static T RunSync<T>(Func<Task<T>> action)
        {
            SynchronizationContext previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                return action().GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }
    }
}
