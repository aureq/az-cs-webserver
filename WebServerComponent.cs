using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Azure.Compute;
using Pulumi.Azure.Compute.Inputs;
using Pulumi.Azure.Network;
using Pulumi.Azure.Network.Inputs;
using Pulumi.Azure.Storage;

class WebServerComponentArgs
{
    /// <summary>
    /// The resource group in which to create the Web Server component.
    /// </summary>
    public ResourceGroup WebServerResourceGroup { get; set; } = null!;
    /// <summary>
    /// The storage account that contains internal reports.
    /// </summary>
    public Account ReportStorageAccount { get; set; } = null!;
    /// <summary>
    /// The network address range for the virtual network.
    /// Default: <c>10.0.0.0/16</c>
    /// </summary>
    public Input<string>? AddressSpace { get; set; }
    /// <summary>
    /// The subnet address range for this virtual network.
    /// Default: <c>10.0.1.0/24</c>
    /// </summary>
    public Input<string>? AddressPrefix { get; set; }
    /// <summary>
    /// The default admin user name for the webserver.
    /// </summary>
    public Input<string> AdminUser { get; set; } = null!;
    /// <summary>
    /// The password associated to the default admin user.
    /// </summary>
    public Input<string> AdminPassword { get; set; } = null!;
}

class WebServerComponent : Pulumi.ComponentResource
{
    public Output<string> IpAddress;
    public Output<string> WgetCommand;

    public WebServerComponent(string name, WebServerComponentArgs args, ComponentResourceOptions? opts = null)
        : base("pkg:index:WebServerComponent", name, opts)
    {
        var network = new VirtualNetwork("server-network",
            new VirtualNetworkArgs
            {
                ResourceGroupName = args.WebServerResourceGroup.Name,
                AddressSpaces = { args.AddressSpace ?? "10.0.0.0/16" },
                Subnets =
                {
                    new VirtualNetworkSubnetArgs {Name = "default", AddressPrefix = args.AddressPrefix ?? "10.0.1.0/24"}
                }
            },
            new CustomResourceOptions {
                Parent = this,
            }
        );

        var publicIp = new PublicIp("server-ip",
            new PublicIpArgs
            {
                ResourceGroupName = args.WebServerResourceGroup.Name,
                AllocationMethod = "Dynamic"
            },
            new CustomResourceOptions {
                Parent = network,
            });

        var networkInterface = new NetworkInterface("server-nic",
            new NetworkInterfaceArgs
            {
                ResourceGroupName = args.WebServerResourceGroup.Name,
                IpConfigurations =
                {
                    new NetworkInterfaceIpConfigurationArgs
                    {
                        Name = "webserveripcfg",
                        SubnetId = network.Subnets.Apply(subnets => subnets[0].Id)!,
                        PrivateIpAddressAllocation = "Dynamic",
                        PublicIpAddressId = publicIp.Id
                    }
                }
            },
            new CustomResourceOptions {
                Parent = network,
            });

        var vm = new VirtualMachine("server-vm",
            new VirtualMachineArgs
            {
                ResourceGroupName = args.WebServerResourceGroup.Name,
                NetworkInterfaceIds = {networkInterface.Id},
                VmSize = "Standard_A1_v2",
                DeleteDataDisksOnTermination = true,
                DeleteOsDiskOnTermination = true,
                OsProfile = new VirtualMachineOsProfileArgs
                {
                    ComputerName = "hostname",
                    AdminUsername = args.AdminUser,
                    AdminPassword = args.AdminPassword,
                    CustomData =
                        @"#!/bin/bash
echo ""Hello, World!"" > index.html
nohup python -m SimpleHTTPServer 80 &"
                },
                OsProfileLinuxConfig = new VirtualMachineOsProfileLinuxConfigArgs
                {
                    DisablePasswordAuthentication = false
                },
                StorageOsDisk = new VirtualMachineStorageOsDiskArgs
                {
                    CreateOption = "FromImage",
                    Name = "myosdisk1"
                },
                StorageImageReference = new VirtualMachineStorageImageReferenceArgs
                {
                    Publisher = "canonical",
                    Offer = "UbuntuServer",
                    Sku = "16.04-LTS",
                    Version = "latest"
                }
            },
            new CustomResourceOptions
            {
                Parent = network,
                DeleteBeforeReplace = true
            });

        // The public IP address is not allocated until the VM is running, so wait for that
        // resource to create, and then lookup the IP address again to report its public IP.
        this.IpAddress = Output
            .Tuple<string, string, string>(vm.Id, publicIp.Name, args.WebServerResourceGroup.Name)
            .Apply<string>(async t =>
            {
                (_, string name, string resourceGroupName) = t;
                var ip = await GetPublicIP.InvokeAsync(new GetPublicIPArgs
                    {Name = name, ResourceGroupName = resourceGroupName});
                return ip.IpAddress;
            });

        this.WgetCommand = this.IpAddress.Apply(ip => "wget -q -O - -S http://" + ip);


        this.RegisterOutputs();
    }
}