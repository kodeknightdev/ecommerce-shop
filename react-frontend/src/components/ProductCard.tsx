import { Link } from 'react-router-dom';
import type { ProductSummary } from '../api/types';
import { formatMoney } from '../lib/format';
import { StarRating } from './StarRating';

export function ProductCard({ product }: { product: ProductSummary }) {
  return (
    <Link to={`/products/${product.id}`} className="card group flex flex-col overflow-hidden transition hover:shadow-md">
      <div className="aspect-[4/3] overflow-hidden bg-slate-100">
        <img src={product.imageUrl} alt={product.name} loading="lazy" className="h-full w-full object-cover transition group-hover:scale-105" />
      </div>
      <div className="flex flex-1 flex-col gap-1 p-4">
        <span className="text-xs uppercase tracking-wide text-slate-500">{product.categoryName}</span>
        <h3 className="font-medium text-slate-900">{product.name}</h3>
        {product.reviewCount > 0 && (
          <span className="flex items-center gap-1 text-xs text-slate-500">
            <StarRating value={product.rating} size="text-sm" /> ({product.reviewCount})
          </span>
        )}
        <span className="mt-auto pt-2 text-lg font-semibold text-brand-700">{formatMoney(product.price, product.currency)}</span>
      </div>
    </Link>
  );
}
