var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Cosmos DB Emulator
var cosmosDb = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator(cosmosBuilder =>
    {
        cosmosBuilder.WithLifetime(ContainerLifetime.Persistent);
    })
    .AddCosmosDatabase("WhatIsInMyFridge");

// Add Azure Storage Emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");

var api = builder.AddProject<Projects.WhatIsInMyFridge_Api>("api")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(cosmosDb)
    .WithReference(blobs);

builder.AddDeno("frontend", "../../frontend", "dev")
    .WithReference(api);

builder.Build().Run();

