using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Models;

namespace AzureReports.Reports;

public static class AppServiceReport
{
    public static async Task Run(ArmClient client, DirectoryInfo targetDirectory)
    {
        try
        {
            await RunInternal(client, targetDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    // also interesting:
    // - public internet access, limited access, no public access
    // - ignore docker appsettings
    // - show last deployoment date, docker container

    private static async Task RunInternal(ArmClient client, DirectoryInfo targetDirectory)
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

                    var resourceGroup = await subscription.GetResourceGroupAsync(a.ResourceGroup);
                    var appService = (await resourceGroup.Value.GetWebSiteAsync(a.Name)).Value;
                    var appServiceConfig = (await appService.GetWebSiteConfig().GetAsync()).Value.Data;
                    var appSettings = await appService.GetApplicationSettingsAsync();
                    var scmPolicy = (await appService.GetScmSiteBasicPublishingCredentialsPolicy().GetAsync()).Value.Data;
                    var ftpPolicy = (await appService.GetWebSiteFtpPublishingCredentialsPolicy().GetAsync()).Value.Data;

                    var healthChecks = new Dictionary<string, bool>();

                    if (appService.Data.State == "Running")
                    {
                        using var httpClient = new HttpClient();
                        foreach (var hostname in appService.Data.HostNames)
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
                        Name = appService.Data.Name,
                        AppServicePlan = appServicePlan.Data.Name,
                        ResourceGroup = appService.Data.ResourceGroup,
                        HostNames = appService.Data.HostNameSslStates.ToDictionary(x => x.Name, x => x.SslState != HostNameBindingSslState.Disabled),
                        IsStopped = appService.Data.State != "Running",
                        HttpsOnly = appService.Data.IsHttpsOnly == true,
                        AlwaysOn = appServiceConfig.IsAlwaysOn == true,
                        SessionAffinity = appService.Data.IsClientAffinityEnabled == true,
                        Http2 = appServiceConfig.IsHttp20Enabled == true,
                        FtpEnabled = appServiceConfig.FtpsState != AppServiceFtpsState.Disabled,
                        FtpBasicAuthEnabled = ftpPolicy.Allow != false,
                        ScmBasicAuthEnabled = scmPolicy.Allow != false,
                        EnvironmentVariables = appSettings.Value.Properties.ToDictionary(x => x.Key, x => x.Value),
                        SystemManagedIdentity = appService.Data.Identity?.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssigned,
                        HealthChecks = healthChecks,
                        DatabaseName = FindDatabaseName(appSettings.Value)
                    });
                }
            }
        }

        WriteToFile(targetDirectory, result.OrderBy(x => x.AppServicePlan).ThenBy(x => x.IsStopped).ThenBy(x => x.Name).ToList());
    }

    private static string? FindDatabaseName(AppServiceConfigurationDictionary appSettings)
    {
        foreach (var (_, value) in appSettings.Properties)
        {
            var regex = new Regex("[ ;]Initial Catalog=([^;]+)", RegexOptions.IgnoreCase);
            var match = regex.Match(value);
            if (match.Success) return match.Groups[1].Value;

            regex = new Regex("[ ;]Database=([^;]+)", RegexOptions.IgnoreCase);
            match = regex.Match(value);
            if (match.Success) return match.Groups[1].Value;
        }

        return null;
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
            "AppService",
            "AppServicePlan",
            "RessourceGroup",
            "Status",
            "Man.-Id.",
            "Domains",
            2, "Database",
            7, "Miscellaneous",
            2, "Env.-Vars.",
            2, "Health Checks");
        sb.BeginTbody();

        foreach (var a in appServices)
        {
            sb.BeginTr();
            sb.Td(a.Name);
            sb.Td(a.AppServicePlan);
            sb.Td(a.ResourceGroup);
            sb.Td(a.IsStopped ? "Off" : "", "", a is { IsPreview: false, IsStopped: true });
            sb.Td(a.SystemManagedIdentity ? "" : "Off", "", !a.SystemManagedIdentity);
            sb.Td(string.Join("<br />", a.HostNames
                .OrderBy(x => x.Key)
                .Where(x => !x.Key.Contains("azurewebsites.net", StringComparison.OrdinalIgnoreCase))
                .Select(x => (x.Value ? "" : "[!]") + " " + x.Key)
            ));

            sb.Td(a.DatabaseName ?? "");
            sb.Td(a.DatabaseNameDoesNotMatchPreview ? "Database name" : "", "", a.DatabaseNameDoesNotMatchPreview);

            sb.Td(a.HttpsOnly ? "" : "HTTPS only Off", "", !a.HttpsOnly);
            sb.Td(a.Http2 ? "" : "HTTP2 Off", "", !a.Http2);
            sb.Td(a.SessionAffinity ? "Session Affinity" : "", "", a.SessionAffinity);
            sb.Td(a.FtpEnabled ? "FTP Enabled" : "", "", a.FtpEnabled);
            sb.Td(a.FtpBasicAuthEnabled ? "FTP BasicAuth Enabled" : "", "", a.FtpBasicAuthEnabled);
            sb.Td(a.ScmBasicAuthEnabled ? "SCM BasicAuth Enabled" : "", "", a.ScmBasicAuthEnabled);
            sb.Td(a.AlwaysOn ? "Always On" : "", "Always On", a is { IsPreview: true, AlwaysOn: true });

            sb.Td(a.EnvironmentVariables.Count.ToString("N0", CultureInfo.InvariantCulture));
            sb.Td(a.EnvironmentVariablesContainPassword ? "Contains password" : "", "", a.EnvironmentVariablesContainPassword);
            sb.Td(a.HealthChecks.Count.ToString("N0", CultureInfo.InvariantCulture));
            sb.Td(a.HasFailedHealthChecks ? RenderFailedHealthChecks(a.HealthChecks) : "", "", a.HasFailedHealthChecks);
            sb.EndTr();
        }

        sb.EndTbody();
        sb.EndTable();
        sb.EndHtml();

        File.WriteAllText(targetFile, sb.ToString());
    }

    private static string RenderFailedHealthChecks(IDictionary<string, bool> healthChecks)
    {
        var failedHealthChecks = healthChecks
            .Where(x => !x.Value)
            .Select(x => x.Key)
            .Select(x => $"<a href=\"{x}\">{x}</a>")
            .ToList();
        return string.Join("<br />", failedHealthChecks);
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
                foreach (var (_, v) in EnvironmentVariables)
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
        public bool FtpEnabled { get; init; }
        public bool FtpBasicAuthEnabled { get; init; }
        public bool ScmBasicAuthEnabled { get; init; }
        public bool SystemManagedIdentity { get; init; }

        public string? DatabaseName { get; set; }

        public bool IsPreview => Name.Contains("-staging", StringComparison.OrdinalIgnoreCase)
                                 || Name.Contains("-preview", StringComparison.OrdinalIgnoreCase)
                                 || Name.Contains("-test", StringComparison.OrdinalIgnoreCase);

        public bool DatabaseNameDoesNotMatchPreview
        {
            get
            {
                var databasePreviewNames = new[]
                {
                    "-preview-",
                    "-staging-"
                };

                if (DatabaseName == null) return false;
                if (IsPreview && !databasePreviewNames.Any(x => DatabaseName.Contains(x))) return true;
                if (!IsPreview && databasePreviewNames.Any(x => DatabaseName.Contains(x))) return true;
                return false;
            }
        }
    }
}
