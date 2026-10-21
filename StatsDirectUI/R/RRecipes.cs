using StatsDirect.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StatsDirect.R
{
    /// <summary>A recipe of the manifest: the R file that reproduces an analysis, and what it says of itself.</summary>
    public sealed class RRecipe
    {
        public string File { get; set; }
        public string Detail { get; set; }
        public Dictionary<string, string> ParentFiles { get; set; }
        /// <summary>A chart follow-on's plots section, appended after the recipe: the plots the program draws after its parent's analysis.</summary>
        public string After { get; set; }
    }

    /// <summary>
    /// The R recipes shared with the Mac version (the R folder of the program: analysis-recipes.json maps engine operation names to
    /// recipe files under recipes/).  A recipe is the analysis as the help's R code shows it, with a header naming the variables it
    /// takes; the script writer puts the run's data and settings before it under those names.
    /// </summary>
    public static class RRecipes
    {
        private static string folder;
        private static Dictionary<string, RRecipe> manifest;

        /// <summary>The program's R folder; the checks point it at the repository.</summary>
        public static string Folder
        {
            get => folder ?? Path.Combine(SDConfiguration.InstallationDirectory, "R");
            set { folder = value; manifest = null; }
        }

        private static Dictionary<string, RRecipe> Manifest
        {
            get
            {
                if (manifest == null)
                {
                    string path = Path.Combine(Folder, "analysis-recipes.json");
                    manifest = System.IO.File.Exists(path)
                        ? JsonSerializer.Deserialize<Dictionary<string, RRecipe>>(System.IO.File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
                        : new();
                }
                return manifest;
            }
        }

        public static IEnumerable<string> Operations => Manifest.Keys;
        public static bool Has(string operation) => !string.IsNullOrEmpty(operation) && Manifest.ContainsKey(operation);
        public static RRecipe Find(string operation) => Has(operation) ? Manifest[operation] : null;

        /// <summary>Shared follow-ons select the recipe of the analysis actually continued, rather than their first possible prerequisite.</summary>
        public static RRecipe Find(JsonElement record)
        {
            string operation = RScriptWriter.Text(record, "operation") ?? "";
            RRecipe recipe = Find(operation);
            if (recipe != null && operation == "BoxWhiskerPlot" &&
                record.TryGetProperty("parameters", out JsonElement boxParameters) &&
                boxParameters.TryGetProperty("boxWhisker", out JsonElement box) && box.ValueKind == JsonValueKind.Object &&
                box.TryGetProperty("method", out JsonElement method) && method.ValueKind == JsonValueKind.Number && method.TryGetInt32(out int selected) && selected == 7)
                return new RRecipe { File = "box-whisker-tukey-plot.R", Detail = "Tukey hinges box plot using R's standard boxplot()." };
            if (recipe?.ParentFiles == null || recipe.ParentFiles.Count == 0) return recipe;
            string parent = RScriptWriter.Text(record, "rootOperation");
            if (string.IsNullOrEmpty(parent) && record.TryGetProperty("parameters", out JsonElement parameters))
            {
                // Legacy records already retain the explicit linearized model choice, while simple regression has none.
                if (operation == "Ranv") parent = parameters.TryGetProperty("model", out _) ? "LinearizedEstimates" : "SimpleLinearRegression";
                else if (parameters.TryGetProperty("doseResponseModel", out JsonElement model) && model.ValueKind == JsonValueKind.String)
                    parent = model.GetString() == "probit" ? "Probit" : model.GetString() == "logit" ? "Logit" : null;
            }
            return parent != null && recipe.ParentFiles.TryGetValue(parent, out string file)
                ? new RRecipe { File = file, Detail = recipe.Detail, After = recipe.After } : null;
        }

        /// <summary>A recipe file: a header naming the variables it takes, then the analysis as the help shows it in R.</summary>
        public static string Recipe(string file)
        {
            if (string.IsNullOrEmpty(file) || file.Contains('/') || file.Contains('\\') || !file.EndsWith(".R", StringComparison.Ordinal))
                throw new ArgumentException("Not a recipe file: " + file);
            return Read(file);
        }

        private static string Read(string file)
        {
            string path = Path.Combine(Folder, "recipes", file);
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Replace("\r\n", "\n") : "# (" + file + " is not in the program's R folder)\n";
        }
    }
}
