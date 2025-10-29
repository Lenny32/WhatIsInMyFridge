var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.WhatIsInMyFridge_Api>("api")
    .WithHttpEndpoint(port: 5000, name: "http");

builder.AddDeno("frontend", "../../frontend", "dev")
    .WithReference(api);

builder.Build().Run();

