import { fireEvent, screen, waitFor } from '@testing-library/react';
import { ProductReviews } from '../components/ProductReviews';
import { mockFetch, renderWithProviders, signIn } from '../test/utils';

const at = '2023-09-01T10:00:00Z';
const page = (items: unknown[]) => ({
  items, page: 1, pageSize: 5, total: items.length, averageRating: items.length ? 4 : 0, reviewCount: items.length,
  distribution: { 1: 0, 2: 0, 3: 0, 4: items.length, 5: 0 },
});
const review = { id: 'r1', productId: 'p1', authorName: 'Jane D.', rating: 4, title: 'Read it in two evenings', comment: 'Lovely writing.', createdAt: at, updatedAt: at };

beforeEach(() => localStorage.clear());

it('lists reviews with the average and asks guests to sign in', async () => {
  mockFetch((url) => (url.startsWith('/api/products/p1/reviews?') ? { body: page([review]) } : undefined));
  renderWithProviders(<ProductReviews productId="p1" />);

  expect(await screen.findByText('Read it in two evenings')).toBeInTheDocument();
  expect(screen.getByTestId('review-summary')).toHaveTextContent('4.0 out of 5 · 1 review');
  expect(screen.getByText(/Jane D\. · Verified purchase/)).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Sign in' })).toBeInTheDocument();
});

it('tells customers who have not bought it that they cannot review', async () => {
  signIn();
  mockFetch((url) => {
    if (url === '/api/products/p1/reviews/me') return { body: { canReview: false, review: null, hidden: false } };
    if (url.startsWith('/api/products/p1/reviews?')) return { body: page([]) };
    return undefined;
  });
  renderWithProviders(<ProductReviews productId="p1" />);

  expect(await screen.findByText('Only customers who bought this product can review it.')).toBeInTheDocument();
  expect(screen.queryByRole('form', { name: 'Your review' })).not.toBeInTheDocument();
});

it('lets a verified buyer post a review', async () => {
  signIn();
  const fetch = mockFetch((url, init) => {
    if (url === '/api/products/p1/reviews/me' && init?.method === 'PUT') return { status: 201, body: { ...review, id: 'r2' } };
    if (url === '/api/products/p1/reviews/me') return { body: { canReview: true, review: null, hidden: false } };
    if (url.startsWith('/api/products/p1/reviews?')) return { body: page([]) };
    return undefined;
  });
  renderWithProviders(<ProductReviews productId="p1" />);

  await screen.findByRole('form', { name: 'Your review' });
  const post = screen.getByRole('button', { name: 'Post review' });
  expect(post).toBeDisabled();

  fireEvent.click(screen.getByRole('radio', { name: '4 stars' }));
  fireEvent.change(screen.getByPlaceholderText('What did you think?'), { target: { value: 'Good grip, thick enough.' } });
  fireEvent.click(post);

  await waitFor(() => {
    const put = fetch.mock.calls.find(([, init]) => init?.method === 'PUT');
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ rating: 4, comment: 'Good grip, thick enough.' });
  });
});
