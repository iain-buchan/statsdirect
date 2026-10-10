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
    }

    /// <summary>
    /// The R recipes shared with the Mac version (the R folder of the program: analysis-recipes.json maps engine operation names to
    /// recipe files under recipes/, with helpers.R that every script carries).  A recipe is plain base R that reads the run's data and
    /// settings through the helpers; the script writer puts the data and settings before it.
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
        public static string Helpers => Read("helpers.R");

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
