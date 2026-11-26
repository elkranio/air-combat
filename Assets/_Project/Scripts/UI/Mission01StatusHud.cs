using UnityEngine;
using Phase = AirCombat.Mission01LaunchSequence.Phase;

namespace AirCombat
{
    // Screen-only presentation. The flight helpers still own warning state.
    [DisallowMultipleComponent]
    public sealed class Mission01StatusHud : MonoBehaviour
    {
        private Mission01LaunchSequence mission;
        private GUIStyle boundaryLabel, landingLabel;

        internal static void Attach(Mission01LaunchSequence owner)
        {
            var view = owner.GetComponent<Mission01StatusHud>();
            if (view == null) view = owner.gameObject.AddComponent<Mission01StatusHud>();
            view.mission = owner;
        }

        private void OnGUI()
        {
            if (mission == null || !mission.isActiveAndEnabled || Event.current.type != EventType.Repaint ||
                MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            var State = mission.State;
            if (State == Phase.Flight)
            {
                DrawBoundary(mission.Boundary);
                DrawApproach(mission.Landing);
                return;
            }
            string message = State == Phase.Intro ? "MISSION INTRO" :
                State == Phase.Ready ? "PRESS W TO TAKE OFF" :
                State == Phase.Crashed ? "AIRCRAFT LOST  /  PRESS R TO RETRY" :
                State == Phase.BoundaryFailed ? "MISSION FAILED — OPERATION AREA ABANDONED  /  PRESS R TO RETRY" :
                State == Phase.Landing ? "AUTOPILOT / LANDING" :
                State == Phase.Landed ? (mission.LandingTestOnly ? "LANDING TEST COMPLETE  /  PRESS R TO RETRY" :
                    "MISSION COMPLETE  /  PRESS R TO RETRY") : "TAKEOFF";
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .025f, 16f, 32f)),
                fontStyle = FontStyle.Bold
            };
            var rect = new Rect(0f, Screen.height * .84f, Screen.width, 48f);
            Color previous = GUI.color;
            GUI.color = Color.white;
            style.normal.textColor = new Color(.05f, .07f, .09f, .9f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), message, style);
            style.normal.textColor = State == Phase.BoundaryFailed ? new Color(1f, .40f, .32f) : Color.white;
            GUI.Label(rect, message, style);
            GUI.color = previous;
        }

        private void DrawBoundary(MissionBoundaryGuard boundary)
        {
            if (boundary == null || boundary.WarningLevel == 0) return;
            int warningLevel = boundary.WarningLevel;
            float distance = boundary.WarningDistance;
            if (boundaryLabel == null) boundaryLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            boundaryLabel.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .022f, 14f, 26f));
            float width = Mathf.Min(Screen.width * .62f, 660f);
            var rect = new Rect((Screen.width - width) * .5f, Screen.height * .085f, width,
                Mathf.Max(36f, boundaryLabel.fontSize * 1.8f));
            Color previous = GUI.color;
            GUI.color = new Color(.035f, .045f, .055f, .82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            Color ink = warningLevel == 2 ? new Color(1f, .32f, .25f) : new Color(1f, .79f, .35f);
            if (warningLevel == 2) ink.a = Mathf.Lerp(.68f, 1f, .5f + .5f * Mathf.Sin(Time.unscaledTime * 9f));
            GUI.color = ink;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            boundaryLabel.normal.textColor = ink;
            string message = warningLevel == 2 ? "RETURN TO COMBAT AREA" : "APPROACHING OPERATION BOUNDARY";
            GUI.Label(rect, $"{message}  /  {Mathf.CeilToInt(distance)} m", boundaryLabel);
            GUI.color = previous;
        }

        private void DrawApproach(ScriptedLanding landing)
        {
            if (landing == null || !landing.ApproachVisible) return;
            bool aligned = landing.ApproachAligned;
            float held = landing.ApproachHeld;
            float holdSeconds = landing.ApproachHoldSeconds;
            if (landingLabel == null) landingLabel = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            landingLabel.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .022f, 14f, 25f));
            float width = Mathf.Min(Screen.width * .62f, 620f);
            var rect = new Rect((Screen.width - width) * .5f, Screen.height * .76f, width, 38f);
            Color previous = GUI.color;
            GUI.color = new Color(.035f, .06f, .07f, .82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            landingLabel.normal.textColor = aligned ? new Color(.5f, 1f, .82f) : new Color(.65f, .94f, 1f);
            GUI.Label(rect, aligned ? "LANDING / MAINTAIN APPROACH" : "LANDING / ENTER APPROACH CORRIDOR", landingLabel);
            if (held > 0f)
            {
                GUI.color = landingLabel.normal.textColor;
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 3f, rect.width * held / holdSeconds, 3f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }
    }
}
