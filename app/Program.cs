using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace app
{
    class Program
    {
        const string DEFAULT_MANAGER_SIZE = "Standard_A1";
        const string DEFAULT_WORKER_SIZE = "Standard_A1";

        static string subscriptionId = null;
        static string resourceGroup = null;
        static string servicePrincipalName = null;
        static string region = null;
        static string sshPublicKey = null;
        static string channel = "stable";
        static bool enableExtLogs = true;
        static bool enableSystemPrune = false;
        static int managerCount = 1;
        static string swarmname = "dockerswarm";
        static int workerCount = 1;
        static string managerSize = DEFAULT_MANAGER_SIZE;
        static string workerSize = DEFAULT_WORKER_SIZE;

        /// <summary>
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (args.Contains("--help"))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("This app does the following:");
                sb.AppendLine("    1. Log into Azure CLI if not already logged in");
                sb.AppendLine("    2. Sets the chosen subscription context");
                sb.AppendLine("    3. Creates a new resource group");
                sb.AppendLine("    4. Creates a new Service Principal scoped to the new resource group");
                sb.AppendLine("    5. Deploys a Docker for Azure swarm");
                sb.AppendLine("    6. Opens up port 2376 on the Manager LoadBalancer for communication with Docker Cloud");
                sb.AppendLine();
                sb.AppendLine("The user will be prompted to supply any required parameters not passed from the command line.");
                sb.AppendLine();
                sb.AppendLine("Options:");
                sb.AppendLine();
                sb.AppendLine("--resource-group");
                sb.AppendLine("    The name of the new resource group to be created");
                sb.AppendLine("--service-principal-name");
                sb.AppendLine("    The name of the new Service Principal to be created");
                sb.AppendLine("--region");
                sb.AppendLine("    The region you want to use");
                sb.AppendLine("--ssh-public-key");
                sb.AppendLine("    The SSH public key used to authenticate with the created swarm.");
                sb.AppendLine("--subscription");
                sb.AppendLine("    The subscription ID you want to use. You don't need to enter it if you only have 1 subscription.");
                sb.AppendLine("--channel (stable | edge)");
                sb.AppendLine("    The docker channel you want to use. Default: stable");
                sb.AppendLine("--enable-ext-logs (y | n)");
                sb.AppendLine("    Stores container logs in storage container on azure. Default: y");
                sb.AppendLine("--enable-system-prune (y | n)");
                sb.AppendLine("    Cleans up unused images, containers, networks, and volumes. Default: n");
                sb.AppendLine("--manager-size");
                sb.AppendLine("    The size of the manager nodes. Standard_A0 is the cheapest.");
                sb.AppendLine("    See https://download.docker.com/azure/stable/Docker.tmpl for listing.");
                sb.AppendLine("    Default: " + DEFAULT_MANAGER_SIZE);
                sb.AppendLine("--worker-size");
                sb.AppendLine("    The size of the worker nodes. Standard_A0 is the cheapest.");
                sb.AppendLine("    See https://download.docker.com/azure/stable/Docker.tmpl for listing.");
                sb.AppendLine("    Default: " + DEFAULT_WORKER_SIZE);
                sb.AppendLine("--manager-count (1 | 3 | 5");
                sb.AppendLine("    How many manager nodes you want. Default: 1");
                sb.AppendLine("--worker-count (1-15)");
                sb.AppendLine("    How many worker nodes you want. Default: 1");
                sb.AppendLine("--swarm-name");
                sb.AppendLine("    Define how the swarm resources should be named. Default: dockerswarm");
            }

            if (!Tools.IsLoggedIn())
            {
                Tools.Login();
            }

            InitArgs(args);

            bool resourceGroupCreated = false;
            bool servicePrincipalCreated = false;
            ServicePrincipal sp = null;
            try
            {
                // Set subscription context
                Tools.SetSubscriptionContext(subscriptionId);

                // Create resource group
                Tools.CreateResourceGroup(resourceGroup, region);
                resourceGroupCreated = true;

                // Create Service Princal
                sp = Tools.CreateServicePrincipal(subscriptionId, servicePrincipalName, resourceGroup, region);
                servicePrincipalCreated = true;

                // Deploy Swarm
                Tools.DeploySwarm(resourceGroup, channel, sp.appId, sp.password, sshPublicKey, enableExtLogs, enableSystemPrune, managerCount, swarmname, workerCount, managerSize, workerSize);

                // Open port 2376 on manager load balancer
                Tools.OpenManagerPort2376(resourceGroup);

                Console.WriteLine(String.Format("All done. Your Service Principal password is: {0}", sp.password));
            }
            catch(Exception)
            {
                if (resourceGroupCreated)
                {
                    Tools.DeleteResourceGroup(resourceGroup);
                }
                if (servicePrincipalCreated && sp != null)
                {
                    Tools.DeleteServicePrincipal(sp);
                }
            }
        }

        static void InitArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--subscription":
                        subscriptionId = args[i + 1];
                        break;
                    case "--resource-group":
                        resourceGroup = args[i + 1];
                        break;
                    case "--service-principal-name":
                        servicePrincipalName = args[i + 1];
                        break;
                    case "--region":
                        region = args[i + 1];
                        break;
                    case "--ssh-public-key":
                        sshPublicKey = args[i + 1];
                        break;
                    case "--channel":
                        channel = args[i + 1];
                        break;
                    case "--enable-ext-logs":
                        if (args[i + 1].Equals("y"))
                        {
                            enableExtLogs = true;
                        }
                        else if (args[i + 1].Equals("n"))
                        {
                            enableExtLogs = false;
                        }
                        else
                        {
                            throw new Exception("Invalid entry for --enable-ext-logs");
                        }
                        break;
                    case "--enable-system-prune":
                        if (args[i + 1].Equals("y"))
                        {
                            enableSystemPrune = true;
                        }
                        else if (args[i + 1].Equals("n"))
                        {
                            enableSystemPrune = false;
                        }
                        else
                        {
                            throw new Exception("Invalid entry for --enable-system-prune");
                        }
                        break;
                    case "--manager-count":
                        managerCount = Int32.Parse(args[i + 1]);
                        break;
                    case "--swarm-name":
                        swarmname = args[i + 1];
                        break;
                    case "--worker-count":
                        workerCount = Int32.Parse(args[i + 1]);
                        break;
                    case "--manager-size":
                        managerSize = args[i + 1];
                        break;
                    case "--worker-size":
                        workerSize = args[i + 1];
                        break;
                    default:
                        break;
                }
            }

            if (String.IsNullOrWhiteSpace(subscriptionId))
            {
                subscriptionId = Tools.GetChosenSubscription().id;
            }

            if (String.IsNullOrWhiteSpace(resourceGroup))
            {
                resourceGroup = Tools.GetChosenResourceGroup();
            }

            if (String.IsNullOrWhiteSpace(servicePrincipalName))
            {
                servicePrincipalName = Tools.GetChosenServicePrincipalName();
            }

            if (String.IsNullOrWhiteSpace(region))
            {
                region = Tools.GetChosenRegion();
            }

            if (String.IsNullOrWhiteSpace(sshPublicKey))
            {
                sshPublicKey = Tools.GetChosenSshPublicKey();
            }

            if (String.IsNullOrWhiteSpace(channel))
            {
                channel = Tools.GetChosenChannel();
            }
        }
    }
}
