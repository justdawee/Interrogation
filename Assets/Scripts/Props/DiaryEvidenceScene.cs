using System.Collections;
using UnityEngine;

namespace Interrogation.Props
{
    /// <summary>
    /// „Reinhardt naplója": a mappa a kamera elé emelkedik és kinyílik, a nagyító végighalad a
    /// bizonyító soron, majd a napló a gyanúsított elé csúszik. A válasz után minden visszaáll.
    /// </summary>
    public sealed class DiaryEvidenceScene : EvidencePropScene
    {
        [Header("Mappa")]
        [SerializeField] private SkinnedMeshRenderer cover;
        [Tooltip("A morph-animáció teljesen nyitott képkockája.")]
        [SerializeField] private int openFrame = 100;
        [SerializeField] private Transform diaryPage;
        [Tooltip("A bizonyító sor helye a lap (Quad) lokális koordinátáiban.")]
        [SerializeField] private Vector2 highlightLocal = new Vector2(0.04f, 0.065f);
        [Tooltip("A gyanúsított elé letett mappa legalsó pontja ennyivel az asztallap fölött (m) — " +
                 "e nélkül a nyitott fedél az asztal síkjában villódzik.")]
        [SerializeField] private float deskClearance = 0.0015f;

        [Header("Nagyító (a modell forgáspontja messze van a lencsétől — mért értékek)")]
        [SerializeField] private Transform magnifier;
        [SerializeField] private Vector3 lensCenterLocal = new Vector3(0.440f, 0.001f, -0.267f);
        [SerializeField] private Vector3 lensNormalLocal = new Vector3(0f, -1f, 0f);
        [SerializeField] private Vector3 handleDirLocal = new Vector3(0.903f, 0f, 0.43f);
        [Tooltip("A lencse a lap és a kamera közti szakasz ekkora hányadánál áll.")]
        [Range(0.1f, 0.8f)] [SerializeField] private float lensFraction = 0.4f;
        [SerializeField] private float sweepHalfWidth = 0.045f;
        [Tooltip("A lencse ennyivel a lap közepétől jobbra kerüli meg a nyitott naplót (m).")]
        [SerializeField] private float passAside = 0.26f;
        [SerializeField] private float pickupHeight = 0.05f;

        [Header("Időzítés (mp)")]
        [SerializeField] private float liftSeconds = 1.1f;
        [SerializeField] private float openSeconds = 1.3f;
        [SerializeField] private float lensInSeconds = 1.5f;
        [SerializeField] private float sweepSeconds = 1.8f;
        [SerializeField] private float lensOutSeconds = 1.3f;
        [SerializeField] private float slideSeconds = 1.2f;
        [SerializeField] private float closeSeconds = 1.0f;

        private FolderCoverMorph _morph;
        private PropPose _home, _magHome;
        private Vector3 _pageLocal, _pageNormalHome, _pageUpHome;
        private float _pageHeightAboveDesk;

        private void Awake()
        {
            _morph = new FolderCoverMorph(cover);
            _home = PropPose.Of(transform);
            if (magnifier != null) _magHome = PropPose.Of(magnifier);
            if (diaryPage != null)
            {
                _pageLocal = transform.InverseTransformPoint(diaryPage.position);
                _pageNormalHome = -diaryPage.forward; // a Quad látható oldala a -Z
                _pageUpHome = diaryPage.up;
                // A mappa legalsó pontja a lap alatt; kinyitva, laposan fekve ugyanennyi (mért: 19,5 mm).
                var deskY = cover != null ? cover.bounds.min.y : transform.position.y;
                _pageHeightAboveDesk = diaryPage.position.y - deskY + deskClearance;
            }
        }

        public override IEnumerator Present(PropStage stage)
        {
            if (stage == null || diaryPage == null) yield break;
            IsPresented = true;
            var cam = stage.View;

            var point = stage.PresentPoint;
            var rotation = PropStage.Facing(_home.Rotation, _pageNormalHome, _pageUpHome, stage.TowardCamera(point), cam.up);
            var lift = PropTween.RootPoseFor(_pageLocal, point, rotation, transform.lossyScale);
            _morph.SetFrame(0f);
            stage.SetPresentLight(true);
            var opening = StartCoroutine(OpenAfter(liftSeconds * 0.45f));
            yield return PropTween.Move(transform, lift, liftSeconds, 0.05f);
            yield return opening;

            if (magnifier != null) yield return SweepMagnifier(stage);

            // A gyanúsított előtt már a lámpa fénykörében van, a derítőfény kiúszik.
            stage.SetPresentLight(false);
            if (stage.SuspectSpot != null)
            {
                var spot = stage.SuspectSpot;
                var onDesk = PropStage.Facing(_home.Rotation, _pageNormalHome, _pageUpHome, Vector3.up, -spot.forward);
                var target = spot.position + Vector3.up * _pageHeightAboveDesk;
                yield return PropTween.Move(transform, PropTween.RootPoseFor(_pageLocal, target, onDesk, transform.lossyScale), slideSeconds, 0.06f);
            }
        }

