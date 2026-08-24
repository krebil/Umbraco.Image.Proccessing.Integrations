using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Azurite: local, no-Azure-subscription-needed emulator for the Blob backends (ADR-0007 /
// production-hardening tickets 05 and 11). The connection is always made available to both apps
// below; whether either actually uses it for anything is a separate, config-driven decision (see
// "Storage:Mode" below) — local disk stays the real default for both the derivative cache and
// original-image resolution.
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

var imageProcessingService = builder.AddProject<Projects.Umbraco_Image_Processing_Service>("image-processing-service")
    .WithReference(blobs);

IResourceBuilder<ProjectResource> umbraco = builder.AddProject<Projects.Umbraco>("umbraco")
    .WithEnvironment("ImageProcessing__Standalone__BaseUrl", imageProcessingService.GetEndpoint("http"))
    .WithReference(blobs)
    .WaitFor(imageProcessingService);

// Reverse direction of the reference above: the Service needs Umbraco's own internal endpoint to
// fetch raw originals over HTTP when Storage:Mode=HttpProxy (production-hardening ticket 12). Always
// set, not conditional — mirrors ImageProcessing__Standalone__BaseUrl's unconditional wiring above:
// cheap to wire, and harmless when the Service isn't configured to read it (Storage:Mode defaults to
// LocalDisk). Deliberately not paired with a `.WaitFor(umbraco)` on imageProcessingService: umbraco
// already WaitFor(imageProcessingService) above, so adding the reverse would deadlock startup — each
// side would wait on the other. Not needed anyway: endpoint references resolve to an address as soon
// as Aspire allocates the port, they don't require the referenced resource to already be running.
imageProcessingService.WithEnvironment("ImageProcessing__Proxy__UmbracoBaseUrl", umbraco.GetEndpoint("http"));

// Forwarded from the AppHost's own config to Umbraco's env, not left to appsettings.json's default
// ("InProcess"): needed so a consumer of this AppHost (e.g. the E2E test project driving it via
// DistributedApplicationTestingBuilder with --ImageProcessing:Mode=Standalone) can actually put
// Umbraco into Standalone mode. Bug found while building ticket 12's redirect-loop regression test —
// the E2E harness already passed this flag to the AppHost, but nothing forwarded it to Umbraco's own
// process, so Umbraco silently stayed in its appsettings.json default (InProcess) regardless.
string imageProcessingMode = builder.Configuration.GetValue("ImageProcessing:Mode", "InProcess");
umbraco.WithEnvironment("ImageProcessing__Mode", imageProcessingMode);

string storageMode = builder.Configuration.GetValue("Storage:Mode", "LocalDisk");

// Opt-in, not default: pass --Storage:Mode=AzureBlob (e.g. from the E2E test project driving this
// AppHost via DistributedApplicationTestingBuilder) to point both apps' media storage at the same
// Azurite-backed container instead of local disk (production-hardening ticket 11). Local disk
// (LocalDiskOriginalImageSource + Umbraco's own physical media file system) needs no wiring here —
// they already share a filesystem via each app's own OriginalsRootPath/default media path config.
if (storageMode == "AzureBlob")
{
    const string mediaContainerName = "media";

    // Both Umbraco.StorageProviders.AzureBlob and AzureBlobOriginalImageSource expect the container
    // to already exist (matching real Azure, where it's provisioned ahead of deploy via IaC, not
    // created on first write) — confirmed the hard way: without this, Umbraco's first real media save
    // against Azurite fails with a 404 ContainerNotFound from the Blob SDK. Declaring it here makes
    // Aspire provision it against Azurite at startup, the dev/test equivalent of that IaC step.
    storage.AddBlobContainer("media-container", mediaContainerName);

    imageProcessingService
        .WithEnvironment("ImageProcessing__Storage__Mode", "AzureBlob")
        .WithEnvironment("ImageProcessing__Storage__AzureBlob__ContainerName", mediaContainerName)
        .WithEnvironment("ImageProcessing__Storage__AzureBlob__ConnectionString", blobs);

    umbraco
        .WithEnvironment("ImageProcessing__Storage__Mode", "AzureBlob")
        .WithEnvironment("Umbraco__Storage__AzureBlob__Media__ContainerName", mediaContainerName)
        .WithEnvironment("Umbraco__Storage__AzureBlob__Media__ConnectionString", blobs);
}
else if (storageMode == "HttpProxy")
{
    // Umbraco's own media storage stays local disk — this mode only flips the Service's own
    // IOriginalImageSource to HttpOriginalImageSource (production-hardening ticket 12); Umbraco needs
    // no storage-side changes at all, and ImageProcessing__Proxy__UmbracoBaseUrl is already wired
    // unconditionally above.
    imageProcessingService.WithEnvironment("ImageProcessing__Storage__Mode", "HttpProxy");
}

// Opt-in database override: absent for normal `dotnet run` (Umbraco keeps its own checked-in SQLite
// connection string, appsettings.json), but lets a consumer point this app's database anywhere —
// used by the E2E test project to run each test against an isolated temp SQLite file instead of the
// developer's real local App_Data database (production-hardening ticket 11).
var umbracoDbConnectionStringOverride = builder.Configuration["ConnectionStrings:umbracoDbDSN"];
if (!string.IsNullOrEmpty(umbracoDbConnectionStringOverride))
{
    umbraco.WithEnvironment("ConnectionStrings__umbracoDbDSN", umbracoDbConnectionStringOverride);
}

builder.Build().Run();
