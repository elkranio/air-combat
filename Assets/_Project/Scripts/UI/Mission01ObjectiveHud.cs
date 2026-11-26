using UnityEngine;

namespace AirCombat
{
    // Mission01's objective overlay. Reads current unit state; owns no gameplay.
    [DisallowMultipleComponent]
    public sealed class Mission01ObjectiveHud : MonoBehaviour
    {
        private Mission01ReconEncounter primary;
        private OverlordTarget secondary;

        internal static void Attach(Mission01LaunchSequence mission)
        {
            var view = mission.GetComponent<Mission01ObjectiveHud>();
            if (view == null) view = mission.gameObject.AddComponent<Mission01ObjectiveHud>();
            view.primary = mission.GetComponent<Mission01ReconEncounter>();
            view.secondary = null;
            // Mission01 has one optional aircraft. Resolve once, within this scene,
            // without changing any prefab or serialized scene references.
            foreach (var root in mission.gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<OverlordTarget>(true))
                {
                    if (view.secondary != null)
                    {
                        view.secondary = null;
                        Debug.LogWarning("Mission01 objectives HUD: multiple Overlords; optional label not bound.", mission);
                        return;
                    }
                    view.secondary = candidate;
                }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (primary != null && primary.isActiveAndEnabled && primary.InFlight)
                DrawPrimary(primary.PrimaryProgress);
            if (secondary != null && secondary.isActiveAndEnabled && secondary.FlightActive &&
                secondary.ObjectiveState != ObjectiveTargetState.Pending)
                DrawSecondary(secondary.ObjectiveState);
        }

        private static void DrawPrimary(ObjectiveProgress progress)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .022f, 15, 25)), fontStyle = FontStyle.Bold };
            string label = progress.Complete ? $"RECON COMPLETE  /  {progress.Destroyed} OF {progress.Total}" : $"RECON  /  {progress.Destroyed} OF {progress.Total}";
            style.normal.textColor = new Color(.05f, .07f, .08f, .9f); GUI.Label(new Rect(21, 21, 440, 40), label, style);
            style.normal.textColor = progress.Complete ? new Color(.58f, .94f, .76f) : Color.white; GUI.Label(new Rect(20, 20, 440, 40), label, style);
        }

        private static void DrawSecondary(ObjectiveTargetState state)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .019f, 13, 22)), fontStyle = FontStyle.Bold };
            string message = "SECONDARY / OVERLORD / " + ObjectiveStatusText.For(state);
            style.normal.textColor = new Color(.05f,.07f,.08f,.9f);
            GUI.Label(new Rect(21, 56, 620, 35), message, style);
            style.normal.textColor = state == ObjectiveTargetState.Destroyed ? new Color(.58f,.94f,.76f) : state == ObjectiveTargetState.Escaped ? new Color(1f,.52f,.4f) : new Color(.97f,.74f,.36f);
            GUI.Label(new Rect(20, 55, 620, 35), message, style);
        }
    }
}
