using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VU1WPF.Tests;

public sealed class AutomationIdUniquenessTests
{
    private static readonly string[] XamlFiles =
    {
        "MainWindow.xaml",
        "AboutWindow.xaml",
        "ThresholdsWindow.xaml",
        "SetColorWindow.xaml",
    };

    [Fact]
    public void AutomationIds_AreUniqueAcrossTargetedXamlFiles()
    {
        string vu1wpfRoot = ResolveVu1WpfRoot();

        var duplicates = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string xamlFile in XamlFiles)
        {
            string filePath = Path.Combine(vu1wpfRoot, xamlFile);
            Assert.True(File.Exists(filePath), $"Expected XAML file to exist: {filePath}");

            string content = File.ReadAllText(filePath);
            foreach (Match match in Regex.Matches(content, "AutomationProperties\\.AutomationId=\"([^\"]+)\""))
            {
                string id = match.Groups[1].Value;
                Assert.False(string.IsNullOrWhiteSpace(id), $"AutomationId in {xamlFile} must be non-empty.");

                if (seen.TryGetValue(id, out string? firstFile))
                {
                    if (!duplicates.TryGetValue(id, out List<string>? files))
                    {
                        files = new List<string> { firstFile };
                        duplicates[id] = files;
                    }

                    if (!files.Contains(xamlFile, StringComparer.Ordinal))
                    {
                        files.Add(xamlFile);
                    }
                }
                else
                {
                    seen[id] = xamlFile;
                }
            }
        }

        string duplicateSummary = string.Join(
            Environment.NewLine,
            duplicates.Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}"));

        Assert.True(duplicates.Count == 0, $"Duplicate AutomationIds detected:{Environment.NewLine}{duplicateSummary}");
        Assert.True(seen.Count > 0, "Expected at least one AutomationId declaration in targeted XAML files.");
    }

    private static string ResolveVu1WpfRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "VU1WPF");
            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(candidate, "MainWindow.xaml"))
                && File.Exists(Path.Combine(candidate, "VU1WPF.csproj")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve VU1WPF root for AutomationId uniqueness test.");
    }
}
