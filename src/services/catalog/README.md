# catalog

Categories and products: search, filtering, sorting, paging, and admin CRUD. Also the **gRPC server**
other services use for authoritative product data and prices.

## REST endpoints

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/categories` | - | with active product counts |
| POST/PUT | `/api/categories[/{id}]` | Admin | |
| GET | `/api/products` | - | `search`, `category` (slug or id), `minPrice`, `maxPrice`, `sort` (`name`, `name_desc`, `price_asc`, `price_desc`, `newest`, `rating`), `page`, `pageSize` (max 60) |
| GET | `/api/products/{id}` | - | detail with attributes |
| GET | `/api/products/by-slug/{slug}` | - | |
| POST | `/api/products` | Admin | |
| PUT | `/api/products/{id}` | Admin | publishes `catalog.product-price-changed` when the price changes |
| DELETE | `/api/products/{id}` | Admin | soft delete (`isActive=false`) |
| GET | `/api/products/{id}/reviews` | - | visible reviews, `sort` (`newest`, `highest`, `lowest`), average and 1-5 distribution |
| GET / PUT / DELETE | `/api/products/{id}/reviews/me` | user | your review; PUT creates or edits it, 403 unless you bought the product |
| GET | `/api/products/reviews?hidden=&rating=` | Admin | moderation list |
| PUT | `/api/products/reviews/{reviewId}/visibility` | Admin | `{ hidden, reason }` |
| GET | `/health`, `/health/ready` | - | Postgres + Kafka |

## gRPC

`shared/protos/catalog.proto`, served on the HTTP/2-only port **8081** (5012 locally).

| RPC | Purpose |
|---|---|
| `GetProduct` | one product |
| `GetProductsByIds` | batch lookup (max 200), returns `missing_ids` as well |

Prices travel as `price_cents` (int64). Server reflection is enabled in Development, so
`grpcurl -plaintext localhost:5012 list` works.

## Reviews

One review per customer and product, and only from customers with a confirmed order for it. Catalog
never asks orders: it consumes `orders.order-confirmed`, whose payload carries the order lines, and
keeps `(user, product)` pairs in `verified_purchases` (event-carried state transfer, with the usual
`processed_events` inbox). Each write recomputes the product's `rating` and `reviewCount` from the
visible reviews, so product lists can show and sort by them. Hidden reviews drop out of both.

## Kafka

- Produces: `catalog.product-price-changed` (key: productId)
- Consumes (group `catalog`): `orders.order-confirmed` and its `.retry` topic - who bought what, for reviews

## Data

`catalog_db`: `categories`, `products` (attributes as `jsonb`), `product_reviews`, `verified_purchases`,
`processed_events`. Seeded with 6 categories and 40
products; the product ids are shared with the inventory seed.

## Run

```bash
dotnet run --project src/services/catalog/EcommerceShop.Catalog   # REST :5002, gRPC :5012
```
