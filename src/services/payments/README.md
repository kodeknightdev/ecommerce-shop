# payments

Charges an order once its stock is reserved. The payment logic only talks to `IPaymentProvider`, an
**anti-corruption layer** with two implementations:

- **Stripe (test mode)** - used when `Stripe__SecretKey` is an `sk_test_...` key. Creates and confirms a PaymentIntent with the payment method chosen at checkout (`pm_card_visa`, `pm_card_chargeDeclined`, ...). Any key that isn't a test key stops the service at startup. Only [StripePaymentProvider.cs](EcommerceShop.Payments/Providers/StripePaymentProvider.cs) references Stripe types.
- **Mock** - used when no key is set. Understands the same test payment method ids, declines `pm_card_chargeDeclined` / `..._chargeDeclinedInsufficientFunds`, and refuses amounts over 5,000.

Exactly one payment per order:
- The **order id is the Stripe idempotency key**.
- A unique index on `OrderId` prevents a second record.
- The `processed_events` inbox skips a redelivered event.

The payment record, the inbox row and the outcome event (in `outbox_messages`) are saved in **one
transaction**, and the shared outbox relay publishes the outcome.

## Endpoints

| Method | Path | Auth |
|---|---|---|
| GET | `/api/payments/orders/{orderId}` | owner / Admin (also used by orders' details composition) |
| GET | `/api/payments?status=` | Admin |
| GET | `/api/payments/provider` | - (`mock` or `stripe`) |
| POST | `/api/payments/reconciliation/runs` | Admin - run now |
| GET | `/api/payments/reconciliation/runs`, `/runs/{id}` | Admin |
| GET | `/health`, `/health/ready` | - |

## Kafka

- Consumes (group `payments`): `inventory.stock-reserved`, `orders.order-cancelled` and their `.retry` topics
- Produces (outbox): `payments.payment-succeeded`, `payments.payment-failed`, `payments.payment-refunded`

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

In Development, payment records for Jane's seeded orders are created
([DemoPaymentSeeder.cs](EcommerceShop.Payments/Data/DemoPaymentSeeder.cs)).
