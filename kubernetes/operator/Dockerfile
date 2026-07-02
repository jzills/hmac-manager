# The operator targets net8.0 (single TFM). Base images are pinned to 8.0 accordingly.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo
COPY ["kubernetes/operator/HmacManager.Operator.csproj", "kubernetes/operator/"]
COPY ["src/HmacManager.csproj", "src/"]
RUN dotnet restore "kubernetes/operator/HmacManager.Operator.csproj"
COPY . .
RUN dotnet publish "kubernetes/operator/HmacManager.Operator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HmacManager.Operator.dll"]
