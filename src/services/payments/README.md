# payments

Charges an order once its stock is reserved. Two providers:

- **Stripe (test mode)** - when `Stripe__SecretKey` is an `sk_test_...` key. Creates and confirms a
  PaymentIntent with the payment method chosen at checkout (`pm_card_visa`,
  `pm_card_chargeDeclined`, ...). Any key that isn't a test key stops the service at startup.
- **Mock** - when no key is set. Understands the same test payment method ids, declines
  `pm_card_chargeDeclined` / `..._chargeDeclinedInsufficientFunds`, and refuses amounts over 5,000.

Exactly one payment record per order (unique index on `OrderId`). The Stripe call uses the
idempotency key `ecommerce-shop-order-<orderId>`; a redelivered event re-publishes the stored outcome
instead of charging again.

## Endpoints

| Method | Path | Auth |
|---|---|---|
| GET | `/api/payments/orders/{orderId}` | owner / Admin |
| GET | `/api/payments?status=` | Admin |
| GET | `/api/payments/provider` | - (`mock` or `stripe`) |
| GET | `/health`, `/health/ready` | - |

## Kafka

- Consumes (group `payments`): `inventory.stock-reserved`, `orders.order-cancelled` and their `.retry` topics
- Produces: `payments.payment-succeeded`, `payments.payment-failed`, `payments.payment-refunded`

## Reconciliation

Every 15 minutes (`Reconciliation__IntervalMinutes`) and on demand from the admin page,
[ReconciliationService.cs](EcommerceShop.Payments/Application/ReconciliationService.cs) compares the
last 7 days of payments with the provider and with the orders it knows were cancelled
(`orders.order-cancelled`, kept in `cancelled_orders`):

| Finding | What happens |
|---|---|
| Charged, but the order was cancelled | Refunded automatically, `payments.payment-refunded` published |
| Recorded as failed, but the provider took the money | Refunded automatically |
| Amount or status differs from the provider, or the provider has no such payment | Recorded for review, nothing changed |

Refunds use `refund-<orderId>` as the idempotency key, so a retried run never refunds twice, and a
Postgres advisory lock keeps two replicas from running at once. The mock provider remembers its
charges in memory only; after a restart older payments are counted as *unverified* rather than flagged.

## Run

```bash
dotnet run --project src/services/payments/EcommerceShop.Payments   # http://localhost:5004
# with Stripe test mode:
Stripe__SecretKey=sk_test_... dotnet run --project src/services/payments/EcommerceShop.Payments
```
