using UnityEngine;

namespace ChainboundRaid
{
    public enum RaidChainBand { Slack, Taut, Buffered, Overstretched }
    public enum RaidChainState { Slack, Taut, Overstretched, Wrapped, Snatched, Grabbed, Down }
    public enum RaidChainAnchor { None, FrontLeg, Trunk }

    public static class RaidChainRules
    {
        public static float Length(Vector3 first, Vector3 second, Vector3? anchor)
        {
            return anchor.HasValue ? Vector3.Distance(first, anchor.Value) +
                Vector3.Distance(anchor.Value, second) : Vector3.Distance(first, second);
        }

        public static RaidChainBand Band(float length, float warning, float nominal, float hard)
        {
            if (length > hard) return RaidChainBand.Overstretched;
            if (length > nominal) return RaidChainBand.Buffered;
            if (length >= warning) return RaidChainBand.Taut;
            return RaidChainBand.Slack;
        }

        public static RaidChainState Display(RaidChainBand band, bool wrapped, bool snatched,
            bool grabbed, bool down)
        {
            if (grabbed) return RaidChainState.Grabbed;
            if (down) return RaidChainState.Down;
            if (snatched) return RaidChainState.Snatched;
            if (wrapped) return RaidChainState.Wrapped;
            return band == RaidChainBand.Overstretched ? RaidChainState.Overstretched :
                band == RaidChainBand.Slack ? RaidChainState.Slack : RaidChainState.Taut;
        }

        public static Vector3 ConstrainDisplacement(Vector3 first, Vector3 second, Vector3? anchor,
            bool moveFirst, Vector3 displacement, float nominal, float hard, float slowdown,
            out bool attemptedHard)
        {
            attemptedHard = false;
            float before = Length(first, second, anchor);
            float after = LengthAfter(first, second, anchor, moveFirst, displacement);
            if (after <= before || after <= nominal) return displacement;
            float factor = before <= nominal ? 1f :
                Mathf.Lerp(1f, slowdown, Mathf.InverseLerp(nominal, hard, before));
            displacement *= factor;
            after = LengthAfter(first, second, anchor, moveFirst, displacement);
            if (after <= hard) return displacement;
            attemptedHard = true;
            if (before >= hard) return Vector3.zero;
            float low = 0f, high = 1f;
            for (int i = 0; i < 12; i++)
            {
                float mid = (low + high) * .5f;
                if (LengthAfter(first, second, anchor, moveFirst, displacement * mid) <= hard)
                    low = mid;
                else high = mid;
            }
            return displacement * low;
        }

        static float LengthAfter(Vector3 first, Vector3 second, Vector3? anchor,
            bool moveFirst, Vector3 displacement)
        {
            if (moveFirst) first += displacement; else second += displacement;
            return Length(first, second, anchor);
        }
    }
}
