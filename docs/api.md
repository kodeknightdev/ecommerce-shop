# API

Everything is reached through the gateway at `http://localhost:5000` (or `GATEWAY_PORT`). Errors are
always RFC 7807 `application/problem+json`. Most also carry `instance` and `correlationId`; the id is
in the `X-Correlation-ID` header of every response either way. Authenticated calls take
`Authorization: Bearer <access token>` from `/api/auth/login`.

## OpenAPI / Swagger

| Service | Through the gateway | Directly (dev override ports) |
|---|---|---|
| all services, one UI | http://localhost:5000/swagger | - |
| identity | `/docs/identity/swagger.json` | http://localhost:5001/swagger |
| catalog | `/docs/catalog/swagger.json` | http://localhost:5002/swagger |
| orders | `/docs/orders/swagger.json` | http://localhost:5003/swagger |
| payments | `/docs/payments/swagger.json` | http://localhost:5004/swagger |
| inventory | `/docs/inventory/openapi.json` | http://localhost:8001/docs |
| cart | `/docs/cart/openapi.json` | http://localhost:3001/api-docs |
| email | `/docs/email/openapi.json` | http://localhost:3002/api-docs |

## identity

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/auth/register` | - | creates a Customer, returns tokens |
| POST | `/api/auth/login` | - | rate limited (10/min per IP) |
| POST | `/api/auth/refresh` | - | rotates the refresh token; replaying an old one revokes the family |
| POST | `/api/auth/logout` | - | revokes the refresh token |
| GET | `/api/auth/me` | user | |
| GET | `/api/users?search=&page=` | Admin | |
| PUT | `/api/users/{id}/role` | Admin | `Admin` or `Customer` |

**Example - sign in, then rotate the refresh token:**

```http
POST /api/auth/login
Content-Type: application/json

{ "email": "customer@ecommerceshop.local", "password": "Demo@12345" }
```

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ…",
  "expiresAt": "2023-10-16T22:36:02.4698347+00:00",
  "refreshToken": "L2uJsT-lE-LOHw1Wgth1dL-S8Rri6sCUWCasO0n-…",
  "refreshTokenExpiresAt": "2023-10-23T22:21:02.4698347+00:00",
  "user": {
    "id": "8e3a4d55-2c1b-4f3a-a6d4-7b9c0e1f2a02",
    "email": "customer@ecommerceshop.local",
    "fullName": "Casey Customer",
    "role": "Customer",
    "createdAt": "2023-10-16T22:18:45.882894+00:00"
  }
}
```

`POST /api/auth/refresh` with `{ "refreshToken": "..." }` returns the same shape with a new pair.
Sending the old refresh token again revokes the whole family:

```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Refresh token expired or revoked",
  "status": 401
}
```

## catalog

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/categories` | - | with product counts |
| POST / PUT | `/api/categories[/{id}]` | Admin | |
| GET | `/api/products` | - | `search`, `category` (slug or id), `minPrice`, `maxPrice`, `sort` (`name`, `name_desc`, `price_asc`, `price_desc`, `newest`), `page`, `pageSize` ≤ 60 |
| GET | `/api/products/{id}` | - | cache-aside; response header `X-Cache: HIT\|MISS` |
| GET | `/api/products/by-slug/{slug}` | - | cache-aside |
| POST | `/api/products` | Admin | |
| PUT | `/api/products/{id}` | Admin | invalidates the cache; a price change publishes `catalog.product-price-changed` |
| DELETE | `/api/products/{id}` | Admin | soft delete |
| GET | `/api/products/{id}/reviews` | - | visible reviews: `sort` (`newest`, `highest`, `lowest`), `page`, `pageSize`; average and 1-5 distribution |
| GET | `/api/products/{id}/reviews/me` | user | `{ canReview, review, hidden }` for the product page |
| PUT | `/api/products/{id}/reviews/me` | user | `{ rating, title?, comment }`; 201 on create, 200 on edit, 403 unless you bought it |
| DELETE | `/api/products/{id}/reviews/me` | user | |
| GET | `/api/products/reviews?hidden=&rating=&page=` | Admin | moderation list |
| PUT | `/api/products/reviews/{reviewId}/visibility` | Admin | `{ hidden, reason? }` |

**Example - one product, twice:**

```bash
curl -i http://localhost:5000/api/products/c5e86daf-d93e-5727-95ea-bcdc9a873dff   # X-Cache: MISS
curl -i http://localhost:5000/api/products/c5e86daf-d93e-5727-95ea-bcdc9a873dff   # X-Cache: HIT
```

```json
{
  "id": "c5e86daf-d93e-5727-95ea-bcdc9a873dff",
  "sku": "B-1019",
  "name": "The Quiet Coast",
  "slug": "the-quiet-coast",
  "description": "The Quiet Coast - an ecommerce-shop favourite. Built to last and priced fairly.",
  "price": 14.99,
  "currency": "USD",
  "imageUrl": "https://picsum.photos/seed/the-quiet-coast/640/480",
  "categoryId": "8143b8b5-8003-5720-bedb-44c8bb6170c0",
  "categoryName": "Books",
  "categorySlug": "books",
  "attributes": {
    "pages": "320",
    "author": "J. Brennan",
    "format": "Paperback"
  },
  "isActive": true,
  "rating": 0,
  "reviewCount": 0,
  "createdAt": "2023-10-16T22:19:44.145655+00:00",
  "updatedAt": "2023-10-16T22:19:44.145655+00:00"
}
```

A bad filter is a validation problem:

```bash
curl "http://localhost:5000/api/products?minPrice=50&maxPrice=10"
```

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "minPrice": [
      "minPrice must be less than or equal to maxPrice."
    ]
  }
}
```

