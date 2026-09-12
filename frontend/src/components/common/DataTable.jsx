import EmptyState from './EmptyState'
import LoadingSpinner from './LoadingSpinner'

// columns: [{ key, header, render?(row) }]
export default function DataTable({ columns, rows, loading, emptyTitle = 'Nothing here yet', emptyDescription, rowKey = 'id' }) {
  return (
    <div className="rounded-xl border border-outline-variant bg-surface-container-lowest overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <thead>
            <tr className="bg-surface-container-low">
              {columns.map((col) => (
                <th
                  key={col.key}
                  className="text-left px-4 py-3 font-label-md text-label-md uppercase tracking-wide text-on-surface-variant"
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {!loading &&
              rows.map((row) => (
                <tr key={row[rowKey]} className="border-t border-outline-variant/60 hover:bg-surface-container-low/60">
                  {columns.map((col) => (
                    <td key={col.key} className="px-4 py-3 font-body-md text-body-md text-on-surface">
                      {col.render ? col.render(row) : row[col.key]}
                    </td>
                  ))}
                </tr>
              ))}
          </tbody>
        </table>
      </div>
      {loading && <LoadingSpinner center label="Loading data…" />}
      {!loading && rows.length === 0 && (
        <EmptyState icon="grid" title={emptyTitle} description={emptyDescription} />
      )}
    </div>
  )
}
