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
        float currentDist = 4.5f;

        public void SnapBehind(float yaw) { Yaw = yaw; pitch = 15f; currentDist = Distance; }
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
            // Only walls and props pull the camera in: creatures walking behind the hero don't make it jump.
            float nearest = dist;
            foreach (var hit in Physics.SphereCastAll(pivot, 0.25f, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is CharacterController || hit.transform.IsChildOf(Target)) continue;
                if (hit.distance <= 0f) continue; // started inside this collider
                nearest = Mathf.Min(nearest, hit.distance);
            }
            if (nearest < dist) wanted = pivot + dir.normalized * Mathf.Max(0.5f, nearest - 0.1f);
            // Smooth only when the view opens up again, so walls never clip through but there's no snapping.
            float wantedDist = (wanted - pivot).magnitude;
            currentDist = wantedDist < currentDist ? wantedDist : Mathf.MoveTowards(currentDist, wantedDist, Time.unscaledDeltaTime * 6f);
            wanted = pivot + dir.normalized * currentDist;

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
