using Hollowmark.Data;
using Hollowmark.Visuals;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>Root component on the player object; holds references to the other player parts.</summary>
    public class Hero : MonoBehaviour
    {
        public static Hero Instance { get; private set; }

        public PlayerStats Stats { get; private set; }
        public PlayerController Controller { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public PlayerVisual Visual { get; private set; }
        public ThirdPersonCamera Cam { get; private set; }

        /// <summary>True while a menu is open; blocks movement and attacks.</summary>
        public bool InputLocked;

        public Interactable Focus { get; private set; }

        public static Hero Create(Camera camera)
        {
            var go = new GameObject("Hero");
            go.tag = "Player";
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            var hero = go.AddComponent<Hero>();
            Instance = hero;
            hero.Stats = go.AddComponent<PlayerStats>();
            hero.Visual = go.AddComponent<PlayerVisual>();
            hero.Controller = go.AddComponent<PlayerController>();
            hero.Combat = go.AddComponent<PlayerCombat>();
            go.AddComponent<InsanityEffects>();

            hero.Cam = camera.gameObject.GetComponent<ThirdPersonCamera>();
            if (hero.Cam == null) hero.Cam = camera.gameObject.AddComponent<ThirdPersonCamera>();
            hero.Cam.Target = go.transform;

            var b = GameDatabase.Balance;
            var inv = hero.Stats.Inventory;
            inv.Gold = b.startGold;
            var start = GameDatabase.Weapon(b.startWeapon);
            if (start != null) { inv.AddWeapon(start); inv.Equipped = start; }
            if (b.startArmor != null)
                foreach (var id in b.startArmor)
                {
                    var def = GameDatabase.ArmorPiece(id);
                    if (def == null) continue;
                    inv.AddArmor(def);
                    inv.Wear(inv.Armor[inv.Armor.Count - 1]);
                }
            if (b.startItems != null)
                foreach (var id in b.startItems) if (GameDatabase.Item(id) != null) inv.AddItem(id);

            hero.Visual.Build();
            return hero;
        }

        void Update()
        {
            if (Stats.Dead) { Focus = null; return; }
            Focus = FindFocus();
            if (InputLocked || Focus == null) return;
            if (GameInput.KeyDown(KeyCode.E)) Focus.Interact(this);
            if (Focus != null && Focus.HoldToUse && GameInput.Key(KeyCode.E)) Focus.HoldTick(this, Time.deltaTime);
        }

        Interactable FindFocus()
        {
            Interactable best = null;
            float bestDist = float.MaxValue;
            var cols = Physics.OverlapSphere(transform.position + Vector3.up, 2.6f, ~0, QueryTriggerInteraction.Collide);
            foreach (var c in cols)
            {
                var it = c.GetComponentInParent<Interactable>();
                if (it == null || !it.enabled || !it.CanUse(this)) continue;
                float d = (it.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { best = it; bestDist = d; }
            }
            return best;
        }

        public bool CanWield(WeaponDef w) { return w != null && Stats.Strength.Level >= w.requiredStrength; }

        public void Equip(WeaponDef w)
        {
            if (w == null) return;
            if (!CanWield(w))
            {
                Say(w.name + " is too heavy! Train with the weights until Strength " + w.requiredStrength + ".");
                return;
            }
            Stats.Inventory.Equipped = w;
            Stats.Inventory.LoadedRounds = 0;
            Stats.Inventory.NotifyChanged();
            Say("Equipped " + w.name + ".");
        }

        public void Say(string message) { if (Stats.OnMessage != null) Stats.OnMessage(message); }
    }
}
