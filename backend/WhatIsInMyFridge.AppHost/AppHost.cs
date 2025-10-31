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

var deno = builder.AddDeno("frontend", "../../frontend", "dev").WithEndpoint("http", e => e.Port = 5173);

var api = builder.AddProject<Projects.WhatIsInMyFridge_Api>("api")
    .WithEndpoint("http", ep => ep.Port = 5000)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("CosmosDb", cosmosDb)
    .WithEnvironment("ALLOWED_ORIGINS", deno.GetEndpoint("http")) // <-- pass origin
    .WithReference(cosmosDb)
    .WithReference(blobs);


builder.Build().Run();

