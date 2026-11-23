namespace AirCombat
{
    // A pilot (mouse, AI, replay) supplies the trigger state; the weapon
    // owns its rate, burst/cooldown and the physical rounds it creates.
    public interface ITriggeredWeapon
    {
        void TickTrigger(bool held, float now);
        void CeaseFire(float now);
    }
}
