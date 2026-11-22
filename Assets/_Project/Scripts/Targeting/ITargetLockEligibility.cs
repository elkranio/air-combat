namespace AirCombat
{
    // Implemented by a target owner, not by a weapon or a particular scene.
    public interface ITargetLockEligibility
    {
        bool CanBeLocked { get; }
    }
}
