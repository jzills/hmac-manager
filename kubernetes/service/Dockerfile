ARG DOTNET_VERSION=10

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}.0 AS base
WORKDIR /app
EXPOSE 8080

# SDK must match the library's highest TFM. DOTNET_VERSION controls the runtime
# image and published framework only; update this line when a new TFM is added.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG DOTNET_VERSION
WORKDIR /repo
COPY ["kubernetes/service/HmacManager.Kubernetes.csproj", "kubernetes/service/"]
COPY ["src/HmacManager/HmacManager.csproj", "src/HmacManager/"]
RUN dotnet restore "kubernetes/service/HmacManager.Kubernetes.csproj"
COPY . .
RUN dotnet build "kubernetes/service/HmacManager.Kubernetes.csproj" \
    -c Release -f net${DOTNET_VERSION}.0 -o /app/build

FROM build AS publish
ARG DOTNET_VERSION
RUN dotnet publish "kubernetes/service/HmacManager.Kubernetes.csproj" \
    -c Release -f net${DOTNET_VERSION}.0 -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HmacManager.Kubernetes.dll"]
