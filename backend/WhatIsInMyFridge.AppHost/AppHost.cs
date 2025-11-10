var builder = DistributedApplication.CreateBuilder(args);
#pragma warning disable ASPIRECOSMOSDB001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Add Azure Cosmos DB Emulator
var cosmosDb = builder.AddAzureCosmosDB("cosmos")
    .RunAsPreviewEmulator(cosmosBuilder =>
    {
        cosmosBuilder.WithLifetime(ContainerLifetime.Persistent);
        cosmosBuilder.WithDataExplorer();
    })
    .AddCosmosDatabase("WhatIsInMyFridge");
#pragma warning restore ASPIRECOSMOSDB001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Add Azure Storage Emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");

var deno = builder.AddDeno("frontend", "../../frontend", "dev").WithEndpoint(5174, 5173, "http", "http");

var api = builder.AddProject<Projects.WhatIsInMyFridge_Api>("api")
    .WithEndpoint("http", ep => ep.Port = 5000)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__CosmosDb", cosmosDb)
    .WithEnvironment("ConnectionStrings__BlobStorage", blobs)
    .WithEnvironment("CosmosDb__DatabaseName", "WhatIsInMyFridge")
    .WithEnvironment("JWT_SECRET_KEY", "nA1fNQirJ/k/CB8CaWcSjOzcYeRTyC98J1Ng+VY+ia8=")
    .WithEnvironment("ALLOWED_ORIGINS", deno.GetEndpoint("http"))
    .WithReference(cosmosDb)
    .WithReference(blobs);



builder.Build().Run();

