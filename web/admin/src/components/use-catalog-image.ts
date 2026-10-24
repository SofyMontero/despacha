import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { apiSend, apiUpload } from '../api/client'
import { ApiError, messageOf } from '../api/errors'

type VersionedImage = { version: number; imageUrl: string | null }

export function useCatalogImage<T extends VersionedImage>(
  path: string | null,
  initial: { version?: number; imageUrl: string | null },
  queryKey: readonly unknown[],
) {
  const queryClient = useQueryClient()
  const [version, setVersion] = useState(initial.version)
  const [imageUrl, setImageUrl] = useState(initial.imageUrl)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string>()

  async function apply(saved: T) {
    setVersion(saved.version)
    setImageUrl(saved.imageUrl)
    await queryClient.invalidateQueries({ queryKey })
  }

  async function upload(file: File) {
    if (!path) return
    setPending(true)
    setError(undefined)
    try {
      await apply(await apiUpload<T>(path, file))
    } catch (caught) {
      const fileError = caught instanceof ApiError ? caught.errors.File?.join(' ') : undefined
      setError(fileError || messageOf(caught))
    } finally {
      setPending(false)
    }
  }

  async function remove() {
    if (!path) return
    setPending(true)
    setError(undefined)
    try {
      await apply(await apiSend<T>(path, 'DELETE'))
    } catch (caught) {
      setError(messageOf(caught))
    } finally {
      setPending(false)
    }
  }

  return { version, imageUrl, pending, error, upload, remove }
}
