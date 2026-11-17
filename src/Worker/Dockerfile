# Build from the repo root: docker build -f src/Worker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Contracts/ src/Contracts/
COPY src/Worker/ src/Worker/
RUN dotnet publish src/Worker -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:11.0
RUN useradd --uid 10001 --no-create-home app
USER 10001
COPY --from=build /out /app
ENTRYPOINT ["dotnet", "/app/CodeBench.Worker.dll"]
