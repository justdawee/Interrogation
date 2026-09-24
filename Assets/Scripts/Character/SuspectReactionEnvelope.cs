using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>
    /// A futó reakció és súlya közvetlenül az Animatorból, így az arc és a tekintet külön időzítő
    /// nélkül, a testtel szinkronban reagál.
    /// </summary>
    public static class SuspectReactionEnvelope
    {
        private const int BodyLayer = 0;

        private static readonly (int hash, SuspectReaction reaction)[] States =
        {
            (Animator.StringToHash("Caught"), SuspectReaction.Caught),
            (Animator.StringToHash("Outburst"), SuspectReaction.Outburst),
            (Animator.StringToHash("SmugLaugh"), SuspectReaction.SmugLaugh)
        };

        /// <param name="seconds">A reakció kezdete óta eltelt idő (a ritmikus mimikához).</param>
        public static bool TryGet(Animator animator, out SuspectReaction reaction, out float weight, out float seconds)
        {
            reaction = default;
            weight = 0f;
            seconds = 0f;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;

            var current = animator.GetCurrentAnimatorStateInfo(BodyLayer);
            if (animator.IsInTransition(BodyLayer))
            {
                var next = animator.GetNextAnimatorStateInfo(BodyLayer);
                var progress = animator.GetAnimatorTransitionInfo(BodyLayer).normalizedTime;
                if (Match(next, out reaction)) // beúszás
                {
                    weight = Smooth(progress);
                    seconds = next.normalizedTime * next.length;
                    return true;
                }
                if (Match(current, out reaction)) // kiúszás
                {
                    weight = 1f - Smooth(progress);
                    seconds = current.normalizedTime * current.length;
                    return true;
                }
                return false;
            }

            if (!Match(current, out reaction)) return false;
            weight = 1f;
            seconds = current.normalizedTime * current.length;
            return true;
        }

        public static float Weight(Animator animator) =>
            TryGet(animator, out _, out var weight, out _) ? weight : 0f;

        private static bool Match(AnimatorStateInfo info, out SuspectReaction reaction)
        {
            foreach (var (hash, r) in States)
            {
                if (info.shortNameHash != hash) continue;
                reaction = r;
                return true;
            }
            reaction = default;
            return false;
        }

        private static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }
}
