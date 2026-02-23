FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PaymentService.Domain/PaymentService.Domain.csproj", "PaymentService.Domain/"]
COPY ["PaymentService.Application/PaymentService.Application.csproj", "PaymentService.Application/"]
COPY ["PaymentService.Infrastructure/PaymentService.Infrastructure.csproj", "PaymentService.Infrastructure/"]
COPY ["PaymentService.Api/PaymentService.Api.csproj", "PaymentService.Api/"]

RUN dotnet restore "PaymentService.Api/PaymentService.Api.csproj"

COPY . .

WORKDIR /src/PaymentService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]
