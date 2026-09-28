import { downloadBlob, request, upload } from './http'
import type { DeleteAccountInput, FileDto, MeDto } from './types'

export const meApi = {
  get: () => request<MeDto>('/me'),

  /** JSON export of the account's personal data (GDPR-style). */
  export: () => request<unknown>('/me/export'),

  remove: (body: DeleteAccountInput) => request<void>('/me', { method: 'DELETE', body }),

  uploadResume: (file: File) => upload<FileDto>('/files/resume', file),

  /** Resume files need an Authorization header, so this returns a Blob. */
  resumeBlob: (fileId: string) => downloadBlob(`/files/${fileId}`),
}
