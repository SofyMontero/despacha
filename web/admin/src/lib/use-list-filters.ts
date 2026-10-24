import { useEffect, useState } from 'react'
import { useDebounced } from './use-debounced'

export function useListFilters() {
  const [page, setPage] = useState(1)
  const [q, setQ] = useState('')
  const [active, setActive] = useState<'' | 'true' | 'false'>('')
  const debounced = useDebounced(q)

  useEffect(() => {
    setPage(1)
  }, [debounced, active])

  return { page, setPage, q, setQ, active, setActive, debounced }
}
