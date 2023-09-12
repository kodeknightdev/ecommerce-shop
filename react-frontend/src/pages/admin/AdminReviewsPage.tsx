import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../../api/endpoints';
import type { AdminReview } from '../../api/types';
import { ErrorMessage, Spinner } from '../../components/ErrorMessage';
import { Pagination } from '../../components/Pagination';
import { StarRating } from '../../components/StarRating';
import { formatDateTime } from '../../lib/format';

function ReviewRow({ review }: { review: AdminReview }) {
  const queryClient = useQueryClient();
  const toggle = useMutation({
    mutationFn: () => adminApi.setReviewHidden(review.id, !review.hidden, review.hidden ? undefined : 'Hidden by a moderator'),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-reviews'] }),
  });

  return (
    <tr className={review.hidden ? 'bg-slate-50 text-slate-500' : undefined}>
      <td className="p-3"><Link to={`/products/${review.productId}`} className="text-brand-700 hover:underline">{review.productName}</Link></td>
      <td className="p-3"><StarRating value={review.rating} size="text-sm" /></td>
      <td className="max-w-md p-3">
        {review.title && <span className="font-medium">{review.title} - </span>}{review.comment}
        {review.hiddenReason && <div className="text-xs text-amber-700">{review.hiddenReason}</div>}
      </td>
      <td className="p-3">{review.authorName}</td>
      <td className="whitespace-nowrap p-3">{formatDateTime(review.createdAt)}</td>
      <td className="p-3">
        <button className={review.hidden ? 'btn-secondary' : 'btn-danger'} disabled={toggle.isLoading} onClick={() => toggle.mutate()}>
          {review.hidden ? 'Show' : 'Hide'}
        </button>
        <ErrorMessage error={toggle.error} />
      </td>
    </tr>
  );
}

export function AdminReviewsPage() {
  const [filter, setFilter] = useState<'' | 'visible' | 'hidden'>('');
  const [page, setPage] = useState(1);
  const reviews = useQuery({
    queryKey: ['admin-reviews', filter, page],
    queryFn: () => adminApi.reviews({ hidden: filter === '' ? undefined : filter === 'hidden', page }),
    keepPreviousData: true,
  });

  return (
    <div className="space-y-4">
      <select aria-label="Visibility" className="input w-auto" value={filter} onChange={(e) => { setFilter(e.target.value as typeof filter); setPage(1); }}>
        <option value="">All reviews</option>
        <option value="visible">Visible</option>
        <option value="hidden">Hidden</option>
      </select>
      {reviews.isLoading && <Spinner />}
      <ErrorMessage error={reviews.error} />
      {reviews.data && (
        <>
          <table className="card w-full text-sm">
            <thead className="bg-slate-50 text-left text-slate-500">
              <tr><th className="p-3">Product</th><th className="p-3">Rating</th><th className="p-3">Review</th><th className="p-3">By</th><th className="p-3">Posted</th><th className="p-3" /></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {reviews.data.items.map((r) => <ReviewRow key={r.id} review={r} />)}
            </tbody>
          </table>
          {reviews.data.total === 0 && <p className="text-sm text-slate-500">No reviews.</p>}
          <Pagination page={reviews.data.page} pageSize={reviews.data.pageSize} total={reviews.data.total} onChange={setPage} />
        </>
      )}
    </div>
  );
}
