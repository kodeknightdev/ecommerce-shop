import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../../api/endpoints';
import type { ReconciliationIssue, ReconciliationRun } from '../../api/types';
import { ErrorMessage, Spinner } from '../../components/ErrorMessage';
import { formatDateTime } from '../../lib/format';

const KIND_LABELS: Record<ReconciliationIssue['kind'], string> = {
  PaidButCancelled: 'Paid but cancelled',
  ChargedButMarkedFailed: 'Charged but marked failed',
  StatusMismatch: 'Status mismatch',
  AmountMismatch: 'Amount mismatch',
  MissingAtProvider: 'Missing at provider',
};

const RESOLUTION_STYLES: Record<ReconciliationIssue['resolution'], string> = {
  Refunded: 'bg-emerald-100 text-emerald-800',
  RefundFailed: 'bg-red-100 text-red-800',
  NeedsReview: 'bg-amber-100 text-amber-800',
};

function RunIssues({ runId }: { runId: string }) {
  const run = useQuery({ queryKey: ['reconciliation-run', runId], queryFn: () => adminApi.reconciliationRun(runId) });
  if (run.isLoading) return <Spinner />;
  if (run.error) return <ErrorMessage error={run.error} />;
  const issues = run.data?.issues ?? [];
  if (issues.length === 0) return <p className="p-3 text-sm text-slate-500">Everything matched.</p>;

  return (
    <ul className="divide-y divide-slate-100 text-sm">
      {issues.map((i) => (
        <li key={i.id} className="flex flex-wrap items-start gap-3 p-3">
          <span className={`rounded px-2 py-0.5 text-xs ${RESOLUTION_STYLES[i.resolution]}`}>{i.resolution}</span>
          <span className="font-medium">{KIND_LABELS[i.kind]}</span>
          <Link to={`/orders/${i.orderId}`} className="font-mono text-brand-700 hover:underline">{i.orderId.slice(0, 8)}</Link>
          <span className="basis-full text-slate-600">{i.detail}</span>
        </li>
      ))}
    </ul>
  );
}

function RunRow({ run, open, onToggle }: { run: ReconciliationRun; open: boolean; onToggle: () => void }) {
  return (
    <>
      <tr className="cursor-pointer hover:bg-slate-50" onClick={onToggle}>
        <td className="whitespace-nowrap p-3">{formatDateTime(run.startedAt)}</td>
        <td className="p-3 capitalize">{run.trigger}</td>
        <td className="p-3">{run.checked}</td>
        <td className="p-3">{run.unverified}</td>
        <td className="p-3">{run.issueCount}</td>
        <td className="p-3">{run.refunded}</td>
        <td className="p-3 text-brand-700">{open ? 'Hide' : 'Details'}</td>
      </tr>
      {open && (
        <tr><td colSpan={7} className="bg-slate-50 p-0"><RunIssues runId={run.id} /></td></tr>
      )}
    </>
  );
}

export function AdminReconciliationPage() {
  const queryClient = useQueryClient();
  const [openId, setOpenId] = useState<string | null>(null);
  const runs = useQuery({ queryKey: ['reconciliation-runs'], queryFn: adminApi.reconciliationRuns });
  const runNow = useMutation({
    mutationFn: adminApi.reconcileNow,
    onSuccess: (run) => {
      queryClient.setQueryData(['reconciliation-run', run.id], run);
      queryClient.invalidateQueries({ queryKey: ['reconciliation-runs'] });
      setOpenId(run.id);
    },
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-2xl text-sm text-slate-600">
          Payments compares its records with the payment provider and with cancelled orders every 15 minutes. Charges for cancelled orders
          are refunded automatically; anything else is left here for review.
        </p>
        <button className="btn-primary" disabled={runNow.isLoading} onClick={() => runNow.mutate()}>
          {runNow.isLoading ? 'Running...' : 'Run now'}
        </button>
      </div>
      <ErrorMessage error={runNow.error} />
      {runs.isLoading && <Spinner />}
      <ErrorMessage error={runs.error} />
      {runs.data && runs.data.length === 0 && <p className="text-sm text-slate-500">No runs yet.</p>}
      {runs.data && runs.data.length > 0 && (
        <table className="card w-full text-sm">
          <thead className="bg-slate-50 text-left text-slate-500">
            <tr><th className="p-3">Started</th><th className="p-3">Trigger</th><th className="p-3">Checked</th><th className="p-3">Unverified</th><th className="p-3">Issues</th><th className="p-3">Refunded</th><th className="p-3" /></tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {runs.data.map((r) => <RunRow key={r.id} run={r} open={openId === r.id} onToggle={() => setOpenId(openId === r.id ? null : r.id)} />)}
          </tbody>
        </table>
      )}
    </div>
  );
}
