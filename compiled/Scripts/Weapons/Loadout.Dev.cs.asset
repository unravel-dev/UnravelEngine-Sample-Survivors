namespace Survivors
{
    /// <summary>
    /// Dev hooks of the loadout for effect, performance and card reviews (the run's Dev* calls forward here). Not used
    /// by the game itself.
    /// </summary>
    public sealed partial class Loadout
    {
        /// <summary>Dev (card reviews): level-ups offer only boons.</summary>
        public bool devForceBoons;

        /// <summary>Dev (effect reviews): the class active now, whatever its cooldown.</summary>
        public bool DevActivate() => kit.DevActivate();

        /// <summary>Dev (effect reviews): fills the class signature meter.</summary>
        public void DevFillSignature() => kit.DevFillSignature();

        /// <summary>Dev (performance reviews): adds a weapon at a rank, evolved when asked, while a slot is free.</summary>
        public bool DevAddWeapon(string id, int rank, bool evolve)
        {
            WeaponDef def = WeaponCatalog.FindWeapon(id);
            if (def == null || weaponCount >= WEAPON_SLOTS || FindWeapon(id) != null)
            {
                return false;
            }
            AddWeapon(def);
            SetDevRank(weaponObjects[weaponCount - 1], rank, evolve);
            OnItemsChanged();
            return true;
        }

        /// <summary>Dev (effect reviews): the loadout becomes this one weapon at a rank, evolved when asked.</summary>
        public bool DevSetOnlyWeapon(string id, int rank, bool evolve)
        {
            WeaponDef def = WeaponCatalog.FindWeapon(id);
            if (def == null)
            {
                return false;
            }
            DisposeWeapons();
            weaponCount = 0;
            context.zones.Clear();
            AddWeapon(def);
            SetDevRank(weaponObjects[0], rank, evolve);
            OnItemsChanged();
            return true;
        }

        private static void SetDevRank(Weapon weapon, int rank, bool evolve)
        {
            weapon.SetRank(rank);
            if (evolve)
            {
                weapon.Evolve();
            }
        }
    }
}
