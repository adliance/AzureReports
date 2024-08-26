using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;

namespace AzureReporting.Reports;

public static class AppServicePlanReport
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

    private static void RunInternal(ArmClient client, DirectoryInfo targetDirectory)
    {
        var result = new List<AppServicePlan>();

        Console.WriteLine("Building app service plans report ...");
        Console.WriteLine("\tLoading subscriptions...");
        var subscriptions = client.GetSubscriptions().ToList();

        foreach (var subscription in subscriptions)
        {
            Console.WriteLine($"\tWorking on subscription '{subscription.Data.DisplayName}' ...");

            Console.WriteLine("\t\tLoading app services plans ...");
            var appServicePlans = subscription.GetAppServicePlans().ToList();
            foreach (var appServicePlan in appServicePlans)
            {
                var appServices = appServicePlan.GetWebApps().ToList();
                result.Add(new AppServicePlan
                {
                    Name = appServicePlan.Data.Name,
                    Region = appServicePlan.Data.GeoRegion,
                    Subscription = subscription.Data.DisplayName,
                    RunningAppServicesCount = appServices.Count(x => x.State == "Running"),
                    StoppedAppServicesCount = appServices.Count(x => x.State != "Running")
                });
            }
        }

        WriteToFile(targetDirectory, result);
    }

    private static void WriteToFile(DirectoryInfo targetDirectory, List<AppServicePlan> appServices)
    {
        var targetFile = Path.Combine(targetDirectory.FullName, "AppServicePlans.html");
        Console.WriteLine($"\tWriting HTML to file '{targetFile}' ...");

        var sb = new StringBuilder();
        sb.BeginHtml();
        sb.Hero("App Service Plans");
        sb.BeginTable();
        sb.Thead("Name", "Subscription", "Region", "App Services (Running)", "App Services (Stopped)");
        sb.BeginTbody();

        foreach (var a in appServices)
        {
            sb.BeginTr();
            sb.Td(a.Name);
            sb.Td(a.Subscription);
            sb.Td(a.Region);
            sb.Td(a.RunningAppServicesCount.ToString("N0", CultureInfo.InvariantCulture), a.RunningAppServicesCount <= 0);
            sb.Td(a.StoppedAppServicesCount.ToString("N0", CultureInfo.InvariantCulture));
            sb.EndTr();
        }

        sb.EndTbody();
        sb.EndTable();
        sb.EndHtml();

        File.WriteAllText(targetFile, sb.ToString());
    }

    private sealed class AppServicePlan
    {
        public string Name { get; init; } = "";
        public string Subscription { get; init; } = "";
        public string Region { get; init; } = "";
        public int RunningAppServicesCount { get; init; }
        public int StoppedAppServicesCount { get; init; }
    }
}
