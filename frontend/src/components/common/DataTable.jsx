import './common.css'
import EmptyState from './EmptyState'
import LoadingSpinner from './LoadingSpinner'

// columns: [{ key, header, render?(row) }]
export default function DataTable({ columns, rows, loading, emptyTitle = 'Nothing here yet', emptyDescription, rowKey = 'id' }) {
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            {columns.map((col) => <th key={col.key}>{col.header}</th>)}
          </tr>
        </thead>
        <tbody>
          {!loading && rows.map((row) => (
            <tr key={row[rowKey]}>
              {columns.map((col) => (
                <td key={col.key}>{col.render ? col.render(row) : row[col.key]}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {loading && <LoadingSpinner center label="Loading data…" />}
      {!loading && rows.length === 0 && (
        <EmptyState icon="grid" title={emptyTitle} description={emptyDescription} />
      )}
    </div>
  )
}
