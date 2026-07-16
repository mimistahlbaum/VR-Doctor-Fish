using System.Collections.Generic;
using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// Maps vibration unit addresses to their anchors on the virtual legs
    /// (registered by DoctorFishBootstrap from HapticNodeLayout). Lets
    /// contact events resolve a visual touch position to the nearest
    /// physical actuator, so the felt position matches the seen one.
    /// </summary>
    public class HapticNodeMap : MonoBehaviour
    {
        readonly Dictionary<int, Transform> anchors =
            new Dictionary<int, Transform>();

        public void Register(int addr, Transform anchor)
        {
            anchors[addr] = anchor;
        }

        public Transform AnchorFor(int addr)
        {
            return anchors.TryGetValue(addr, out var anchor) ? anchor : null;
        }

        /// <summary>
        /// Address of the small unit on the given leg closest to a world
        /// position, or -1 if none is registered. Left leg units are even,
        /// right leg units are odd; the big actuators (32/33) are reserved
        /// for bites and stings.
        /// </summary>
        public int NearestSmallUnit(Vector3 worldPosition, HapticLeg leg)
        {
            var parity = leg == HapticLeg.Right ? 1 : 0;
            var best = -1;
            var bestSqr = float.MaxValue;
            foreach (var pair in anchors)
            {
                if (pair.Key >= 32 || (pair.Key & 1) != parity)
                    continue;
                var sqr = (pair.Value.position - worldPosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = pair.Key;
                }
            }
            return best;
        }
    }
}
