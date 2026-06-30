using System;

namespace Interrogation.Game
{
    /// <summary>
    /// A gyanúsított stressz-szintje: a körönkénti deltákat 0 és a küszöb közé klampolva gyűjti.
    /// </summary>
    public sealed class StressMeter
    {
        public int Threshold { get; }
        public int Current { get; private set; }

        /// <summary>(új érték, alkalmazott delta, küszöb)</summary>
        public event Action<int, int, int> Changed;

        public StressMeter(int threshold)
        {
            if (threshold <= 0) throw new ArgumentOutOfRangeException(nameof(threshold));
            Threshold = threshold;
            Current = 0;
        }

        public bool AtThreshold => Current >= Threshold;

        public float Normalized => (float)Current / Threshold;

        public int Apply(int delta)
        {
            var next = Current + delta;
            if (next < 0) next = 0;
            else if (next > Threshold) next = Threshold;

            Current = next;
            Changed?.Invoke(Current, delta, Threshold);
            return Current;
        }

        public void Reset()
        {
            Current = 0;
            Changed?.Invoke(Current, 0, Threshold);
        }
    }
}
