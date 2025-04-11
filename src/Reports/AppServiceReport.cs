using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;

namespace AzureReports.Reports;

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
                Console.WriteLine($"\t\t\tLoading app services on '{appServicePlan.Data.Name}' ...");
                var appServices = appServicePlan.GetWebApps().ToList();

                foreach (var a in appServices)
                {
                    Console.WriteLine($"\t\t\t\tWorking on app service '{a.Name}' ...");

                    var resourceGroup = subscription.GetResourceGroup(a.ResourceGroup);
                    var appService = resourceGroup.Value.GetWebSite(a.Name);
                    var appSettings = appService.Value.GetApplicationSettings();

                    var healthChecks = new Dictionary<string, bool>();
                    foreach (var hostname in a.HostNames)
                    {
                        using (var httpClient = new HttpClient())
                        {
                            var url = "https://" + hostname + "/health";
                            try
                            {
                                var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
                                response.EnsureSuccessStatusCode();
                                healthChecks.Add(url, true);
                            }
                            catch
                            {
                                healthChecks.Add(url, false);
                            }
                        }
                    }

                    result.Add(new AppService
                    {
                        Name = a.Name,
                        AppServicePlan = appServicePlan.Data.Name,
                        ResourceGroup = a.ResourceGroup,
                        HostNames = a.HostNameSslStates.ToDictionary(x => x.Name, x => x.SslState != HostNameBindingSslState.Disabled),
                        IsStopped = a.State != "Running",
                        HttpsOnly = a.IsHttpsOnly == true,
                        AlwaysOn = a.SiteConfig.IsAlwaysOn == true,
                        SessionAffinity = a.IsClientAffinityEnabled == true,
                        Http2 = a.SiteConfig.IsHttp20Enabled == true,
                        EnvironmentVariables = appSettings.Value.Properties.ToDictionary(x => x.Key, x => x.Value),
                        HealthChecks = healthChecks
                    });
                }
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
        sb.Thead(
            "Name",
            "App Service Plan",
            "Resource Group",
            "Status",
            "Host Names",
            "HTTPS only",
            "HTTP2",
            "Session Affinity",
            "Always On",
            "Env Variables",
            "Health Checks");
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
            sb.Td(a.EnvironmentVariables.Count.ToString("N0", CultureInfo.InvariantCulture)
                  + (a.EnvironmentVariablesContainPassword ? "<br />[!]&nbsp;Password" : "")
            );
            sb.Td(a.HealthChecks.Count.ToString("N0", CultureInfo.InvariantCulture)
                  + (a.HasFailedHealthChecks
                      ? "<br />[!]&nbsp;" + a.HealthChecks.Where(x => !x.Value).Select(x => x.Key).Aggregate((x, y) => $"{x}<br />[!]&nbsp;{y}")
                      : "")
            );
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
        public IDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();
        public IDictionary<string, bool> HealthChecks { get; init; } = new Dictionary<string, bool>();
        public bool HasFailedHealthChecks => HealthChecks.Any(x => !x.Value);

        public bool EnvironmentVariablesContainPassword
        {
            get
            {
                foreach (var (k, v) in EnvironmentVariables)
                {
                    if (v.Contains("password", StringComparison.OrdinalIgnoreCase)) return true;
                }

                return false;
            }
        }

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
