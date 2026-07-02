# build-id: 2026-07-02
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ClosingTechGaps.Web/ClosingTechGaps.Web.csproj", "ClosingTechGaps.Web/"]
COPY ["ClosingTechGaps.Application/ClosingTechGaps.Application.csproj", "ClosingTechGaps.Application/"]
COPY ["ClosingTechGaps.Domain/ClosingTechGaps.Domain.csproj", "ClosingTechGaps.Domain/"]
COPY ["ClosingTechGaps.Infrastructure/ClosingTechGaps.Infrastructure.csproj", "ClosingTechGaps.Infrastructure/"]
RUN dotnet restore "ClosingTechGaps.Web/ClosingTechGaps.Web.csproj"
COPY . .
RUN dotnet publish "ClosingTechGaps.Web/ClosingTechGaps.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ClosingTechGaps.Web.dll"]
