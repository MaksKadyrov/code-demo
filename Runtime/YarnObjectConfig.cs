using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UGI.CoreUtils;
using UGI.CSVLoading;
using UGI.Puzzle.Validation;
using UGI.Serialization;
using UGI.Structures;
using UGI.Validation;
using VContainer;
using ValidationResult = UGI.Validation.ValidationResult;

namespace UGI.Puzzle
{
    [Serializable]
    [KnownSerializedType("yarn_object")]
    public class YarnObjectConfig : ICsvParser, IValidatable<YarnObjectConfig>
    {
        [KnownSerializedType("yarn_object_collection")]
        public class Collection : List<YarnObjectConfig>{}
        
        [JsonProperty("object_id")] public string Id { get; private set; }
        [JsonProperty("parts")] public ObjectPartsModel Parts { get; private set; }

        public int CalculateMovesCount()
        {
            return Parts.Values.Sum(p => p.StackCount + 1);
        }
        
        public async Task<object> ParseSheet(DataTable<string> dataTable, CsvSheetInfo csvTableInfo)
        {
            var sheetData = new SheetData(dataTable);
            var result = new Dictionary<string, YarnObjectConfig>();
            
            foreach (var row in sheetData.GetNamedRows())
            {
                var objectId = row["object_id"];

                if (!result.TryGetValue(objectId, out var obj))
                {
                    result.Add(objectId, obj = new YarnObjectConfig()
                    {
                        Id = objectId,
                        Parts = new()
                    });
                }

                var newPart = row.CreateMapped<YarnPartModel>();
                obj.Parts.Add(newPart.Id, newPart);
            }
            
            var collection = new Collection();
            collection.AddRange(result.Values);

            foreach (var objectConfig in collection)
            {
                (await Validate(objectConfig)).Assert();
            }
            
            new ValidateColorExists().Validate(collection).Assert();
            return collection;
        }

        public async Task<ValidationResult> Validate(YarnObjectConfig instance)
        {
            var containerBuilder = new ContainerBuilder();
            var configs = new ConfigsContainer(containerBuilder);
            var container = containerBuilder.Build();
            await configs.LoadAll();
            
            var result = new CompositeValidationResult();
            
            var movesCount = instance.CalculateMovesCount();
            var overflow = movesCount % Box.SlotsCount;
            if (overflow != 0)
            {
                result.Add(ValidationResult.CreateError($"Object {instance.Id} have invalid moves count: {movesCount} with overflow {overflow}."));
            }
            
            var levelConfigs = container.Resolve<LevelConfig.Collection>();

            if (levelConfigs == null) return ValidationResult.Success;

            var yarnLevel = levelConfigs.Values.FirstOrDefault(pair => pair.ObjectId == instance.Id);
            if (yarnLevel == null) return ValidationResult.Success;

            foreach (var partConfig in instance.Parts.Values)
            {
                if (partConfig.Color.IsNullOrEmpty()) continue;
                if (yarnLevel.ColorsPool.Contains(partConfig.Color)) continue;
                
                result.Add(ValidationResult.CreateError($"Object {instance.Id} have part {partConfig.Id} with color {partConfig.Color} not available in level {yarnLevel.Number} pool."));
            }
            
            return result;
        }
    }
}