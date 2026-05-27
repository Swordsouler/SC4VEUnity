using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Pile d'annulation/rétablissement partagée par toutes les commandes.
    /// Chaque commande destructive enregistre ses actions via <see cref="Push"/>.
    /// </summary>
    public static class CommandHistory
    {
        private static readonly Stack<(Action Undo, Action Redo)> _undoStack = new();
        private static readonly Stack<(Action Undo, Action Redo)> _redoStack = new();

        public static bool CanUndo => _undoStack.Count > 0;
        public static bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Enregistre une paire undo/redo dans la pile.
        /// Vider la pile redo à chaque nouvelle action.
        /// </summary>
        public static void Push(Action undoAction, Action redoAction)
        {
            _undoStack.Push((undoAction, redoAction));
            _redoStack.Clear();
        }

        public static void Undo()
        {
            if (!CanUndo) { Debug.Log("[History] Rien à annuler."); return; }
            var (undo, redo) = _undoStack.Pop();
            undo?.Invoke();
            _redoStack.Push((undo, redo));
            Debug.Log("[History] Annulation effectuée.");
        }

        public static void Redo()
        {
            if (!CanRedo) { Debug.Log("[History] Rien à rétablir."); return; }
            var (undo, redo) = _redoStack.Pop();
            redo?.Invoke();
            _undoStack.Push((undo, redo));
            Debug.Log("[History] Rétablissement effectué.");
        }

        public static void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
