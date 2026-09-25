using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Props
{
    /// <summary>
    /// „Telefonnapló": a kagyló lekerül az asztalra a készülék mellé. A kagyló darabjait egy
    /// <see cref="RigidGroup"/> mozgatja együtt, közben a <see cref="CoiledCord"/> köti a készülékhez.
    /// </summary>
    public sealed class PhoneEvidenceScene : EvidencePropScene
    {
        [SerializeField] private Transform[] handsetParts = new Transform[0];
        [SerializeField] private Renderer staticCord;
        [SerializeField] private CoiledCord coiledCord;

        [Tooltip("A kagyló letételi helye a villán lévő pozícióhoz képest (világ-eltolás, m).")]
        [SerializeField] private Vector3 deskOffset = new Vector3(-0.01f, 0f, 0.19f); // a telefon és az újság közé
        [SerializeField] private float deskYaw = 80f;
        [SerializeField] private float deskHeight = 0.7762f; // az asztallap mért magassága
        [Tooltip("A kagyló előbb ennyit emelkedik függőlegesen (hogy ne súrolja a villát), és ennyiről ereszkedik le (m).")]
        [SerializeField] private float liftHeight = 0.07f;
        [SerializeField] private float moveSeconds = 1.4f;

        private readonly RigidGroup _handset = new RigidGroup();
        private PropPose _home;
        private float _halfHeight;

        private void Awake()
        {
            var bounds = new Bounds();
            var first = true;
            var parts = new List<Transform>();
            foreach (var p in handsetParts)
            {
                if (p == null) continue;
                parts.Add(p);
                var r = p.GetComponent<Renderer>();
                if (r == null) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            _home = new PropPose(bounds.center, Quaternion.identity);
            _halfHeight = bounds.extents.y;
            _handset.Capture(parts, _home);
        }

        public override IEnumerator Present(PropStage stage)
        {
            IsPresented = true;
            SwapCord(procedural: true);
            var target = _home.Position + deskOffset;
            target.y = deskHeight + _halfHeight;
            var onDesk = new PropPose(target, Quaternion.AngleAxis(deskYaw, Vector3.up));
            yield return _handset.Follow(Route(_home, onDesk), moveSeconds);
        }

        public override IEnumerator Restore(PropStage stage)
        {
            yield return _handset.Follow(Route(_handset.Pivot, _home), moveSeconds);
            SwapCord(procedural: false);
            IsPresented = false;
        }

        // Függőleges emelés és letétel, hogy ne súrolja a villát.
        private PropPose[] Route(PropPose from, PropPose to) => new[]
        {
            from,
            new PropPose(from.Position + Vector3.up * liftHeight, from.Rotation),
            new PropPose(to.Position + Vector3.up * liftHeight, to.Rotation),
            to,
        };

        private void SwapCord(bool procedural)
        {
            if (staticCord != null) staticCord.enabled = !procedural;
            if (coiledCord != null) coiledCord.SetVisible(procedural);
        }
    }
}
