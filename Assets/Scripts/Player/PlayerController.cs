using Hollowmark.Data;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>Third-person movement: walk, sprint (stamina), jump and dodge roll.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        CharacterController cc;
        Hero hero;
        float verticalSpeed;
        float rollTimer;
        Vector3 rollDir;
        float rollSpeed = 9f;

        public float CurrentSpeed { get; private set; }
        public bool Sprinting { get; private set; }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            hero = GetComponent<Hero>();
        }

        /// <summary>A short burst of movement with the roll's invulnerability, used by the Lunge ability.</summary>
        public void Dash(Vector3 dir, float speed, float time)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            rollDir = dir.normalized;
            rollSpeed = speed;
            rollTimer = time;
        }

        public void Teleport(Vector3 position, float yaw)
        {
            cc.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cc.enabled = true;
            verticalSpeed = 0f;
            if (hero.Cam != null) hero.Cam.SnapBehind(yaw);
        }

        void Update()
        {
            var stats = hero.Stats;
            var b = GameDatabase.Balance;
            if (stats.Dead) { hero.Visual.Rig.Dead = true; return; }
            hero.Visual.Rig.Dead = false;

            Vector2 input = hero.InputLocked ? Vector2.zero : GameInput.Move;
            float camYaw = hero.Cam != null ? hero.Cam.Yaw : transform.eulerAngles.y;
            var camRot = Quaternion.Euler(0f, camYaw, 0f);
            Vector3 move = camRot * new Vector3(input.x, 0f, input.y);

            float speed = b.walkSpeed;
            Sprinting = false;
            if (!hero.InputLocked && GameInput.Key(KeyCode.LeftShift) && move.sqrMagnitude > 0.1f && stats.Stamina > 1f && !stats.Blocking)
            {
                Sprinting = true;
                speed = b.sprintSpeed;
                stats.Stamina = Mathf.Max(0f, stats.Stamina - b.sprintStaminaPerSecond * stats.Progress.MoveStaminaMult * Time.deltaTime);
                stats.TrainSkill(stats.Athletics, 0.25f * Time.deltaTime);
            }
            speed *= stats.Progress.MoveSpeedMult;
            speed /= 1f + stats.Inventory.ArmorWeight * 0.03f;
            if (stats.Inventory.InUnderwear) speed *= 1.1f; // light as a feather, at least
            if (stats.Cold) speed *= 0.85f;
            if (stats.Blocking) speed *= 0.45f;
            if (hero.Combat.Reloading) speed *= 0.6f;

            // Dodge roll: brief invulnerability.
            if (!hero.InputLocked && rollTimer <= 0f && (GameInput.KeyDown(KeyCode.C) || GameInput.KeyDown(KeyCode.LeftControl)) && cc.isGrounded)
            {
                if (stats.UseStamina(b.rollStaminaCost * stats.Progress.MoveStaminaMult))
                {
                    stats.TrainSkill(stats.Athletics, 1.5f);
                    rollSpeed = 9f;
                    rollTimer = 0.45f;
                    rollDir = move.sqrMagnitude > 0.1f ? move.normalized : -transform.forward;
                }
            }

            Vector3 horizontal;
            if (rollTimer > 0f)
            {
                rollTimer -= Time.deltaTime;
                stats.Rolling = rollTimer > 0.1f;
                horizontal = rollDir * rollSpeed;
            }
            else
            {
                stats.Rolling = false;
                horizontal = move * speed;
            }

            // Facing: toward the camera when fighting, otherwise toward movement.
            bool combatFacing = hero.Combat.WantsCameraFacing;
            Vector3 face = combatFacing ? camRot * Vector3.forward : horizontal;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), Time.deltaTime * (combatFacing ? 18f : 12f));

            if (cc.isGrounded)
            {
                verticalSpeed = -2f;
                if (!hero.InputLocked && GameInput.KeyDown(KeyCode.Space) && stats.UseStamina(8f * stats.Progress.MoveStaminaMult)) { verticalSpeed = 5.5f; stats.TrainSkill(stats.Athletics, 0.5f); }
            }
            else verticalSpeed -= 18f * Time.deltaTime;

            cc.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            var flat = cc.velocity; flat.y = 0f;
            CurrentSpeed = flat.magnitude;
            hero.Visual.Rig.MoveSpeed = CurrentSpeed;

            if (transform.position.y < -30f) Teleport(GameManager.Instance.SpawnPoint, 0f); // fell out of the world
        }
    }
}
