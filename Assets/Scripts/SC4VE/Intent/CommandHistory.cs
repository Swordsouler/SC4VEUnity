using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Pile d'annulation/rétablissement partagée par toutes les commandes.
    /// Chaque commande destructive enregistre ses actions via <see cref="Push"/>.
    /// Chaque entrée mémorise aussi les objets affectés, pour que l'undo/redo
    /// puisse les re-sélectionner (cf. ResolveCommands / SelectionManager).
    /// </summary>
    public static class CommandHistory
    {
        private class Entry
        {
            public Action Undo;
            public Action Redo;
            public List<SemantizationCore> Affected = new();
        }

        private static readonly Stack<Entry> _undoStack = new();
        private static readonly Stack<Entry> _redoStack = new();

        public static bool CanUndo => _undoStack.Count > 0;
        public static bool CanRedo => _redoStack.Count > 0;
        public static int UndoCount => _undoStack.Count;

        /// <summary>
        /// Enregistre une paire undo/redo dans la pile.
        /// Vide la pile redo à chaque nouvelle action.
        /// </summary>
        public static void Push(Action undoAction, Action redoAction)
        {
            _undoStack.Push(new Entry { Undo = undoAction, Redo = redoAction });
            _redoStack.Clear();
        }

        /// <summary>
        /// Mémorise les objets affectés par la dernière action poussée, afin de
        /// les re-sélectionner lors d'un undo/redo. Appelé par ResolveCommands.
        /// </summary>
        public static void SetLastAffected(List<SemantizationCore> affected)
        {
            if (_undoStack.Count > 0) _undoStack.Peek().Affected = affected ?? new();
        }

        /// <summary>Annule la dernière action et retourne les objets affectés (à re-sélectionner).</summary>
        public static List<SemantizationCore> Undo()
        {
            if (!CanUndo) { Debug.Log("[History] Rien à annuler."); return new(); }
            Entry e = _undoStack.Pop();
            e.Undo?.Invoke();
            _redoStack.Push(e);
            Debug.Log("[History] Annulation effectuée.");
            return e.Affected ?? new();
        }

        /// <summary>Rétablit la dernière action annulée et retourne les objets affectés (à re-sélectionner).</summary>
        public static List<SemantizationCore> Redo()
        {
            if (!CanRedo) { Debug.Log("[History] Rien à rétablir."); return new(); }
            Entry e = _redoStack.Pop();
            e.Redo?.Invoke();
            _undoStack.Push(e);
            Debug.Log("[History] Rétablissement effectué.");
            return e.Affected ?? new();
        }

        public static void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
