namespace AirCombat
{
    // Read-only combat HUD contract. Input and AI continue using ITriggeredWeapon.
    // A weapon exposes its own true ready state; the HUD never estimates cooldowns.
    public interface IWeaponHudStatus
    {
        string HudName { get; }
        float Readiness01(float now);
    }
}
