FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY Trips.sln ./
COPY src/Trips.Domain/Trips.Domain.vbproj src/Trips.Domain/
COPY src/Trips.Application/Trips.Application.vbproj src/Trips.Application/
COPY src/Trips.Infrastructure/Trips.Infrastructure.vbproj src/Trips.Infrastructure/
COPY src/Trips.Api/Trips.Api.vbproj src/Trips.Api/
COPY tests/Trips.Tests/Trips.Tests.vbproj tests/Trips.Tests/
RUN dotnet restore Trips.sln

COPY . .
RUN dotnet publish src/Trips.Api/Trips.Api.vbproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Trips.Api.dll"]
