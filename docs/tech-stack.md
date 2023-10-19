# Tech stack

Every version is pinned exactly - in `.csproj` files, lockfiles (`package-lock.json`),
`requirements.txt` and image tags.

## .NET services (identity, catalog, orders, payments, gateway)

| Technology | Version | Used for |
|---|---|---|
| .NET SDK | 7.0.401 in images; `global.json` pins 7.0.101 with `rollForward: latestFeature` | Building and testing every .NET project in `EcommerceShop.sln` |
| ASP.NET Core runtime | 7.0.11 (`mcr.microsoft.com/dotnet/aspnet:7.0.11`) | Running identity, catalog, orders, payments and the gateway (Minimal APIs, rate limiting, ProblemDetails) |
| C# | 11 | All .NET code (records for contracts and DTOs, file-scoped namespaces) |
| EF Core / `dotnet-ef` | 7.0.2 | Data access and migrations in identity, catalog, orders and payments |
| Npgsql.EntityFrameworkCore.PostgreSQL | 7.0.1 | PostgreSQL provider for those four services; `jsonb` product attributes in catalog |
| Confluent.Kafka | 2.0.2 | Kafka producers and consumers in `shared/EcommerceShop.BuildingBlocks` (orders, payments, catalog) |
| Grpc.AspNetCore / Grpc.Net.ClientFactory / Grpc.Tools | 2.51.0 | catalog's gRPC server and the orders client that fetches prices |
| Google.Protobuf | 3.21.12 | Message types generated from `shared/protos/catalog.proto` |
| Yarp.ReverseProxy | 2.0.1 | Routing in the gateway |
| Polly / Microsoft.Extensions.Http.Polly | 7.2 / 7.0.2 | Retry, circuit breaker, timeout and bulkhead on orders → catalog, cart and payments |
| Microsoft.Extensions.Caching.StackExchangeRedis | 7.0.2 | catalog's product cache in Redis |
| Stripe.net | 41.15.0 | payments: PaymentIntents, lookups and refunds in Stripe test mode |
| Serilog.AspNetCore | 6.1.0 | JSON logs with the correlation id in every .NET service |
| FluentValidation | 11.4.0 | Request validation filters in identity, catalog and orders |
| Swashbuckle.AspNetCore | 6.5.0 | OpenAPI documents and Swagger UI per service, and the gateway's combined UI |
| BCrypt.Net-Next | 4.0.3 | Password hashing in identity |
| xUnit / FluentAssertions | 2.5.0 / 6.12.0 | .NET unit and integration tests in `tests/` |
| Microsoft.AspNetCore.Mvc.Testing / EF Core InMemory | 7.0.2 | In-memory service hosts for the integration tests |
| coverlet.collector | 6.0.0 | Code coverage in the pipeline |

## Python service (inventory)

| Technology | Version | Used for |
|---|---|---|
| Python | 3.11.5 (`python:3.11.5-slim`) | The inventory service and its image |
| FastAPI / Starlette | 0.95.1 / 0.26.1 | inventory's HTTP API and OpenAPI page |
| pydantic | 1.10.7 | Request models and settings (`BaseSettings`) |
| SQLAlchemy | 2.0.9 | Stock, reservations and inbox tables, with row locks for reservations |
| psycopg2-binary | 2.9.6 | PostgreSQL driver |
| confluent-kafka | 2.1.0 | Consuming saga events and publishing `stock-reserved` / `stock-rejected` |
| redis-py | 4.5.4 | Availability cache |
| PyJWT | 2.6.0 | Validating tokens issued by identity |
| uvicorn | 0.21.1 | Serving the app |
| pytest / httpx / fakeredis | 7.3.1 / 0.24.0 / 2.10.3 | inventory tests against SQLite and an in-memory Redis |

## Node services (cart, email)

| Technology | Version | Used for |
|---|---|---|
| Node.js | 18.17.1 (`node:18.17.1-alpine3.18`) | Running cart and email |
| Express | 4.18.2 | cart's HTTP API |
| NestJS | 10.1.0 | email's modules, controllers and dependency injection |
| kafkajs | 2.2.4 | Kafka consumers in cart (price changes) and email (saga events) |
| ioredis | 5.3.2 | Cart storage and the Redis inboxes of cart and email |
| cockatiel | 3.1.1 | Circuit breaker, retry, timeout and bulkhead on cart → catalog |
| zod | 3.21.4 | cart request validation |
| class-validator / class-transformer | 0.14.0 / 0.5.1 | email DTO validation |
| nodemailer / handlebars | 6.9.4 / 4.7.7 | Sending mail over SMTP and rendering the HTML templates |
| jsonwebtoken | 9.0.0 (cart), 9.0.1 (email) | JWT validation |
| Jest / ts-jest / supertest | 29.5 (cart) and 29.6 (email) / 29.1.1 / 6.3.3 | cart and email tests |
| ioredis-mock | 8.7.0 | Redis in those tests |

## Frontend

| Technology | Version | Used for |
|---|---|---|
| React | 18.2.0 | The storefront and admin UI in `react-frontend/` |
| Vite | 4.4.9 | Dev server with the `/api` proxy, and the production build |
| TypeScript | 5.1.6 | The typed API client and all pages |
| React Router | 6.15.0 | Page routes, including the nested admin area |
| TanStack Query | 4.33.0 | Fetching and caching API data; polling the order page while the saga runs |
| Tailwind CSS | 3.3.3 | Styling |
| Vitest / Testing Library | 0.34.2 / 14.0.0 | Component and page tests in jsdom |
| nginx | 1.25.2 (`nginx:1.25.2-alpine`) | Serving the built app and proxying `/api` in the web container |

## Messaging, data and infrastructure

| Technology | Version | Used for |
|---|---|---|
| Apache Kafka | 3.5.1 (Bitnami image, KRaft mode - no ZooKeeper) | Saga events, price changes, refunds, and their retry and dead-letter topics |
| Kafka UI | v0.7.1 (provectuslabs) | Browsing topics and messages locally |
| PostgreSQL | 15.4 (`postgres:15.4-alpine`) | The identity, catalog, orders, payments and inventory databases |
| Redis | 7.2.1 (`redis:7.2.1-alpine`) | Carts, the catalog and inventory caches, and the cart and email inboxes |
| MailHog | v1.0.1 | Catching outgoing mail locally |
| Grafana Loki / Promtail / Grafana | 2.8.4 / 2.8.4 / 10.0.3 | Collecting and searching logs from all containers |
| Docker Compose | Compose spec v2 | Running the whole stack locally (`deploy/docker-compose.yml`) |
| Kubernetes / Kustomize | AKS 1.27 APIs; Kustomize built into kubectl | `deploy/k8s` base plus `dev` / `prod` overlays |
| Azure DevOps Pipelines | multi-stage YAML | Build and test, images to ACR, deploys to AKS dev and prod (`azure-pipelines.yml`) |
