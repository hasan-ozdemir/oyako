// Codex developer note: Explains the purpose and flow of webapp-oyako/src/services/chatApi.ts for maintainers.
import type {
  ChatStreamEvent,
  CrawlStatus,
  ApiHealthResponse,
  HealthResponse,
  KnowledgeHealthResponse,
  KnowledgeBankResponse,
  KnowledgeDocumentContentResponse,
  KnowledgeDocumentDiagnosticsResponse,
  KnowledgeFileImportResponse,
  KnowledgeFilePreviewResponse,
  KnowledgeRedownloadResponse,
  KnowledgeSourceDiagnosticsResponse,
  KnowledgeUploadSettingsResponse,
  AiSettingsResponse,
  QnaExperienceSettingsResponse,
  ReadyQuestionsResponse,
  RuntimeStatus,
} from '../types/chat'

// Defines a reusable frontend value used by the surrounding module.
const API_BASE = import.meta.env.VITE_API_BASE || '/api'

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchCrawlStatus(): Promise<CrawlStatus> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/knowledge-source-refresh/status`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Bağlantı kurulamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('Durum sorgulanamıyor')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchApiHealth(): Promise<ApiHealthResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/api-health`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('API sağlığı alınamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('API sağlığı alınamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchKnowledgeHealth(): Promise<KnowledgeHealthResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/knowledge-health`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Bilgi sağlığı alınamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('Bilgi sağlığı alınamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchHealth(): Promise<HealthResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/health`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Sistem sağlığı alınamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('Sistem sağlığı alınamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchKnowledgeBank(): Promise<KnowledgeBankResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/knowledge-bank`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Bilgi Bankası alınamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('Bilgi Bankası alınamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that fetches normalized text for the in-app DocumentViewer.
export async function fetchKnowledgeDocumentContent(documentId: number): Promise<KnowledgeDocumentContentResponse> {
  const response = await fetch(`${API_BASE}/knowledge-documents/${documentId}/content`)
  const payload = await response.json().catch(() => null) as KnowledgeDocumentContentResponse | { message?: string } | null
  if (!response.ok) {
    throw new Error((payload as { message?: string } | null)?.message || 'Belge içeriği alınamadı.')
  }

  return payload as KnowledgeDocumentContentResponse
}

// Implements a frontend function that fetches source warning diagnostics for the Knowledge Bank LogViewer.
export async function fetchKnowledgeSourceDiagnostics(sourceId: number): Promise<KnowledgeSourceDiagnosticsResponse> {
  const response = await fetch(`${API_BASE}/knowledge-sources/${sourceId}/diagnostics`)
  const payload = await response.json().catch(() => null) as KnowledgeSourceDiagnosticsResponse | { message?: string } | null
  if (!response.ok) {
    throw new Error((payload as { message?: string } | null)?.message || 'Kaynak uyarı günlüğü alınamadı.')
  }

  return payload as KnowledgeSourceDiagnosticsResponse
}

// Implements a frontend function that fetches document warning diagnostics for the Knowledge Bank LogViewer.
export async function fetchKnowledgeDocumentDiagnostics(documentId: number): Promise<KnowledgeDocumentDiagnosticsResponse> {
  const response = await fetch(`${API_BASE}/knowledge-documents/${documentId}/diagnostics`)
  const payload = await response.json().catch(() => null) as KnowledgeDocumentDiagnosticsResponse | { message?: string } | null
  if (!response.ok) {
    throw new Error((payload as { message?: string } | null)?.message || 'Belge uyarı günlüğü alınamadı.')
  }

  return payload as KnowledgeDocumentDiagnosticsResponse
}

// Implements a frontend function that supports Oyako user or API behavior.
async function sendKnowledgeMutation(path: string, init: RequestInit): Promise<KnowledgeBankResponse> {
  // Calls the backend API endpoint required for this frontend workflow.
  const response = await fetch(`${API_BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
    ...init,
  })
  // Defines a reusable frontend value used by the surrounding module.
  const payload = await response.json().catch(() => null) as KnowledgeBankResponse | { message?: string } | null
  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error((payload as { message?: string } | null)?.message || 'Bilgi Bankası işlemi tamamlanamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return payload as KnowledgeBankResponse
}

// Implements a frontend function that supports Oyako user or API behavior.
export function addKnowledgeSource(sourceType: string, name: string, description: string, address: string): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation('/knowledge-sources', {
    method: 'POST',
    body: JSON.stringify({ sourceType, name, description, address }),
  })
}

// Implements a frontend function that supports Oyako user or API behavior.
export function updateKnowledgeSource(id: number, sourceType: string, name: string, description: string, address: string, isEnabled: boolean): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-sources/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ sourceType, name, description, address, isEnabled }),
  })
}

// Implements a frontend function that submits multipart Knowledge Bank operations.
async function sendKnowledgeMultipart<TResponse>(path: string, body: FormData): Promise<TResponse> {
  const response = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    body,
  })
  const payload = await response.json().catch(() => null) as TResponse | { message?: string } | null
  if (!response.ok) {
    throw new Error((payload as { message?: string } | null)?.message || 'Dosya işlemi tamamlanamadı.')
  }

  return payload as TResponse
}

// Implements a frontend function that asks the backend to parse files before the user imports them.
export function previewKnowledgeFiles(files: File[], relativePaths: string[]): Promise<KnowledgeFilePreviewResponse> {
  const form = new FormData()
  files.forEach((file) => form.append('files', file, file.name))
  relativePaths.forEach((relativePath) => form.append('relativePaths', relativePath))
  return sendKnowledgeMultipart<KnowledgeFilePreviewResponse>('/knowledge-files/preview', form)
}

// Implements a frontend function that imports parsed local files into a selected local-files source.
export function importKnowledgeFiles(sourceId: number, files: File[], titles: string[], relativePaths: string[]): Promise<KnowledgeFileImportResponse> {
  const form = new FormData()
  files.forEach((file) => form.append('files', file, file.name))
  titles.forEach((title) => form.append('titles', title))
  relativePaths.forEach((relativePath) => form.append('relativePaths', relativePath))
  return sendKnowledgeMultipart<KnowledgeFileImportResponse>(`/knowledge-sources/${sourceId}/documents/import`, form)
}

// Implements a frontend function that creates a user-managed web-link document under a Web Bağlantıları source.
export function addManualWebDocument(sourceId: number, url: string, title: string, isEnabled = true): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-sources/${sourceId}/web-documents`, {
    method: 'POST',
    body: JSON.stringify({ url, title, isEnabled }),
  })
}

// Implements a frontend function that loads Knowledge Bank upload limits from the backend.
export async function fetchKnowledgeSettings(): Promise<KnowledgeUploadSettingsResponse> {
  const response = await fetch(`${API_BASE}/knowledge-settings`)
  if (!response.ok) {
    throw new Error('Dosya yükleme ayarları alınamadı.')
  }

  return response.json()
}

// Implements a frontend function that persists Knowledge Bank upload limits.
export async function updateKnowledgeSettings(maxFileSizeMb: number, maxBatchFileCount: number, maxBatchSizeMb: number): Promise<KnowledgeUploadSettingsResponse> {
  const response = await fetch(`${API_BASE}/knowledge-settings`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ maxFileSizeMb, maxBatchFileCount, maxBatchSizeMb }),
  })
  const payload = await response.json().catch(() => null) as KnowledgeUploadSettingsResponse | { message?: string } | null
  if (!response.ok) {
    throw new Error((payload as { message?: string } | null)?.message || 'Dosya yükleme ayarları kaydedilemedi.')
  }

  return payload as KnowledgeUploadSettingsResponse
}