**Example - reviews.** Jane bought the bottle in a seeded order, so she may review it:

```http
PUT /api/products/b09a7e72-39b3-559c-8e31-f7549f90dbf5/reviews/me
Authorization: Bearer <Jane's access token>
Content-Type: application/json

{"rating": 5, "title": "Still cold after a day", "comment": "Ice was still there the next morning. Fits a car cup holder."}
```

`201 Created`:

```json
{
  "id": "e5ebc1d6-547e-4c0a-8ea2-8bc5c84d7d94",
  "productId": "b09a7e72-39b3-559c-8e31-f7549f90dbf5",
  "authorName": "Jane D.",
  "rating": 5,
  "title": "Still cold after a day",
  "comment": "Ice was still there the next morning. Fits a car cup holder.",
  "createdAt": "2023-10-17T16:17:48.5071557+00:00",
  "updatedAt": "2023-10-17T16:17:48.5071564+00:00"
}
```

The same request from `customer@ecommerceshop.local`, who has not bought it:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "Only customers who bought this product can review it.",
  "instance": "/api/products/b09a7e72-39b3-559c-8e31-f7549f90dbf5/reviews/me",
  "correlationId": "c0470b35a6f44049af3d442186039cce"
}
```

As soon as that customer's own order for the bottle is confirmed, `GET .../reviews/me` changes from
`{ "canReview": false, ... }` to:

```json
{
  "canReview": true,
  "review": null,
  "hidden": false
}
```

The list, after an admin hid the customer's review (`PUT /api/products/reviews/{reviewId}/visibility`
with `{ "hidden": true, "reason": "Duplicate of a support ticket" }`):

```bash
curl "http://localhost:5000/api/products/b09a7e72-39b3-559c-8e31-f7549f90dbf5/reviews"
```

```json
{
  "total": 1,
  "averageRating": 5,
  "reviewCount": 1,
  "distribution": {
    "1": 0,
    "2": 0,
    "3": 0,
    "4": 0,
    "5": 1
  },
  "items": [
    "... Jane's review ..."
  ],
  "page": 1,
  "pageSize": 10
}
```

Product lists and details carry `rating` and `reviewCount` (`sort=rating` orders by them).

### gRPC - `shared/protos/catalog.proto`

Served by catalog on port 8081 (HTTP/2 only), consumed by orders.

| RPC | Request → Reply | Notes |
|---|---|---|
| `GetProduct` | `product_id` → `ProductReply` | `NotFound` / `InvalidArgument` status codes |
| `GetProductsByIds` | `product_ids[]` (≤ 200) → `products[]`, `missing_ids[]` | prices as `price_cents` (int64) |

## cart

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/cart` | user | items, `itemCount`, `subtotal`, `hasStalePrices` |
| POST | `/api/cart/items` | user | `{ productId, quantity }`; price looked up in catalog |
| PUT | `/api/cart/items/{productId}` | user | `{ quantity }`, 0 removes |
| DELETE | `/api/cart/items/{productId}` | user | |
| DELETE | `/api/cart` | user | used by orders after checkout |
| POST | `/api/cart/validate` | user | re-reads every line from catalog |

**Example - add an item:**

```http
POST /api/cart/items
Authorization: Bearer <access token>
Content-Type: application/json

{ "productId": "c5e86daf-d93e-5727-95ea-bcdc9a873dff", "quantity": 2 }
```

