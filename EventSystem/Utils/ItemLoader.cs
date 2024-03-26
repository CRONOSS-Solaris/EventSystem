using NLog;
using Sandbox.Definitions;
using System.Collections.Generic;

namespace EventSystem.Utils
{
    public static class ItemLoader
    {
        // Przyjmij jako argument słowniki z klasy VoteRewards
        public static void LoadAvailableItemTypesAndSubtypes(List<string> availableItemTypes, Dictionary<string, List<string>> availableItemSubtypes, Logger log, EventSystemConfig config)
        {
            foreach (var definition in MyDefinitionManager.Static.GetPhysicalItemDefinitions())
            {
                string typeId = definition.Id.TypeId.ToString();
                string subtypeId = definition.Id.SubtypeId.String;

                LoggerHelper.DebugLog(log, config, $"ITEM(): Loading item: TypeId={typeId}, SubtypeId={subtypeId}");

                if (!availableItemTypes.Contains(typeId))
                {
                    availableItemTypes.Add(typeId);
                }

                if (!availableItemSubtypes.ContainsKey(typeId))
                {
                    availableItemSubtypes[typeId] = new List<string>();
                }

                if (!availableItemSubtypes[typeId].Contains(subtypeId))
                {
                    availableItemSubtypes[typeId].Add(subtypeId);
                }
            }

            // Upewnij się, że każdy typ przedmiotu ma przypisaną listę, nawet jeśli jest pusta
            foreach (var itemType in availableItemTypes)
            {
                if (!availableItemSubtypes.ContainsKey(itemType))
                {
                    availableItemSubtypes[itemType] = new List<string>();
                }
            }

            LoggerHelper.DebugLog(log, config, $"ITEM(): Loaded {availableItemTypes.Count} item types and {availableItemSubtypes.Count} item subtypes.");
            foreach (var kvp in availableItemSubtypes)
            {
                LoggerHelper.DebugLog(log, config, $"ITEM(): Type: {kvp.Key}, Subtypes: {string.Join(", ", kvp.Value)}");
            }
        }
    }
}