// Implements a frontend function that supports Oyako user or API behavior.
export function setKnowledgeSourceEnabled(id: number, isEnabled: boolean): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-sources/${id}/enabled`, {
    method: 'PATCH',
    body: JSON.stringify({ isEnabled }),
  })
}

// Implements a frontend function that supports Oyako user or API behavior.
export function setKnowledgeSourceArchived(id: number, isArchived: boolean): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-sources/${id}/archive`, {
    method: 'PATCH',
    body: JSON.stringify({ isArchived }),
  })
}

// Implements a frontend function that supports Oyako user or API behavior.
export function deleteKnowledgeSource(id: number): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-sources/${id}`, { method: 'DELETE' })
}

// Implements a frontend function that supports Oyako user or API behavior.
export function setKnowledgeDocumentEnabled(id: number, isEnabled: boolean): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-documents/${id}/enabled`, {
    method: 'PATCH',
    body: JSON.stringify({ isEnabled }),
  })
}

// Implements a frontend function that supports Oyako user or API behavior.
export function setKnowledgeDocumentArchived(id: number, isArchived: boolean): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-documents/${id}/archive`, {
    method: 'PATCH',
    body: JSON.stringify({ isArchived }),
  })
}

// Implements a frontend function that supports Oyako user or API behavior.
export function updateKnowledgeDocument(id: number, title: string, content: string, isEnabled: boolean): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-documents/${id}/local-content`, {
    method: 'PUT',
    body: JSON.stringify({ title, content, isEnabled }),
  })
}

