using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;

class WebServerStack : Stack
{
    public WebServerStack()
    {
        var stackConfig = new Config();

        var resourceGroup = new ResourceGroup("webserver-demo-rg");

        // Create an Azure Storage Account
        var storageAccount = new Account("storage",
            new AccountArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountReplicationType = "LRS",
                AccountTier = "Standard",
                // Tags = new InputMap<string>
                // {
                //     {"ProjectName", "AcmeProject"}
                // },
            },
            new CustomResourceOptions
            {
                Parent = resourceGroup
            });

        var webServer = new WebServerComponent("webserver-component",
            new WebServerComponentArgs
            {
                WebServerResourceGroup = resourceGroup,
                ReportStorageAccount = storageAccount,
                AdminUser = "AcmeAdmin",
                // AdminPassword = "HardcodedPasswords!AreBad4YourH3alth#",
                AdminPassword = stackConfig.RequireSecret("adminpassword"), // pulumi config set adminpassword --secret
            },
            new ComponentResourceOptions {
                Parent = resourceGroup
            });

        this.IpAddress = webServer.IpAddress;
        this.WgetCommand = webServer.WgetCommand;
    }

    [Output] public Output<string> IpAddress { get; set; }
    [Output] public Output<string> WgetCommand { get; set; }
}
