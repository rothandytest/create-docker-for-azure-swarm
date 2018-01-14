Docker Hub: https://hub.docker.com/r/andrewroth/create-docker-for-azure-swarm/

Running this container does the following:
  1. Logs into Azure CLI
  2. Creates a new Resource Group with the name you specify
  3. Creates a new Azure Active Directory Service Principal for the swarm to use
  4. Deploys the swarm to the new Resource Group
  5. Opens port 2376 on the manager node(s) to allow communication with Docker Cloud

# Usage:

    docker run -it --rm andrewroth/create-docker-for-azure-swarm:latest

# Options:

    --resource-group
The name of the new resource group to be created

    --service-principal-name
The name of the new Service Principal to be created.
You'll be asked to enter it if it isn't included in the command line.

    --region
The region you want to use.
You'll be asked to enter it if it isn't included in the command line.

    --ssh-public-key
The SSH public key used to authenticate with the created swarm.
You'll be asked to enter it if it isn't included in the command line.

    --subscription
The subscription ID you want to use. You don't need to enter it if you only have 1 subscription.
If you have more than one, and you don't specify which one you want to use in the command line,
you will be prompted to pick which one you want to use.

    --channel (stable | edge)
The docker channel you want to use. Default: stable

    --enable-ext-logs (y | n)
Stores container logs in storage container on azure. Default: y

    --enable-system-prune (y | n)
Cleans up unused images, containers, networks, and volumes. Default: n

    --manager-size
The size of the manager nodes. Standard_A0 is the cheapest.
See https://download.docker.com/azure/stable/Docker.tmpl for listing.
Default: Standard_A1

    --worker-size
The size of the worker nodes. Standard_A0 is the cheapest.
See https://download.docker.com/azure/stable/Docker.tmpl for listing.
Default: Standard_A1

    --manager-count (1 | 3 | 5)
How many manager nodes you want. Default: 1

    --worker-count (1-15)
How many worker nodes you want. Default: 1

    --swarm-name
Define how the swarm resources should be named. Default: dockerswarm
