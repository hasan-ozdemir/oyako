// Codex developer note: Explains the purpose and flow of webapp-oyako/src/types/chat.ts for maintainers.
export type MessageRole = 'user' | 'assistant'

// Defines the TypeScript contract used by the Oyako frontend.
export interface ChatMessage {
  id: string
  role: MessageRole
  content: string
  sourceAttributions?: SourceAttribution[]
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface CrawlStatus {
  id: number | null
  startedAtUtc: string | null
  completedAtUtc: string | null
  pageCount: number
  errorCount: number
  warningCount: number
  status: string | number | null
  errorMessage: string | null
  warningMessage: string | null
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface RuntimeStatus {
  operation: string
  phase: string
  stepKey: string
  stepIndex: number
  stepCount: number
  isTerminal: boolean
  message: string
  severity: string
  icon: string
  pageCount: number | null
  updatedAtUtc: string
}

// Defines a reusable TypeScript type for frontend data flow.
export type ChatStreamEvent =
  | { type: 'answer'; answer_content: string; suggested_questions: string[]; source_attributions: SourceAttribution[] }
  | { type: 'html'; content: string }
  | { type: 'phase'; phase: string }
  | { type: 'error'; content: string }

// Defines the verified source link metadata returned by the backend.
export interface SourceAttribution {
  sourceId: number
  sourceName: string
  sourceType: 'web_site' | 'web_links' | 'local_files' | string
  documentId: number
  documentTitle: string
  displayLabel: string
  url: string
  openMode: 'external' | 'document_viewer' | string
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeBankSource {
  id: number
  tenantGuid: string
  tenantKnowledgeGuid: string
  knowledgeSourceGuid: string
  sourceType: 'web_site' | 'web_links' | 'local_files' | string
  name: string
  description: string
  address: string
  protocol: string
  isEnabled: boolean
  isArchived: boolean
  statusCode: string
  statusLabel: string
  statusMessage: string
  webPageAdditionMode: 'automatic' | 'manual' | string
  webPageAdditionModeLabel: string
  documentCount: number
  activeDocumentCount: number
  lastCheckedAtUtc: string | null
  updatedAtUtc: string
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeBankFolder {
  id: number
  knowledgeSourceGuid: string
  sourceFolderGuid: string
  folderName: string
  normalizedFolderPath: string
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeBankDocument {
  id: number
  sourceId: number | null
  sourceName: string
  sourceType: string
  tenantGuid: string
  tenantKnowledgeGuid: string
  knowledgeSourceGuid: string
  sourceFolderGuid: string
  folderDocumentGuid: string
  title: string
  url: string
  content: string
  contentPreview: string
  isEnabled: boolean
  isArchived: boolean
  statusCode: string
  statusLabel: string
  statusMessage: string
  httpStatusCode: number | null
  previewStatus: string
  previewGeneratedAtUtc: string | null
  lastCheckedAtUtc: string | null
  lastCrawledAtUtc: string
  originalFileName: string
  normalizedRelativePath: string
  normalizedFolderPath: string
  storedFileName: string
  fileExtension: string
  fileSizeBytes: number
  parseStatus: string
  ocrStatus: string
  origin: string
}

// Defines the document content contract used by the in-app DocumentViewer.
export interface KnowledgeDocumentContentResponse {
  id: number
  sourceId: number | null
  sourceName: string
  sourceType: string
  title: string
  url: string
  content: string
  originalFileName: string
  lastCheckedAtUtc: string | null
  lastCrawledAtUtc: string
}

// Defines the source diagnostics contract used by the Knowledge Bank warning log viewer.
export interface KnowledgeDiagnosticSource {
  id: number
  sourceType: string
  name: string
  description: string
  address: string
  isEnabled: boolean
  isArchived: boolean
  statusCode: string
  statusLabel: string
  statusMessage: string
  documentCount: number
  activeDocumentCount: number
  lastCheckedAtUtc: string | null
  updatedAtUtc: string | null
}

// Defines the document diagnostics contract used by the Knowledge Bank warning log viewer.
export interface KnowledgeDiagnosticDocument {
  id: number
  sourceId: number
  sourceName: string
  sourceType: string
  title: string
  url: string
  isEnabled: boolean
  isArchived: boolean
  statusCode: string
  statusLabel: string
  statusMessage: string
  httpStatusCode: number | null
  previewStatus: string
  parseStatus: string
  ocrStatus: string
  lastCheckedAtUtc: string | null
  lastCrawledAtUtc: string | null
}

// Defines the source diagnostics response used by the Knowledge Bank warning log viewer.
export interface KnowledgeSourceDiagnosticsResponse {
  itemType: 'source'
  source: KnowledgeDiagnosticSource
  warningDocuments: KnowledgeDiagnosticDocument[]
}

// Defines the document diagnostics response used by the Knowledge Bank warning log viewer.
export interface KnowledgeDocumentDiagnosticsResponse {
  itemType: 'document'
  document: KnowledgeDiagnosticDocument
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeBankResponse {
  sourceCount: number
  documentCount: number
  folders: KnowledgeBankFolder[]
  sources: KnowledgeBankSource[]
  documents: KnowledgeBankDocument[]
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeUploadSettingsResponse {
  maxFileSizeMb: number
  maxBatchFileCount: number
  maxBatchSizeMb: number
  updatedAtUtc: string
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeFilePreviewItem {
  clientFileId: string
  fileName: string
  relativePath: string
  defaultTitle: string
  content: string
  contentPreview: string
  parseStatus: string
  ocrStatus: string
  errorMessage: string | null
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeFilePreviewResponse {
  items: KnowledgeFilePreviewItem[]
  messages: string[]
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeFileImportResponse {
  importedCount: number
  updatedCount: number
  skippedCount: number
  failedItems: Array<{ fileName: string; relativePath: string; message: string }>
  knowledgeBank: KnowledgeBankResponse
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface ReadyQuestion {
  text: string
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface ReadyQuestionsResponse {
  questions: ReadyQuestion[]
  source: string
  generatedAtUtc: string | null
  totalAvailable: number
  sourceFingerprint: string | null
  isRefreshing: boolean
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface HealthComponent {
  name: string
  status: string
  message: string | null
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface ApiHealthResponse {
  status: string
  activeAiProvider: string
  activeAiModel: string
  aiProvider: string
  runtime: string
  chat: string
  message: string
  checkedAtUtc: string
  providerStatuses: HealthComponent[]
  components: HealthComponent[]
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeHealthResponse {
  status: string
  database: string
  crawler: string
  scraper: string
  browser: string
  cache: string
  readyQuestions: string
  message: string
  pageCount: number
  sourceCount: number
  warningCount: number
  errorCount: number
  lastCrawlStatus: string | null
  lastCrawlStartedAtUtc: string | null
  lastCrawlCompletedAtUtc: string | null
  lastCrawlErrorMessage: string | null
  lastCrawlWarningMessage: string | null
  sourceFingerprint: string | null
  readyQuestionsFingerprint: string | null
  readyQuestionsFingerprintMatches: boolean
  readyQuestionsCount: number
  readyQuestionsGeneratedAtUtc: string | null
  knowledgeCacheBuiltAtUtc: string | null
  checkedAtUtc: string
  components: HealthComponent[]
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface HealthResponse {
  status: string
  api: ApiHealthResponse
  knowledge: KnowledgeHealthResponse
  checkedAtUtc: string
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface KnowledgeRedownloadResponse {
  status: string
  backupSetId: string
  startedAtUtc: string
  completedAtUtc: string
  pageCount: number
  warningCount: number
  errorCount: number
  readyQuestionsCount: number
  sourceFingerprint: string | null
  cacheBuiltAtUtc: string | null
  restoredFromBackup: boolean
  message: string
  knowledgeBank: KnowledgeBankResponse | null
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface AiModelOption {
  id: string
  label: string
  isAvailable: boolean
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface AiProviderOption {
  id: 'azure' | 'ollama-local' | 'ollama-cloud' | string
  label: string
  selectedModel: string
  isAvailable: boolean
  models: AiModelOption[]
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface AiSettingsResponse {
  activeProvider: string
  activeModel: string
  providers: AiProviderOption[]
}

// Defines the TypeScript contract used by the Oyako frontend.
export interface QnaExperienceSettingsResponse {
  displayedReadyQuestionCount: number
  displayedSuggestedQuestionCount: number
  autoSubmitPromptButtons: boolean
  showAnswerSourceDocumentNames: boolean
  updatedAtUtc: string
}
