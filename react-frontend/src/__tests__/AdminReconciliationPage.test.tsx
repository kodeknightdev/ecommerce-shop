import { fireEvent, screen } from '@testing-library/react';
import { AdminReconciliationPage } from '../pages/admin/AdminReconciliationPage';
import { mockFetch, renderWithProviders, signIn } from '../test/utils';

const at = '2023-09-01T10:00:00Z';
const run = {
  id: 'run1', trigger: 'manual', since: at, startedAt: at, finishedAt: at, checked: 12, unverified: 0, issueCount: 2, refunded: 1,
  issues: [
    { id: 'i1', paymentId: 'pay1', orderId: 'a7e0c1d2-0001', kind: 'PaidButCancelled', resolution: 'Refunded',
      detail: 'Order was cancelled (Inventory: Reservation expired) after the payment went through.', createdAt: at },
    { id: 'i2', paymentId: 'pay2', orderId: 'b8f1d2e3-0002', kind: 'AmountMismatch', resolution: 'NeedsReview',
      detail: 'Recorded 10.00 USD, mock charged 12.00 USD.', createdAt: at },
  ],
};

beforeEach(() => signIn('Admin'));

it('runs a reconciliation on demand and shows what it found', async () => {
  let ran = false;
  mockFetch((url, init) => {
    if (url === '/api/payments/reconciliation/runs' && init?.method === 'POST') {
      ran = true;
      return { body: run };
    }
    if (url === '/api/payments/reconciliation/runs') return { body: ran ? [{ ...run, issues: null }] : [] };
    if (url === '/api/payments/reconciliation/runs/run1') return { body: run };
    return undefined;
  });
  renderWithProviders(<AdminReconciliationPage />);

  expect(await screen.findByText('No runs yet.')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Run now' }));

  expect(await screen.findByText('Paid but cancelled')).toBeInTheDocument();
  expect(screen.getByText('Amount mismatch')).toBeInTheDocument();
  expect(screen.getByText(/after the payment went through/)).toBeInTheDocument();
  expect(screen.getByText('NeedsReview')).toBeInTheDocument();
});
