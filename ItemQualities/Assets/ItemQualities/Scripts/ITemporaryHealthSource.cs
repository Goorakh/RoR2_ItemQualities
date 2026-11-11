using RoR2;

namespace ItemQualities
{
    public interface ITemporaryHealthSource
    {
        /// <summary>
        /// Gets the amount of temporary health a body has from this source
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        float GetTemporaryHealthBonus(CharacterBody body);

        /// <summary>
        /// Attempts to take am amount of temporary health from a body.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="toRemove"></param>
        /// <returns>How much temporary health of this source was removed from the body.</returns>
        float TryTakeTemporaryHealthBonus(CharacterBody body, float toRemove);
    }
}
