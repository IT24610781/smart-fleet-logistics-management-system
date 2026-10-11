import { useState } from 'react'
import DataTable from './DataTable'
import Button from './Button'

export default function AdminTable({ columns, rows, ...rest }) {
  const [sort,setSort] = useState('')
  const [direction,setDirection] = useState('asc')
  const [page,setPage] = useState(1)
  const sortable = columns.filter(c => c.key !== 'actions')
  const sorted = sort ? [...rows].sort((a,b) => String(a[sort] ?? '').localeCompare(String(b[sort] ?? ''),undefined,{numeric:true}) * (direction === 'asc' ? 1 : -1)) : rows
  const pages = Math.max(1,Math.ceil(sorted.length / 10))
  const current = Math.min(page,pages)
  return <div>
    <div className="admin-table-tools">
      <label>Sort by <select value={sort} onChange={e => { setSort(e.target.value);setPage(1) }}><option value="">Original order</option>{sortable.map(c => <option key={c.key} value={c.key}>{c.header}</option>)}</select></label>
      <label>Order <select value={direction} onChange={e => setDirection(e.target.value)}><option value="asc">Ascending</option><option value="desc">Descending</option></select></label>
      <span role="status">{rest.loading ? 'Loading?' : `${rows.length} records`}</span>
    </div>
    <DataTable {...rest} columns={columns} rows={sorted.slice((current-1)*10,current*10)} />
    {rows.length > 10 && <div className="admin-pagination"><Button variant="secondary" disabled={current===1} onClick={() => setPage(current-1)}>Previous</Button><span>Page {current} of {pages}</span><Button variant="secondary" disabled={current===pages} onClick={() => setPage(current+1)}>Next</Button></div>}
  </div>
}