        public override IEnumerator Restore(PropStage stage)
        {
            if (stage != null) stage.SetPresentLight(false);
            var up = new PropPose(transform.position + Vector3.up * 0.16f, transform.rotation);
            var closing = StartCoroutine(_morph.Play(_morph.Frame, 0f, closeSeconds));
            yield return PropTween.Move(transform, up, closeSeconds * 0.8f);
            yield return closing;
            _morph.Rest();
            yield return PropTween.Move(transform, _home, liftSeconds, 0.04f);
            IsPresented = false;
        }

        private IEnumerator OpenAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return _morph.Play(0f, openFrame, openSeconds);
        }

        private IEnumerator SweepMagnifier(PropStage stage)
        {
            PlanMagnifier(stage, out var approach, out var retreat);
            yield return PropTween.Follow(magnifier, approach, lensInSeconds);
            var from = approach[approach.Length - 1];
            var to = retreat[0];
            yield return PropTween.Run(sweepSeconds, k => PropTween.Lerp(from, to, k).ApplyTo(magnifier));
            yield return new WaitForSeconds(0.4f);
            yield return PropTween.Follow(magnifier, retreat, lensOutSeconds);
        }

        // Egyenes úton a nagyító átmenne a felemelt lapon, ezért a napló jobb széle mellett kerül a
        // kamera oldalára, és csak ott fordul be a sor elé. Visszafelé ugyanígy.
        private void PlanMagnifier(PropStage stage, out PropPose[] approach, out PropPose[] retreat)
        {
            var cam = stage.View;
            var origin = diaryPage.position;
            var right = diaryPage.right;
            var up = diaryPage.up;
            var normal = -diaryPage.forward; // a lap látható oldala, a kamera felé
            var line = diaryPage.TransformPoint(new Vector3(highlightLocal.x, highlightLocal.y, 0f));
            var handle = (-cam.up * 0.8f + cam.right * 0.6f).normalized; // nyél: jobbra le, mint a kézben
            var held = PropStage.Facing(_magHome.Rotation, _magHome.Rotation * lensNormalLocal,
                _magHome.Rotation * handleDirLocal, stage.TowardCamera(line), handle);

            Vector3 LensAt(float offset) => Vector3.Lerp(line + right * offset, cam.position, lensFraction);
            PropPose Pose(Vector3 lens, Quaternion rotation) =>
                PropTween.RootPoseFor(lensCenterLocal, lens, rotation, magnifier.lossyScale);
            Vector3 Aside(float height, float depth) => origin + right * passAside + up * height + normal * depth;

            var homeLens = _magHome.Position + _magHome.Rotation * Vector3.Scale(magnifier.lossyScale, lensCenterLocal);
            var pickup = homeLens + Vector3.up * pickupHeight;
            var start = LensAt(-sweepHalfWidth);
            var end = LensAt(sweepHalfWidth);
            var behind = Vector3.Dot(pickup - origin, normal);
            var front = Vector3.Dot(start - origin, normal);
            // A kerülő a sor magasságában halad, így a lefelé álló nyél sem ér az asztalba.
            var startUp = Vector3.Dot(start - origin, up);
            var endUp = Vector3.Dot(end - origin, up);
            // A lap mögött még csak félig fordul be — a teljes fordulatnál a nyél az asztalba lógna.
            var turning = Quaternion.Slerp(_magHome.Rotation, held, 0.5f);

            approach = new[]
            {
                _magHome,
                Pose(pickup, _magHome.Rotation),
                Pose(Aside(startUp, behind), turning),
                Pose(Aside(startUp, front), held),
                Pose(start, held),
            };
            retreat = new[]
            {
                Pose(end, held),
                Pose(Aside(endUp, front), held),
                Pose(Aside(endUp, behind), turning),
                Pose(pickup, _magHome.Rotation),
                _magHome,
            };
        }
    }
}
