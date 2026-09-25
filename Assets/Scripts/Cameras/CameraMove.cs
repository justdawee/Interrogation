using System.Collections;
using UnityEngine;

namespace Interrogation.Cameras
{
    public static class CameraMove
    {
        /// <summary>A jelenlegi póztól hátrébb indul, és lágyan „beül" a detektív-nézetbe.</summary>
        public static IEnumerator IntroDolly(Transform cam, Vector3 localOffset, float seconds)
        {
            if (cam == null) yield break;
            var endPos = cam.position;
            var endRot = cam.rotation;
            var startPos = endPos + endRot * localOffset;
            var focus = endPos + endRot * Vector3.forward * 2f;
            var startRot = Quaternion.LookRotation(focus - startPos, Vector3.up);

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / seconds);
                cam.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k), Quaternion.Slerp(startRot, endRot, k));
                yield return null;
            }
            cam.SetPositionAndRotation(endPos, endRot);
        }

        public static IEnumerator Glide(Transform cam, Vector3 toPos, Quaternion toRot, float seconds)
        {
            if (cam == null) yield break;
            var fromPos = cam.position;
            var fromRot = cam.rotation;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / seconds);
                cam.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
                yield return null;
            }
            cam.SetPositionAndRotation(toPos, toRot);
        }
    }
}
