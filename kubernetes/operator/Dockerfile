# The operator targets net8.0. The build stage uses the SDK matching the library's
# highest TFM (net10.0) so it can restore the multi-targeted src project reference,
# then publishes the operator for net8.0; the runtime image matches that.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ["kubernetes/operator/HmacManager.Operator.csproj", "kubernetes/operator/"]
COPY ["src/HmacManager.csproj", "src/"]
RUN dotnet restore "kubernetes/operator/HmacManager.Operator.csproj"
COPY . .
RUN dotnet publish "kubernetes/operator/HmacManager.Operator.csproj" -c Release -f net8.0 -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HmacManager.Operator.dll"]
