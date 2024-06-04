using UnityEngine;
using System.Collections.Generic;

namespace PackagedBleachStone
{
    public class PackagedBleachStoneConfig : IEntityConfig
    {
        public const string ID = "llunak.PackagedBleachStone";

        public const float PackageSize = 5; // 5kg in one item

        public static ComplexRecipe recipe;

        public string[] GetDlcIds()
        {
            return DlcManager.AVAILABLE_ALL_VERSIONS;
        }

        public GameObject CreatePrefab()
        {
            GameObject template = EntityTemplates.CreateLooseEntity(ID, STRINGS.PACKAGEDBLEACHSTONE.NAME,
                STRINGS.PACKAGEDBLEACHSTONE.DESC, 1f, unitMass: true, Assets.GetAnim("llunak_packagedbleachstone_kanim"),
                "object", Grid.SceneLayer.Front, EntityTemplates.CollisionShape.RECTANGLE, 0.8f, 0.4f, isPickupable: true,
                element : SimHashes.BleachStone);
            ComplexRecipe.RecipeElement[] array = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement(SimHashes.BleachStone.CreateTag(), PackageSize)
            };
            ComplexRecipe.RecipeElement[] array2 = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement(ID, 1f)
            };
            string text = "Apothecary";
            recipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID(text, array, array2), array, array2)
            {
                time = 10f,
                description = STRINGS.PACKAGEDBLEACHSTONE.RECIPEDESC,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag> { text },
                sortOrder = 50,
                requiredTech = "MedicineII"
            };
            template.AddOrGet<EntitySplitter>();
            template.AddTag(GameTags.ConsumableOre);
            template.AddTag(ID.ToTag()); // For Storage to find it by tag.
            return template;
        }

        public void OnPrefabInit(GameObject inst)
        {
        }

        public void OnSpawn(GameObject inst)
        {
        }
    }
}
