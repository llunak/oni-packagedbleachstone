using HarmonyLib;

namespace PackagedBleachStone
{
    public class Mod : KMod.UserMod2
    {
        public override void OnLoad( Harmony harmony )
        {
            base.OnLoad( harmony );
            LocString.CreateLocStringKeys( typeof( STRINGS.PACKAGEDBLEACHSTONE ));
            GeoTunerConfig_Patch.Patch();
        }
    }
}
