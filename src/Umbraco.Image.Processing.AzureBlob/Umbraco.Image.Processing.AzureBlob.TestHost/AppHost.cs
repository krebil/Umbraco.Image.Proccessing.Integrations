var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("blobs");

builder.Build().Run();
