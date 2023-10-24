# ecommerce-shop

An online store built as a set of small services: customers browse a catalog, fill a cart and check
out, and checkout runs as an **event-driven saga over Kafka** (reserve stock → take payment → confirm,
or compensate). It is a learning and portfolio project for people who want to see common
microservice patterns - API gateway, saga, outbox/inbox, circuit breakers, cache-aside - working
together in one repository that starts with a single `docker compose up`.

## Features

- **Catalog** - 6 categories and 40 products with images and attributes; search, category and price filters, sorting, paging
- **Reviews** - star ratings and comments from customers who actually bought the product, average rating on every product, admin moderation
- **Cart** - per-user carts that survive for a week, quantity edits, a warning when a price changed since you added an item
- **Checkout & orders** - one-click checkout with Stripe test cards, order history, a live status timeline, cancellation while pending
- **Inventory** - stock levels, reservations that expire, a low-stock view
- **Payments** - Stripe test mode, or an offline mock that behaves the same; one payment record per order; scheduled reconciliation that refunds charges for cancelled orders
- **Emails** - order placed, confirmed, cancelled, payment failed and refund issued, viewable in MailHog
- **Admin** - product editor, stock adjustments, order list with filters, daily sales summary, review moderation, payment reconciliation
- **Accounts** - register / sign in, customer and admin roles

## Tech stack

.NET 7 · ASP.NET Core · EF Core · gRPC · YARP · Kafka · PostgreSQL · Redis · Stripe (test mode) · Python/FastAPI ·
Node/Express/NestJS · React/Vite/Tailwind · Docker · Docker Compose · Kubernetes (Kustomize) · Azure DevOps · Grafana Loki

## Getting started

Prerequisites: Docker Desktop (or Docker Engine with Compose v2). Nothing else is needed to run it.

```bash
cp .env.example .env
docker compose -f deploy/docker-compose.yml --env-file .env up --build
```

The first start takes a few minutes (image builds, migrations, topic creation, seeding).
Leave `STRIPE_SECRET_KEY` empty to use the built-in mock payment provider, or paste your own Stripe
**test** key (`sk_test_...`) to charge through Stripe test mode. Live keys are refused at startup.

| What | URL |
|---|---|
| Shop | http://localhost:3000 |
| API docs (Swagger for every service, via the gateway) | http://localhost:5000/swagger |
| Kafka UI (topics, retry and dead-letter queues) | http://localhost:8085 |
| MailHog (sent emails) | http://localhost:8025 |
| Grafana (aggregated logs, search by correlation id) | http://localhost:3030 |

On macOS port 5000 may be taken by AirPlay Receiver - set `GATEWAY_PORT` in `.env`.
Running `docker compose up` from inside `deploy/` also loads the dev override, which publishes every
service on its own host port.

To start again from fresh demo data, drop the volumes; the next `up` migrates and seeds again:

```bash
docker compose -f deploy/docker-compose.yml down -v
```

## Seed data and demo logins

In Development (the compose default) the seeders create 6 categories, 40 products with stock levels
(a few deliberately sold out or low), the demo accounts below, and an order history for Jane - two
confirmed orders and one cancelled after a declined card - with matching payment records, and Jane's
reviews of three of the four products she bought.

| Role | Email |
|---|---|
| Customer | `customer@ecommerceshop.local` |
| Customer (with order history and reviews) | `jane@ecommerceshop.local` |
| Admin | `admin@ecommerceshop.local` |

All demo accounts use the password `Demo@12345`. It comes from `SEED_DEMO_PASSWORD` in
`.env.example` and is used only by the Development seeder - it is local demo data, not a real
credential. Outside Development no demo accounts are created.

At checkout, pick the `4242 4242 4242 4242` test card for a successful order, or
`4000 0000 0000 0002` to see the payment-failure compensation. A sold-out product shows the
inventory rejection path.

## Running tests

```bash
dotnet test EcommerceShop.sln
```

The Python, Node and React suites run with `pytest` / `npm test` inside each service folder.
