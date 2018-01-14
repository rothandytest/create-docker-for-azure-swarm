using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace app
{
    class Tools
    {
        /// <summary>
        /// Checks to see if the user is logged in and has at least one active subscription
        /// </summary>
        /// <returns>true if the user is logged in and has at least one active subscription</returns>
        public static bool IsLoggedIn()
        {
            string exePath = GetAzureCliExePath();

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = exePath;
            p.StartInfo.Arguments = "account list --output json";
            p.StartInfo.RedirectStandardOutput = true;
            Console.WriteLine("Checking to see if you are logged in");
            p.Start();
            p.WaitForExit();
            string output = p.StandardOutput.ReadToEnd();
            string jsonString = output.Substring(output.IndexOf('['));
            List<Subscription> subscriptions = JsonConvert.DeserializeObject<List<Subscription>>(jsonString);

            return subscriptions.Count > 0;
        }

        internal static void Login()
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = GetAzureCliExePath();
            p.StartInfo.Arguments = "login";
            Console.WriteLine("Logging in");
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new Exception("Something went wrong when trying to log in");
            }
        }

        /// <summary>
        /// Get the list of subscriptions
        /// </summary>
        /// <returns>The list of subscriptions</returns>
        public static List<Subscription> GetSubscriptions()
        {
            string exePath = GetAzureCliExePath();
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new FileNotFoundException("Azure CLI executable not found");
            }

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = exePath;
            p.StartInfo.Arguments = "account list --output json";
            p.StartInfo.RedirectStandardOutput = true;
            Console.WriteLine("Getting list of enabled subscriptions");
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new Exception("Something went wrong trying to get list of subscriptions.");
            }
            string output = p.StandardOutput.ReadToEnd();
            string jsonString = output.Substring(output.IndexOf('['));
            List<Subscription> subscriptions = JsonConvert.DeserializeObject<List<Subscription>>(jsonString);
            return subscriptions;
        }

        internal static string GetChosenSshPublicKey()
        {
            Console.WriteLine("Enter your SSH public key: ");
            Console.SetIn(new StreamReader(Console.OpenStandardInput(8192)));
            string publicKey = Console.ReadLine();
            return publicKey;
        }

        internal static ServicePrincipal CreateServicePrincipal(string subscriptionId, string servicePrincipalName, string resourceGroup, string region)
        {
            string password = Guid.NewGuid().ToString();

            string azExe = GetAzureCliExePath();
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = azExe;
            p.StartInfo.Arguments = String.Format("ad sp create-for-rbac --name {0} --password {1} --scopes /subscriptions/{2}/resourceGroups/{3} --years 20", servicePrincipalName,
                password, subscriptionId, resourceGroup);
            p.StartInfo.RedirectStandardOutput = true;
            Console.WriteLine("Creating Service Principal");
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new Exception("Something went wrong trying to create Service Principal.");
            }
            string output = p.StandardOutput.ReadToEnd();
            string jsonString = output.Substring(output.IndexOf('{'));
            ServicePrincipal newServicePrincipal = JsonConvert.DeserializeObject<ServicePrincipal>(jsonString);
            if (String.IsNullOrEmpty(newServicePrincipal?.appId))
            {
                throw new Exception("Something went wrong generating the ServicePrincipal");
            }
            return newServicePrincipal;
        }

        internal static string GetChosenChannel()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Choose the number corresponding to which channel you would like to use: ");
            sb.AppendLine("    (1) stable");
            sb.AppendLine("    (2) edge");
            Console.WriteLine(sb.ToString());
            string choice = Console.ReadLine();
            if (choice == "1")
            {
                return "stable";
            }
            else if (choice == "2")
            {
                return "edge";
            }
            else
            {
                throw new Exception("Invalid choice for chosen channel");
            }
        }

        internal static string GetChosenRegion()
        {
            Console.WriteLine("Enter the region you would like to use: ");
            string choice = Console.ReadLine();
            return choice;
        }

        internal static string GetChosenServicePrincipalName()
        {
            Console.WriteLine("Enter the name for the new Service Principal:");
            string choice = Console.ReadLine();
            return choice;
        }

        internal static void DeleteServicePrincipal(ServicePrincipal sp)
        {
            Process p = new Process();
            p.StartInfo.FileName = GetAzureCliExePath();
            p.StartInfo.Arguments = String.Format("ad sp delete --id {0}", sp.appId);
            Console.WriteLine("Deleting Service Principal");
            p.Start();
            p.WaitForExit();
        }

        internal static void DeleteResourceGroup(string resourceGroup)
        {
            Process p = new Process();
            p.StartInfo.FileName = GetAzureCliExePath();
            p.StartInfo.Arguments = String.Format("group delete --name {0} -y --no-wait", resourceGroup);
            Console.WriteLine("Deleting the created resource group");
            p.Start();
            p.WaitForExit();
        }

        internal static void OpenManagerPort2376(string resourceGroup)
        {
            Process p = new Process();
            p.StartInfo.FileName = GetAzureCliExePath();
            p.StartInfo.Arguments = String.Format("network lb inbound-nat-pool create --backend-port 2376 --frontend-port-range-start 2376 --frontend-port-range-end 2377 --lb-name externalSSHLoadBalancer --name docker-cloud " +
                "--protocol Tcp --resource-group {0}", resourceGroup);
            Console.WriteLine("Opening port 2376 for Docker Cloud use");
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new Exception("Something went wrong trying to open port 2376 on the manager VMSS");
            }
        }

        internal static void DeploySwarm(string resourceGroup, string channel, string appId, string password, string sshPublicKey, bool enableExtLogs, bool enableSystemPrune, int managerCount, string swarmname, int workerCount, string managerSize, string workerSize)
        {
            try
            {
                string sshParam = "{\"$schema\":\"https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#\",\"contentVersion\":\"1.0.0.0\",\"parameters\": {\"sshPublicKey\":{\"value\":\"MyValueHere\"}}}".Replace("MyValueHere",sshPublicKey);
                File.WriteAllText("sshParam.json", sshParam);

                string azExe = GetAzureCliExePath();
                Process p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = false;
                p.StartInfo.FileName = azExe;
                p.StartInfo.Arguments = String.Format("group deployment create --resource-group {0} --name docker.template --template-uri https://download.docker.com/azure/{1}/Docker.tmpl " +
                    "--parameters @sshParam.json --parameters adServicePrincipalAppID={2} --parameters adServicePrincipalAppSecret={3} --parameters enableExtLogs={4} --parameters enableSystemPrune={5} " +
                    "--parameters managerCount={6} --parameters swarmName={7} --parameters workerCount={8} --parameters managerVMSize={9} --parameters workerVMSize={10} --verbose",
                    resourceGroup,
                    channel,
                    appId,
                    password,
                    enableExtLogs ? "yes" : "no",
                    enableSystemPrune ? "yes" : "no",
                    managerCount.ToString(),
                    swarmname,
                    workerCount,
                    managerSize,
                    workerSize);
                Console.WriteLine("Deploying Swarm. This could take several minutes");
                p.Start();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    throw new Exception("Something went wrong when trying to deploy the swarm");
                }
            }
            finally
            {
                if (File.Exists("sshParam.json"))
                {
                    File.Delete("sshParam.json");
                }
            }
        }

        internal static void CreateResourceGroup(string groupName, string region)
        {
            Process p = new Process();
            p.StartInfo.FileName = GetAzureCliExePath();
            p.StartInfo.Arguments = String.Format("group create --location {0} --name {1}", region, groupName);
            Console.WriteLine("Creating resource group");
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new Exception("Something went wrong when trying to create the resource group");
            }
        }

        internal static void SetSubscriptionContext(string subscriptionId)
        {
            if (GetSubscriptions().Count > 1)
            {
                Process p = new Process();
                p.StartInfo.FileName = GetAzureCliExePath();
                p.StartInfo.Arguments = "account set --subscription " + subscriptionId;
                Console.WriteLine("Setting subscription context");
                p.Start();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    throw new Exception("Something went wrong when trying to set subscription context");
                }
            }
        }

        internal static string GetChosenResourceGroup()
        {
            Console.WriteLine("Enter the resource group you would like to create: ");
            string choice = Console.ReadLine();
            return choice;
        }

        internal static Subscription GetChosenSubscription()
        {
            List<Subscription> subscriptions = GetSubscriptions();
            if (subscriptions.Count < 1)
            {
                throw new InvalidStateException("You don't have any subscriptions to use. Please create one first");
            }
            else if (subscriptions.Count == 1)
            {
                return subscriptions[0];
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Please select the number corresponding to which subscription you would like to use:");
                for (int i = 0; i < subscriptions.Count; i++)
                {
                    sb.AppendLine(String.Format("    ({0} {1} - {2})", i.ToString(), subscriptions[i].name, subscriptions[i].id));
                }
                Console.WriteLine(sb.ToString());
                string choice = Console.ReadLine();
                Subscription chosenSubscription;
                chosenSubscription = subscriptions[Int32.Parse(choice)];
                return chosenSubscription;
            }
        }

        /// <summary>
        /// Get the fully qualified path to the azure cli executable
        /// </summary>
        /// <returns>The full qualified path to the azure cli executable, or null if not found</returns>
        private static string GetAzureCliExePath()
        {
            string environmentPath = Environment.GetEnvironmentVariable("PATH");
            string[] pathsSemiColon = environmentPath.Split(';');
            string exePath = pathsSemiColon.Select(x => Path.Combine(x, "az")).Where(x => File.Exists(x)).FirstOrDefault();
            if (String.IsNullOrWhiteSpace(exePath))
            {
                exePath = pathsSemiColon.Select(x => Path.Combine(x, "az.cmd")).Where(x => File.Exists(x)).FirstOrDefault();
            }
            if (String.IsNullOrWhiteSpace(exePath))
            {
                string[] pathsColon = environmentPath.Split(':');
                exePath = pathsColon.Select(x => Path.Combine(x, "az")).Where(x => File.Exists(x)).FirstOrDefault();
            }
            if (String.IsNullOrEmpty(exePath))
            {
                throw new Exception("Azure CLI executable not found.");
            }
            return exePath;
        }
    }
}
