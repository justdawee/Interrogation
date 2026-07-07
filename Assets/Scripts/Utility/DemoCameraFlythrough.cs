using System.Collections.Generic;
using UnityEngine;

namespace Interrogation.Utility
{
    /// <summary>
    /// Bemutató kamera-út: Catmull-Rom spline mentén bejárja a szobát, és a gameplay-kamera pózába
    /// „ül be". Kikapcsoláskor visszaállítja a kamerát és a UI-t.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoCameraFlythrough : MonoBehaviour
    {
        [Header("Cél")]
        [Tooltip("A mozgatott kamera. Ha üres, ennek a GameObjectnek a Transformja.")]
        [SerializeField] Transform cameraToMove;

        [Tooltip("Bejárás alatt elrejtett UI (pl. az Interrogation Canvas). Opcionális.")]
        [SerializeField] Canvas uiToHide;

        [Header("Időzítés")]
        [Tooltip("A teljes bejárás hossza másodpercben.")]
        [Min(1f)] [SerializeField] float duration = 13f;

        [Tooltip("Végtelenítve ismételje a bejárást (zárt hurok, vágás nélkül).")]
        [SerializeField] bool loop = true;

        [Tooltip("Lassú indítás/érkezés (ease-in-out) a nyers lineáris idő helyett.")]
        [SerializeField] bool easeInOut = true;

        [Header("Útvonal (világkoordináták)")]
        [Tooltip("A kamera pozíció-pontjai. A spline ezeken halad át.")]
        [SerializeField] List<Vector3> waypoints = new()
        {
            new Vector3(-2.0f, 2.30f, -2.2f),
            new Vector3(-2.0f, 1.45f,  0.6f),
            new Vector3(-0.6f, 1.05f,  1.8f),
            new Vector3( 1.1f, 1.25f,  1.6f),
            new Vector3( 1.8f, 2.00f, -0.4f),
            new Vector3( 0.4f, 2.45f, -2.0f),
            new Vector3(-2.0f, 2.59f, -2.0f),
        };

        [Tooltip("Mire nézzen a kamera az egyes pontokban. Ugyanannyi elem, mint a Waypoints.")]
        [SerializeField] List<Vector3> lookTargets = new()
        {
            new Vector3( 0.0f, 1.20f,  0.0f),
            new Vector3( 0.5f, 1.10f,  0.4f),
            new Vector3( 0.3f, 1.00f, -0.4f),
            new Vector3(-0.3f, 1.15f, -0.5f),
            new Vector3( 0.0f, 1.40f,  0.0f),
            new Vector3( 0.0f, 1.05f,  0.3f),
            new Vector3(-0.33f, 1.09f, -0.01f),
        };

        Transform _cam;
        Vector3 _startPos;
        Quaternion _startRot;
        bool _uiWasActive;
        float _elapsed;
        bool _running;

        void OnEnable()
        {
            _cam = cameraToMove != null ? cameraToMove : transform;

            if (waypoints.Count < 2 || waypoints.Count != lookTargets.Count)
            {
                Debug.LogWarning(
                    $"[{nameof(DemoCameraFlythrough)}] Legalább 2 pont kell, és a " +
                    "Waypoints/LookTargets listáknak egyforma hosszúnak kell lenniük. " +
                    "A bemutató nem indul el.", this);
                enabled = false;
                return;
            }

            _startPos = _cam.position;
            _startRot = _cam.rotation;

            if (uiToHide != null)
            {
                _uiWasActive = uiToHide.gameObject.activeSelf;
                uiToHide.gameObject.SetActive(false);
            }

            _elapsed = 0f;
            _running = true;
        }

        void OnDisable()
        {
            if (uiToHide != null)
                uiToHide.gameObject.SetActive(_uiWasActive);

            if (_cam != null)
            {
                _cam.position = _startPos;
                _cam.rotation = _startRot;
            }

            _running = false;
        }

        void Update()
        {
            if (!_running)
                return;

            _elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(_elapsed / duration);

            if (raw >= 1f)
            {
                if (loop)
                {
                    _elapsed = 0f;
                    raw = 0f;
                }
                else
                {
                    ApplyPose(1f);
                    _running = false;
                    return;
                }
            }

            float t = easeInOut ? Mathf.SmoothStep(0f, 1f, raw) : raw;
            ApplyPose(t);
        }

        void ApplyPose(float t)
        {
            Vector3 pos = SampleSpline(waypoints, t);
            Vector3 look = SampleSpline(lookTargets, t);

            _cam.position = pos;
            Vector3 dir = look - pos;
            if (dir.sqrMagnitude > 1e-6f)
                _cam.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // Loop esetén zárt görbe, különben a végpontok rögzítettek.
        Vector3 SampleSpline(IReadOnlyList<Vector3> pts, float t)
        {
            int count = pts.Count;
            int segments = loop ? count : count - 1;
            float scaled = Mathf.Clamp(t, 0f, 0.999999f) * segments;
            int i = Mathf.FloorToInt(scaled);
            float u = scaled - i;

            Vector3 p0 = pts[WrapIndex(i - 1, count)];
            Vector3 p1 = pts[WrapIndex(i, count)];
            Vector3 p2 = pts[WrapIndex(i + 1, count)];
            Vector3 p3 = pts[WrapIndex(i + 2, count)];

            return CatmullRom(p0, p1, p2, p3, u);
        }

        int WrapIndex(int index, int count)
        {
            if (loop)
                return ((index % count) + count) % count;
            return Mathf.Clamp(index, 0, count - 1);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Count < 2)
                return;

            Gizmos.color = Color.cyan;
            Vector3 prev = SampleSpline(waypoints, 0f);
            const int steps = 120;
            for (int s = 1; s <= steps; s++)
            {
                Vector3 p = SampleSpline(waypoints, s / (float)steps);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }

            Gizmos.color = Color.yellow;
            foreach (var w in waypoints)
                Gizmos.DrawWireSphere(w, 0.06f);
        }
#endif
    }
}
