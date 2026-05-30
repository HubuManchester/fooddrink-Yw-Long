using System.Text.Json.Serialization;

namespace WhatToEat.Models
{
    /// <summary>
    /// Full recipe detail returned by Spoonacular /recipes/{id}/information
    /// </summary>
    public class RecipeDetail
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public int ReadyInMinutes { get; set; }
        public int Servings { get; set; }
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("extendedIngredients")]
        public List<SpoonIngredient> ExtendedIngredients { get; set; } = new();

        [JsonPropertyName("analyzedInstructions")]
        public List<SpoonInstructionGroup> AnalyzedInstructions { get; set; } = new();

        /// <summary>Flattened steps across all instruction groups</summary>
        public List<SpoonStep> Steps =>
            AnalyzedInstructions.SelectMany(g => g.Steps).ToList();

        /// <summary>Plain text summary (strips HTML tags from Spoonacular response)</summary>
        public string PlainSummary => System.Text.RegularExpressions.Regex
            .Replace(Summary, "<.*?>", string.Empty);
    }

    public class SpoonIngredient
    {
        public string Original { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class SpoonInstructionGroup
    {
        public List<SpoonStep> Steps { get; set; } = new();
    }

    public class SpoonStep
    {
        public int Number { get; set; }
        public string Step { get; set; } = string.Empty;
    }
}