using System.Collections;
using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Enemies
{
    /// <summary>
    /// A boss: much taller than a 2 m man, with heavy swings, a telegraphed ground slam,
    /// summoned minions at 66% and 33% health, and an enrage phase.
    /// All values come from bosses.json, so every dungeon's boss uses this one script.
    /// </summary>
    public class BossAI : EnemyAI
    {
        public BossDef Boss;
        public string DungeonId;
        float nextSlam;
        bool summoned66, summoned33, enraged, slamming;

        public static BossAI SpawnBoss(BossDef boss, Vector3 position, string dungeonId, int tier, Transform parent)
        {
            var go = new GameObject(boss.name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;

            var cc = go.AddComponent<CharacterController>();
            cc.height = boss.height;
            cc.radius = boss.width * 0.45f;
            cc.center = Vector3.up * boss.height * 0.5f;
            cc.stepOffset = 0.5f;

            var ai = go.AddComponent<BossAI>();
            ai.Boss = boss;
            ai.DungeonId = dungeonId;
            // Bosses reuse the enemy pipeline through a generated EnemyDef.
            var def = new EnemyDef
            {
                id = boss.id, name = boss.name, description = boss.description, bodyShape = boss.bodyShape,
                height = boss.height, width = boss.width, moveSpeed = boss.moveSpeed, health = boss.health,
                damage = boss.damage, attackTime = boss.attackTime, attackRange = boss.attackRange,
                sightRange = 30f, stamina = 9999f, stealth = "None", strangeWeapon = boss.weaponName,
                skinColors = new[] { boss.color }, eyeColor = boss.eyeColor, goldMin = boss.rewardGold, goldMax = boss.rewardGold,
                weaponDropChance = 0f, armorDropChance = 0f, itemDropChance = 1f,
                itemDrops = new[] { "physic", "salt_pork", "water_flask" }, model = boss.model,
            };
            def.Init();
            ai.Setup(def, 1);
            ai.nextSlam = Time.time + 6f;
            ai.Current = State.Idle;

            // A brutal placeholder weapon in the boss's hand.
            if (ai.Rig.Hand != null)
                Prim.Make(PrimitiveType.Cube, ai.Rig.Hand, new Vector3(0f, 0f, boss.height * 0.25f),
                    new Vector3(boss.width * 0.25f, boss.width * 0.2f, boss.height * 0.5f), new Color(0.3f, 0.28f, 0.26f), "BossWeapon");
            ai.Rig.CollectRenderers();
            return ai;
        }

        protected override float SpeedMultiplier() { return enraged ? 1.45f : 1f; }

        protected override void Update()
        {
            if (Current == State.Dead || slamming) return;
            base.Update();
            if (Current == State.Chase && Time.time >= nextSlam && Player != null)
            {
                float dist = Vector3.Distance(Player.transform.position, transform.position);
                if (dist < Boss.slamRadius + 4f) StartCoroutine(Slam());
            }
        }

        IEnumerator Slam()
        {
            slamming = true;
            nextSlam = Time.time + Boss.slamCooldown * (enraged ? 0.6f : 1f);
            Rig.MoveSpeed = 0f;
            Vector3 center = transform.position + transform.forward * Boss.slamRadius * 0.4f;
            Effects.Warning(center, Boss.slamRadius, 1.2f);
            Rig.PlayAttack(1.2f);
            yield return new WaitForSeconds(1.1f);
            if (Current == State.Dead) yield break;
            Effects.Puff(center, new Color(0.4f, 0.35f, 0.3f), Boss.slamRadius, 0.6f);
            if (Player != null)
            {
                Player.Cam.Shake(2.5f);
                var flat = Player.transform.position - center; flat.y = 0f;
                if (flat.magnitude < Boss.slamRadius)
                    Player.Stats.TakeDamage(new DamageInfo { Amount = Boss.slamDamage, From = center, Source = gameObject });
            }
            yield return new WaitForSeconds(0.4f);
            slamming = false;
        }

        public override void TakeDamage(DamageInfo info)
        {
            base.TakeDamage(info);
            if (Current == State.Dead) return;
            float frac = Health / MaxHealth;
            if (!summoned66 && frac < 0.66f) { summoned66 = true; Summon(); }
            if (!summoned33 && frac < 0.33f) { summoned33 = true; Summon(); }
            if (!enraged && frac < Boss.enrageThreshold)
            {
                enraged = true;
                if (Player != null) Player.Say(Boss.name + " is enraged!");
            }
        }

        void Summon()
        {
            var def = GameDatabase.Enemy(Boss.summonEnemyId);
            if (def == null) return;
            if (Player != null) Player.Say(Boss.name + " calls for help!");
            for (int i = 0; i < Boss.summonCount; i++)
            {
                var offset = Quaternion.Euler(0f, i * 360f / Boss.summonCount, 0f) * Vector3.forward * 3f;
                var e = Spawn(def, transform.position + offset + Vector3.up * 0.5f, 1, transform.parent);
                e.Reveal();
            }
        }

        protected override void Die()
        {
            base.Die();
            if (GameManager.Instance != null) GameManager.Instance.OnBossDefeated(this);
        }
    }
}
