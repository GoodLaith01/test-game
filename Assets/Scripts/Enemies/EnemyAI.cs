using Hollowmark.Data;
using Hollowmark.Player;
using Hollowmark.Visuals;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.Enemies
{
    /// <summary>
    /// Shared behaviour for all five creature types, configured entirely from EnemyDef:
    /// hiding (shadow or burrow) and ambushing, fast short-bodied melee, or keeping distance and throwing embers.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyAI : MonoBehaviour, IDamageable
    {
        public enum State { Hidden, Idle, Chase, Attack, Recover, Dead }

        public EnemyDef Def;
        public int Tier = 1;
        public State Current = State.Idle;
        public float Health, MaxHealth;
        public CharacterRig Rig;
        public System.Action<EnemyAI> Died;

        protected CharacterController cc;
        protected float nextAttack;
        protected float stamina;
        protected float verticalSpeed;
        protected Vector3 home;
        float recoverUntil;
        float stunUntil;
        bool ambushReady;

        /// <summary>Every creature currently in the world.</summary>
        public static readonly System.Collections.Generic.List<EnemyAI> All = new System.Collections.Generic.List<EnemyAI>();

        public bool IsAlive { get { return Current != State.Dead; } }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }
        protected Hero Player { get { return Hero.Instance; } }

        public static EnemyAI Spawn(EnemyDef def, Vector3 position, int tier, Transform parent)
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var cc = go.AddComponent<CharacterController>();
            cc.height = Mathf.Max(def.height, def.width * 2f + 0.01f);
            cc.radius = def.width * 0.5f;
            cc.center = Vector3.up * cc.height * 0.5f;
            cc.stepOffset = Mathf.Min(0.35f, cc.height * 0.3f);

            var ai = go.AddComponent<EnemyAI>();
            ai.Setup(def, tier);
            return ai;
        }

        protected virtual void Setup(EnemyDef def, int tier)
        {
            Def = def;
            Tier = tier;
            float tierScale = 1f + (tier - 1) * 0.25f;
            MaxHealth = Health = def.health * tierScale;
            stamina = def.stamina;
            home = transform.position;

            var model = CharacterBuilder.TryModel(def.model, transform);
            var palette = def.skinColors != null && def.skinColors.Length > 0 ? def.skinColors : new[] { "#606060" };
            var skin = EnumUtil.ParseColor(palette[Random.Range(0, palette.Length)], Color.gray);
            var eye = EnumUtil.ParseColor(def.eyeColor, Color.yellow);
            Rig = model != null ? model.AddComponent<CharacterRig>()
                : CharacterBuilder.BuildCreature(transform, def.Shape, def.height, def.width, skin, eye, Random.Range(0, int.MaxValue));
            if (model != null) Rig.CollectRenderers();

            // Hitbox for player attacks (the CharacterController also counts, this just makes hits forgiving).
            var hitbox = gameObject.AddComponent<CapsuleCollider>();
            hitbox.isTrigger = true;
            hitbox.radius = def.width * 0.65f;
            hitbox.height = def.height;
            hitbox.center = Vector3.up * def.height * 0.5f;

            if (def.Stealth != StealthKind.None) Hide();
        }

        protected void Hide()
        {
            Current = State.Hidden;
            ambushReady = true;
            if (Def.Stealth == StealthKind.Shadow) Rig.SetVisible(false, true);      // only glowing eyes in the dark
            else if (Def.Stealth == StealthKind.Burrow) Rig.transform.localPosition = Vector3.down * Def.height; // under the floor
        }

        public void Reveal()
        {
            Rig.SetVisible(true, true);
            Rig.transform.localPosition = Vector3.zero;
            if (Def.Stealth == StealthKind.Burrow) Effects.Puff(transform.position, new Color(0.3f, 0.25f, 0.2f), 1.5f, 0.8f);
            Current = State.Chase;
        }

        protected virtual void Update()
        {
            if (Current == State.Dead) return;
            var hero = Player;
            if (hero == null || hero.Stats.Dead) { Rig.MoveSpeed = 0f; ApplyGravity(Vector3.zero); return; }

            Vector3 to = hero.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            stamina = Mathf.Min(Def.stamina, stamina + Time.deltaTime * 8f);

            switch (Current)
            {
                case State.Hidden:
                    // Burrowers creep toward the player underground; shadow-lurkers wait.
                    if (Def.Stealth == StealthKind.Burrow && dist < Def.sightRange) Move(to.normalized * Def.moveSpeed * 0.6f);
                    else ApplyGravity(Vector3.zero);
                    if (dist < Def.revealDistance || (Def.ranged && dist < Def.sightRange)) Reveal();
                    break;

                case State.Idle:
                    ApplyGravity(Vector3.zero);
                    if (dist < Def.sightRange) Current = State.Chase;
                    break;

                case State.Chase:
                    if (Time.time < stunUntil) { ApplyGravity(Vector3.zero); break; }
                    Face(to);
                    if (Def.ranged)
                    {
                        // Keep a distance and throw.
                        Vector3 move = Vector3.zero;
                        if (dist < Def.preferredDistance - 1.5f) move = -to.normalized;
                        else if (dist > Def.preferredDistance + 1.5f) move = to.normalized;
                        move += Vector3.Cross(Vector3.up, to.normalized) * Mathf.Sin(Time.time + GetInstanceID()) * 0.6f;
                        Move(move * Def.moveSpeed);
                        if (dist < Def.attackRange && Time.time >= nextAttack) StartAttack();
                    }
                    else
                    {
                        if (dist > Def.attackRange * 0.8f) Move(to.normalized * Def.moveSpeed * SpeedMultiplier());
                        else ApplyGravity(Vector3.zero);
                        if (dist <= Def.attackRange && Time.time >= nextAttack) StartAttack();
                    }
                    if (dist > Def.sightRange * 2.2f) Current = State.Idle;
                    break;

                case State.Attack:
                    ApplyGravity(Vector3.zero);
                    Face(to);
                    break;

                case State.Recover:
                    // Short creatures dart back after striking, then come again.
                    Move(-to.normalized * Def.moveSpeed * 0.5f);
                    if (Time.time > recoverUntil) Current = State.Chase;
                    break;
            }
        }

        protected virtual float SpeedMultiplier()
        {
            // Enemies tire, but have far more stamina than a human.
            stamina -= Time.deltaTime * 6f;
            return stamina > 10f ? 1f : 0.6f;
        }

        protected void Face(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }

        protected void Move(Vector3 horizontal)
        {
            ApplyGravity(horizontal);
        }

        protected void ApplyGravity(Vector3 horizontal)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            if (!cc.enabled) return;
            verticalSpeed = cc.isGrounded ? -2f : verticalSpeed - 18f * Time.deltaTime;
            cc.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            var flat = horizontal; flat.y = 0f;
            Rig.MoveSpeed = flat.magnitude;
        }

        protected virtual void StartAttack()
        {
            Current = State.Attack;
            nextAttack = Time.time + Def.attackTime;
            Rig.PlayAttack(Def.attackTime * 0.6f);
            Invoke("ResolveAttack", Def.attackTime * 0.35f);
        }

        void ResolveAttack()
        {
            if (Current == State.Dead) return;
            var hero = Player;
            float tierScale = 1f + (Tier - 1) * 0.2f;
            if (hero != null && !hero.Stats.Dead)
            {
                float dmg = Def.damage * tierScale * (ambushReady ? Def.ambushMultiplier : 1f);
                ambushReady = false;
                if (Def.ranged)
                {
                    var origin = transform.position + Vector3.up * Def.height * 0.8f + transform.forward * 0.4f;
                    Projectile.Launch(origin, hero.transform.position + Vector3.up * 1.2f, Def.projectileSpeed, dmg, gameObject);
                }
                else
                {
                    var to = hero.transform.position - transform.position; to.y = 0f;
                    if (to.magnitude <= Def.attackRange + 0.4f && Vector3.Angle(transform.forward, to) < 70f)
                        hero.Stats.TakeDamage(new DamageInfo { Amount = dmg, From = transform.position, Source = gameObject, IsMelee = true });
                }
            }
            Current = State.Recover;
            recoverUntil = Time.time + (Def.ranged ? 0.1f : Random.Range(0.3f, 0.8f));
        }

        public virtual void TakeDamage(DamageInfo info)
        {
            if (Current == State.Dead) return;
            if (Current == State.Hidden) Reveal();
            Health -= info.Amount;
            Rig.PlayHurt();
            Effects.HitSpark(transform.position + Vector3.up * Def.height * 0.6f, new Color(0.6f, 0.05f, 0.05f));
            stunUntil = Time.time + 0.15f;
            if (Current == State.Idle) Current = State.Chase;
            if (Health <= 0f) Die();
        }

        protected virtual void Die()
        {
            Current = State.Dead;
            CancelInvoke();
            Rig.Dead = true;
            Rig.SetVisible(true, true);
            Rig.transform.localPosition = Vector3.zero;
            if (cc != null) cc.enabled = false;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            LootTable.DropFor(this);
            if (Died != null) Died(this);
            Destroy(gameObject, 4f);
        }
    }
}
