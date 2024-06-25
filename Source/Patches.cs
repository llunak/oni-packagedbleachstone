using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace PackagedBleachStone
{
    // Patch apothecary to make the ingredient storage sealed.
    [HarmonyPatch(typeof(ApothecaryConfig))]
    public class ApothecaryConfig_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ConfigureBuildingTemplate))]
        public static void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            Apothecary fabricator = go.GetComponent< Apothecary >();
            FieldInfo defaultStoredItemModifersField = AccessTools.Field( typeof( Storage ), "defaultStoredItemModifers" );
            List< Storage.StoredItemModifier > storedItemModifiers;
            storedItemModifiers = new List< Storage.StoredItemModifier >(
                ( List< Storage.StoredItemModifier > ) defaultStoredItemModifersField.GetValue( fabricator.inStorage ));
            storedItemModifiers.Add( Storage.StoredItemModifier.Seal );
            fabricator.inStorage.SetDefaultStoredItemModifiers( storedItemModifiers );
            storedItemModifiers = new List< Storage.StoredItemModifier >(
                ( List< Storage.StoredItemModifier > ) defaultStoredItemModifersField.GetValue( fabricator.buildStorage ));
            storedItemModifiers.Add( Storage.StoredItemModifier.Seal );
            fabricator.buildStorage.SetDefaultStoredItemModifiers( storedItemModifiers );
        }
    }

    [HarmonyPatch(typeof(HandSanitizerConfig))]
    public class HandSanitizerConfig_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ConfigureBuildingTemplate))]
        public static void ConfigureBuildingTemplate(GameObject go)
        {
            HandSanitizer handSanitizer = go.GetComponent<HandSanitizer>();
            handSanitizer.massConsumedPerUse /= PackagedBleachStoneConfig.PackageSize;
            // The new item is not an element, so must be replaced by patching.
//            handSanitizer.consumedElement = SimHashes.BleachStone;
            ManualDeliveryKG manualDeliveryKG = go.GetComponent<ManualDeliveryKG>();
            manualDeliveryKG.RequestedItemTag = PackagedBleachStoneConfig.ID.ToTag();
            manualDeliveryKG.capacity /= PackagedBleachStoneConfig.PackageSize;
            manualDeliveryKG.refillMass /= PackagedBleachStoneConfig.PackageSize;
            manualDeliveryKG.MinimumMass /= PackagedBleachStoneConfig.PackageSize;
            go.AddTag( HandSanitizer_Patch.usesPackageTag );
        }
    }

    [HarmonyPatch(typeof(HandSanitizer.SMInstance))]
    public class HandSanitizer_SMInstance_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(HasSufficientMass))]
        public static void HasSufficientMass(ref bool __result, HandSanitizer.SMInstance __instance)
        {
            if( !__instance.HasTag( HandSanitizer_Patch.usesPackageTag ))
                return;
            Storage storage = __instance.GetComponent<Storage>();
            __result = storage.GetMassAvailable(PackagedBleachStoneConfig.ID.ToTag()) >= __instance.master.massConsumedPerUse;
        }
    }

    [HarmonyPatch(typeof(HandSanitizer.Work))]
    public class HandSanitizer_Work_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(OnWorkTick))]
        public static IEnumerable<CodeInstruction> OnWorkTick(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found1 = false;
            int found2 = 0;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Debug.Log("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // The function has code:
                // float massAvailable = component2.GetMassAvailable(component.consumedElement);
                // Change to:
                // float massAvailable = OnWorkTick_Hook1( <the call above>, component, component2 );
                if( codes[ i ].IsLdloc()
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].IsLdloc()
                    && codes[ i + 2 ].opcode == OpCodes.Ldfld && codes[ i + 2 ].operand.ToString() == "SimHashes consumedElement"
                    && codes[ i + 3 ].opcode == OpCodes.Callvirt
                    && codes[ i + 3 ].operand.ToString() == "Single GetMassAvailable(SimHashes)"
                    && codes[ i + 4 ].IsStloc())
                {
                    // The return value from the call is now on the stack.
                    codes.Insert( i + 4, codes[ i + 1 ].Clone()); // load 'component2'
                    codes.Insert( i + 5, codes[ i ].Clone()); // load 'component'
                    codes.Insert( i + 6, new CodeInstruction( OpCodes.Call,
                        typeof( HandSanitizer_Work_Patch ).GetMethod( nameof( OnWorkTick_Hook1 ))));
                    found1 = true;
                }
                // The function has code:
                // ElementLoader.FindElementByHash(component.consumedElement).tag
                // Change to:
                // OnWorkTick_Hook2( <the call above>, component )
                if( codes[ i ].IsLdloc()
                    && i + 3 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldfld && codes[ i + 1 ].operand.ToString() == "SimHashes consumedElement"
                    && codes[ i + 2 ].opcode == OpCodes.Callvirt
                    && codes[ i + 2 ].operand.ToString() == "Element FindElementByHash(SimHashes)"
                    && codes[ i + 3 ].opcode == OpCodes.Ldfld
                    && codes[ i + 3 ].operand.ToString() == "tag" )
                {
                    // The value of the expression is now on the stack.
                    codes.Insert( i + 4, codes[ i ].Clone()); // load 'component'
                    codes.Insert( i + 5, new CodeInstruction( OpCodes.Call,
                        typeof( HandSanitizer_Work_Patch ).GetMethod( nameof( OnWorkTick_Hook2 ))));
                    ++found2;
                }
            }
            if( !found1 || found2 == 0 )
                Debug.LogWarning("PackagedBleachStone: Failed to patch HandSanitizer.Work.OnWorkTick()");
            return codes;
        }

        public static float OnWorkTick_Hook1( float massAvailable, HandSanitizer instance, Storage storage )
        {
            if( !instance.HasTag( HandSanitizer_Patch.usesPackageTag ))
                return massAvailable;
            return storage.GetMassAvailable(PackagedBleachStoneConfig.ID.ToTag());
        }

        public static Tag OnWorkTick_Hook2( Tag tag, HandSanitizer instance )
        {
            if( !instance.HasTag( HandSanitizer_Patch.usesPackageTag ))
                return tag;
            return PackagedBleachStoneConfig.ID.ToTag();
        }
    }

    [HarmonyPatch(typeof(HandSanitizer))]
    public class HandSanitizer_Patch
    {
        public static readonly Tag usesPackageTag = "llunak.HandSanitizerUsesPackage";

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(RefreshMeters))]
        public static IEnumerable<CodeInstruction> RefreshMeters(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Debug.Log("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // The function has code:
                // PrimaryElement primaryElement = GetComponent<Storage>().FindPrimaryElement(consumedElement);
                // Append:
                // PrimaryElement primaryElement = RefreshMeters_Hook( <the call above>, this );
                if( codes[ i ].opcode == OpCodes.Ldfld && codes[ i ].operand.ToString() == "SimHashes consumedElement"
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Callvirt
                    && codes[ i + 1 ].operand.ToString() == "PrimaryElement FindPrimaryElement(SimHashes)"
                    && codes[ i + 2 ].IsStloc())
                {
                    // The return value from the call is now on the stack.
                    codes.Insert( i + 2, new CodeInstruction( OpCodes.Ldarg_0 )); // load 'this'
                    codes.Insert( i + 3, new CodeInstruction( OpCodes.Call,
                        typeof( HandSanitizer_Patch ).GetMethod( nameof( RefreshMeters_Hook ))));
                    found = true;
                    break;
                }
            }
            if( !found )
                Debug.LogWarning("PackagedBleachStone: Failed to patch HandSanitizer.RefreshMeters()");
            return codes;
        }

        public static PrimaryElement RefreshMeters_Hook( PrimaryElement primaryElement, HandSanitizer instance )
        {
            if( !instance.HasTag( HandSanitizer_Patch.usesPackageTag ))
                return primaryElement;
            GameObject go = instance.GetComponent< Storage >().Find( PackagedBleachStoneConfig.ID.GetHashCode());
            return go?.GetComponent< Pickupable >()?.PrimaryElement;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(RequirementDescriptors))]
        public static bool RequirementDescriptors(ref List<Descriptor> __result, HandSanitizer __instance)
        {
            if( !__instance.HasTag( usesPackageTag ))
                return true;
            __result = new List<Descriptor>
            {
                new Descriptor(string.Format(STRINGS.UI.BUILDINGEFFECTS.ELEMENTCONSUMEDPERUSE,
                    STRINGS.PACKAGEDBLEACHSTONE.NAME,
                    GameUtil.GetFormattedUnits(__instance.massConsumedPerUse, floatFormatOverride : "{0:0.####}")),
                    string.Format(STRINGS.UI.BUILDINGEFFECTS.TOOLTIPS.ELEMENTCONSUMEDPERUSE,
                    STRINGS.PACKAGEDBLEACHSTONE.NAME,
                    GameUtil.GetFormattedUnits(__instance.massConsumedPerUse, floatFormatOverride : "{0:0.####}")),
                    Descriptor.DescriptorType.Requirement)
            };
            return false;
        }
    }

    [HarmonyPatch(typeof(HotTubConfig))]
    public class HotTubConfig_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ConfigureBuildingTemplate))]
        public static void ConfigureBuildingTemplate(GameObject go)
        {
            ManualDeliveryKG manualDeliveryKG = go.GetComponent<ManualDeliveryKG>();
            manualDeliveryKG.RequestedItemTag = PackagedBleachStoneConfig.ID.ToTag();
            manualDeliveryKG.capacity /= PackagedBleachStoneConfig.PackageSize;
            manualDeliveryKG.refillMass /= PackagedBleachStoneConfig.PackageSize;
            manualDeliveryKG.MinimumMass /= PackagedBleachStoneConfig.PackageSize;
            HotTub hotTub = go.GetComponent<HotTub>();
            hotTub.bleachStoneConsumption /= PackagedBleachStoneConfig.PackageSize;
        }
    }

    [HarmonyPatch(typeof(HotTub.StatesInstance))]
    public class HotTub_StatesInstance_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(HasBleachStone))]
        public static bool HasBleachStone(ref bool __result, HotTub.StatesInstance __instance)
        {
            __result = __instance.master.waterStorage.GetMassAvailable(
                PackagedBleachStoneConfig.ID.ToTag()) > 0;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ConsumeBleachstone))]
        public static bool ConsumeBleachstone(HotTub.StatesInstance __instance, float dt)
        {
            __instance.master.waterStorage.ConsumeIgnoringDisease(
                PackagedBleachStoneConfig.ID.ToTag(), __instance.master.bleachStoneConsumption * dt);
            return false;
        }
    }

    public class GeoTunerConfig_Patch
    {
        // The static variable is set up in the class constructor, which cannot be patched.
        // Change it manually for mod setup.
        public static void Patch()
        {
            // GeotunedGeyserSettings a struct, so it is copied by value. Create a new one and
            // replace the old one everywhere.
            GeoTunerConfig.Category[] keys1 = GeoTunerConfig.CategorySettings.Keys.ToArray();
            foreach( var key in keys1 )
            {
                var setting = GeoTunerConfig.CategorySettings[ key ];
                if( setting.material != SimHashes.BleachStone.CreateTag())
                    continue;
                setting.material = PackagedBleachStoneConfig.ID.ToTag();
                setting.quantity /= PackagedBleachStoneConfig.PackageSize;
                GeoTunerConfig.CategorySettings[ key ] = setting;
            }
            HashedString[] keys2 = GeoTunerConfig.geotunerGeyserSettings.Keys.ToArray();
            foreach( var key in keys2 )
            {
                var setting = GeoTunerConfig.geotunerGeyserSettings[ key ];
                if( setting.material != SimHashes.BleachStone.CreateTag())
                    continue;
                setting.material = PackagedBleachStoneConfig.ID.ToTag();
                setting.quantity /= PackagedBleachStoneConfig.PackageSize;
                GeoTunerConfig.geotunerGeyserSettings[ key ] = setting;
            }
        }
    }

    [HarmonyPatch(typeof(GeoTuner))]
    public class GeoTuner_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(WorkRequirementsMet))]
        public static bool WorkRequirementsMet(ref bool __result, GeoTuner.Instance smi)
        {
            if( smi.manualDelivery.RequestedItemTag != PackagedBleachStoneConfig.ID.ToTag())
                return true;
            __result = false;
            // With the packs capacityKg is actually units.
            if(GeoTuner.IsInLabRoom(smi))
                if( smi.storage.UnitsStored() == smi.storage.capacityKg )
                    __result = true;
            return false;
        }

        // Patching the class causes it to be initialized, i.e. its cctor is called.
        // And that's where the liquidGeyserTuningSoundPath etc. fields are initialized,
        // but doing that already here is apparently too soon and GlobalAssets is not ready yet,
        // causing the field to be null. So patch the only function where they are used
        // and make sure the values are valid.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TriggerSoundsForGeyserChange))]
        public static void TriggerSoundsForGeyserChange()
        {
            if( GeoTuner.liquidGeyserTuningSoundPath != null )
                return;
            GeoTuner.liquidGeyserTuningSoundPath = GlobalAssets.GetSound("GeoTuner_Tuning_Geyser");
            GeoTuner.gasGeyserTuningSoundPath = GlobalAssets.GetSound("GeoTuner_Tuning_Vent");
            GeoTuner.metalGeyserTuningSoundPath = GlobalAssets.GetSound("GeoTuner_Tuning_Volcano");
        }
    }
}
