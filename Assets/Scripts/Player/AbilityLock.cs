using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Shared mutual-exclusion gate for abilities that must never run at the
    /// same time (Heat Vision / Freeze Breath). Tracks a single "owner" —
    /// the requester currently allowed to act, or null if unclaimed.
    /// Whichever requester's key is held first claims ownership and keeps it
    /// as long as it keeps calling CanActivate(self, true); the other
    /// requester is simply refused (not queued, not overridden) every frame
    /// until the owner reports isHeld == false (its key was released),
    /// which clears ownership so either side can claim it fresh.
    /// </summary>
    public class AbilityLock : MonoBehaviour
    {
        private object owner;

        /// <summary>
        /// Call once per requester per frame with that requester's raw held
        /// state. Returns the gated held state the requester should
        /// actually act on.
        /// </summary>
        public bool CanActivate(object requester, bool isHeld)
        {
            if (!isHeld)
            {
                if (owner == requester)
                {
                    owner = null;
                }
                return false;
            }

            if (owner == null)
            {
                owner = requester;
            }

            return owner == requester;
        }
    }
}