// Implements a frontend function that updates a web document URL after the backend re-scrapes it.
export function updateKnowledgeDocumentWebLink(id: number, url: string): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-documents/${id}/web-link`, {
    method: 'PATCH',
    body: JSON.stringify({ url }),
  })
}

// Implements a frontend function that supports Oyako user or API behavior.
export function deleteKnowledgeDocument(id: number): Promise<KnowledgeBankResponse> {
  return sendKnowledgeMutation(`/knowledge-documents/${id}`, { method: 'DELETE' })
}

// Implements a frontend function that supports Oyako user or API behavior.
async function sendKnowledgeRedownload(path: string): Promise<KnowledgeRedownloadResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}${path}`, { method: 'POST' })
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Bilgi Bankası yeniden indirilemedi.')
  }

  // Defines a reusable frontend value used by the surrounding module.
  const payload = await response.json().catch(() => null) as KnowledgeRedownloadResponse | null
  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error(payload?.message || 'Bilgi Bankası yeniden indirilemedi.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return payload as KnowledgeRedownloadResponse
}

// Implements a frontend function that redownloads one Knowledge Bank source.
export function redownloadKnowledgeSource(sourceId: number): Promise<KnowledgeRedownloadResponse> {
  return sendKnowledgeRedownload(`/knowledge-source-redownload/${sourceId}`)
}

// Implements a frontend function that redownloads one Knowledge Bank document.
export function redownloadKnowledgeDocument(documentId: number): Promise<KnowledgeRedownloadResponse> {
  return sendKnowledgeRedownload(`/source-document-redownload/${documentId}`)
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchAiSettings(): Promise<AiSettingsResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/ai-settings`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Ayarlar alınamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('Ayarlar alınamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function updateAiSettings(provider: string, model: string): Promise<AiSettingsResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/ai-settings`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ provider, model }),
    })
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Ayarlar kaydedilemedi.')
  }

  // Defines a reusable frontend value used by the surrounding module.
  const payload = await response.json().catch(() => null) as AiSettingsResponse | { message?: string } | null
  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error((payload as { message?: string } | null)?.message || 'Ayarlar kaydedilemedi.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return payload as AiSettingsResponse
}

// Implements a frontend function that loads Q&A experience settings from the backend.
export async function fetchQnaExperienceSettings(): Promise<QnaExperienceSettingsResponse> {
  const response = await fetch(`${API_BASE}/qna-experience-settings`)
  if (!response.ok) {
    throw new Error('Soru-cevap deneyimi ayarları alınamadı.')
  }

  return response.json()
}

// Implements a frontend function that persists Q&A experience settings.
export async function updateQnaExperienceSettings(
  displayedReadyQuestionCount: number,
  displayedSuggestedQuestionCount: number,
  autoSubmitPromptButtons: boolean,
  showAnswerSourceDocumentNames: boolean,
): Promise<QnaExperienceSettingsResponse> {
  const response = await fetch(`${API_BASE}/qna-experience-settings`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      displayedReadyQuestionCount,
      displayedSuggestedQuestionCount,
      autoSubmitPromptButtons,
      showAnswerSourceDocumentNames,
    }),
  })
  const payload = await response.json().catch(() => null) as QnaExperienceSettingsResponse | { message?: string } | null
  if (!response.ok) {
    throw new Error((payload as { message?: string } | null)?.message || 'Soru-cevap deneyimi ayarları kaydedilemedi.')
  }

  return payload as QnaExperienceSettingsResponse
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchReadyQuestions(count = 4): Promise<ReadyQuestionsResponse> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/ready-questions?count=${count}`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Hazır sorular alınamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('Hazır sorular alınamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that supports Oyako user or API behavior.
export async function fetchRuntimeStatus(): Promise<RuntimeStatus> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/runtime/status`)
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Canlı durum alınamadı.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Creates the browser or application object needed for this step.
    throw new Error('Canlı durum alınamadı.')
  }

  // Returns the value or JSX produced by this frontend workflow.
  return response.json()
}

// Implements a frontend function that supports Oyako user or API behavior.
export function createRuntimeStatusStream(): EventSource {
  // Returns the value or JSX produced by this frontend workflow.
  return new EventSource(`${API_BASE}/runtime/status/stream`)
}

// Implements a frontend function that detects backend/proxy errors before they pollute assistant content.
function looksLikeTechnicalChatFailure(value: string): boolean {
  const lower = value.toLowerCase()
  // Returns the value or JSX produced by this frontend workflow.
  return lower.includes('response status code') ||
    lower.includes('service unavailable') ||
    lower.includes('503') ||
    lower.includes('exception') ||
    lower.includes('stack trace')
}

