var builder = DistributedApplication.CreateBuilder(args);

var imageProcessingService = builder.AddProject<Projects.Umbraco_Image_Processing_Service>("image-processing-service");

builder.AddProject<Projects.Umbraco>("umbraco")
    .WithEnvironment("ImageProcessing__Standalone__BaseUrl", imageProcessingService.GetEndpoint("http"))
    .WaitFor(imageProcessingService);

builder.Build().Run();
