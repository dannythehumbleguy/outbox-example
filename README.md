# Orders API

ASP.NET Core API for creating orders using clean architecture.

## Tech Stack

- .NET 9.0
- PostgreSQL 17
- Dapper (data access)
- FluentMigrator (database migrations)
- Docker

## Project Structure

```
src/OrderService/
├── OrderService.Domain/          # Entities and enums
├── OrderService.Application/     # Interfaces, DTOs, services
├── OrderService.Infrastructure/  # Dapper repository, migrations
└── OrderService.Api/             # Controllers, configuration
```

## Running with Docker

```bash
docker-compose up --build
```

The API will be available at `http://localhost:5000`.

## API Endpoints

### Create Order

```
POST /api/orders
```

Request body:
```json
{
  "goodsName": "Laptop",
  "price": 999.99,
  "status": 0
}
```

**Status values:** 0=Pending, 1=Processing, 2=Completed, 3=Cancelled