export async function* streamChat(message: string): AsyncGenerator<ChatStreamEvent> {
  let response: Response
  try {
    // Calls the backend API endpoint required for this frontend workflow.
    response = await fetch(`${API_BASE}/chat/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message }),
    })
  } catch {
    // Creates the browser or application object needed for this step.
    throw new Error('Bağlantı kurulamadı. API servisinin çalıştığını kontrol edin.')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.ok) {
    // Defines a reusable frontend value used by the surrounding module.
    const body = await response.text()
    // Creates the browser or application object needed for this step.
    throw new Error(body || 'Chat isteği başarısız')
  }

  // Guards this branch so the UI handles the condition intentionally.
  if (!response.body) {
    // Creates the browser or application object needed for this step.
    throw new Error('Sunucu yanıt akışı alınamadı')
  }

  // Defines a reusable frontend value used by the surrounding module.
  const reader = response.body.getReader()
  // Defines a reusable frontend value used by the surrounding module.
  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    // Defines a reusable frontend value used by the surrounding module.
    const chunk = await reader.read()
    // Guards this branch so the UI handles the condition intentionally.
    if (chunk.done) {
      // Returns the value or JSX produced by this frontend workflow.
      return
    }

    buffer += decoder.decode(chunk.value, { stream: true })
    let idx: number
    while ((idx = buffer.indexOf('\n\n')) >= 0) {
      // Defines a reusable frontend value used by the surrounding module.
      const rawLine = buffer.slice(0, idx).trim()
      buffer = buffer.slice(idx + 2)
      // Guards this branch so the UI handles the condition intentionally.
      if (!rawLine.startsWith('data:')) {
        continue
      }

      // Defines a reusable frontend value used by the surrounding module.
      const payload = rawLine.slice('data:'.length).trim()
      // Guards this branch so the UI handles the condition intentionally.
      if (!payload || payload === '[DONE]') {
        continue
      }

      let parsed: ChatStreamEvent | { type: 'chunk'; content: string }
      try {
        parsed = JSON.parse(payload) as ChatStreamEvent | { type: 'chunk'; content: string }
      } catch {
        // Guards this branch so the UI handles the condition intentionally.
        if (looksLikeTechnicalChatFailure(payload)) {
          // Creates the browser or application object needed for this step.
          throw new Error(payload)
        }

        yield { type: 'answer', answer_content: payload, suggested_questions: [], source_attributions: [] }
        continue
      }

      // Guards this branch so the UI handles the condition intentionally.
      if (parsed.type === 'error') {
        // Creates the browser or application object needed for this step.
        throw new Error(parsed.content)
      }

      // Guards this branch so the UI handles the condition intentionally.
      if (parsed.type === 'answer' && parsed.answer_content) {
        // Guards this branch so the UI handles the condition intentionally.
        if (looksLikeTechnicalChatFailure(parsed.answer_content)) {
          // Creates the browser or application object needed for this step.
          throw new Error(parsed.answer_content)
        }

        yield {
          type: 'answer',
          answer_content: parsed.answer_content,
          suggested_questions: Array.isArray(parsed.suggested_questions)
            ? parsed.suggested_questions.filter((question) => typeof question === 'string' && question.trim().length > 0)
            : [],
          source_attributions: Array.isArray(parsed.source_attributions)
            ? parsed.source_attributions.map((item) => normalizeSourceAttribution(item)).filter((item): item is NonNullable<ReturnType<typeof normalizeSourceAttribution>> => item !== null)
            : [],
        }
      } else if (parsed.type === 'html' && parsed.content) {
        yield { type: 'answer', answer_content: parsed.content, suggested_questions: [], source_attributions: [] }
      } else if (parsed.type === 'chunk' && parsed.content) {
        yield { type: 'answer', answer_content: parsed.content, suggested_questions: [], source_attributions: [] }
      } else if (parsed.type === 'phase') {
        yield parsed
      }
    }
  }
}

// Converts backend snake_case attribution payloads into the frontend camelCase contract.
function normalizeSourceAttribution(value: unknown) {
  if (!value || typeof value !== 'object') {
    return null
  }

  const item = value as Record<string, unknown>
  const sourceId = Number(item.source_id)
  const documentId = Number(item.document_id)
  if (!Number.isFinite(sourceId) || sourceId <= 0 || !Number.isFinite(documentId) || documentId <= 0) {
    return null
  }

  return {
    sourceId,
    sourceName: String(item.source_name ?? ''),
    sourceType: String(item.source_type ?? ''),
    documentId,
    documentTitle: String(item.document_title ?? ''),
    displayLabel: String(item.display_label ?? item.document_title ?? ''),
    url: String(item.url ?? ''),
    openMode: String(item.open_mode ?? 'external'),
  }
}
