using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Only this adapter knows the mouse. Later a stick can supply the same screen point.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class TargetingCursor : MonoBehaviour
    {
        private Camera view;
        private bool ownsCursor, previousVisibility;
        public Vector2 Position { get; private set; }
        public bool HasPointer { get; private set; }

        public void Bind(Camera camera) { view = camera; }

        private void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse != null) Position = mouse.position.ReadValue();
            HasPointer = view != null && mouse != null && Application.isFocused && view.pixelRect.Contains(Position);
            if (HasPointer)
            {
                if (!ownsCursor) { previousVisibility = Cursor.visible; ownsCursor = true; }
                Cursor.visible = false;
            }
            else RestoreCursor();
        }

        private void RestoreCursor()
        {
            HasPointer = false;
            if (!ownsCursor) return;
            Cursor.visible = previousVisibility;
            ownsCursor = false;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) RestoreCursor(); }
        private void OnDisable() { RestoreCursor(); }
        private void OnDestroy() { RestoreCursor(); }

        private void OnGUI()
        {
            if (!HasPointer || !Application.isFocused || Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(view.pixelHeight / 720f, .75f, 3f);
            Vector2 p = new Vector2(Position.x, Screen.height - Position.y);
            Color old = GUI.color;
            int depth = GUI.depth;
            GUI.depth = -100;
            // Compact interrupted reticle; no GUI input or mouse warping.
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = i == 0 ? Vector2.right : i == 1 ? Vector2.left : i == 2 ? Vector2.up : Vector2.down;
                Vector2 c = p + d * 8f * s;
                Vector2 size = i < 2 ? new Vector2(5f, 1.5f) * s : new Vector2(1.5f, 5f) * s;
                Paint(new Rect(c - (size + Vector2.one * 2f * s) * .5f, size + Vector2.one * 2f * s), new Color(.06f, .08f, .09f, .85f));
                Paint(new Rect(c - size * .5f, size), new Color(.9f, .97f, 1f, .92f));
            }
            Paint(new Rect(p - Vector2.one * s, Vector2.one * 2f * s), Color.white);
            GUI.color = old;
            GUI.depth = depth;
        }

        private static void Paint(Rect rect, Color tint)
        {
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
