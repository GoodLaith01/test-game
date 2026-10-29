using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>Orbit camera with wall collision, plus sway and shake when the hero's mind slips.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 4.5f;
        public float Sensitivity = 3f;
        public float Yaw { get; private set; }
        float pitch = 15f;
        float shake;

        public void SnapBehind(float yaw) { Yaw = yaw; pitch = 15f; }
        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void LateUpdate()
        {
            if (Target == null) return;
            var hero = Hero.Instance;
            bool locked = hero != null && hero.InputLocked;
            if (!locked && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = GameInput.MouseDelta;
                Yaw += d.x * Sensitivity;
                pitch = Mathf.Clamp(pitch - d.y * Sensitivity, -30f, 70f);
                Distance = Mathf.Clamp(Distance - GameInput.Scroll * 0.5f, 2f, 8f);
            }

            float mind = hero != null ? hero.Stats.MindPenalty : 0f;
            float t = Time.time;
            float swayYaw = Mathf.Sin(t * 0.7f) * 4f * mind + Mathf.PerlinNoise(t * 0.5f, 0f) * 3f * mind;
            float swayRoll = Mathf.Sin(t * 0.43f) * 6f * mind;

            var rot = Quaternion.Euler(pitch, Yaw + swayYaw, swayRoll);
            Vector3 pivot = Target.position + Vector3.up * 1.6f;
            Vector3 wanted = pivot - rot * Vector3.forward * Distance + rot * Vector3.right * 0.5f;

            // Pull in when a wall is between the hero and the camera.
            var dir = wanted - pivot;
            float dist = dir.magnitude;
            RaycastHit hit;
            if (Physics.SphereCast(pivot, 0.25f, dir.normalized, out hit, dist, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(Target))
                wanted = pivot + dir.normalized * Mathf.Max(0.5f, hit.distance - 0.1f);

            if (shake > 0f)
            {
                wanted += Random.insideUnitSphere * shake * 0.15f;
                shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 3f);
            }

            transform.position = wanted;
            transform.rotation = rot;
        }
    }
}
