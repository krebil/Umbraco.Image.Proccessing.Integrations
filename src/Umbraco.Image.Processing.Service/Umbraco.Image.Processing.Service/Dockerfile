# Reference Dockerfile for Umbraco.Image.Processing.Service.
#
# Unsupported, like the rest of the reference implementation (see CONTEXT.md) — no CI builds or
# publishes this image; build and push it by hand when you need one.
#
# Build from the REPO ROOT (needs sibling project references):
#   docker build -f src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service/Dockerfile \
#     --build-arg IMAGE_PROCESSOR=ImageFlow -t umbraco-image-processing-service:imageflow .
#
# IMAGE_PROCESSOR selects which processor(s) get compiled in: SkiaSharp, ImageFlow, or All (default,
# both — today's runtime-config-switch behavior). Matches the ImageProcessor MSBuild property on
# Umbraco.Image.Processing.Service.csproj.
#
# Only linux-x64 is targeted: Imageflow.AllPlatforms/SkiaSharp.NativeAssets.* ship every supported
# RID's native binaries, and a container only ever runs one. Both stages are pinned to
# linux/amd64 to match — building on an arm64 host (e.g. Apple Silicon) needs QEMU emulation
# (Docker Desktop enables this by default) rather than switching the RID to the host's own
# architecture.

ARG IMAGE_PROCESSOR=All

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG IMAGE_PROCESSOR
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Umbraco.Image.Processing.Core/Umbraco.Image.Processing.Core/*.csproj src/Umbraco.Image.Processing.Core/Umbraco.Image.Processing.Core/
COPY src/Umbraco.Image.Processing.SkiaSharp/Umbraco.Image.Processing.SkiaSharp/*.csproj src/Umbraco.Image.Processing.SkiaSharp/Umbraco.Image.Processing.SkiaSharp/
COPY src/Umbraco.Image.Processing.ImageFlow/Umbraco.Image.Processing.ImageFlow/*.csproj src/Umbraco.Image.Processing.ImageFlow/Umbraco.Image.Processing.ImageFlow/
COPY src/Umbraco.Image.Processing.AzureBlob/Umbraco.Image.Processing.AzureBlob/*.csproj src/Umbraco.Image.Processing.AzureBlob/Umbraco.Image.Processing.AzureBlob/
COPY src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service/*.csproj src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service/
RUN dotnet restore src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service.csproj \
    -p:ImageProcessor=$IMAGE_PROCESSOR -r linux-x64

COPY src/Umbraco.Image.Processing.Core/Umbraco.Image.Processing.Core/ src/Umbraco.Image.Processing.Core/Umbraco.Image.Processing.Core/
COPY src/Umbraco.Image.Processing.SkiaSharp/Umbraco.Image.Processing.SkiaSharp/ src/Umbraco.Image.Processing.SkiaSharp/Umbraco.Image.Processing.SkiaSharp/
COPY src/Umbraco.Image.Processing.ImageFlow/Umbraco.Image.Processing.ImageFlow/ src/Umbraco.Image.Processing.ImageFlow/Umbraco.Image.Processing.ImageFlow/
COPY src/Umbraco.Image.Processing.AzureBlob/Umbraco.Image.Processing.AzureBlob/ src/Umbraco.Image.Processing.AzureBlob/Umbraco.Image.Processing.AzureBlob/
COPY src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service/ src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service/

RUN dotnet publish src/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service/Umbraco.Image.Processing.Service.csproj \
    -c Release -r linux-x64 --self-contained false -p:ImageProcessor=$IMAGE_PROCESSOR \
    -o /app/publish

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Umbraco.Image.Processing.Service.dll"]
