using HarmonyLib;

namespace Taum
{
    /// <summary>
    /// Everything that has to be thrown away when the world changes.
    ///
    /// A world load - including logging out to the menu and back in - destroys ZNetScene and
    /// ObjectDB and builds new ones. Anything this mod cached off the old ones is then a
    /// reference to a destroyed Unity object, and a destroyed object does not behave like
    /// null: it compares equal to null through Unity's operator overload while still being a
    /// non-null reference, so it survives the wrong kind of check and fails later somewhere
    /// unrelated. A borrowed material that has been through this draws magenta.
    ///
    /// Both entry points are needed and they are not the same path. A local world comes in
    /// through Awake. A client joining a server is handed the host's item list through
    /// CopyOtherDB and never sees a second Awake, so a mod that hooks only the first is
    /// correct alone and stale in multiplayer - which is the harder half to notice.
    /// </summary>
    [HarmonyPatch]
    internal static class Worlds
    {
        [HarmonyPostfix]
        // Named by string, not nameof: ObjectDB.Awake is private, and nameof cannot see it.
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static void Awake()
        {
            Forget();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void CopyOtherDB()
        {
            Forget();
        }

        private static void Forget()
        {
            Skins.Invalidate();
            PropIndex.Forget();
            WornHalter.Forget();
        }
    }
}