```json
{
  "userId": "8e3a4d55-2c1b-4f3a-a6d4-7b9c0e1f2a02",
  "items": [
    {
      "productId": "c5e86daf-d93e-5727-95ea-bcdc9a873dff",
      "sku": "B-1019",
      "name": "The Quiet Coast",
      "imageUrl": "https://picsum.photos/seed/the-quiet-coast/640/480",
      "unitPrice": 14.99,
      "quantity": 2,
      "priceStale": false,
      "addedAt": "2023-10-16T22:21:04.072Z"
    }
  ],
  "itemCount": 2,
  "subtotal": 29.98,
  "hasStalePrices": false,
  "updatedAt": "2023-10-16T22:21:04.072Z"
}
```

After an admin changes the price of something in the cart, `GET /api/cart` flags the line instead of
silently changing it:

```json
{
  "userId": "8e3a4d55-2c1b-4f3a-a6d4-7b9c0e1f2a02",
  "items": [
    {
      "productId": "35a25877-1ae7-5dcd-8c92-f25de0de1b5a",
      "sku": "B-1017",
      "name": "Night Garden",
      "imageUrl": "https://picsum.photos/seed/night-garden/640/480",
      "unitPrice": 16.99,
      "quantity": 1,
      "priceStale": true,
      "addedAt": "2023-10-16T22:24:11.151Z",
      "latestPrice": 18.49
    }
  ],
  "itemCount": 1,
  "subtotal": 16.99,
  "hasStalePrices": true,
  "updatedAt": "2023-10-16T22:24:18.650Z"
}
```

Invalid input (zod):

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/cart/items",
  "correlationId": "7cf01377a3bb49dfbb8aae983f692e13",
  "errors": {
    "productId": [
      "productId must be a UUID"
    ],
    "quantity": [
      "Number must be greater than or equal to 1"
    ]
  }
}
```

## orders

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/orders` | user | `{ shippingAddress, paymentMethod }` with `pm_card_*`; optional `Idempotency-Key` header → 201, or 200 for a replay |
| GET | `/api/orders?page=` | user | own orders |
| GET | `/api/orders/{id}` | owner / Admin | order with status timeline |
| GET | `/api/orders/{id}/details` | owner / Admin | **API composition**: `{ order, payment, products, warnings }` |
| POST | `/api/orders/{id}/cancel` | owner / Admin | while Pending / InventoryReserved |
| GET | `/api/admin/orders` | Admin | `status`, `email`, `from`, `to`, `page` |
| GET | `/api/admin/sales-summary?days=14` | Admin | orders and confirmed revenue per day |

**Example - place an order:**

```http
POST /api/orders
Authorization: Bearer <access token>
Idempotency-Key: 5fc7670e-41f6-4a1c-a74b-25211131fcd9
Content-Type: application/json

{
  "shippingAddress": {
    "fullName": "Casey Customer",
    "line1": "1 Main St",
    "city": "Springfield",
    "postalCode": "12345",
    "country": "US"
  },
  "paymentMethod": "pm_card_visa"
}
```

`201 Created`, with `X-Correlation-ID: 0e8c083735dc475fb4900ea986df03f9`:

```json
{
  "id": "963809ee-aabd-42b1-af2e-236cd87c19bf",
  "number": "ES-231016-653760",
  "status": "Pending",
  "total": 29.98,
  "currency": "USD",
  "email": "customer@ecommerceshop.local",
  "paymentMethod": "pm_card_visa",
  "shippingAddress": {
    "fullName": "Casey Customer",
    "line1": "1 Main St",
    "line2": null,
    "city": "Springfield",
    "postalCode": "12345",
    "country": "US"
  },
  "items": [
    {
      "productId": "c5e86daf-d93e-5727-95ea-bcdc9a873dff",
      "sku": "B-1019",
      "name": "The Quiet Coast",
      "quantity": 2,
      "unitPrice": 14.99,
      "lineTotal": 29.98,
      "imageUrl": "https://picsum.photos/seed/the-quiet-coast/640/480"
    }
  ],
  "timeline": [
    {
      "from": null,
      "to": "Pending",
      "note": "Order placed",
      "occurredAt": "2023-10-16T22:21:04.5696235+00:00"
    }
  ],
  "cancellationReason": null,
  "canCancel": true,
  "createdAt": "2023-10-16T22:21:04.5696066+00:00",
  "updatedAt": "2023-10-16T22:21:04.5696066+00:00",
  "priceAdjustments": []
}
```

The same request with the same `Idempotency-Key` returns `200 OK` and the same order
(`"id": "963809ee-aabd-42b1-af2e-236cd87c19bf"`) - nothing new is saved or published.

