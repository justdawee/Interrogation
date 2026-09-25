using UnityEngine;

namespace Interrogation.Props
{
    /// <summary>
    /// A tárgy-jelenetek közös színpada: a kamera előtti felmutatási pont és a gyanúsított előtti
    /// asztal-pont, egy helyen hangolhatóan.
    /// </summary>
    public sealed class PropStage : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("A detektív-nézet körülnézése (a Cinemachine-beállításon ül, nem a Main Camerán).")]
        [SerializeField] private Cameras.CameraLookAround lookAround;
        [Tooltip("Pont az asztalon a gyanúsított előtt (a +Z tengelye a gyanúsított felé néz).")]
        [SerializeField] private Transform suspectSpot;
        [Tooltip("A felmutatott tárgy távolsága a kamerától (m).")]
        [SerializeField] private float presentDistance = 0.42f;
        [Tooltip("A felmutatott tárgy ennyivel a kamera optikai tengelye alatt van (m).")]
        [SerializeField] private float presentDrop = 0.05f;
        [Tooltip("A kamera melletti derítőfény: a felmutatott lapot a detektív felől világítja meg (a " +
                 "szoba fényei mind a lap mögött vannak). Csak felmutatás alatt ég.")]
        [SerializeField] private Light presentLight;
        [SerializeField] private float presentLightFade = 0.7f;

        private float _presentIntensity;
        private Coroutine _fade;

        public Camera ViewCamera => viewCamera != null ? viewCamera : Camera.main;

        private void Awake()
        {
            if (presentLight == null) return;
            _presentIntensity = presentLight.intensity;
            presentLight.intensity = 0f;
            presentLight.enabled = false;
        }

        public void SetPresentLight(bool on)
        {
            if (presentLight == null) return;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadePresentLight(on ? _presentIntensity : 0f));
        }

        private System.Collections.IEnumerator FadePresentLight(float target)
        {
            presentLight.enabled = true;
            var from = presentLight.intensity;
            yield return PropTween.Run(presentLightFade, k => presentLight.intensity = Mathf.Lerp(from, target, k));
            presentLight.enabled = target > 0f;
            _fade = null;
        }
        public Transform SuspectSpot => suspectSpot;

        /// <summary>A kamera alap-pózához igazodik, így a tárgy oda kerül, ahová a visszaálló kamera néz.</summary>
        public Vector3 PresentPoint
        {
            get
            {
                GetViewPose(out var pos, out var rot);
                return pos + rot * Vector3.forward * presentDistance - rot * Vector3.up * presentDrop;
            }
        }

        public Vector3 TowardCamera(Vector3 from)
        {
            GetViewPose(out var pos, out _);
            return (pos - from).normalized;
        }

        public ViewPose View
        {
            get
            {
                GetViewPose(out var pos, out var rot);
                return new ViewPose(pos, rot);
            }
        }

        public readonly struct ViewPose
        {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public ViewPose(Vector3 position, Quaternion rotation) { this.position = position; this.rotation = rotation; }
            public Vector3 up => rotation * Vector3.up;
            public Vector3 right => rotation * Vector3.right;
            public Vector3 forward => rotation * Vector3.forward;
        }

        private void GetViewPose(out Vector3 position, out Quaternion rotation)
        {
            var cam = ViewCamera.transform;
            var look = lookAround != null ? lookAround : cam.GetComponent<Cameras.CameraLookAround>();
            position = look != null ? look.BasePosition : cam.position;
            rotation = look != null ? look.BaseRotation : cam.rotation;
        }

        /// <summary>A tárgy egy felületét a kívánt normális és „felfelé" irány felé fordítja.</summary>
        public static Quaternion Facing(Quaternion homeRotation, Vector3 homeNormal, Vector3 homeUp,
            Vector3 desiredNormal, Vector3 desiredUp)
        {
            var from = Quaternion.LookRotation(homeNormal, homeUp);
            var to = Quaternion.LookRotation(desiredNormal, desiredUp);
            return to * Quaternion.Inverse(from) * homeRotation;
        }
    }
}
