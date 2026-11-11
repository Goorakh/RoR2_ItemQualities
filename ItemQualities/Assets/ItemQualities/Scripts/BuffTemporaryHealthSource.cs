using RoR2;
using UnityEngine;

namespace ItemQualities
{
    /// <summary>
    /// Simple implementation of temporary health using a buff
    /// </summary>
    public readonly struct BuffTemporaryHealthSource : ITemporaryHealthSource
    {
        public readonly BuffDef buff;

        public BuffTemporaryHealthSource(BuffDef buff)
        {
            this.buff = buff;
        }

        public readonly float GetTemporaryHealthBonus(CharacterBody body)
        {
            return body.GetBuffCount(buff);
        }

        public readonly float TryTakeTemporaryHealthBonus(CharacterBody body, float toRemove)
        {
            int tempHealthCount = body.GetBuffCount(buff);
            int tempHealthTaken = Mathf.FloorToInt(Mathf.Min(toRemove, tempHealthCount));

            if (tempHealthTaken > 0)
            {
                int remainingTemporaryHealth = tempHealthCount - tempHealthTaken;
                body.SetBuffCount(buff.buffIndex, remainingTemporaryHealth);
            }

            return tempHealthTaken;
        }
    }
}
