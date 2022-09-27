"use strict";
const azure = require("@pulumi/azure");
const policy = require("@pulumi/policy");

new policy.PolicyPack("azure-webserver-demo", {
    policies: [{
        name: "storage-replication-tier",
        description: "Check Azure Account Storage resources and their replication types.",
        enforcementLevel: "advisory",
        validateResource: policy.validateResourceOfType(azure.storage.Account, (account, args, reportViolation) => {
            if (account.accountReplicationType === "LRS") {
                reportViolation(
                    "Azure Storage Account replcaition shouldn't be set lower than 'ZRS' as per " +
                    "company policy. Use either 'ZRS' or 'GRS'. Read more about read access here: " +
                    "https://wiki.intra.acmecorp.com/cloud/azure/policies");
            }
        }),
    }],
});