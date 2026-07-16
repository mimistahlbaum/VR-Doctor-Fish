namespace DoctorFish
{
    /// <summary>
    /// The staged sequence of the VR Doctor Fish experience, in order.
    /// Visuals, audio and haptics are all driven from the current stage.
    /// </summary>
    public enum ExperienceStage
    {
        Welcome,
        SmallFish,
        BigFish,
        Jellyfish,
        Calm,
        Finished
    }

    /// <summary>
    /// Which leg a one-shot haptic pattern should play on. Patterns are
    /// authored for the left leg (even addresses); the right leg mirrors
    /// every address at +1.
    /// </summary>
    public enum HapticLeg
    {
        Left,
        Right,
        Both
    }
}
