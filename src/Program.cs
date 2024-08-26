using System;
using System.IO;
using System.Linq;
using Azure.Identity;
using Azure.ResourceManager;
using AzureReporting.Reports;

var targetDirectory = new DirectoryInfo("./");

var client = new ArmClient(new DefaultAzureCredential(includeInteractiveCredentials: true));
Console.Write("\tAuthenticating ...");
var subscriptions = client.GetSubscriptions().Count();
Console.WriteLine($" {subscriptions} subscriptions found.");

AppServicePlanReport.Run(client, targetDirectory);
AppServiceReport.Run(client, targetDirectory);
ResourceGroupsReport.Run(client, targetDirectory);
