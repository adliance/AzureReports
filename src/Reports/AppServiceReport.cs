using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;

namespace AzureReporting.Reports;

public static class AppServiceReport
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
        var result = new List<AppService>();

        Console.WriteLine("Building app services report ...");
        Console.WriteLine("\tLoading subscriptions...");
        var subscriptions = client.GetSubscriptions().ToList();

        foreach (var subscription in subscriptions)
        {
            Console.WriteLine($"\tWorking on subscription '{subscription.Data.DisplayName}' ...");

            Console.WriteLine("\t\tLoading app services plans ...");
            var appServicePlans = subscription.GetAppServicePlans().ToList();
            foreach (var appServicePlan in appServicePlans)
            {
                Console.Write($"\t\t\tLoading app services on '{appServicePlan.Data.Name}' ...");
                var appServices = appServicePlan.GetWebApps().ToList();

                foreach (var appService in appServices)
                {
                    result.Add(new AppService
                    {
                        Name = appService.Name,
                        AppServicePlan = appServicePlan.Data.Name,
                        ResourceGroup = appService.ResourceGroup,
                        HostNames = appService.HostNameSslStates.ToDictionary(x => x.Name, x => x.SslState != HostNameBindingSslState.Disabled),
                        IsStopped = appService.State != "Running",
                        HttpsOnly = appService.IsHttpsOnly == true,
                        AlwaysOn = appService.SiteConfig.IsAlwaysOn == true,
                        SessionAffinity = appService.IsClientAffinityEnabled == true,
                        Http2 = appService.SiteConfig.IsHttp20Enabled == true
                    });
                }

                Console.WriteLine($" {appServices.Count} app services found.");
            }
        }

        WriteToFile(targetDirectory, result.OrderBy(x => x.AppServicePlan).ThenBy(x => x.IsStopped).ThenBy(x => x.Name).ToList());
    }

    private static void WriteToFile(DirectoryInfo targetDirectory, List<AppService> appServices)
    {
        var targetFile = Path.Combine(targetDirectory.FullName, "AppServices.html");
        Console.WriteLine($"\tWriting HTML to file '{targetFile}' ...");

        var sb = new StringBuilder();
        sb.BeginHtml();
        sb.Hero("App Services");
        sb.BeginTable();
        sb.Thead("Name", "App Service Plan", "Resource Group", "Status", "Host Names", "HTTPS only", "HTTP2", "Session Affinity", "Always On");
        sb.BeginTbody();

        foreach (var a in appServices)
        {
            sb.BeginTr();
            sb.Td(a.Name);
            sb.Td(a.AppServicePlan);
            sb.Td(a.ResourceGroup);
            sb.Td(a.IsStopped ? "Off" : "", a is { IsPreview: false, IsStopped: true });
            sb.Td(string.Join("<br />", a.HostNames
                .OrderBy(x => x.Key)
                .Where(x => !x.Key.Contains("azurewebsites.net", StringComparison.OrdinalIgnoreCase))
                .Select(x => (x.Value ? "" : "[!]") + " " + x.Key)
            ));
            sb.Td(a.HttpsOnly ? "" : "Off", !a.HttpsOnly);
            sb.Td(a.Http2 ? "" : "Off", !a.Http2);
            sb.Td(a.SessionAffinity ? "On" : "", a.SessionAffinity);
            sb.Td(a.AlwaysOn ? "On" : "", a is { IsPreview: true, AlwaysOn: true });
            sb.EndTr();
        }

        sb.EndTbody();
        sb.EndTable();
        sb.EndHtml();

        File.WriteAllText(targetFile, sb.ToString());
    }

    private sealed class AppService
    {
        public string Name { get; init; } = "";
        public string AppServicePlan { get; init; } = "";
        public string ResourceGroup { get; init; } = "";
        public IDictionary<string, bool> HostNames { get; init; } = new Dictionary<string, bool>();
        public bool IsStopped { get; init; }
        public bool Http2 { get; init; }
        public bool SessionAffinity { get; init; }
        public bool AlwaysOn { get; init; }
        public bool HttpsOnly { get; init; }

        public bool IsPreview => Name.Contains("-staging", StringComparison.OrdinalIgnoreCase)
                                 || Name.Contains("-preview", StringComparison.OrdinalIgnoreCase)
                                 || Name.Contains("-test", StringComparison.OrdinalIgnoreCase);
    }
}
