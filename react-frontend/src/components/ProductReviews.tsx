import { FormEvent, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { reviewsApi } from '../api/endpoints';
import type { MyReview } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { formatDay } from '../lib/format';
import { ErrorMessage, Spinner } from './ErrorMessage';
import { Pagination } from './Pagination';
import { StarInput, StarRating } from './StarRating';

function ReviewForm({ productId, mine }: { productId: string; mine: MyReview }) {
  const queryClient = useQueryClient();
  const existing = mine.review;
  const [editing, setEditing] = useState(!existing);
  const [rating, setRating] = useState(existing?.rating ?? 0);
  const [title, setTitle] = useState(existing?.title ?? '');
  const [comment, setComment] = useState(existing?.comment ?? '');

  useEffect(() => setEditing(!existing), [existing]);

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['reviews', productId] });
    queryClient.invalidateQueries({ queryKey: ['my-review', productId] });
    queryClient.invalidateQueries({ queryKey: ['product', productId] });
  };
  const save = useMutation({ mutationFn: () => reviewsApi.save(productId, { rating, title: title || undefined, comment }), onSuccess: refresh });
  const remove = useMutation({ mutationFn: () => reviewsApi.remove(productId), onSuccess: refresh });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    save.mutate();
  };

  if (existing && !editing) {
    return (
      <div className="card flex flex-wrap items-center justify-between gap-3 p-4 text-sm">
        <span>
          You rated this <StarRating value={existing.rating} size="text-sm" />
          {mine.hidden && <span className="ml-2 text-amber-700">(hidden by a moderator)</span>}
        </span>
        <span className="flex gap-2">
          <button className="btn-secondary" onClick={() => setEditing(true)}>Edit review</button>
          <button className="btn-danger" disabled={remove.isLoading} onClick={() => remove.mutate()}>Delete</button>
        </span>
        <ErrorMessage error={remove.error} />
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="card space-y-3 p-4" aria-label="Your review">
      <h3 className="font-semibold">{existing ? 'Edit your review' : 'Write a review'}</h3>
      <StarInput value={rating} onChange={setRating} />
      <input className="input" placeholder="Title (optional)" maxLength={120} value={title} onChange={(e) => setTitle(e.target.value)} />
      <textarea className="input min-h-[6rem]" placeholder="What did you think?" maxLength={2000} required value={comment}
        onChange={(e) => setComment(e.target.value)} />
      <ErrorMessage error={save.error} />
      <div className="flex gap-2">
        <button className="btn-primary" disabled={rating === 0 || comment.trim().length < 3 || save.isLoading}>
          {save.isLoading ? 'Saving...' : 'Post review'}
        </button>
        {existing && <button type="button" className="btn-secondary" onClick={() => setEditing(false)}>Cancel</button>}
      </div>
    </form>
  );
}

export function ProductReviews({ productId }: { productId: string }) {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState('newest');
  const reviews = useQuery({ queryKey: ['reviews', productId, page, sort], queryFn: () => reviewsApi.list(productId, page, sort), keepPreviousData: true });
  const mine = useQuery({ queryKey: ['my-review', productId], queryFn: () => reviewsApi.mine(productId), enabled: !!user });

  const data = reviews.data;
  return (
    <section className="space-y-4" aria-labelledby="reviews-heading">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 id="reviews-heading" className="text-xl font-bold">Customer reviews</h2>
          {data && data.reviewCount > 0 && (
            <p className="mt-1 flex items-center gap-2 text-sm text-slate-600" data-testid="review-summary">
              <StarRating value={data.averageRating} /> {data.averageRating.toFixed(1)} out of 5 · {data.reviewCount} review{data.reviewCount === 1 ? '' : 's'}
            </p>
          )}
        </div>
        {data && data.reviewCount > 1 && (
          <select aria-label="Sort reviews" className="input w-auto" value={sort} onChange={(e) => { setSort(e.target.value); setPage(1); }}>
            <option value="newest">Newest</option>
            <option value="highest">Highest rated</option>
            <option value="lowest">Lowest rated</option>
          </select>
        )}
      </div>

      {!user && <p className="text-sm text-slate-500"><Link to="/login" className="text-brand-700 hover:underline">Sign in</Link> to review products you have bought.</p>}
      {mine.data && (mine.data.canReview || mine.data.review)
        ? <ReviewForm productId={productId} mine={mine.data} />
        : mine.data && <p className="text-sm text-slate-500">Only customers who bought this product can review it.</p>}

      {reviews.isLoading && <Spinner label="Loading reviews..." />}
      <ErrorMessage error={reviews.error} />
      {data && data.total === 0 && <p className="text-sm text-slate-500">No reviews yet.</p>}
      <ul className="divide-y divide-slate-100">
        {data?.items.map((r) => (
          <li key={r.id} className="py-4">
            <div className="flex items-center gap-2 text-sm">
              <StarRating value={r.rating} size="text-sm" />
              {r.title && <span className="font-semibold text-slate-900">{r.title}</span>}
            </div>
            <p className="mt-1 text-slate-700">{r.comment}</p>
            <p className="mt-1 text-xs text-slate-500">{r.authorName} · Verified purchase · {formatDay(r.createdAt)}</p>
          </li>
        ))}
      </ul>
      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onChange={setPage} />}
    </section>
  );
}
