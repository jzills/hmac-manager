# The operator targets net10.0 (.NET 8 left support on 2026-11-10). The build stage
# uses the SDK matching the library's highest TFM, which is also net10.0, and the
# runtime image matches the operator's TFM.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ["kubernetes/operator/HmacManager.Operator.csproj", "kubernetes/operator/"]
COPY ["src/HmacManager/HmacManager.csproj", "src/HmacManager/"]
RUN dotnet restore "kubernetes/operator/HmacManager.Operator.csproj"
COPY . .
RUN dotnet publish "kubernetes/operator/HmacManager.Operator.csproj" -c Release -f net10.0 -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HmacManager.Operator.dll"]
