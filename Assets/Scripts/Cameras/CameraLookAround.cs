using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interrogation.Cameras
{
    /// <summary>
    /// Visszafogott körülnézés a detektív-nézetből a kurzor helyzete szerint, holtzónával és korlátos
    /// szögekkel. Bármely kérő letilthatja (<see cref="SetSuppressed"/>); ilyenkor a kamera simán
    /// visszaáll az alap-pózra. A Brain előtt fut, hogy ugyanabban a képkockában érvényesüljön.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class CameraLookAround : MonoBehaviour
    {
        [Header("Korlátok (fok)")]
        [SerializeField] [Range(0f, 45f)] private float maxYaw = 20f;
        [SerializeField] [Range(0f, 30f)] private float maxPitchUp = 8f;
        [SerializeField] [Range(0f, 30f)] private float maxPitchDown = 12f;

        [Header("Érzet")]
        [Tooltip("A képernyő középső része (0..1, normalizált), ahol a kamera nem mozdul.")]
        [SerializeField] [Range(0f, 0.6f)] private float deadZone = 0.2f;
        [Tooltip("Csillapítás szabad körülnézéskor (mp).")]
        [SerializeField] private float lookSmoothTime = 0.35f;
        [Tooltip("Csillapítás az alap-pózba való visszaálláskor (mp).")]
        [SerializeField] private float returnSmoothTime = 0.22f;
        [Tooltip("A forgáspont (nyak) helye a kamerához képest, lokálisan (m).")]
        [SerializeField] private Vector3 neckPivot = new Vector3(0f, -0.08f, -0.09f);

        private readonly HashSet<object> _suppressors = new HashSet<object>();
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector2 _angles;          // (yaw, pitch), pozitív pitch = lefelé
        private Vector2 _velocity;
        private bool _hasBase;

        public Vector3 BasePosition => _hasBase ? _basePosition : transform.position;
        public Quaternion BaseRotation => _hasBase ? _baseRotation : transform.rotation;
        public bool IsFree => _suppressors.Count == 0;
        /// <summary>Eltérés az alap-póztól fokban.</summary>
        public Vector2 Offset => _angles;

        private void OnEnable()
        {
            if (!_hasBase) CaptureBase();
        }

        private void OnDisable()
        {
            if (!_hasBase) return;
            _angles = Vector2.zero;
            _velocity = Vector2.zero;
            Apply();
        }

        public void CaptureBase()
        {
            _basePosition = transform.position;
            _baseRotation = transform.rotation;
            _angles = Vector2.zero;
            _velocity = Vector2.zero;
            _hasBase = true;
        }

        public void SetSuppressed(object key, bool suppressed)
        {
            if (key == null) return;
            if (suppressed) _suppressors.Add(key);
            else _suppressors.Remove(key);
        }

        private void LateUpdate()
        {
            var target = IsFree ? TargetAngles() : Vector2.zero;
            var smooth = IsFree ? lookSmoothTime : returnSmoothTime;
            _angles.x = Mathf.SmoothDamp(_angles.x, target.x, ref _velocity.x, smooth);
            _angles.y = Mathf.SmoothDamp(_angles.y, target.y, ref _velocity.y, smooth);
            Apply();
        }

        private Vector2 TargetAngles()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused) return Vector2.zero;

            var pos = mouse.position.ReadValue();
            if (pos.x < 0f || pos.y < 0f || pos.x > Screen.width || pos.y > Screen.height) return _angles;

            var n = new Vector2(pos.x / Screen.width * 2f - 1f, pos.y / Screen.height * 2f - 1f);
            var x = Shape(n.x);
            var y = Shape(n.y);
            return new Vector2(x * maxYaw, y >= 0f ? -y * maxPitchUp : -y * maxPitchDown);
        }

        // Holtzóna + smoothstep: a széleken sem ugrik, középen nyugodt.
        private float Shape(float v)
        {
            var a = Mathf.Abs(v);
            if (a <= deadZone) return 0f;
            var t = Mathf.Clamp01((a - deadZone) / (1f - deadZone));
            return Mathf.Sign(v) * t * t * (3f - 2f * t);
        }

        private void Apply()
        {
            if (!_hasBase) return;
            // Előbb a vízszintes fordulás, aztán a bólintás, így nincs döntés (roll).
            var look = Quaternion.AngleAxis(_angles.x, Vector3.up) * _baseRotation * Quaternion.AngleAxis(_angles.y, Vector3.right);
            var pivot = _basePosition + _baseRotation * neckPivot;
            var offset = look * Quaternion.Inverse(_baseRotation) * (_basePosition - pivot);
            transform.SetPositionAndRotation(pivot + offset, look);
        }
    }
}
