using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TD.PushBox;

namespace JobTest.TacticsVoxel
{
    [InitializeOnLoad]
    public static class VoxelPlayUndoGuard
    {
        private static readonly FieldInfo GlobalHandler = typeof(EditorApplication).GetField("globalEventHandler", BindingFlags.Static | BindingFlags.NonPublic);
        private static Delegate handler;

        static VoxelPlayUndoGuard()
        {
            Install();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Uninstall;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change) => Install();

        private static void Install()
        {
            // Unity filters normal Shortcut contexts while Game View is playing. Intercept
            // the event before its menu shortcut handler, without rebinding user shortcuts.
            if (GlobalHandler == null)
            {
                Debug.LogError("无法隔离游戏悔棋快捷键，请暂时只使用游戏内悔棋按钮。");
                return;
            }
            handler ??= Delegate.CreateDelegate(GlobalHandler.FieldType, typeof(VoxelPlayUndoGuard).GetMethod(nameof(HandleGlobalEvent), BindingFlags.Static | BindingFlags.NonPublic));
            var existing = (Delegate)GlobalHandler.GetValue(null);
            GlobalHandler.SetValue(null, Delegate.Combine(handler, Delegate.Remove(existing, handler)));
        }

        private static void Uninstall()
        {
            if (GlobalHandler != null && handler != null)
                GlobalHandler.SetValue(null, Delegate.Remove((Delegate)GlobalHandler.GetValue(null), handler));
        }

        private static void HandleGlobalEvent()
        {
            ConsumeUndoEvent(Event.current, EditorApplication.isPlaying, () =>
            {
                var puzzle=UnityEngine.Object.FindFirstObjectByType<PushBoxController>();
                puzzle?.UndoCommand();
            });
        }

        public static bool ConsumeUndoEvent(Event evt, bool playMode, Action undo)
        {
            if (!playMode || evt == null) return false;
            bool key = (evt.type == EventType.KeyDown || evt.type == EventType.KeyUp) && !evt.alt
                && (evt.control || evt.command) && (evt.keyCode == KeyCode.Z || evt.keyCode == KeyCode.Y);
            bool command = (evt.type == EventType.ValidateCommand || evt.type == EventType.ExecuteCommand)
                && (evt.commandName == "Undo" || evt.commandName == "Redo");
            if (!key && !command) return false;
            bool executeUndo = key && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Z && !evt.shift
                || command && evt.type == EventType.ExecuteCommand && evt.commandName == "Undo";
            // Consume even when busy or out of battle history. Never fall through to editor undo.
            evt.Use();
            if (executeUndo) undo?.Invoke();
            return true;
        }
    }
}
