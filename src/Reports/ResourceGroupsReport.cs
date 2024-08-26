using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Azure.ResourceManager;

namespace AzureReporting.Reports;

public static class ResourceGroupsReport
{
    public static void Run(ArmClient client, DirectoryInfo targetDirectory)
    {
        try
        {
            RunInternal(client, targetDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static void RunInternal(ArmClient client,  DirectoryInfo targetDirectory)
    {
        var result = new List<ResourceGroup>();

        Console.WriteLine("Building resource groups report ...");
        Console.WriteLine("\tLoading subscriptions...");
        var subscriptions = client.GetSubscriptions().ToList();

        foreach (var subscription in subscriptions)
        {
            Console.WriteLine($"\tWorking on subscription '{subscription.Data.DisplayName}' ...");

            Console.Write("\t\tLoading resources groups ...");
            var resourceGroups = subscription.GetResourceGroups().ToList();
            foreach (var resourceGroup in resourceGroups)
            {
                result.Add(new ResourceGroup
                {
                    Name = resourceGroup.Data.Name,
                    Subscription = subscription.Data.DisplayName,
                    ResourcesCount = resourceGroup.GetGenericResources().Count()
                });
            }

            Console.WriteLine($" {resourceGroups.Count} resource groups found.");
        }

        WriteToFile(targetDirectory, result);
    }

    private static void WriteToFile(DirectoryInfo targetDirectory, List<ResourceGroup> resourceGroups)
    {
        var targetFile = Path.Combine(targetDirectory.FullName, "ResourceGroups.html");
        Console.WriteLine($"\tWriting HTML to file '{targetFile}' ...");

        var sb = new StringBuilder();
        sb.BeginHtml();
        sb.Hero("Resource Groups");
        sb.BeginTable();
        sb.Thead("Name", "Subscription", "Resources");
        sb.BeginTbody();

        foreach (var r in resourceGroups)
        {
            sb.BeginTr();
            sb.Td(r.Name);
            sb.Td(r.Subscription);
            sb.Td(r.ResourcesCount.ToString("N0", CultureInfo.InvariantCulture), r.ResourcesCount <= 0);
            sb.EndTr();
        }

        sb.EndTbody();
        sb.EndTable();
        sb.EndHtml();

        File.WriteAllText(targetFile, sb.ToString());
    }

    private sealed class ResourceGroup
    {
        public string Name { get; init; } = "";
        public string Subscription { get; init; } = "";
        public int ResourcesCount { get; init; }
    }
}
