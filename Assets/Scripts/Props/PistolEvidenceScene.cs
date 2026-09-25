using System.Collections;
using UnityEngine;

namespace Interrogation.Props
{
    /// <summary>
    /// „Rendőrségi jegyzőkönyv hiánya": a pisztoly a tárral és a hüvelyekkel a gyanúsított elé csúszik,
    /// a válasz után ugyanazon az íven vissza.
    /// </summary>
    public sealed class PistolEvidenceScene : EvidencePropScene
    {
        [Tooltip("A fegyver elforgatása (fok, függőleges tengely körül) a gyanúsított előtt.")]
        [SerializeField] private float yawAtSuspect = 110f;
        [SerializeField] private float slideSeconds = 1.4f;
        [SerializeField] private float returnSeconds = 1.2f;
        [Tooltip("A fegyver ennyire emelkedik el az asztaltól a mozgatás alatt (m).")]
        [SerializeField] private float liftHeight = 0.025f;
        [Tooltip("Oldalra billenés (fok) a mozgás közben, mintha kézben vinnék.")]
        [SerializeField] private float carryTilt = 8f;
        [Tooltip("A letett fegyver legalsó pontja ennyivel az asztallap fölött (m).")]
        [SerializeField] private float deskClearance = 0.001f;

        private PropPose _home;
        private Vector3 _centerLocal;   // a csoport látható közepe a gyökérhez képest
        private float _heightAboveDesk; // a közép magassága az asztal síkja felett

        private void Awake()
        {
            _home = PropPose.Of(transform);
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            _centerLocal = transform.InverseTransformPoint(bounds.center);
            _heightAboveDesk = bounds.center.y - bounds.min.y + deskClearance;
        }

        public override IEnumerator Present(PropStage stage)
        {
            if (stage == null || stage.SuspectSpot == null) yield break;
            IsPresented = true;

            var rotation = Quaternion.AngleAxis(yawAtSuspect, Vector3.up) * _home.Rotation;
            var target = stage.SuspectSpot.position + Vector3.up * _heightAboveDesk;
            var pose = PropTween.RootPoseFor(_centerLocal, target, rotation, transform.lossyScale);
            yield return PropTween.Follow(transform, Route(_home, pose), slideSeconds);
        }

        public override IEnumerator Restore(PropStage stage)
        {
            yield return PropTween.Follow(transform, Route(PropPose.Of(transform), _home), returnSeconds);
            IsPresented = false;
        }

        // A billentés a mozgásirány tengelye körül halad, így a fegyver nem egy tömbben „úszik".
        private PropPose[] Route(PropPose from, PropPose to)
        {
            var scale = transform.lossyScale;
            Vector3 CenterOf(PropPose p) => p.Position + p.Rotation * Vector3.Scale(scale, _centerLocal);
            var a = CenterOf(from);
            var b = CenterOf(to);
            var travel = b - a;
            travel.y = 0f;
            var tilt = Quaternion.AngleAxis(carryTilt, travel.sqrMagnitude > 1e-6f ? travel.normalized : Vector3.forward);
            var lift = Vector3.up * liftHeight;

            // A látható közép körül forog, mert a modell gyökere messze esik tőle.
            PropPose Carry(float k) => PropTween.RootPoseFor(_centerLocal, Vector3.Lerp(a, b, k) + lift,
                tilt * Quaternion.Slerp(from.Rotation, to.Rotation, k), scale);
            return new[] { from, Carry(0.2f), Carry(0.8f), to };
        }
    }
}
