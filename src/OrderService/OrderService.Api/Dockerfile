FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OrderService.Domain/OrderService.Domain.csproj", "OrderService.Domain/"]
COPY ["OrderService.Application/OrderService.Application.csproj", "OrderService.Application/"]
COPY ["OrderService.Infrastructure/OrderService.Infrastructure.csproj", "OrderService.Infrastructure/"]
COPY ["OrderService.Api/OrderService.Api.csproj", "OrderService.Api/"]

RUN dotnet restore "OrderService.Api/OrderService.Api.csproj"

COPY . .

WORKDIR /src/OrderService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