Validation, a missing order and a customer calling an admin endpoint:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PaymentMethod": [
      "PaymentMethod must be a Stripe payment method id such as pm_card_visa."
    ],
    "ShippingAddress.Country": [
      "Country must be a 2-letter ISO code."
    ]
  }
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Resource not found",
  "status": 404,
  "detail": "Order 77a66498-9007-485e-a744-e43acf19e9af not found.",
  "instance": "/api/orders/77a66498-9007-485e-a744-e43acf19e9af",
  "correlationId": "777804244d2240e2a8ac9444867d9777"
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "instance": "/api/admin/sales-summary",
  "correlationId": "00e11f5931894f4390bfca5c3f35f587"
}
```

`GET /api/orders/{id}/details` is shown in full in
[how-it-works.md](how-it-works.md#5-order-details---one-call-three-services).

## payments

| Method | Path | Auth |
|---|---|---|
| GET | `/api/payments/orders/{orderId}` | owner / Admin |
| GET | `/api/payments?status=` | Admin |
| GET | `/api/payments/provider` | - (`mock` or `stripe`) |
| POST | `/api/payments/reconciliation/runs` | Admin - run now; 409 if a run is already in progress |
| GET | `/api/payments/reconciliation/runs?page=` | Admin - latest runs, newest first |
| GET | `/api/payments/reconciliation/runs/{id}` | Admin - one run with its issues |

**Example - reconciliation.** Order `a033ef3c-134b-4869-8ae9-68cd0c67f9ee` was charged after the customer had
already cancelled it (payments was stopped at the time). A manual run:

```bash
curl -X POST localhost:5000/api/payments/reconciliation/runs -H "Authorization: Bearer $ADMIN_TOKEN"
```

```json
{
  "id": "d893ad61-988f-4163-a15a-389810b8505b",
  "trigger": "manual",
  "since": "2023-10-10T16:18:37.4096487+00:00",
  "startedAt": "2023-10-17T16:18:37.4097347+00:00",
  "finishedAt": "2023-10-17T16:18:37.7748198+00:00",
  "checked": 3,
  "unverified": 2,
  "issueCount": 1,
  "refunded": 1,
  "issues": [
    {
      "id": "010c1522-ae70-4fd3-84e6-aa80ec6dc7fd",
      "paymentId": "c7539ad9-b895-4496-b531-c276f3db6e6b",
      "orderId": "a033ef3c-134b-4869-8ae9-68cd0c67f9ee",
      "kind": "PaidButCancelled",
      "resolution": "Refunded",
      "detail": "Order was cancelled (Cancelled by customer) after the payment went through.",
      "createdAt": "2023-10-17T16:18:37.7545407+00:00"
    }
  ]
}
```

`unverified` counts payments the provider could not answer for - the mock forgets its charges on a
restart. Running it again finds nothing: the payment is now `Refunded` (`refundReference`,
`refundedAt` are set on `GET /api/payments/orders/{orderId}`), so the second run reports
`"issueCount": 0`.

## inventory

| Method | Path | Auth |
|---|---|---|
| GET | `/api/inventory/{productId}` | - (`available`, `inStock`, `lowStock`; cached 15 s) |
| POST | `/api/inventory/availability` `{ productIds }` | - |
| GET | `/api/inventory?lowStock=&page=&pageSize=` | Admin |
| POST | `/api/inventory/{productId}/adjust` `{ delta, reason, sku?, lowStockThreshold? }` | Admin |
| GET | `/api/inventory/reservations?status=` | Admin |

**Example:**

```bash
curl http://localhost:5000/api/inventory/c5e86daf-d93e-5727-95ea-bcdc9a873dff
```

```json
{
  "productId": "c5e86daf-d93e-5727-95ea-bcdc9a873dff",
  "sku": "B-1019",
  "available": 8,
  "inStock": true,
  "lowStock": false
}
```

## email

| Method | Path | Auth |
|---|---|---|
| GET | `/api/emails/recent?limit=` | Admin |
| GET | `/api/emails/templates` | Admin |
| POST | `/api/emails/preview` `{ template, data }` → HTML | Admin |

## Health (every service)

`GET /health` - liveness, no dependencies. `GET /health/ready` - readiness with per-dependency
checks. The gateway's readiness pings every downstream service and reports `Degraded` when one is
down.

```bash
curl http://localhost:5000/health/ready
```

```json
{
  "status": "Healthy",
  "durationMs": 28.9324,
  "checks": [
    {
      "name": "downstream",
      "status": "Healthy",
      "description": "All services reachable",
      "error": null
    }
  ]
}
```

## Events

The Kafka topics and payloads are listed in [architecture.md](architecture.md#event-and-topic-catalog).
