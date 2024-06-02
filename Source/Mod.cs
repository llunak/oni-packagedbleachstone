using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace PackagedBleachSone
{
    public class Mod : KMod.UserMod2
    {
        public override void OnLoad( Harmony harmony )
        {
            base.OnLoad( harmony );
            LocString.CreateLocStringKeys( typeof( STRINGS.PACKAGEDBLEACHSTONE ));
        }
    }
}
