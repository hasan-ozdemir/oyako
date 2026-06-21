// Codex developer note: Explains the purpose and flow of webapp-oyako/src/App.tsx for maintainers.
import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent, KeyboardEvent as ReactKeyboardEvent, MutableRefObject, ReactNode } from 'react'
import {
  Activity,
  AlertTriangle,
  ArrowLeft,
  BookOpen,
  Bot,
  CheckCircle2,
  Database,
  ExternalLink,
  FileText,
  FileUp,
  FolderOpen,
  Globe2,
  HardDrive,
  HelpCircle,
  Loader2,
  Mail,
  MessageSquareText,
  MoreHorizontal,
  PencilLine,
  Plus,
  Radio,
  RefreshCw,
  Save,
  Search,
  Send,
  Settings,
  Sparkles,
  Trash2,
  Upload,
  WifiOff,
} from 'lucide-react'
import type { AiSettingsResponse, ChatMessage, KnowledgeBankDocument, KnowledgeBankResponse, KnowledgeBankSource, KnowledgeDocumentContentResponse, KnowledgeDocumentDiagnosticsResponse, KnowledgeFilePreviewItem, KnowledgeRedownloadResponse, KnowledgeSourceDiagnosticsResponse, KnowledgeUploadSettingsResponse, QnaExperienceSettingsResponse, RuntimeStatus, SourceAttribution, TenantConfigResponse } from './types/chat'
import {
  addKnowledgeSource,
  addManualWebDocument,
  createRuntimeStatusStream,
  deleteKnowledgeDocument,
  deleteKnowledgeSource,
  fetchAiSettings,
  fetchKnowledgeBank,
  fetchKnowledgeDocumentContent,
  fetchKnowledgeDocumentDiagnostics,
  fetchKnowledgeHealth,
  fetchKnowledgeSourceDiagnostics,
  fetchKnowledgeSettings,
  fetchQnaExperienceSettings,
  fetchReadyQuestions,
  fetchRuntimeStatus,
  fetchTenantConfig,
  importKnowledgeFiles,
  previewKnowledgeFiles,
  redownloadKnowledgeDocument,
  redownloadKnowledgeSource,
  setKnowledgeDocumentArchived,
  setKnowledgeDocumentEnabled,
  setKnowledgeSourceArchived,
  setKnowledgeSourceEnabled,
  streamChat,
  updateAiSettings,
  updateKnowledgeSettings,
  updateQnaExperienceSettings,
  updateKnowledgeDocument,
  updateKnowledgeDocumentWebLink,
  updateKnowledgeSource,
} from './services/chatApi'
import HelpPage from './HelpPage'
import './App.css'

// Defines the in-app document viewer state for local-file source attributions.
interface DocumentViewerState {
  documentId: number
  content: KnowledgeDocumentContentResponse | null
  isLoading: boolean
  error: string
}

// Defines the warning-log viewer state for source and document diagnostics.
interface LogViewerState {
  kind: 'source' | 'document'
  itemId: number
  source: KnowledgeSourceDiagnosticsResponse | null
  document: KnowledgeDocumentDiagnosticsResponse | null
  isLoading: boolean
  error: string
  returnFocusTo: HTMLElement | null
}

// Defines a reusable frontend value used by the surrounding module.
const readyQuestionsStorageKey = 'oyako.readyQuestions'

// Defines the primary Q&A experience defaults used before backend settings load.
const defaultQnaExperienceSettings: QnaExperienceSettingsResponse = {
  displayedReadyQuestionCount: 4,
  displayedSuggestedQuestionCount: 4,
  autoSubmitPromptButtons: true,
  showAnswerSourceDocumentNames: true,
  updatedAtUtc: new Date(0).toISOString(),
}

// Defines the default tenant shown before runtime tenant config is loaded.
const defaultTenantConfig: TenantConfigResponse = {
  tenantId: '013dfb350ed64e324a805eae86646ddf',
  tenantOrderNumber: 1,
  tenantName: 'oyakdijital',
  tenantDisplayName: 'Oyak Dijital',
  tenantAzureDomainName: 'oyako',
  tenantCustomDomainName: 'oyako.oyakdijital.com.tr',
  tenantWebUrl: 'https://www.oyakdijital.com.tr',
  tenantAdminEmail: 'admin@oyakdijital.com.tr',
  tenantFeedbackEmail: 'iletisim@oyakdijital.com.tr',
  primaryAiProvider: 'ollama-cloud',
  secondaryAiProvider: 'azure',
  uiWebBrandName: 'Oyak Dijital',
  uiWebAssistantName: 'Oyako',
  uiWebTitle: 'Oyako: Oyak Dijital Soru-Cevap Platformu',
  uiWebHeaderTitle: 'Oyak Dijital soru-cevap platformu',
  uiWebBrandLogoUrl: '/tenants/oyakdijital/brand-logo.svg',
  uiWebAssistantWelcomeMessage: 'Merhaba, ben dijital asistanınız Oyako. Oyak Dijital ile ilgili merak ettiğiniz her şeyi bana sorabilirsiniz. Cevaplamak için hazırım.',
  uiWebAssistantHeaderTitle: 'Oyak Dijital hakkında öğrenmek istediğinizi sorun:',
  uiWebMoreMenuBrandLink: 'Oyak Dijital',
  uiWebMoreMenuFeedbackLink: 'Geri Bildirim Gönderin',
  uiWebMoreMenuHelpLink: 'Yardım',
  uiWebSettingsPageTitle: 'Ayarlar',
  uiWebSettingsHeaderTitle: 'Oyako çalışma ayarları',
  uiWebKnowledgeBankHeaderTitle: 'Bilgi Bankası',
  uiWebKnowledgeSourceHeaderTitle: 'Bilgi Kaynakları',
  uiWebKnowledgeSourceHeaderMessage: 'Oyako, sorularınıza cevap verirken aşağıda gösterilen {sourceCount} adet bilgi kaynağını ve {documentCount} adet belgeyi kullanabilir.',
  uiWebKnowledgeSourcesTableTitle: 'Şu kaynaklar kullanılabilir:',
  uiWebKnowledgeDocumentsTableTitle: 'Şu belgeler kullanılabilir:',
}

// Implements a frontend function that supports Oyako user or API behavior.
function loadPersistedReadyQuestions(): string[] {
  try {
    // Defines a reusable frontend value used by the surrounding module.
    const raw = window.localStorage.getItem(readyQuestionsStorageKey)
    // Guards this branch so the UI handles the condition intentionally.
    if (!raw) {
      // Returns the value or JSX produced by this frontend workflow.
      return []
    }

    // Defines a reusable frontend value used by the surrounding module.
    const parsed = JSON.parse(raw) as unknown
    // Guards this branch so the UI handles the condition intentionally.
    if (!Array.isArray(parsed)) {
      // Returns the value or JSX produced by this frontend workflow.
      return []
    }

    // Returns the value or JSX produced by this frontend workflow.
    return parsed
      .map((item) => (typeof item === 'string' ? item.trim() : ''))
      .filter(Boolean)
      .slice(0, 10)
  } catch {
    // Returns the value or JSX produced by this frontend workflow.
    return []
  }
}

// Implements a frontend function that supports Oyako user or API behavior.
function persistReadyQuestions(questions: string[]) {
  try {
    window.localStorage.setItem(readyQuestionsStorageKey, JSON.stringify(questions.slice(0, 10)))
  } catch {
    // localStorage may be unavailable in restricted browser contexts.
  }
}

const initialRuntimeStatus: RuntimeStatus = {
  operation: 'app',
  phase: 'app_loading',
  stepKey: 'loading',
  stepIndex: 1,
  stepCount: 2,
  isTerminal: false,
  message: 'Sayfa Yükleniyor',
  severity: 'info',
  icon: 'activity',
  pageCount: null,
  // Creates the browser or application object needed for this step.
  updatedAtUtc: new Date().toISOString(),
}

// Implements a frontend function that supports Oyako user or API behavior.
function nonBlank(value: string | null | undefined): string | null {
  // Defines a reusable frontend value used by the surrounding module.
  const trimmed = value?.trim()
  // Returns the value or JSX produced by this frontend workflow.
  return trimmed ? trimmed : null
}

// Implements a frontend function that supports Oyako user or API behavior.
function normalizeRuntimeStatus(
  status: Partial<RuntimeStatus> | null | undefined,
  baseStatus: RuntimeStatus = initialRuntimeStatus,
): RuntimeStatus {
  // Defines a reusable frontend value used by the surrounding module.
  const phase = nonBlank(status?.phase) ?? nonBlank(baseStatus.phase) ?? initialRuntimeStatus.phase
  // Defines a reusable frontend value used by the surrounding module.
  const message = phase === 'ready_for_question'
    ? 'Uygulama Hazır'
    : nonBlank(status?.message) ?? baseStatus.message

  // Returns the value or JSX produced by this frontend workflow.
  return {
    operation: nonBlank(status?.operation) ?? baseStatus.operation,
    phase,
    stepKey: nonBlank(status?.stepKey) ?? baseStatus.stepKey,
    stepIndex: typeof status?.stepIndex === 'number' && status.stepIndex > 0 ? status.stepIndex : baseStatus.stepIndex,
    stepCount: typeof status?.stepCount === 'number' && status.stepCount > 0 ? status.stepCount : baseStatus.stepCount,
    isTerminal: typeof status?.isTerminal === 'boolean' ? status.isTerminal : baseStatus.isTerminal,
    message,
    severity: nonBlank(status?.severity) ?? baseStatus.severity,
    icon: nonBlank(status?.icon) ?? baseStatus.icon,
    pageCount: typeof status?.pageCount === 'number' ? status.pageCount : baseStatus.pageCount,
    // Creates the browser or application object needed for this step.
    updatedAtUtc: nonBlank(status?.updatedAtUtc) ?? baseStatus.updatedAtUtc ?? new Date().toISOString(),
  }
}

// Renders verified answer source attributions as safe external links or in-app document links.
function SourceAttributionLine({
  attributions,
  onOpenDocument,
  fallbackSourceName,
}: {
  attributions: SourceAttribution[]
  onOpenDocument: (attribution: SourceAttribution) => void
  fallbackSourceName: string
}) {
  const groups = attributions.reduce<Array<{ key: string; sourceName: string; items: SourceAttribution[] }>>((acc, attribution) => {
    const key = `${attribution.sourceId}:${attribution.sourceName}`
    const existing = acc.find((group) => group.key === key)
    if (existing) {
      existing.items.push(attribution)
      return acc
    }

    acc.push({ key, sourceName: attribution.sourceName, items: [attribution] })
    return acc
  }, [])

  if (groups.length === 0) {
    return (
      <p className="answer-source-line">
        <strong>Kaynak:</strong>
        <span>{fallbackSourceName}</span>
      </p>
    )
  }

  return (
    <p className="answer-source-line">
      <strong>Kaynak:</strong>
      {groups.map((group, groupIndex) => (
        <span key={group.key} className="answer-source-group">
          {groupIndex > 0 ? <span className="source-separator">|</span> : null}
          <span className="answer-source-name">{group.sourceName} - </span>
          {group.items.map((item, itemIndex) => (
            <span key={`${item.documentId}:${item.displayLabel}`} className="answer-source-document">
              {itemIndex > 0 ? <span className="document-separator">; </span> : null}
              {item.openMode === 'document_viewer' ? (
                <button type="button" aria-label={item.displayLabel || item.documentTitle} onClick={() => onOpenDocument(item)}>
                  {item.displayLabel || item.documentTitle}
                </button>
              ) : (
                <a href={item.url} target="_blank" rel="noopener noreferrer" aria-label={item.displayLabel || item.documentTitle}>
                  {item.displayLabel || item.documentTitle}
                </a>
              )}
            </span>
          ))}
        </span>
      ))}
    </p>
  )
}

// Implements a frontend function that supports Oyako user or API behavior.
function formatDate(value: string): string {
  // Returns the value or JSX produced by this frontend workflow.
  return new Intl.DateTimeFormat('tr-TR', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

// Implements a frontend function that supports Oyako user or API behavior.
function StatusIcon({ phase }: { phase: string }) {
  // Defines a reusable frontend value used by the surrounding module.
  const props = { size: 18, 'aria-hidden': true as const }

  switch (phase) {
    case 'app_loading':
      // Returns the value or JSX produced by this frontend workflow.
      return <Loader2 {...props} className="status-loading-icon" />
    case 'browser_preparing':
      // Returns the value or JSX produced by this frontend workflow.
      return <Radio {...props} />
    case 'crawling':
      // Returns the value or JSX produced by this frontend workflow.
      return <Search {...props} />
    case 'persisting':
      // Returns the value or JSX produced by this frontend workflow.
      return <Save {...props} />
    case 'cache_building':
      // Returns the value or JSX produced by this frontend workflow.
      return <Database {...props} />
    case 'ready_questions_building':
      // Returns the value or JSX produced by this frontend workflow.
      return <RefreshCw {...props} />
    case 'asking':
      // Returns the value or JSX produced by this frontend workflow.
      return <Send {...props} />
    case 'answering':
      // Returns the value or JSX produced by this frontend workflow.
      return <Sparkles {...props} />
    case 'ready_for_question':
      // Returns the value or JSX produced by this frontend workflow.
      return <CheckCircle2 {...props} />
    case 'sources_loading':
      // Returns the value or JSX produced by this frontend workflow.
      return <BookOpen {...props} />
    case 'degraded':
      // Returns the value or JSX produced by this frontend workflow.
      return <WifiOff {...props} />
    case 'error':
      // Returns the value or JSX produced by this frontend workflow.
      return <AlertTriangle {...props} />
    default:
      // Returns the value or JSX produced by this frontend workflow.
      return <Activity {...props} />
  }
}

function focusFirstInteractiveElement(root: HTMLElement | null) {
  if (!root) {
    return
  }

  const [first] = getFocusableElements(root)
  window.setTimeout(() => (first ?? root).focus(), 0)
}

// Returns the keyboard-focusable controls inside the currently active modal surface.
function getFocusableElements(root: HTMLElement | null): HTMLElement[] {
  if (!root) {
    return []
  }

  return Array.from(root.querySelectorAll<HTMLElement>(
    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )).filter((element) => element.getAttribute('aria-hidden') !== 'true')
}

// Keeps keyboard focus inside the active modal so background application controls cannot be used.
function trapFocusInsideElement(root: HTMLElement | null, event: KeyboardEvent) {
  if (!root) {
    return
  }

  const focusableElements = getFocusableElements(root)
  if (focusableElements.length === 0) {
    event.preventDefault()
    root.focus()
    return
  }

  const first = focusableElements[0]
  const last = focusableElements[focusableElements.length - 1]
  const active = document.activeElement

  if (!root.contains(active)) {
    event.preventDefault()
    first.focus()
    return
  }

  if (event.shiftKey && active === first) {
    event.preventDefault()
    last.focus()
    return
  }

  if (!event.shiftKey && active === last) {
    event.preventDefault()
    first.focus()
  }
}

// Defines the TypeScript contract used by the Oyako frontend.
interface ProgressStep {
  label: string
  status: 'pending' | 'active' | 'done' | 'error'
}

// Defines a standard modal shell used by blocking dialogs, message boxes, confirmations, and progress viewers.
interface ModalFrameProps {
  dialogRef?: MutableRefObject<HTMLDivElement | null>
  className: string
  role?: 'dialog' | 'alertdialog'
  labelledBy: string
  describedBy?: string
  children: ReactNode
}

// Renders a shared modal layer that blocks the background application surface.
function ModalFrame({
  dialogRef,
  className,
  role = 'dialog',
  labelledBy,
  describedBy,
  children,
}: ModalFrameProps) {
  return (
    <div className="modal-layer">
      <div
        ref={(node) => {
          if (dialogRef) {
            dialogRef.current = node
          }
        }}
        className={`modal-surface ${className}`}
        role={role}
        aria-modal="true"
        aria-labelledby={labelledBy}
        aria-describedby={describedBy}
        tabIndex={-1}
      >
        {children}
      </div>
    </div>
  )
}

// Defines a reusable frontend value used by the surrounding module.
const knowledgeRedownloadStepLabels = [
  'Yedek alınıyor',
  'Bilgi tabloları temizleniyor',
  'Bilgi tarayıcı hazırlanıyor',
  'Bilgiler alınıyor',
  'Bilgiler kaydediliyor',
  'Sistem talimatları yeniden oluşturuluyor',
  'Hazır sorular üretiliyor',
  'Hazır sorular kaydediliyor',
  'Tamamlandı',
]

// Defines the canonical source types supported by the Knowledge Bank UI.
type KnowledgeSourceType = 'web_site' | 'web_links' | 'local_files'

// Defines the state for adding or editing user-managed web document links.
interface WebDocumentLinkEditorState {
  mode: 'create' | 'edit'
  sourceId: number
  document: KnowledgeBankDocument | null
  url: string
  title: string
}

// Defines a local upload card that connects a browser File with backend preview metadata.
interface LocalUploadCard {
  id: string
  file: File
  relativePath: string
  title: string
  preview: string
  parseStatus: string
  ocrStatus: string
  errorMessage: string | null
}

// Defines accepted file extensions for browser file pickers.
const supportedKnowledgeFileAccept = '.txt,.docx,.pdf,.pptx,.rtf,.epub,.htm,.html,.md'

// Implements a frontend function that supports Oyako user or API behavior.
function buildProgressSteps(labels: string[], activeStep: number, failed = false): ProgressStep[] {
  // Defines a reusable frontend value used by the surrounding module.
  const currentStep = Math.max(1, Math.min(activeStep, labels.length))
  // Returns the value or JSX produced by this frontend workflow.
  return labels.map((label, index) => ({
    label,
    status: failed && index + 1 === currentStep
      ? 'error'
      : index + 1 < currentStep
        ? 'done'
        : index + 1 === currentStep
          ? 'active'
          : 'pending',
  }))
}

// Implements a frontend function that supports Oyako user or API behavior.
function buildCompletedSteps(labels: string[]): ProgressStep[] {
  // Returns the value or JSX produced by this frontend workflow.
  return labels.map((label) => ({ label, status: 'done' }))
}

// Implements a frontend function that supports Oyako user or API behavior.
function getProgressReachedCount(steps: ProgressStep[]): number {
  // Returns the value or JSX produced by this frontend workflow.
  return steps.filter((step) => step.status !== 'pending').length
}

// Defines the properties for the standardized blocking progress viewer.
interface ProgressDialogProps {
  title: string
  steps: ProgressStep[]
  message: string
  error: string
  isDismissible: boolean
  onDismiss: () => void
  dialogRef: MutableRefObject<HTMLDivElement | null>
}

// Renders every long-running workflow with the same accessible progress dialog pattern.
function ProgressDialog({
  title,
  steps,
  message,
  error,
  isDismissible,
  onDismiss,
  dialogRef,
}: ProgressDialogProps) {
  const reachedCount = getProgressReachedCount(steps)
  const hasError = Boolean(error) || steps.some((step) => step.status === 'error')
  const canDismiss = isDismissible || hasError

  return (
    <ModalFrame
      dialogRef={dialogRef}
      className="progress-dialog compact-progress"
      role={hasError ? 'alertdialog' : 'dialog'}
      labelledBy="progress-dialog-title"
    >
      <div className="progress-status-region" role="status" aria-live="polite">
        <div className="compact-progress-top progress-dialog-top">
          <strong id="progress-dialog-title">{title}</strong>
          <span>{reachedCount}/{steps.length}</span>
        </div>
        <div className="compact-progress-bar" aria-hidden="true">
          <span
            style={{
              width: `${steps.length > 0 ? Math.round((reachedCount / steps.length) * 100) : 0}%`,
            }}
          />
        </div>
        <ol>
          {steps.map((step) => (
            <li
              key={step.label}
              className={`progress-${step.status}`}
              aria-current={step.status === 'active' ? 'step' : undefined}
            >
              <span className="progress-step-icon" aria-hidden="true">
                {step.status === 'error'
                  ? <AlertTriangle size={14} />
                  : step.status === 'pending'
                    ? <span className="progress-step-dot" />
                    : <CheckCircle2 size={14} />}
              </span>
              <span>{step.label}</span>
            </li>
          ))}
        </ol>
      </div>
      {message ? <p className="success-banner" role="alert">{message}</p> : null}
      {error ? <p className="error-banner" role="alert">{error}</p> : null}
      {canDismiss ? (
        <div className="compact-progress-actions">
          <button type="button" className="ack-button" onClick={onDismiss}>
            Tamam, teşekkürler
          </button>
        </div>
      ) : null}
    </ModalFrame>
  )
}

// Defines the properties for modal message boxes that must not pollute the chat transcript.
interface MessageDialogProps {
  message: string
  onClose: () => void
  dialogRef: MutableRefObject<HTMLDivElement | null>
}

// Renders user-friendly error or warning messages as a blocking modal message box.
function MessageDialog({ message, onClose, dialogRef }: MessageDialogProps) {
  return (
    <ModalFrame
      dialogRef={dialogRef}
      className="message-dialog"
      role="alertdialog"
      labelledBy="message-dialog-title"
      describedBy="message-dialog-description"
    >
      <div className="message-dialog-head">
        <AlertTriangle size={20} aria-hidden="true" />
        <div>
          <h2 id="message-dialog-title">İşlem tamamlanamadı</h2>
          <p id="message-dialog-description">{message}</p>
        </div>
        <button type="button" className="message-dialog-close" aria-label="Mesajı kapat" onClick={onClose}>
          ×
        </button>
      </div>
    </ModalFrame>
  )
}

// Defines the data needed to ask the user for a modal confirmation.
interface ConfirmDialogState {
  title: string
  message: string
  confirmLabel: string
  cancelLabel: string
  onConfirm: () => void
}

// Defines the editable source form draft used by the source edit modal.
interface SourceEditorState {
  source: KnowledgeBankSource
  name: string
  description: string
  address: string
}

// Defines the properties for destructive and important confirmation dialogs.
interface ConfirmDialogProps extends ConfirmDialogState {
  onCancel: () => void
  dialogRef: MutableRefObject<HTMLDivElement | null>
}

// Renders native-confirm replacements as accessible alert dialogs.
function ConfirmDialog({
  title,
  message,
  confirmLabel,
  cancelLabel,
  onConfirm,
  onCancel,
  dialogRef,
}: ConfirmDialogProps) {
  return (
    <ModalFrame
      dialogRef={dialogRef}
      className="confirm-dialog"
      role="alertdialog"
      labelledBy="confirm-dialog-title"
      describedBy="confirm-dialog-description"
    >
      <div className="confirm-dialog-icon" aria-hidden="true">
        <AlertTriangle size={24} />
      </div>
      <div className="confirm-dialog-copy">
        <h2 id="confirm-dialog-title">{title}</h2>
        <p id="confirm-dialog-description">{message}</p>
      </div>
      <div className="confirm-dialog-actions">
        <button type="button" className="secondary-action-button" onClick={onCancel}>
          {cancelLabel}
        </button>
        <button type="button" className="danger-action-button" onClick={onConfirm}>
          {confirmLabel}
        </button>
      </div>
    </ModalFrame>
  )
}

// Implements a frontend function that converts technical chat failures into user-friendly copy.
function normalizeChatErrorMessage(rawMessage: string, assistantName: string): string {
  const compact = rawMessage.replace(/\s+/g, ' ').trim()
  const lower = compact.toLowerCase()

  if (
    lower.includes('503') ||
    lower.includes('service unavailable') ||
    lower.includes('response status code') ||
    lower.includes('ollama') ||
    lower.includes('azure')
  ) {
    return `${assistantName} şu anda yanıt üretemedi. Lütfen birkaç saniye sonra tekrar deneyin.`
  }

  if (lower.includes('bağlantı kurulamadı') || lower.includes('failed to fetch') || lower.includes('network')) {
    return `${assistantName} servislerine şu anda ulaşılamıyor. Lütfen bağlantınızı kontrol edip tekrar deneyin.`
  }

  if (lower.includes('sunucu yanıt akışı') || lower.includes('stream')) {
    return 'Yanıt akışı başlatılamadı. Lütfen kısa bir süre sonra tekrar deneyin.'
  }

  if (!compact || compact.length > 180 || lower.includes('exception') || lower.includes('stack trace')) {
    return 'Beklenmeyen bir durum oluştu. Lütfen tekrar deneyin.'
  }

  // Returns the value or JSX produced by this frontend workflow.
  return compact
}

// Implements a frontend function that detects whether a displayed address should be opened as an external link.
function isExternalUrl(value: string): boolean {
  return /^https?:\/\//i.test(value)
}

// Implements a frontend function that maps internal source type codes to Turkish UI text.
function formatSourceType(sourceType: string): string {
  if (sourceType === 'local_files') {
    return 'Yerel Dosyalar'
  }

  if (sourceType === 'web_links') {
    return 'Web Bağlantıları'
  }

  return 'Web Sitesi'
}

function formatTenantCountMessage(template: string, sourceCount: number, documentCount: number): string {
  return template
    .replaceAll('{sourceCount}', String(sourceCount))
    .replaceAll('{documentCount}', String(documentCount))
    .replaceAll('[X]', String(sourceCount))
    .replaceAll('[Y]', String(documentCount))
}

// Implements a frontend function that gets the browser-provided relative path for folder uploads.
function getKnowledgeFileRelativePath(file: File): string {
  return (file as File & { webkitRelativePath?: string }).webkitRelativePath || file.name
}

// Implements a frontend function that supports Oyako user or API behavior.
function App() {
  // Creates React state that drives an interactive part of the UI.
  const [messages, setMessages] = useState<ChatMessage[]>([])
  // Creates React state that drives an interactive part of the UI.
  const [input, setInput] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [isStreaming, setIsStreaming] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [floatingMessage, setFloatingMessage] = useState('')
  // Stores the active modal confirmation request that replaces native browser confirm dialogs.
  const [confirmDialog, setConfirmDialog] = useState<ConfirmDialogState | null>(null)
  // Creates React state that drives an interactive part of the UI.
  const [runtimeStatus, setRuntimeStatus] = useState<RuntimeStatus>(initialRuntimeStatus)
  const [tenantConfig, setTenantConfig] = useState<TenantConfigResponse>(defaultTenantConfig)
  const appSurfaceRef = useRef<HTMLDivElement | null>(null)
  const lastFocusTargetRef = useRef<HTMLElement | null>(null)
  const hadFloatingSurfaceRef = useRef(false)
  const messageDialogRef = useRef<HTMLDivElement | null>(null)
  const confirmDialogRef = useRef<HTMLDivElement | null>(null)
  const sourcesDialogRef = useRef<HTMLDivElement | null>(null)
  const settingsDialogRef = useRef<HTMLDivElement | null>(null)
  const readyQuestionsTitleRef = useRef<HTMLHeadingElement | null>(null)
  const newSourceDialogRef = useRef<HTMLDivElement | null>(null)
  const sourceEditorDialogRef = useRef<HTMLDivElement | null>(null)
  const webDocumentLinkDialogRef = useRef<HTMLDivElement | null>(null)
  const newDocumentDialogRef = useRef<HTMLDivElement | null>(null)
  const documentEditorDialogRef = useRef<HTMLDivElement | null>(null)
  const documentViewerDialogRef = useRef<HTMLDivElement | null>(null)
  const logViewerDialogRef = useRef<HTMLDivElement | null>(null)
  const utilityMenuRef = useRef<HTMLDivElement | null>(null)
  const userMenuRef = useRef<HTMLDivElement | null>(null)
  const documentUploadInputRef = useRef<HTMLInputElement | null>(null)
  const localFilesInputRef = useRef<HTMLInputElement | null>(null)
  const localFolderInputRef = useRef<HTMLInputElement | null>(null)
  const composerRef = useRef<HTMLTextAreaElement | null>(null)
  const closeNewLocalDocumentDialogRef = useRef<() => void>(() => {})
  const closeSettingsPageRef = useRef<() => void | Promise<void>>(() => {})
  // Creates React state that drives an interactive part of the UI.
  const [pageCount, setPageCount] = useState(0)
  // Creates React state that drives an interactive part of the UI.
  const [sourceCount, setSourceCount] = useState(0)
  // Creates React state that drives an interactive part of the UI.
  const [knowledgeSources, setKnowledgeSources] = useState<KnowledgeBankSource[]>([])
  // Creates React state that drives an interactive part of the UI.
  const [knowledgeDocuments, setKnowledgeDocuments] = useState<KnowledgeBankDocument[]>([])
  // Creates React state that drives the warning-log modal used by the Knowledge Bank tables.
  const [logViewer, setLogViewer] = useState<LogViewerState | null>(null)
  // Creates React state that drives the selected Knowledge Bank source.
  const [selectedSourceId, setSelectedSourceId] = useState<number | null>(null)
  // Creates React state that drives an interactive part of the UI.
  const [editingDocument, setEditingDocument] = useState<KnowledgeBankDocument | null>(null)
  // Creates React state that drives an interactive part of the UI.
  const [documentDraftContent, setDocumentDraftContent] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [documentEditorError, setDocumentEditorError] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [isDocumentEditorSaving, setIsDocumentEditorSaving] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [isNewSourceOpen, setIsNewSourceOpen] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [newSourceType, setNewSourceType] = useState<KnowledgeSourceType>('web_site')
  // Creates React state that drives an interactive part of the UI.
  const [newSourceName, setNewSourceName] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [newSourceDescription, setNewSourceDescription] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [newSourceAddress, setNewSourceAddress] = useState('')
  // Stores the active source being edited in the standardized source editor modal.
  const [editingSource, setEditingSource] = useState<SourceEditorState | null>(null)
  // Stores the active web document link add/edit modal state.
  const [webDocumentLinkEditor, setWebDocumentLinkEditor] = useState<WebDocumentLinkEditorState | null>(null)
  // Stores web document link editor validation and backend errors.
  const [webDocumentLinkError, setWebDocumentLinkError] = useState('')
  // Tracks whether a web document link add/edit operation is being persisted.
  const [isWebDocumentLinkSaving, setIsWebDocumentLinkSaving] = useState(false)
  // Creates React state that drives the local file import dialog.
  const [isNewDocumentOpen, setIsNewDocumentOpen] = useState(false)
  // Creates React state that stores local file preview cards.
  const [localUploadCards, setLocalUploadCards] = useState<LocalUploadCard[]>([])
  // Creates React state that stores local file import errors.
  const [localFileError, setLocalFileError] = useState('')
  // Creates React state that tracks backend file preview work.
  const [isPreviewingFiles, setIsPreviewingFiles] = useState(false)
  // Creates React state that tracks backend file import work.
  const [isImportingFiles, setIsImportingFiles] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [isSourcesOpen, setIsSourcesOpen] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [isSourcesLoading, setIsSourcesLoading] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [sourcesError, setSourcesError] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [isKnowledgeRedownloading, setIsKnowledgeRedownloading] = useState(false)
  // Creates React state that tracks lightweight knowledge-cache activation changes.
  const [isKnowledgeActivating, setIsKnowledgeActivating] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [knowledgeRedownloadError, setKnowledgeRedownloadError] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [knowledgeRedownloadMessage, setKnowledgeRedownloadMessage] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [knowledgeProgressSteps, setKnowledgeProgressSteps] = useState<ProgressStep[]>([])
  // Creates React state that drives an interactive part of the UI.
  const [isKnowledgeProgressDismissible, setIsKnowledgeProgressDismissible] = useState(false)
  // Creates React state that drives the full-page local document viewer.
  const [documentViewer, setDocumentViewer] = useState<DocumentViewerState | null>(null)
  // Combines heavy and lightweight knowledge operations for controls that must avoid concurrent writes.
  const isKnowledgeMutationBusy = isKnowledgeRedownloading || isKnowledgeActivating
  // Creates React state that drives an interactive part of the UI.
  const [a11yAnnouncement, setA11yAnnouncement] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [isUtilityMenuOpen, setIsUtilityMenuOpen] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [isHelpOpen, setIsHelpOpen] = useState(() => window.location.pathname === '/help')
  // Creates React state that drives an interactive part of the UI.
  const [isSettingsOpen, setIsSettingsOpen] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [isSettingsLoading, setIsSettingsLoading] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [settingsError, setSettingsError] = useState('')
  // Creates React state that drives an interactive part of the UI.
  const [aiSettings, setAiSettings] = useState<AiSettingsResponse | null>(null)
  // Creates React state that stores upload limit settings from the backend.
  const [knowledgeUploadSettings, setKnowledgeUploadSettings] = useState<KnowledgeUploadSettingsResponse | null>(null)
  // Creates React state that stores Q&A experience settings from the backend.
  const [qnaExperienceSettings, setQnaExperienceSettings] = useState<QnaExperienceSettingsResponse>(defaultQnaExperienceSettings)
  // Creates React state that drives an interactive part of the UI.
  const [draftProvider, setDraftProvider] = useState('azure')
  // Creates React state that drives an interactive part of the UI.
  const [draftModel, setDraftModel] = useState('DeepSeek-V4-Flash')
  // Creates React state that edits maximum file size in megabytes.
  const [draftMaxFileSizeMb, setDraftMaxFileSizeMb] = useState(25)
  // Creates React state that edits maximum batch file count.
  const [draftMaxBatchFileCount, setDraftMaxBatchFileCount] = useState(100)
  // Creates React state that edits maximum batch size in megabytes.
  const [draftMaxBatchSizeMb, setDraftMaxBatchSizeMb] = useState(250)
  // Creates React state that edits displayed ready question count.
  const [draftDisplayedReadyQuestionCount, setDraftDisplayedReadyQuestionCount] = useState(defaultQnaExperienceSettings.displayedReadyQuestionCount)
  // Creates React state that edits displayed suggested question count.
  const [draftDisplayedSuggestedQuestionCount, setDraftDisplayedSuggestedQuestionCount] = useState(defaultQnaExperienceSettings.displayedSuggestedQuestionCount)
  // Creates React state that edits prompt button auto-submit behavior.
  const [draftAutoSubmitPromptButtons, setDraftAutoSubmitPromptButtons] = useState(defaultQnaExperienceSettings.autoSubmitPromptButtons)
  // Creates React state that edits answer source document visibility.
  const [draftShowAnswerSourceDocumentNames, setDraftShowAnswerSourceDocumentNames] = useState(defaultQnaExperienceSettings.showAnswerSourceDocumentNames)
  // Creates React state that drives an interactive part of the UI.
  const [isSavingSettings, setIsSavingSettings] = useState(false)
  // Creates React state that drives an interactive part of the UI.
  const [settingsSaveSteps, setSettingsSaveSteps] = useState<ProgressStep[]>([])
  // Creates React state that drives an interactive part of the UI.
  const [latestSuggestionOwnerId, setLatestSuggestionOwnerId] = useState<string | null>(null)
  // Creates React state that drives an interactive part of the UI.
  const [latestSuggestedQuestions, setLatestSuggestedQuestions] = useState<string[]>([])
  // Creates React state that drives an interactive part of the UI.
  const [readyQuestions, setReadyQuestions] = useState<string[]>(() => loadPersistedReadyQuestions())
  // Creates React state that drives an interactive part of the UI.
  const [isReadyQuestionsLoading, setIsReadyQuestionsLoading] = useState(false)
  // Defines the active number of ready questions shown in the web UI.
  const displayedReadyQuestionCount = qnaExperienceSettings.displayedReadyQuestionCount
  // Defines the active number of suggested questions shown below the latest answer.
  const displayedSuggestedQuestionCount = qnaExperienceSettings.displayedSuggestedQuestionCount
  // Defines whether ready and suggested question buttons submit immediately.
  const autoSubmitPromptButtons = qnaExperienceSettings.autoSubmitPromptButtons
  // Defines whether answer source document names are rendered below assistant answers.
  const showAnswerSourceDocumentNames = qnaExperienceSettings.showAnswerSourceDocumentNames
  // Defines the ready questions currently visible to the user.
  const visibleReadyQuestions = readyQuestions.slice(0, displayedReadyQuestionCount)
  // Defines a reusable frontend value used by the surrounding module.
  const listRef = useRef<HTMLDivElement | null>(null)
  // Defines a reusable frontend value used by the surrounding module.
  const knowledgeProgressRef = useRef<HTMLDivElement | null>(null)
  // Defines a reusable frontend value used by the surrounding module.
  const settingsProgressRef = useRef<HTMLDivElement | null>(null)
  // Defines a reusable frontend value used by the surrounding module.
  const isKnowledgeRedownloadingRef = useRef(false)
  // Tracks whether a blocking modal surface is open so the background application can be made inert.
  const isBlockingModalOpen = Boolean(
    floatingMessage ||
    confirmDialog ||
    isSourcesOpen ||
    isSettingsOpen ||
    isHelpOpen ||
    isNewSourceOpen ||
    editingSource ||
    webDocumentLinkEditor ||
    isNewDocumentOpen ||
    editingDocument ||
    documentViewer ||
    logViewer ||
    knowledgeProgressSteps.length > 0 ||
    settingsSaveSteps.length > 0,
  )
  // Identifies the currently active floating surface without depending on every keystroke inside that surface.
  const activeModalFocusKey = useMemo(() => {
    if (settingsSaveSteps.length > 0) {
      return 'settings-progress'
    }

    if (knowledgeProgressSteps.length > 0) {
      return 'knowledge-progress'
    }

    if (confirmDialog) {
      return 'confirm-dialog'
    }

    if (floatingMessage) {
      return 'floating-message'
    }

    if (editingDocument) {
      return `document-editor:${editingDocument.id}`
    }

    if (isNewDocumentOpen) {
      return 'new-document'
    }

    if (editingSource) {
      return `source-editor:${editingSource.source.id}`
    }

    if (webDocumentLinkEditor) {
      return `web-document-link:${webDocumentLinkEditor.mode}:${webDocumentLinkEditor.document?.id ?? webDocumentLinkEditor.sourceId}`
    }

    if (isNewSourceOpen) {
      return 'new-source'
    }

    if (documentViewer) {
      return `document-viewer:${documentViewer.documentId}`
    }

    if (logViewer) {
      return `log-viewer:${logViewer.kind}:${logViewer.itemId}`
    }

    if (isSourcesOpen) {
      return 'sources'
    }

    if (isSettingsOpen) {
      return 'settings'
    }

    if (isUtilityMenuOpen) {
      return 'utility-menu'
    }

    if (isUserMenuOpen) {
      return 'user-menu'
    }

    if (isHelpOpen) {
      return 'help'
    }

    return ''
  }, [
    confirmDialog,
    documentViewer,
    editingDocument,
    editingSource,
    floatingMessage,
    isHelpOpen,
    isNewDocumentOpen,
    isNewSourceOpen,
    isSettingsOpen,
    isSourcesOpen,
    isUserMenuOpen,
    isUtilityMenuOpen,
    knowledgeProgressSteps.length,
    logViewer,
    settingsSaveSteps.length,
    webDocumentLinkEditor,
  ])
  // Defines a reusable frontend value used by the surrounding module.
  const knowledgeProgressPollRef = useRef<number | undefined>(undefined)
  // Defines a reusable frontend value used by the surrounding module.
  const knowledgeProgressDismissTimerRef = useRef<number | undefined>(undefined)

  // Defines a reusable frontend value used by the surrounding module.
  const hasInitialLoadCompletedRef = useRef(false)

  // Implements a frontend function that supports Oyako user or API behavior.
  function normalizeLiveRuntimeStatus(
    status: Partial<RuntimeStatus> | null | undefined,
    baseStatus: RuntimeStatus = runtimeStatus,
  ): RuntimeStatus {
    // Defines a reusable frontend value used by the surrounding module.
    const normalized = normalizeRuntimeStatus(status, baseStatus)
    // Guards this branch so the UI handles the condition intentionally.
    if (!hasInitialLoadCompletedRef.current || normalized.phase !== 'app_loading') {
      // Returns the value or JSX produced by this frontend workflow.
      return normalized
    }

    // Returns the value or JSX produced by this frontend workflow.
    return normalizeRuntimeStatus({
      ...normalized,
      operation: 'app',
      phase: 'ready_for_question',
      stepKey: 'ready',
      stepIndex: 2,
      stepCount: 2,
      isTerminal: true,
      message: 'Uygulama Hazır',
      severity: 'ready',
      icon: 'check',
    }, normalized)
  }

  // Defines a reusable frontend value used by the surrounding module.
  const visibleRuntimeStatus = useMemo(() => normalizeRuntimeStatus(runtimeStatus), [runtimeStatus])
  // Defines a reusable frontend value used by the surrounding module.
  const visibleSourceCount = useMemo(
    () => sourceCount || knowledgeSources.length || (pageCount > 0 ? 1 : 0),
    [knowledgeSources.length, pageCount, sourceCount],
  )
  // Defines the currently selected Knowledge Bank source for document filtering.
  const selectedKnowledgeSource = useMemo(
    () => knowledgeSources.find((source) => source.id === selectedSourceId) ?? knowledgeSources[0] ?? null,
    [knowledgeSources, selectedSourceId],
  )
  // Defines the documents visible for the selected source.
  const visibleKnowledgeDocuments = useMemo(
    () => selectedKnowledgeSource
      ? knowledgeDocuments.filter((document) => document.sourceId === selectedKnowledgeSource.id)
      : knowledgeDocuments,
    [knowledgeDocuments, selectedKnowledgeSource],
  )
  const knowledgeBankSummary = useMemo(
    () => formatTenantCountMessage(tenantConfig.uiWebKnowledgeSourceHeaderMessage, visibleSourceCount, pageCount),
    [pageCount, tenantConfig.uiWebKnowledgeSourceHeaderMessage, visibleSourceCount],
  )

  useEffect(() => {
    document.title = tenantConfig.uiWebTitle
    const description = `${tenantConfig.uiWebAssistantName}, ${tenantConfig.uiWebBrandName} bilgi kaynaklarıyla çalışan soru-cevap platformudur.`
    document.querySelector('meta[name="description"]')?.setAttribute('content', description)
    document.querySelector('meta[name="apple-mobile-web-app-title"]')?.setAttribute('content', tenantConfig.uiWebAssistantName)
  }, [tenantConfig])

  // Runs a React side effect that enables Chromium folder selection for local file imports.
  useEffect(() => {
    localFolderInputRef.current?.setAttribute('webkitdirectory', '')
    localFolderInputRef.current?.setAttribute('directory', '')
  }, [])

  // Runs a React side effect that synchronizes UI state with browser or API behavior.
  useEffect(() => {
    let isDisposed = false
    let pollId: number | undefined
    let source: EventSource | undefined

    // Defines a reusable frontend value used by the surrounding module.
    const completeInitialLoad = async () => {
      const loadedTenantConfig = await fetchTenantConfig().catch(() => defaultTenantConfig)
      setTenantConfig(loadedTenantConfig)

      const initialQnaSettings = await fetchQnaExperienceSettings().catch(() => defaultQnaExperienceSettings)
      setQnaExperienceSettings(initialQnaSettings)
      setDraftDisplayedReadyQuestionCount(initialQnaSettings.displayedReadyQuestionCount)
      setDraftDisplayedSuggestedQuestionCount(initialQnaSettings.displayedSuggestedQuestionCount)
      setDraftAutoSubmitPromptButtons(initialQnaSettings.autoSubmitPromptButtons)
      setDraftShowAnswerSourceDocumentNames(initialQnaSettings.showAnswerSourceDocumentNames)

      // Awaits the asynchronous frontend operation before continuing.
      await Promise.allSettled([
        loadKnowledgeStatus(),
        loadReadyQuestions(initialQnaSettings.displayedReadyQuestionCount),
        loadRuntimeStatus(),
      ])

      // Guards this branch so the UI handles the condition intentionally.
      if (isDisposed) {
        // Returns the value or JSX produced by this frontend workflow.
        return
      }

      hasInitialLoadCompletedRef.current = true
      setRuntimeStatus((current) => normalizeLiveRuntimeStatus(current))
    }

    // Defines a reusable frontend value used by the surrounding module.
    const startPolling = () => {
      // Guards this branch so the UI handles the condition intentionally.
      if (pollId !== undefined) {
        // Returns the value or JSX produced by this frontend workflow.
        return
      }

      pollId = window.setInterval(() => {
        void loadRuntimeStatus()
      }, 5000)
    }

    try {
      source = createRuntimeStatusStream()
      source.onmessage = (event) => {
        // Guards this branch so the UI handles the condition intentionally.
        if (isDisposed) {
          // Returns the value or JSX produced by this frontend workflow.
          return
        }

        try {
          // Defines a reusable frontend value used by the surrounding module.
          const parsed = JSON.parse(event.data) as { type?: string; status?: RuntimeStatus } | RuntimeStatus
          // Defines a reusable frontend value used by the surrounding module.
          const nextStatus = 'status' in parsed && parsed.status ? parsed.status : (parsed as RuntimeStatus)
          setRuntimeStatus((current) => normalizeLiveRuntimeStatus(nextStatus, current))
          // Guards this branch so the UI handles the condition intentionally.
          if (typeof nextStatus.pageCount === 'number') {
            setPageCount(nextStatus.pageCount)
          }
          applyKnowledgeProgress(nextStatus)
        } catch {
        setRuntimeStatus({
          ...initialRuntimeStatus,
          operation: 'app',
          phase: 'degraded',
          stepKey: 'degraded',
          stepIndex: 1,
          stepCount: 1,
          isTerminal: true,
          message: 'Uygulama Çevrimdışı',
          severity: 'warning',
        })
        }
      }
      source.onerror = () => {
        source?.close()
        startPolling()
      }
    } catch {
      startPolling()
    }

    void completeInitialLoad()

    // Returns the value or JSX produced by this frontend workflow.
    return () => {
      isDisposed = true
      source?.close()
      if (pollId !== undefined) {
        window.clearInterval(pollId)
      }
      if (knowledgeProgressPollRef.current !== undefined) {
        window.clearInterval(knowledgeProgressPollRef.current)
      }
      if (knowledgeProgressDismissTimerRef.current !== undefined) {
        window.clearTimeout(knowledgeProgressDismissTimerRef.current)
      }
    }
    // Keeps the bootstrap effect intentionally one-shot because it owns app startup wiring.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (listRef.current) {
      listRef.current.scrollTo({ top: listRef.current.scrollHeight, behavior: 'smooth' })
    }
  }, [messages])

  // Runs a React side effect that clears transient live-region announcements after screen readers can announce them.
  useEffect(() => {
    if (!a11yAnnouncement) {
      return
    }

    const clearAnnouncementId = window.setTimeout(() => setA11yAnnouncement(''), 6000)
    return () => window.clearTimeout(clearAnnouncementId)
  }, [a11yAnnouncement])

  useEffect(() => {
    const surface = appSurfaceRef.current
    if (!surface) {
      return
    }

    if (isBlockingModalOpen) {
      surface.setAttribute('inert', '')
      surface.setAttribute('aria-hidden', 'true')
      document.body.classList.add('oyako-modal-open')
      return () => {
        surface.removeAttribute('inert')
        surface.removeAttribute('aria-hidden')
        document.body.classList.remove('oyako-modal-open')
      }
    }

    surface.removeAttribute('inert')
    surface.removeAttribute('aria-hidden')
    document.body.classList.remove('oyako-modal-open')

    return () => {
      surface.removeAttribute('inert')
      surface.removeAttribute('aria-hidden')
      document.body.classList.remove('oyako-modal-open')
    }
  }, [isBlockingModalOpen])

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const activeModal = settingsSaveSteps.length > 0
        ? settingsProgressRef.current
        : knowledgeProgressSteps.length > 0
          ? knowledgeProgressRef.current
          : confirmDialog
            ? confirmDialogRef.current
            : floatingMessage
              ? messageDialogRef.current
              : editingDocument
                ? documentEditorDialogRef.current
                : isNewDocumentOpen
                  ? newDocumentDialogRef.current
                  : editingSource
                    ? sourceEditorDialogRef.current
                    : webDocumentLinkEditor
                      ? webDocumentLinkDialogRef.current
                      : isNewSourceOpen
                        ? newSourceDialogRef.current
                        : documentViewer
                          ? documentViewerDialogRef.current
                          : logViewer
                            ? logViewerDialogRef.current
                            : isSettingsOpen
                              ? settingsDialogRef.current
                              : isSourcesOpen
                                ? sourcesDialogRef.current
                                : isHelpOpen
                                  ? document.querySelector<HTMLElement>('.help-page')
                                  : null

      if (event.key === 'Tab' && activeModal) {
        trapFocusInsideElement(activeModal, event)
        return
      }

      if (event.key === 'Escape') {
        if (settingsSaveSteps.length > 0 || knowledgeProgressSteps.length > 0) {
          return
        }

        if (confirmDialog) {
          setConfirmDialog(null)
          return
        }

        if (floatingMessage) {
          closeFloatingMessage()
          return
        }

        if (editingDocument && !isDocumentEditorSaving) {
          setEditingDocument(null)
          setDocumentDraftContent('')
          setDocumentEditorError('')
          return
        }

        if (isNewDocumentOpen && !isPreviewingFiles && !isImportingFiles) {
          closeNewLocalDocumentDialogRef.current()
          return
        }

        if (editingSource && !isKnowledgeMutationBusy) {
          setEditingSource(null)
          return
        }

        if (webDocumentLinkEditor && !isWebDocumentLinkSaving) {
          setWebDocumentLinkEditor(null)
          setWebDocumentLinkError('')
          return
        }

        if (isNewSourceOpen && !isKnowledgeMutationBusy) {
          setIsNewSourceOpen(false)
          return
        }

        if (documentViewer) {
          closeDocumentViewer()
          return
        }

        if (logViewer) {
          const target = logViewer.returnFocusTo
          setLogViewer(null)
          window.setTimeout(() => {
            if (target?.isConnected) {
              target.focus()
            }
          }, 0)
          return
        }

        if (isSettingsOpen) {
          void closeSettingsPageRef.current()
          return
        }

        if (isSourcesOpen) {
          setIsSourcesOpen(false)
          return
        }

        if (isHelpOpen) {
          setIsHelpOpen(false)
          if (window.location.pathname === '/help') {
            window.history.pushState({}, '', '/')
          }
          return
        }

        if (isUtilityMenuOpen) {
          setIsUtilityMenuOpen(false)
          return
        }

        if (isUserMenuOpen) {
          setIsUserMenuOpen(false)
        }

        if (window.location.pathname === '/help') {
          window.history.pushState({}, '', '/')
        }
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [
    confirmDialog,
    documentViewer,
    logViewer,
    editingDocument,
    editingSource,
    floatingMessage,
    isDocumentEditorSaving,
    isHelpOpen,
    isImportingFiles,
    isKnowledgeMutationBusy,
    isNewDocumentOpen,
    isNewSourceOpen,
    isWebDocumentLinkSaving,
    isPreviewingFiles,
    isSettingsOpen,
    isSourcesOpen,
    isUserMenuOpen,
    isUtilityMenuOpen,
    knowledgeProgressSteps.length,
    settingsSaveSteps.length,
    webDocumentLinkEditor,
  ])

  useEffect(() => {
    const isFloatingSurfaceOpen = isBlockingModalOpen || isUtilityMenuOpen || isUserMenuOpen

    if (isFloatingSurfaceOpen && !hadFloatingSurfaceRef.current) {
      const activeElement = document.activeElement
      if (activeElement instanceof HTMLElement) {
        lastFocusTargetRef.current = activeElement
      }
    }

    if (!isFloatingSurfaceOpen && hadFloatingSurfaceRef.current) {
      const target = lastFocusTargetRef.current
      window.setTimeout(() => {
        if (target?.isConnected) {
          target.focus()
        }
      }, 0)
    }

    hadFloatingSurfaceRef.current = isFloatingSurfaceOpen
  }, [isBlockingModalOpen, isUserMenuOpen, isUtilityMenuOpen])

  useEffect(() => {
    if (!activeModalFocusKey) {
      return
    }

    if (activeModalFocusKey === 'settings-progress') {
      focusFirstInteractiveElement(settingsProgressRef.current)
    } else if (activeModalFocusKey === 'knowledge-progress') {
      focusFirstInteractiveElement(knowledgeProgressRef.current)
    } else if (activeModalFocusKey === 'confirm-dialog') {
      focusFirstInteractiveElement(confirmDialogRef.current)
    } else if (activeModalFocusKey === 'floating-message') {
      focusFirstInteractiveElement(messageDialogRef.current)
    } else if (activeModalFocusKey.startsWith('document-editor:')) {
      focusFirstInteractiveElement(documentEditorDialogRef.current)
    } else if (activeModalFocusKey === 'new-document') {
      focusFirstInteractiveElement(newDocumentDialogRef.current)
    } else if (activeModalFocusKey.startsWith('source-editor:')) {
      focusFirstInteractiveElement(sourceEditorDialogRef.current)
    } else if (activeModalFocusKey.startsWith('web-document-link:')) {
      focusFirstInteractiveElement(webDocumentLinkDialogRef.current)
    } else if (activeModalFocusKey === 'new-source') {
      focusFirstInteractiveElement(newSourceDialogRef.current)
    } else if (activeModalFocusKey.startsWith('document-viewer:')) {
      focusFirstInteractiveElement(documentViewerDialogRef.current)
    } else if (activeModalFocusKey.startsWith('log-viewer:')) {
      focusFirstInteractiveElement(logViewerDialogRef.current)
    } else if (activeModalFocusKey === 'sources') {
      focusFirstInteractiveElement(sourcesDialogRef.current)
    } else if (activeModalFocusKey === 'settings') {
      focusFirstInteractiveElement(settingsDialogRef.current)
    } else if (activeModalFocusKey === 'utility-menu') {
      focusFirstInteractiveElement(utilityMenuRef.current)
    } else if (activeModalFocusKey === 'user-menu') {
      focusFirstInteractiveElement(userMenuRef.current)
    } else if (activeModalFocusKey === 'help') {
      focusFirstInteractiveElement(document.querySelector<HTMLElement>('.help-page'))
    }
  }, [activeModalFocusKey])

  useEffect(() => {
    function onPopState() {
      setIsHelpOpen(window.location.pathname === '/help')
    }

    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  async function loadRuntimeStatus() {
    try {
      const status = await fetchRuntimeStatus()
      setRuntimeStatus((current) => normalizeLiveRuntimeStatus(status, current))
      if (typeof status.pageCount === 'number') {
        setPageCount(status.pageCount)
      }
      applyKnowledgeProgress(status)
    } catch {
      setRuntimeStatus({
        ...initialRuntimeStatus,
        operation: 'app',
        phase: 'degraded',
        stepKey: 'degraded',
        stepIndex: 1,
        stepCount: 1,
        isTerminal: true,
        message: 'Uygulama Çevrimdışı',
        severity: 'warning',
      })
    }
  }

  async function loadKnowledgeStatus() {
    try {
      const status = await fetchKnowledgeHealth()
      setPageCount(status.pageCount ?? 0)
      setSourceCount(status.sourceCount ?? 0)
    } catch {
      setRuntimeStatus({
        ...initialRuntimeStatus,
        operation: 'app',
        phase: 'degraded',
        stepKey: 'degraded',
        stepIndex: 1,
        stepCount: 1,
        isTerminal: true,
        message: 'Uygulama Çevrimdışı',
        severity: 'warning',
      })
    }
  }

  async function loadReadyQuestions(countOverride?: number) {
    setIsReadyQuestionsLoading(true)
    try {
      const requestedCount = countOverride ?? qnaExperienceSettings.displayedReadyQuestionCount
      const response = await fetchReadyQuestions(requestedCount)
      const nextQuestions = response.questions
        .map((question) => question.text.trim())
        .filter(Boolean)

      if (response.source === 'generated' && nextQuestions.length > 0) {
        setReadyQuestions(nextQuestions.slice(0, requestedCount))
        persistReadyQuestions(nextQuestions)
        return
      }

      setReadyQuestions((current) => {
        if (current.length > 0) {
          return current.slice(0, requestedCount)
        }

        return nextQuestions
      })
    } catch {
      setReadyQuestions((current) => current)
    } finally {
      setIsReadyQuestionsLoading(false)
    }
  }

  function clearCurrentFocus() {
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur()
    }
  }

  function focusReadyQuestionsTitle() {
    readyQuestionsTitleRef.current?.focus({ preventScroll: true })
  }

  async function refreshReadyQuestionsFromButton() {
    clearCurrentFocus()
    await loadReadyQuestions()
    await new Promise<void>((resolve) => {
      window.requestAnimationFrame(() => {
        window.requestAnimationFrame(() => resolve())
      })
    })
    focusReadyQuestionsTitle()
  }

  function applyKnowledgeBankResponse(response: KnowledgeBankResponse) {
    setKnowledgeSources(response.sources)
    setKnowledgeDocuments(response.documents)
    setPageCount(response.documentCount)
    setSourceCount(response.sourceCount)
    setSelectedSourceId((current) => {
      if (current && response.sources.some((source) => source.id === current)) {
        return current
      }

      return response.sources[0]?.id ?? null
    })
  }

  async function loadKnowledgeSources(force = false) {
    if (!force && ((knowledgeSources.length > 0 || knowledgeDocuments.length > 0) || isSourcesLoading)) {
      return
    }

    setSourcesError('')
    setIsSourcesLoading(true)
    try {
      const response = await fetchKnowledgeBank()
      applyKnowledgeBankResponse(response)
    } catch (ex) {
      setSourcesError(ex instanceof Error ? ex.message : 'Bilgi Bankası kaynakları alınamadı.')
    } finally {
      setIsSourcesLoading(false)
      void loadRuntimeStatus()
    }
  }

  async function openKnowledgeBank() {
    setIsSourcesOpen(true)
    setRuntimeStatus({
      ...runtimeStatus,
      operation: 'sources',
      phase: 'sources_loading',
      stepKey: 'sources_loading',
      stepIndex: 1,
      stepCount: 1,
      isTerminal: false,
      message: 'kaynaklar listeleniyor',
      severity: 'info',
      icon: 'book',
    })

    await loadKnowledgeSources(false)
  }

  function applyKnowledgeProgress(status: RuntimeStatus) {
    if (!isKnowledgeRedownloadingRef.current) {
      return
    }

    if (status.operation !== 'knowledge_redownload') {
      return
    }

    if (status.isTerminal && status.severity === 'error') {
      setKnowledgeProgressSteps(buildProgressSteps(knowledgeRedownloadStepLabels, status.stepIndex, true))
      return
    }

    if (status.isTerminal) {
      setKnowledgeProgressSteps(buildCompletedSteps(knowledgeRedownloadStepLabels))
      return
    }

    setKnowledgeProgressSteps((current) => {
      // Prevents delayed polling/status events from regressing a completed refresh ProgressView.
      if (current.length > 0 && current.every((step) => step.status === 'done')) {
        return current
      }

      // Applies the latest backend-provided structured progress step.
      return buildProgressSteps(knowledgeRedownloadStepLabels, status.stepIndex)
    })
  }

  function dismissKnowledgeProgress() {
    if (knowledgeProgressDismissTimerRef.current !== undefined) {
      window.clearTimeout(knowledgeProgressDismissTimerRef.current)
      knowledgeProgressDismissTimerRef.current = undefined
    }

    setKnowledgeProgressSteps([])
    setKnowledgeRedownloadMessage('')
    setIsKnowledgeProgressDismissible(false)
    setA11yAnnouncement('')
  }

  function scheduleKnowledgeProgressDismiss() {
    setIsKnowledgeProgressDismissible(true)
    if (knowledgeProgressDismissTimerRef.current !== undefined) {
      window.clearTimeout(knowledgeProgressDismissTimerRef.current)
    }

    knowledgeProgressDismissTimerRef.current = window.setTimeout(() => {
      dismissKnowledgeProgress()
    }, 10000)
  }

  function startKnowledgeProgressPolling() {
    if (knowledgeProgressPollRef.current !== undefined) {
      return
    }

    knowledgeProgressPollRef.current = window.setInterval(async () => {
      try {
        const status = await fetchRuntimeStatus()
        setRuntimeStatus((current) => normalizeLiveRuntimeStatus(status, current))
        applyKnowledgeProgress(status)
      } catch {
        // The primary refresh request still owns the final error state.
      }
    }, 800)
  }

  function stopKnowledgeProgressPolling() {
    if (knowledgeProgressPollRef.current === undefined) {
      return
    }

    window.clearInterval(knowledgeProgressPollRef.current)
    knowledgeProgressPollRef.current = undefined
  }

  // Shows a blocking message dialog without duplicating its copy in the global live region.
  function showFloatingMessage(message: string) {
    setFloatingMessage(message)
    setA11yAnnouncement('')
  }

  // Closes the blocking message dialog and removes stale live-region text tied to that message.
  function closeFloatingMessage() {
    setFloatingMessage('')
    setA11yAnnouncement('')
  }

  async function runKnowledgeBankMutation(action: () => Promise<KnowledgeBankResponse>, successMessage: string): Promise<boolean> {
    setIsKnowledgeRedownloading(true)
    isKnowledgeRedownloadingRef.current = true
    setKnowledgeRedownloadError('')
    setKnowledgeRedownloadMessage('')
    setIsKnowledgeProgressDismissible(false)
    setKnowledgeProgressSteps(buildProgressSteps(knowledgeRedownloadStepLabels, 1))
    setA11yAnnouncement('Bilgi Bankası güncelleniyor')
    startKnowledgeProgressPolling()
    window.setTimeout(() => knowledgeProgressRef.current?.focus(), 0)

    try {
      const response = await action()
      applyKnowledgeBankResponse(response)
      setKnowledgeProgressSteps(buildCompletedSteps(knowledgeRedownloadStepLabels))
      setKnowledgeRedownloadMessage(successMessage)
      setA11yAnnouncement(successMessage)
      scheduleKnowledgeProgressDismiss()
      await loadKnowledgeStatus()
      await loadReadyQuestions()
      return true
    } catch (ex) {
      setKnowledgeRedownloadError(ex instanceof Error ? ex.message : 'Bilgi Bankası işlemi tamamlanamadı.')
      setKnowledgeProgressSteps((current) => current.length > 0
        ? current
        : buildProgressSteps(knowledgeRedownloadStepLabels, knowledgeRedownloadStepLabels.length, true))
      setA11yAnnouncement('Bilgi Bankası işlemi tamamlanamadı')
      scheduleKnowledgeProgressDismiss()
      return false
    } finally {
      isKnowledgeRedownloadingRef.current = false
      setIsKnowledgeRedownloading(false)
      stopKnowledgeProgressPolling()
      void loadRuntimeStatus()
    }
  }

  async function runLightKnowledgeMutation(action: () => Promise<KnowledgeBankResponse>, successMessage: string, restoreFocus?: () => void): Promise<boolean> {
    closeFloatingMessage()
    setIsKnowledgeActivating(true)
    let shouldRestoreFocus = false
    try {
      const response = await action()
      applyKnowledgeBankResponse(response)
      setA11yAnnouncement(successMessage)
      await loadKnowledgeStatus()
      await loadReadyQuestions()
      shouldRestoreFocus = true
      return true
    } catch (ex) {
      const message = ex instanceof Error ? ex.message : 'Bilgi Bankası işlemi tamamlanamadı.'
      showFloatingMessage(message)
      await loadKnowledgeSources(true)
      return false
    } finally {
      setIsKnowledgeActivating(false)
      void loadRuntimeStatus()
      if (shouldRestoreFocus && restoreFocus) {
        window.requestAnimationFrame(() => {
          restoreFocus()
        })
      }
    }
  }

  async function runFastKnowledgeToggle(action: () => Promise<KnowledgeBankResponse>, successMessage: string, restoreFocus?: () => void): Promise<boolean> {
    closeFloatingMessage()
    setIsKnowledgeActivating(true)
    let shouldRestoreFocus = false
    try {
      const response = await action()
      applyKnowledgeBankResponse(response)
      setA11yAnnouncement(successMessage)
      await loadKnowledgeStatus()
      await loadReadyQuestions()
      shouldRestoreFocus = true
      return true
    } catch (ex) {
      const message = ex instanceof Error ? ex.message : 'Bilgi Bankası işlemi tamamlanamadı.'
      showFloatingMessage(message)
      await loadKnowledgeSources(true)
      return false
    } finally {
      setIsKnowledgeActivating(false)
      void loadRuntimeStatus()
      if (shouldRestoreFocus && restoreFocus) {
        window.requestAnimationFrame(() => {
          restoreFocus()
        })
      }
    }
  }

  async function runKnowledgeRedownload(action: () => Promise<KnowledgeRedownloadResponse>, successMessage: string): Promise<boolean> {
    setIsKnowledgeRedownloading(true)
    isKnowledgeRedownloadingRef.current = true
    setKnowledgeRedownloadError('')
    setKnowledgeRedownloadMessage('')
    setIsKnowledgeProgressDismissible(false)
    setKnowledgeProgressSteps(buildProgressSteps(knowledgeRedownloadStepLabels, 1))
    setA11yAnnouncement(`${successMessage} işlemi başlatıldı`)
    startKnowledgeProgressPolling()
    window.setTimeout(() => knowledgeProgressRef.current?.focus(), 0)

    try {
      const response = await action()
      if (response.knowledgeBank) {
        applyKnowledgeBankResponse(response.knowledgeBank)
      } else {
        await loadKnowledgeSources(true)
      }

      setKnowledgeProgressSteps(buildCompletedSteps(knowledgeRedownloadStepLabels))
      setKnowledgeRedownloadMessage(response.message || successMessage)
      setA11yAnnouncement(response.message || successMessage)
      scheduleKnowledgeProgressDismiss()
      await loadKnowledgeStatus()
      await loadReadyQuestions()
      return true
    } catch (ex) {
      setKnowledgeRedownloadError(ex instanceof Error ? ex.message : 'Yeniden indirme işlemi tamamlanamadı.')
      setKnowledgeProgressSteps((current) => current.length > 0
        ? current
        : buildProgressSteps(knowledgeRedownloadStepLabels, knowledgeRedownloadStepLabels.length, true))
      setA11yAnnouncement('Yeniden indirme işlemi tamamlanamadı')
      scheduleKnowledgeProgressDismiss()
      return false
    } finally {
      isKnowledgeRedownloadingRef.current = false
      setIsKnowledgeRedownloading(false)
      stopKnowledgeProgressPolling()
      void loadRuntimeStatus()
    }
  }

  async function onRedownloadKnowledgeSource(source: KnowledgeBankSource) {
    await runKnowledgeRedownload(
      () => redownloadKnowledgeSource(source.id),
      `${source.name} kaynağı yeniden indirildi`,
    )
  }

  async function onRedownloadKnowledgeDocument(document: KnowledgeBankDocument) {
    await runKnowledgeRedownload(
      () => redownloadKnowledgeDocument(document.id),
      `${document.title} belgesi yeniden indirildi`,
    )
  }

  async function onAddKnowledgeSource(event: FormEvent) {
    event.preventDefault()
    const sourceType = newSourceType
    const name = newSourceName.trim()
    const description = newSourceDescription.trim()
    const address = newSourceAddress.trim()
    if (sourceType === 'web_site' && !address) {
      setKnowledgeRedownloadError('Kaynak adresi boş olamaz.')
      return
    }

    const sourceAction = async () => {
      const response = await addKnowledgeSource(sourceType, name, description, sourceType === 'web_site' ? address : '')
      const addedSource = [...response.sources]
        .reverse()
        .find((source) => source.sourceType === sourceType && (!name || source.name === name))
      setSelectedSourceId(addedSource?.id ?? response.sources[response.sources.length - 1]?.id ?? null)
      setNewSourceType('web_site')
      setNewSourceName('')
      setNewSourceDescription('')
      setNewSourceAddress('')
      setIsNewSourceOpen(false)
      return response
    }

    if (sourceType === 'web_site') {
      await runKnowledgeBankMutation(sourceAction, 'Kaynak eklendi ve Bilgi Bankası yeniden indirildi')
      return
    }

    await runLightKnowledgeMutation(sourceAction, 'Kaynak eklendi.')
  }

  function onEditKnowledgeSource(source: KnowledgeBankSource) {
    setEditingSource({
      source,
      name: source.name,
      description: source.description,
      address: source.address,
    })
  }

  async function saveEditingSource(event: FormEvent) {
    event.preventDefault()
    if (!editingSource) {
      return
    }

    const nextName = editingSource.name.trim()
    const nextDescription = editingSource.description.trim()
    const nextAddress = editingSource.source.sourceType === 'local_files' || editingSource.source.sourceType === 'web_links'
      ? editingSource.source.address
      : editingSource.address.trim()

    if (editingSource.source.sourceType === 'web_site' && !nextAddress) {
      setKnowledgeRedownloadError('Kaynak adresi boş olamaz.')
      return
    }

    const currentAddress = editingSource.source.address.trim().replace(/\/+$/, '')
    const proposedAddress = nextAddress.trim().replace(/\/+$/, '')
    const shouldRedownloadWebSite = editingSource.source.sourceType === 'web_site' && currentAddress !== proposedAddress
    const sourceAction = () => updateKnowledgeSource(
      editingSource.source.id,
      editingSource.source.sourceType,
      nextName,
      nextDescription,
      nextAddress,
      editingSource.source.isEnabled,
    )
    const saved = shouldRedownloadWebSite
      ? await runKnowledgeBankMutation(sourceAction, 'Kaynak güncellendi ve Bilgi Bankası yeniden indirildi')
      : await runLightKnowledgeMutation(sourceAction, 'Kaynak güncellendi.')

    if (saved) {
      setEditingSource(null)
    }
  }

  function onDeleteKnowledgeSource(source: KnowledgeBankSource) {
    setConfirmDialog({
      title: 'Kaynak silinsin mi?',
      message: `${source.name} kaynağı ve bağlı belgeleri silinecek. Bu işlemden sonra aktif bilgi önbelleği hemen güncellenecek.`,
      confirmLabel: 'Kaynağı sil',
      cancelLabel: 'Vazgeç',
      onConfirm: () => {
        setConfirmDialog(null)
        void runLightKnowledgeMutation(() => deleteKnowledgeSource(source.id), 'Kaynak silindi.')
      },
    })
  }

  function onDeleteKnowledgeDocument(document: KnowledgeBankDocument) {
    setConfirmDialog({
      title: 'Belge silinsin mi?',
      message: `${document.title} belgesi silinecek. Bu belge artık soru-cevap bilgi önbelleğinde kullanılmayacak.`,
      confirmLabel: 'Belgeyi sil',
      cancelLabel: 'Vazgeç',
      onConfirm: () => {
        setConfirmDialog(null)
        void runLightKnowledgeMutation(() => deleteKnowledgeDocument(document.id), 'Belge silindi.')
      },
    })
  }

  function buildLocalUploadCards(files: File[], previews: KnowledgeFilePreviewItem[]): LocalUploadCard[] {
    return previews.map((preview, index) => ({
      id: preview.clientFileId || `${Date.now()}-${index}`,
      file: files[index],
      relativePath: preview.relativePath || getKnowledgeFileRelativePath(files[index]),
      title: preview.defaultTitle || files[index].name,
      preview: preview.contentPreview || preview.content || 'Bu dosya için önizleme oluşturulamadı.',
      parseStatus: preview.parseStatus,
      ocrStatus: preview.ocrStatus,
      errorMessage: preview.errorMessage,
    }))
  }

  async function appendLocalFiles(files: File[]) {
    if (files.length === 0) {
      return
    }

    setLocalFileError('')
    setIsPreviewingFiles(true)
    try {
      const relativePaths = files.map(getKnowledgeFileRelativePath)
      const response = await previewKnowledgeFiles(files, relativePaths)
      setLocalUploadCards((current) => [...current, ...buildLocalUploadCards(files, response.items)])
    } catch (ex) {
      setLocalFileError(ex instanceof Error ? ex.message : 'Dosyalar önizlenemedi.')
    } finally {
      setIsPreviewingFiles(false)
    }
  }

  async function replaceLocalUploadCard(cardId: string, files: FileList | null) {
    const file = files?.[0]
    if (!file) {
      return
    }

    setLocalFileError('')
    setIsPreviewingFiles(true)
    try {
      const response = await previewKnowledgeFiles([file], [getKnowledgeFileRelativePath(file)])
      const nextCard = buildLocalUploadCards([file], response.items)[0]
      if (!nextCard) {
        setLocalFileError('Dosya önizlemesi alınamadı.')
        return
      }

      setLocalUploadCards((current) => current.map((card) => (card.id === cardId ? nextCard : card)))
    } catch (ex) {
      setLocalFileError(ex instanceof Error ? ex.message : 'Dosya değiştirilemedi.')
    } finally {
      setIsPreviewingFiles(false)
    }
  }

  function openNewLocalDocumentDialog() {
    setIsNewDocumentOpen(true)
    setLocalFileError('')
    setLocalUploadCards([])
    window.setTimeout(() => localFilesInputRef.current?.focus(), 0)
  }

  function closeNewLocalDocumentDialog() {
    if (isPreviewingFiles || isImportingFiles) {
      return
    }

    setIsNewDocumentOpen(false)
    setLocalFileError('')
    setLocalUploadCards([])
  }

  async function importLocalUploadCards() {
    if (!selectedKnowledgeSource || selectedKnowledgeSource.sourceType !== 'local_files') {
      setLocalFileError('Önce Yerel Dosyalar türünde bir kaynak seçin.')
      return
    }

    const importableCards = localUploadCards.filter((card) => !card.errorMessage)
    if (importableCards.length === 0) {
      setLocalFileError('İçe aktarılabilecek dosya bulunamadı.')
      return
    }

    setIsImportingFiles(true)
    const saved = await runLightKnowledgeMutation(async () => {
      const response = await importKnowledgeFiles(
        selectedKnowledgeSource.id,
        importableCards.map((card) => card.file),
        importableCards.map((card) => card.title.trim() || card.file.name),
        importableCards.map((card) => card.relativePath),
      )

      if (response.failedItems.length > 0) {
        setLocalFileError(`${response.failedItems.length} dosya içe aktarılamadı; diğer dosyalar kaydedildi.`)
      }

      return response.knowledgeBank
    }, 'Dosyalar eklendi.')
    setIsImportingFiles(false)

    if (saved) {
      setIsNewDocumentOpen(false)
      setLocalUploadCards([])
      setLocalFileError('')
    }
  }

  async function onDocumentEditorFileChange(files: FileList | null) {
    const file = files?.[0]
    if (!file) {
      return
    }

    setDocumentEditorError('')
    setIsDocumentEditorSaving(true)
    try {
      const response = await previewKnowledgeFiles([file], [getKnowledgeFileRelativePath(file)])
      const item = response.items[0]
      if (!item || item.errorMessage) {
        setDocumentEditorError(item?.errorMessage || 'Dosya okunamadı.')
        return
      }

      setDocumentDraftContent(item.content || item.contentPreview)
    } catch (ex) {
      setDocumentEditorError(ex instanceof Error ? ex.message : 'Dosya yüklenemedi.')
    } finally {
      setIsDocumentEditorSaving(false)
      if (documentUploadInputRef.current) {
        documentUploadInputRef.current.value = ''
      }
    }
  }

  async function openKnowledgeDocumentEditor(document: KnowledgeBankDocument) {
    setEditingDocument(document)
    setDocumentDraftContent(document.content || document.contentPreview || '')
    setDocumentEditorError('')
    try {
      const fullContent = await fetchKnowledgeDocumentContent(document.id)
      setDocumentDraftContent(fullContent.content)
    } catch (ex) {
      setDocumentEditorError(ex instanceof Error ? ex.message : 'Belge içeriği alınamadı.')
    }
  }

  function closeKnowledgeDocumentEditor() {
    if (isDocumentEditorSaving) {
      return
    }

    setEditingDocument(null)
    setDocumentDraftContent('')
    setDocumentEditorError('')
  }

  async function saveKnowledgeDocument() {
    if (!editingDocument) {
      return
    }

    const content = documentDraftContent.trim()
    if (!content) {
      setDocumentEditorError('Belge içeriği boş olamaz.')
      return
    }

    setIsDocumentEditorSaving(true)
    setDocumentEditorError('')
    const saved = await runLightKnowledgeMutation(
      () => updateKnowledgeDocument(editingDocument.id, editingDocument.title, content, editingDocument.isEnabled),
      'Belge kaydedildi.',
    )
    setIsDocumentEditorSaving(false)

    if (saved) {
      setEditingDocument(null)
      setDocumentDraftContent('')
    }
  }

  function openNewWebDocumentDialog(source: KnowledgeBankSource) {
    setWebDocumentLinkEditor({
      mode: 'create',
      sourceId: source.id,
      document: null,
      url: '',
      title: '',
    })
    setWebDocumentLinkError('')
  }

  function openWebDocumentLinkEditor(document: KnowledgeBankDocument) {
    if (!document.sourceId) {
      showFloatingMessage('Belgenin bağlı olduğu kaynak bulunamadı.')
      return
    }

    setWebDocumentLinkEditor({
      mode: 'edit',
      sourceId: document.sourceId,
      document,
      url: document.url,
      title: document.title,
    })
    setWebDocumentLinkError('')
  }

  async function saveWebDocumentLink(event: FormEvent) {
    event.preventDefault()
    if (!webDocumentLinkEditor) {
      return
    }

    const url = webDocumentLinkEditor.url.trim()
    if (!url) {
      setWebDocumentLinkError('Web bağlantısı boş olamaz.')
      return
    }

    setIsWebDocumentLinkSaving(true)
    setWebDocumentLinkError('')
    const saved = await runLightKnowledgeMutation(
      () => webDocumentLinkEditor.mode === 'create'
        ? addManualWebDocument(webDocumentLinkEditor.sourceId, url, webDocumentLinkEditor.title.trim(), true)
        : updateKnowledgeDocumentWebLink(webDocumentLinkEditor.document!.id, url),
      webDocumentLinkEditor.mode === 'create'
        ? 'Web belgesi eklendi.'
        : 'Belge bağlantısı güncellendi.',
    )
    setIsWebDocumentLinkSaving(false)

    if (saved) {
      setWebDocumentLinkEditor(null)
      setWebDocumentLinkError('')
    }
  }

  async function openSettingsPage() {
    setIsSettingsOpen(true)
    setSettingsError('')
    setIsSettingsLoading(true)
    try {
      const [settings, uploadSettings, qnaSettings] = await Promise.all([
        fetchAiSettings(),
        fetchKnowledgeSettings(),
        fetchQnaExperienceSettings(),
      ])
      setAiSettings(settings)
      setKnowledgeUploadSettings(uploadSettings)
      setQnaExperienceSettings(qnaSettings)
      setDraftProvider(settings.activeProvider)
      setDraftModel(settings.activeModel)
      setDraftMaxFileSizeMb(uploadSettings.maxFileSizeMb)
      setDraftMaxBatchFileCount(uploadSettings.maxBatchFileCount)
      setDraftMaxBatchSizeMb(uploadSettings.maxBatchSizeMb)
      setDraftDisplayedReadyQuestionCount(qnaSettings.displayedReadyQuestionCount)
      setDraftDisplayedSuggestedQuestionCount(qnaSettings.displayedSuggestedQuestionCount)
      setDraftAutoSubmitPromptButtons(qnaSettings.autoSubmitPromptButtons)
      setDraftShowAnswerSourceDocumentNames(qnaSettings.showAnswerSourceDocumentNames)
    } catch (ex) {
      setSettingsError(ex instanceof Error ? ex.message : 'Ayarlar alınamadı.')
    } finally {
      setIsSettingsLoading(false)
    }
  }

  function onDraftProviderChange(provider: string) {
    setDraftProvider(provider)
    const providerOption = aiSettings?.providers.find((item) => item.id === provider)
    setDraftModel(providerOption?.selectedModel || providerOption?.models[0]?.id || '')
  }

  async function closeSettingsPage() {
    if (isSavingSettings) {
      return
    }

    const aiChanged = Boolean(aiSettings && (aiSettings.activeProvider !== draftProvider || aiSettings.activeModel !== draftModel))
    const uploadChanged = Boolean(knowledgeUploadSettings && (
      knowledgeUploadSettings.maxFileSizeMb !== draftMaxFileSizeMb ||
      knowledgeUploadSettings.maxBatchFileCount !== draftMaxBatchFileCount ||
      knowledgeUploadSettings.maxBatchSizeMb !== draftMaxBatchSizeMb
    ))
    const qnaChanged = qnaExperienceSettings.displayedReadyQuestionCount !== draftDisplayedReadyQuestionCount ||
      qnaExperienceSettings.displayedSuggestedQuestionCount !== draftDisplayedSuggestedQuestionCount ||
      qnaExperienceSettings.autoSubmitPromptButtons !== draftAutoSubmitPromptButtons ||
      qnaExperienceSettings.showAnswerSourceDocumentNames !== draftShowAnswerSourceDocumentNames

    if ((!aiSettings || !aiChanged) && (!knowledgeUploadSettings || !uploadChanged) && !qnaChanged) {
      setIsSettingsOpen(false)
      return
    }

    const saveLabels = [
      ...(aiSettings && aiSettings.activeProvider !== draftProvider ? ['AI tedarikçisi güncelleniyor'] : []),
      ...(aiSettings && aiSettings.activeModel !== draftModel ? ['AI modeli güncelleniyor'] : []),
      ...(qnaChanged ? ['Soru-cevap deneyimi ayarları güncelleniyor'] : []),
      ...(uploadChanged ? ['Dosya yükleme limitleri güncelleniyor'] : []),
      'Ayarlar kalıcı olarak kaydediliyor',
      'Ayarlar uygulandı',
    ]
    setSettingsError('')
    setIsSavingSettings(true)
    setSettingsSaveSteps(buildProgressSteps(saveLabels, 0))
    setA11yAnnouncement('Değişiklikler Kaydediliyor')
    window.setTimeout(() => settingsProgressRef.current?.focus(), 0)

    try {
      setSettingsSaveSteps(buildProgressSteps(saveLabels, Math.max(1, saveLabels.length - 2)))
      let nextAiSettings = aiSettings
      if (aiChanged) {
        const next = await updateAiSettings(draftProvider, draftModel)
        setAiSettings(next)
        nextAiSettings = next
      }
      if (uploadChanged) {
        const nextUploadSettings = await updateKnowledgeSettings(draftMaxFileSizeMb, draftMaxBatchFileCount, draftMaxBatchSizeMb)
        setKnowledgeUploadSettings(nextUploadSettings)
        setDraftMaxFileSizeMb(nextUploadSettings.maxFileSizeMb)
        setDraftMaxBatchFileCount(nextUploadSettings.maxBatchFileCount)
        setDraftMaxBatchSizeMb(nextUploadSettings.maxBatchSizeMb)
      }
      if (qnaChanged) {
        const nextQnaSettings = await updateQnaExperienceSettings(
          draftDisplayedReadyQuestionCount,
          draftDisplayedSuggestedQuestionCount,
          draftAutoSubmitPromptButtons,
          draftShowAnswerSourceDocumentNames,
        )
        setQnaExperienceSettings(nextQnaSettings)
        setDraftDisplayedReadyQuestionCount(nextQnaSettings.displayedReadyQuestionCount)
        setDraftDisplayedSuggestedQuestionCount(nextQnaSettings.displayedSuggestedQuestionCount)
        setDraftAutoSubmitPromptButtons(nextQnaSettings.autoSubmitPromptButtons)
        setDraftShowAnswerSourceDocumentNames(nextQnaSettings.showAnswerSourceDocumentNames)
        setLatestSuggestedQuestions((current) => current.slice(0, nextQnaSettings.displayedSuggestedQuestionCount))
        await loadReadyQuestions(nextQnaSettings.displayedReadyQuestionCount)
      }
      if (nextAiSettings) {
        setDraftProvider(nextAiSettings.activeProvider)
        setDraftModel(nextAiSettings.activeModel)
      }
      setSettingsSaveSteps(buildCompletedSteps(saveLabels))
      setA11yAnnouncement('Değişiklikler Kaydedildi')
      await loadRuntimeStatus()
      await new Promise((resolve) => window.setTimeout(resolve, 450))
      setSettingsSaveSteps([])
      setIsSettingsOpen(false)
    } catch (ex) {
      setSettingsError(ex instanceof Error ? ex.message : 'Ayarlar kaydedilemedi.')
      setA11yAnnouncement('Ayarlar kaydedilemedi')
      setSettingsSaveSteps([
        ...saveLabels.slice(0, -1).map((label) => ({ label, status: 'done' as const })),
        { label: 'Ayarlar uygulanamadı', status: 'error' },
      ])
    } finally {
      setIsSavingSettings(false)
    }
  }

  closeNewLocalDocumentDialogRef.current = closeNewLocalDocumentDialog
  closeSettingsPageRef.current = closeSettingsPage

  const canAskQuestion = useMemo(
    () => !isStreaming && visibleRuntimeStatus.phase !== 'app_loading',
    [isStreaming, visibleRuntimeStatus.phase],
  )
  const canSend = useMemo(() => canAskQuestion && input.trim().length > 1, [canAskQuestion, input])

  async function submitQuestion(questionOverride?: string) {
    const trimmed = (questionOverride ?? input).trim()
    if (!trimmed) {
      return
    }

    if (isStreaming) {
      setInput(trimmed)
      return
    }

    if (!canAskQuestion) {
      const message = `${tenantConfig.uiWebAssistantName} hazırlanıyor. Lütfen birkaç saniye sonra tekrar deneyin.`
      showFloatingMessage(message)
      return
    }

    closeFloatingMessage()
    setRuntimeStatus({
      ...runtimeStatus,
      operation: 'chat',
      phase: 'asking',
      stepKey: 'asking',
      stepIndex: 1,
      stepCount: 3,
      isTerminal: false,
      message: 'soru soruluyor',
      severity: 'info',
      icon: 'send',
    })

    const timestamp = Date.now()
    const assistantId = `a-${timestamp + 1}`
    setLatestSuggestionOwnerId(null)
    setLatestSuggestedQuestions([])
    setMessages((prev) => [
      ...prev,
      { id: `u-${timestamp}`, role: 'user', content: trimmed },
      { id: assistantId, role: 'assistant', content: '', sourceAttributions: [] },
    ])
    setInput('')
    setIsStreaming(true)

    try {
      for await (const event of streamChat(trimmed)) {
        if (event.type === 'phase') {
          if (event.phase === 'answering') {
            setRuntimeStatus({
              ...runtimeStatus,
              operation: 'chat',
              phase: 'answering',
              stepKey: 'answering',
              stepIndex: 2,
              stepCount: 3,
              isTerminal: false,
              message: 'cevap veriliyor',
              severity: 'info',
              icon: 'sparkles',
            })
          }
          continue
        }

        if (event.type === 'answer') {
          setRuntimeStatus({
            ...runtimeStatus,
            operation: 'chat',
            phase: 'answering',
            stepKey: 'answering',
            stepIndex: 2,
            stepCount: 3,
            isTerminal: false,
            message: 'cevap veriliyor',
            severity: 'info',
            icon: 'sparkles',
          })
          setMessages((prev) =>
            prev.map((item) =>
              item.id === assistantId ? { ...item, content: event.answer_content, sourceAttributions: event.source_attributions } : item,
            ),
          )
          if (event.suggested_questions.length > 0) {
            setLatestSuggestionOwnerId(assistantId)
            setLatestSuggestedQuestions(event.suggested_questions.slice(0, displayedSuggestedQuestionCount))
          }
        }
      }
    } catch (ex) {
      const message = normalizeChatErrorMessage(ex instanceof Error ? ex.message : 'Yanıt alınamadı.', tenantConfig.uiWebAssistantName)
      showFloatingMessage(message)
      setLatestSuggestionOwnerId(null)
      setLatestSuggestedQuestions([])
      setRuntimeStatus({
        ...runtimeStatus,
        operation: 'chat',
        phase: 'ready_for_question',
        stepKey: 'ready',
        stepIndex: 3,
        stepCount: 3,
        isTerminal: true,
        message: 'Uygulama Hazır',
        severity: 'ready',
        icon: 'message',
      })
      setMessages((prev) => prev.filter((item) => item.id !== assistantId))
    } finally {
      setIsStreaming(false)
      void loadKnowledgeStatus()
      void loadRuntimeStatus()
      void loadReadyQuestions()
    }
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    await submitQuestion()
  }

  function onComposerKeyDown(event: ReactKeyboardEvent<HTMLTextAreaElement>) {
    if (
      event.key !== 'Enter' ||
      event.shiftKey ||
      event.ctrlKey ||
      event.metaKey ||
      event.altKey ||
      event.nativeEvent.isComposing
    ) {
      return
    }

    event.preventDefault()
    void submitQuestion()
  }

  function activatePromptQuestion(question: string) {
    if (autoSubmitPromptButtons) {
      void submitQuestion(question)
      return
    }

    setInput(question)
    window.setTimeout(() => composerRef.current?.focus(), 0)
  }

  function submitSuggestedQuestion(question: string) {
    activatePromptQuestion(question)
  }

  async function openDocumentViewer(attribution: SourceAttribution) {
    setDocumentViewer({
      documentId: attribution.documentId,
      content: null,
      isLoading: true,
      error: '',
    })

    try {
      const content = await fetchKnowledgeDocumentContent(attribution.documentId)
      setDocumentViewer({
        documentId: attribution.documentId,
        content,
        isLoading: false,
        error: '',
      })
    } catch (ex) {
      setDocumentViewer({
        documentId: attribution.documentId,
        content: null,
        isLoading: false,
        error: ex instanceof Error ? ex.message : 'Belge içeriği alınamadı.',
      })
    }
  }

  function closeDocumentViewer() {
    setDocumentViewer(null)
  }

  async function startNewChat() {
    if (isStreaming) {
      return
    }

    setMessages([])
    setInput('')
    closeFloatingMessage()
    setLatestSuggestionOwnerId(null)
    setLatestSuggestedQuestions([])
    setIsUserMenuOpen(false)
    setIsUtilityMenuOpen(false)
    setRuntimeStatus({
      ...runtimeStatus,
      operation: 'app',
      phase: 'ready_for_question',
      stepKey: 'ready',
      stepIndex: 1,
      stepCount: 1,
      isTerminal: true,
      message: 'Uygulama Hazır',
      severity: 'ready',
      icon: 'message',
    })
    listRef.current?.scrollTo({ top: 0 })
    await loadReadyQuestions()
  }

  function openExternal(url: string) {
    setIsUtilityMenuOpen(false)
    window.open(url, '_blank', 'noopener,noreferrer')
  }

  function openHelpPage() {
    setIsUtilityMenuOpen(false)
    setIsHelpOpen(true)
    if (window.location.pathname !== '/help') {
      window.history.pushState({ oyako: 'help' }, '', '/help')
    }
  }

  function closeHelpPage() {
    setIsHelpOpen(false)
    if (window.location.pathname === '/help') {
      window.history.pushState({}, '', '/')
    }
  }

  const selectedDraftProvider = aiSettings?.providers.find((provider) => provider.id === draftProvider)
  // Returns true when a knowledge item status requires user attention.
  function isWarningStatus(statusCode: string | null | undefined) {
    return (statusCode ?? '').trim().toLowerCase() !== 'ok'
  }

  // Gets the documents under a source that currently require user attention.
  function getWarningDocumentsForSource(sourceId: number) {
    return knowledgeDocuments.filter((document) => document.sourceId === sourceId && isWarningStatus(document.statusCode))
  }

  // Returns true when a source or one of its documents should expose the warning log.
  function hasSourceWarning(source: KnowledgeBankSource) {
    return isWarningStatus(source.statusCode) || getWarningDocumentsForSource(source.id).length > 0
  }

  // Opens the source diagnostics viewer and loads the backend warning details.
  async function openSourceLogViewer(source: KnowledgeBankSource, returnFocusTo: HTMLElement) {
    setLogViewer({ kind: 'source', itemId: source.id, source: null, document: null, isLoading: true, error: '', returnFocusTo })
    try {
      const diagnostics = await fetchKnowledgeSourceDiagnostics(source.id)
      setLogViewer((current) => current?.kind === 'source' && current.itemId === source.id
        ? { ...current, source: diagnostics, isLoading: false, error: '' }
        : current)
    } catch (error) {
      setLogViewer((current) => current?.kind === 'source' && current.itemId === source.id
        ? { ...current, isLoading: false, error: error instanceof Error ? error.message : 'Uyarı günlüğü yüklenemedi.' }
        : current)
    }
  }

  // Opens the document diagnostics viewer and loads the backend warning details.
  async function openDocumentLogViewer(document: KnowledgeBankDocument, returnFocusTo: HTMLElement) {
    setLogViewer({ kind: 'document', itemId: document.id, source: null, document: null, isLoading: true, error: '', returnFocusTo })
    try {
      const diagnostics = await fetchKnowledgeDocumentDiagnostics(document.id)
      setLogViewer((current) => current?.kind === 'document' && current.itemId === document.id
        ? { ...current, document: diagnostics, isLoading: false, error: '' }
        : current)
    } catch (error) {
      setLogViewer((current) => current?.kind === 'document' && current.itemId === document.id
        ? { ...current, isLoading: false, error: error instanceof Error ? error.message : 'Uyarı günlüğü yüklenemedi.' }
        : current)
    }
  }

  // Reloads the currently open diagnostics viewer after a Knowledge Bank mutation.
  async function reloadLogViewer() {
    const current = logViewer
    if (!current) {
      return
    }

    setLogViewer({ ...current, isLoading: true, error: '' })
    try {
      if (current.kind === 'source') {
        const diagnostics = await fetchKnowledgeSourceDiagnostics(current.itemId)
        setLogViewer((viewer) => viewer?.kind === 'source' && viewer.itemId === current.itemId
          ? { ...viewer, source: diagnostics, isLoading: false, error: '' }
          : viewer)
      } else {
        const diagnostics = await fetchKnowledgeDocumentDiagnostics(current.itemId)
        setLogViewer((viewer) => viewer?.kind === 'document' && viewer.itemId === current.itemId
          ? { ...viewer, document: diagnostics, isLoading: false, error: '' }
          : viewer)
      }
    } catch (error) {
      setLogViewer((viewer) => viewer
        ? { ...viewer, isLoading: false, error: error instanceof Error ? error.message : 'Uyarı günlüğü yenilenemedi.' }
        : viewer)
    }
  }

  // Closes the diagnostics viewer and restores focus to the warning link that opened it.
  function closeLogViewer() {
    const target = logViewer?.returnFocusTo
    setLogViewer(null)
    window.setTimeout(() => {
      if (target?.isConnected) {
        target.focus()
      }
    }, 0)
  }

  // Applies a source archive selection without leaving the keyboard user on a stale element.
  async function onSourceArchiveSelection(source: KnowledgeBankSource, shouldArchive: boolean, restoreFocusTo: HTMLElement) {
    await runFastKnowledgeToggle(
      () => setKnowledgeSourceArchived(source.id, shouldArchive),
      shouldArchive ? 'Kaynak arşivlensin olarak işaretlendi.' : 'Kaynak arşivlenmesin olarak işaretlendi.',
      () => restoreFocusTo.focus(),
    )
  }

  // Applies a document archive selection without leaving the keyboard user on a stale element.
  async function onDocumentArchiveSelection(document: KnowledgeBankDocument, shouldArchive: boolean, restoreFocusTo: HTMLElement) {
    await runFastKnowledgeToggle(
      () => setKnowledgeDocumentArchived(document.id, shouldArchive),
      shouldArchive ? 'Belge arşivlensin olarak işaretlendi.' : 'Belge arşivlenmesin olarak işaretlendi.',
      () => restoreFocusTo.focus(),
    )
  }

  // Runs a source redownload from the diagnostics viewer and refreshes the visible warning details.
  async function onRedownloadSourceFromLog(source: KnowledgeBankSource) {
    await runKnowledgeRedownload(() => redownloadKnowledgeSource(source.id), 'Kaynak yeniden indirildi.')
    await reloadLogViewer()
  }

  // Runs a document redownload from the diagnostics viewer and refreshes the visible warning details.
  async function onRedownloadDocumentFromLog(document: KnowledgeBankDocument) {
    await runKnowledgeRedownload(() => redownloadKnowledgeDocument(document.id), 'Belge yeniden indirildi.')
    await reloadLogViewer()
  }

  const activeProgressDialog = settingsSaveSteps.length > 0
    ? {
      title: 'Değişiklikler kaydediliyor',
      steps: settingsSaveSteps,
      message: '',
      error: settingsError,
      isDismissible: Boolean(settingsError),
      onDismiss: () => {
        setSettingsSaveSteps([])
        setA11yAnnouncement('')
      },
      dialogRef: settingsProgressRef,
    }
    : knowledgeProgressSteps.length > 0
      ? {
        title: isImportingFiles
          ? 'Dosyalar ekleniyor'
          : isKnowledgeRedownloading
            ? 'Bilgi Bankası yeniden indiriliyor'
            : 'Bilgi Bankası güncelleniyor',
        steps: knowledgeProgressSteps,
        message: knowledgeRedownloadMessage,
        error: knowledgeRedownloadError,
        isDismissible: isKnowledgeProgressDismissible || Boolean(knowledgeRedownloadError),
        onDismiss: dismissKnowledgeProgress,
        dialogRef: knowledgeProgressRef,
      }
      : null

  return (
    <div className="page-shell">
      <div ref={appSurfaceRef} className="app-interaction-surface">
      <header className="topbar" role="banner">
        <div className="brand-lockup" aria-label={tenantConfig.uiWebAssistantName}>
          <div className="brand-mark" aria-hidden="true">
            <Bot size={22} />
          </div>
          <div className="brand-block">
            <p className="brand">{tenantConfig.uiWebAssistantName}</p>
            <h1>{tenantConfig.uiWebHeaderTitle}</h1>
          </div>
        </div>
        <div className="topbar-actions" aria-label="Üst menü işlemleri">
          <button
            className="new-chat-button"
            type="button"
            onClick={() => void startNewChat()}
            disabled={isStreaming}
          >
            <span className="new-chat-icon" aria-hidden="true">
              <MessageSquareText size={18} />
              <span>+</span>
            </span>
            <span>Yeni Sohbet</span>
          </button>

          <a
            className="oyak-brand-link"
            href={tenantConfig.tenantWebUrl}
            target="_blank"
            rel="noreferrer"
            role="link"
            aria-label={tenantConfig.uiWebBrandName}
          >
            <img className="oyak-brand-logo" src={tenantConfig.uiWebBrandLogoUrl} alt={tenantConfig.uiWebBrandName} />
            <span className="oyak-brand-text" aria-hidden="true">{tenantConfig.uiWebBrandName}</span>
          </a>

          <div className="user-menu-shell">
            <button
              className="avatar-button"
              type="button"
              aria-label="Kullanıcı: Ziyaretçi"
              aria-haspopup="menu"
              aria-expanded={isUserMenuOpen}
              onClick={() => setIsUserMenuOpen((current) => !current)}
            >
              <span className="avatar-head" aria-hidden="true" />
              <span className="avatar-shoulders" aria-hidden="true" />
            </button>
            {isUserMenuOpen ? (
              <div ref={userMenuRef} className="user-menu-popover" role="menu" aria-label="Kullanıcı işlemleri">
                <button type="button" role="menuitem" onClick={() => setIsUserMenuOpen(false)}>
                  Giriş Yap
                </button>
                <button type="button" role="menuitem" onClick={() => setIsUserMenuOpen(false)}>
                  Kayıt Ol
                </button>
              </div>
            ) : null}
          </div>

          <button className="settings-open-button" type="button" onClick={() => void openSettingsPage()}>
            <Settings size={18} aria-hidden="true" />
            <span>{tenantConfig.uiWebSettingsPageTitle}</span>
          </button>
        </div>
      </header>

      <main className="workspace" id="main-content">
        <section className="question-panel" aria-labelledby="question-title">
          <div className="assistant-intro-card" aria-label={`${tenantConfig.uiWebAssistantName} karşılama mesajı`}>
            <span className="assistant-intro-icon" aria-hidden="true">
              <Bot size={22} />
            </span>
            <p className="assistant-intro-copy">
              {tenantConfig.uiWebAssistantWelcomeMessage}
            </p>
          </div>

          <div className="section-heading">
            <HelpCircle size={20} aria-hidden="true" />
            <h2 id="question-title">{tenantConfig.uiWebAssistantHeaderTitle}</h2>
          </div>

          <form onSubmit={onSubmit} className="composer" aria-label="Soru gönder">
            <label className="sr-only" htmlFor="question">
              Soru
            </label>
            <textarea
              ref={composerRef}
              id="question"
              rows={7}
              value={input}
              placeholder="Sorunuzu Girin..."
              onChange={(event) => setInput(event.target.value)}
              onKeyDown={onComposerKeyDown}
              disabled={isStreaming}
            />
            <div className="composer-actions">
              <button type="submit" disabled={!canSend}>
                {isStreaming ? <Loader2 size={18} aria-hidden="true" /> : <Send size={18} aria-hidden="true" />}
                <span>{isStreaming ? 'Yanıt hazırlanıyor' : 'Sor'}</span>
              </button>
            </div>
          </form>
        </section>

        <section className="suggestions-panel" aria-labelledby="suggestions-title">
          <div className="suggestions-heading">
            <div className="section-heading">
              <BookOpen size={20} aria-hidden="true" />
              <h2 id="suggestions-title" ref={readyQuestionsTitleRef} tabIndex={-1}>Hazır Sorular</h2>
            </div>
          </div>
          <div className="quick-prompts">
            {visibleReadyQuestions.map((question) => (
              <button
                key={question}
                type="button"
                aria-label={question}
                onClick={() => activatePromptQuestion(question)}
                disabled={!canAskQuestion}
              >
                <span>{question}</span>
                <Send size={15} aria-hidden="true" />
              </button>
            ))}
          </div>
          <button
            className="ready-refresh"
            type="button"
            aria-label="Hazır Soruları Yenile"
            title="Hazır Soruları Yenile"
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => void refreshReadyQuestionsFromButton()}
            disabled={isReadyQuestionsLoading}
          >
            <RefreshCw size={18} aria-hidden="true" />
          </button>
        </section>

        <section className="answer-panel" aria-labelledby="answer-title">
          <div className="answer-header">
            <div className="section-heading">
              <MessageSquareText size={20} aria-hidden="true" />
              <h2 id="answer-title">Soru-Cevap Panosu</h2>
            </div>
            {isStreaming ? <strong>Canlı yanıt</strong> : null}
          </div>

          <div className="messages" ref={listRef}>
            {messages.length === 0 ? (
              <div className="empty-state">
                <Sparkles size={28} aria-hidden="true" />
                <p className="empty-message">Sorunuzu yazın; yanıtınız burada görünür.</p>
              </div>
            ) : (
              messages.map((message) => (
                <article
                  key={message.id}
                  className={`bubble ${message.role === 'user' ? 'bubble-user' : 'bubble-assistant'}`}
                >
                  <div className="bubble-meta">
                    <h4>{message.role === 'user' ? 'Siz' : tenantConfig.uiWebAssistantName}</h4>
                  </div>
                  {message.role === 'user' ? (
                    <p>{message.content}</p>
                  ) : (
                    <>
                      <div
                        className="assistant-html"
                        dangerouslySetInnerHTML={{
                          __html: message.content || '<p>Yanıt hazırlanıyor...</p>',
                        }}
                      />
                      {message.content && showAnswerSourceDocumentNames ? (
                        <SourceAttributionLine
                          attributions={message.sourceAttributions ?? []}
                          fallbackSourceName={tenantConfig.uiWebBrandName}
                          onOpenDocument={(attribution) => void openDocumentViewer(attribution)}
                        />
                      ) : null}
                      {message.id === latestSuggestionOwnerId && latestSuggestedQuestions.length > 0 ? (
                        <aside className="latest-answer-suggestions" aria-label="Son cevabın önerilen soruları">
                          <h5 className="latest-answer-suggestions-title">
                            <Sparkles size={16} aria-hidden="true" />
                            <span>Şunları da sorabilirsiniz:</span>
                          </h5>
                          <div className="latest-answer-suggestion-list">
                            {latestSuggestedQuestions.map((question) => (
                              <button
                                key={question}
                                type="button"
                                aria-label={question}
                                onClick={() => submitSuggestedQuestion(question)}
                                disabled={!canAskQuestion}
                              >
                                <span>{question}</span>
                                <PencilLine size={14} aria-hidden="true" />
                              </button>
                            ))}
                          </div>
                        </aside>
                      ) : null}
                    </>
                  )}
                </article>
              ))
            )}
          </div>

        </section>
      </main>

      <footer className="status-bar" role="contentinfo">
        <div className={`live-status live-${visibleRuntimeStatus.severity}`} aria-live="polite">
          <StatusIcon phase={visibleRuntimeStatus.phase} />
          <span>{visibleRuntimeStatus.message}</span>
        </div>
        <div className="status-message">
          <AlertTriangle size={16} aria-hidden="true" />
          <p>Lütfen cevapların doğruluğunu kontrol edin</p>
        </div>
        <button className="knowledge-link" type="button" onClick={openKnowledgeBank}>
          <Database size={16} aria-hidden="true" />
          <span>{tenantConfig.uiWebKnowledgeBankHeaderTitle}</span>
        </button>
        <div className="utility-menu-wrap">
          <button
            className="utility-menu-button"
            type="button"
            aria-haspopup="menu"
            aria-expanded={isUtilityMenuOpen}
            aria-label="Daha Fazla..."
            onClick={() => setIsUtilityMenuOpen((value) => !value)}
          >
            <HelpCircle size={24} aria-hidden="true" />
            <MoreHorizontal size={24} aria-hidden="true" />
          </button>
          {isUtilityMenuOpen ? (
            <div ref={utilityMenuRef} className="utility-menu" role="menu" aria-label="Daha Fazla...">
              <button type="button" role="menuitem" onClick={() => openExternal(tenantConfig.tenantWebUrl)}>
                <Globe2 size={18} aria-hidden="true" />
                <span>{tenantConfig.uiWebMoreMenuBrandLink}</span>
              </button>
              <button type="button" role="menuitem" onClick={() => openExternal(`mailto:${tenantConfig.tenantFeedbackEmail}`)}>
                <Mail size={18} aria-hidden="true" />
                <span>{tenantConfig.uiWebMoreMenuFeedbackLink}</span>
              </button>
              <button type="button" role="menuitem" onClick={openHelpPage}>
                <HelpCircle size={18} aria-hidden="true" />
                <span>{tenantConfig.uiWebMoreMenuHelpLink}</span>
              </button>
            </div>
          ) : null}
        </div>
      </footer>
      </div>

      {isSourcesOpen ? (
        <div ref={sourcesDialogRef} className="source-dialog" role="dialog" aria-modal="true" aria-labelledby="source-dialog-title" tabIndex={-1}>
          <div className="source-dialog-top">
            <button type="button" className="back-button" onClick={() => setIsSourcesOpen(false)}>
              <ArrowLeft size={18} aria-hidden="true" />
              <span>Geri</span>
            </button>
            <div>
              <p className="eyebrow">{tenantConfig.uiWebKnowledgeBankHeaderTitle}</p>
              <h2 id="source-dialog-title">{tenantConfig.uiWebKnowledgeSourceHeaderTitle}</h2>
            </div>
            <div className="source-action-bar" aria-label="Bilgi Bankası işlemleri">
              <button
                type="button"
                className="secondary-action-button"
                onClick={() => setIsNewSourceOpen((value) => !value)}
                disabled={isKnowledgeMutationBusy}
              >
                <Plus size={17} aria-hidden="true" />
                <span>Yeni kaynak</span>
              </button>
            </div>
          </div>

          <div className="source-dialog-summary">
            <p>
              {knowledgeBankSummary}
            </p>
          </div>

          {isSourcesLoading ? (
            <div className="source-loading">
              <Loader2 size={22} aria-hidden="true" />
              <p>Kaynaklar yükleniyor...</p>
            </div>
          ) : null}

          {knowledgeRedownloadMessage ? <p className="success-banner">{knowledgeRedownloadMessage}</p> : null}
          {sourcesError ? <p className="error-banner">{sourcesError}</p> : null}
          {knowledgeRedownloadError ? <p className="error-banner">{knowledgeRedownloadError}</p> : null}

          {!isSourcesLoading && !sourcesError ? (
            <div className="knowledge-bank-grid">
              <section className="knowledge-bank-section" aria-labelledby="knowledge-sources-title">
                <div className="knowledge-bank-section-head">
                  <h3 id="knowledge-sources-title">{tenantConfig.uiWebKnowledgeSourcesTableTitle}</h3>
                </div>
                <div className="knowledge-table-scroll">
                  <table className="knowledge-table">
                    <thead>
                      <tr>
                        <th scope="col">Kaynak etkin</th>
                        <th scope="col">Arşivlensin</th>
                        <th scope="col">Kaynak türü</th>
                        <th scope="col">Belge Ekleme Yöntemi</th>
                        <th scope="col">Kaynak ismi</th>
                        <th scope="col">Açıklama</th>
                        <th scope="col">Kaynak adresi</th>
                        <th scope="col">Durum</th>
                        <th scope="col">Belgeler</th>
                        <th scope="col">Aksiyonlar</th>
                      </tr>
                    </thead>
                    <tbody>
                      {knowledgeSources.map((source) => (
                        <tr
                          key={source.id}
                          className={selectedKnowledgeSource?.id === source.id ? 'source-row-selected' : undefined}
                          onClick={() => setSelectedSourceId(source.id)}
                        >
                          <td>
                            <input
                              type="checkbox"
                              aria-label="Kaynak Etkin"
                              checked={source.isEnabled}
                              disabled={isKnowledgeMutationBusy}
                              onChange={(event) => {
                                const checkbox = event.currentTarget
                                const isEnabled = checkbox.checked
                                void runFastKnowledgeToggle(
                                  () => setKnowledgeSourceEnabled(source.id, isEnabled),
                                  'Kaynak etkinliği güncellendi',
                                  () => {
                                    if (checkbox.isConnected) {
                                      checkbox.focus()
                                    }
                                  },
                                )
                              }}
                            />
                          </td>
                          <td>
                            <select
                              className="archive-select"
                              aria-label="Arşivlensin"
                              value={source.isArchived ? 'yes' : 'no'}
                              disabled={isKnowledgeMutationBusy}
                              onChange={(event) => void onSourceArchiveSelection(source, event.target.value === 'yes', event.currentTarget)}
                            >
                              <option value="yes">Evet</option>
                              <option value="no">Hayır</option>
                            </select>
                          </td>
                          <td>
                            <span className={`source-type-badge source-type-${source.sourceType}`}>
                              {source.sourceType === 'local_files' ? <HardDrive size={14} aria-hidden="true" /> : <Globe2 size={14} aria-hidden="true" />}
                              <span>{formatSourceType(source.sourceType)}</span>
                            </span>
                          </td>
                          <td>
                            <span className="document-addition-method">
                              {source.webPageAdditionModeLabel || (source.sourceType === 'web_site' ? 'Otomatik Eklenir' : 'Kullanıcı Ekler')}
                            </span>
                          </td>
                          <th scope="row">{source.name}</th>
                          <td className="source-description-cell">{source.description || 'Açıklama eklenmedi.'}</td>
                          <td>
                            {isExternalUrl(source.address) ? (
                              <a href={source.address} target="_blank" rel="noreferrer">
                                <span>{source.address}</span>
                                <ExternalLink size={14} aria-hidden="true" />
                              </a>
                            ) : (
                              <span className="muted-address">{source.address || 'Yerel dosya arşivi'}</span>
                            )}
                          </td>
                          <td>
                            {hasSourceWarning(source) ? (
                              <button
                                className="warning-status-link"
                                type="button"
                                onClick={(event) => void openSourceLogViewer(source, event.currentTarget)}
                              >
                                <AlertTriangle aria-hidden="true" size={16} />
                                <span>Uyarı</span>
                              </button>
                            ) : (
                              <span className={`status-badge status-${source.statusCode}`} aria-label={`Durum: ${source.statusLabel}. ${source.statusMessage}`}>
                                {source.statusLabel}
                              </span>
                            )}
                          </td>
                          <td>{source.activeDocumentCount}/{source.documentCount}</td>
                          <td>
                            <div className="table-actions">
                              <button type="button" onClick={() => onEditKnowledgeSource(source)} disabled={isKnowledgeMutationBusy}>
                                <PencilLine size={14} aria-hidden="true" />
                                <span>Kaynağı Düzenle</span>
                              </button>
                              <button type="button" onClick={() => void onRedownloadKnowledgeSource(source)} disabled={isKnowledgeMutationBusy}>
                                <RefreshCw size={14} aria-hidden="true" />
                                <span>Yeniden İndir</span>
                              </button>
                              <button type="button" onClick={() => onDeleteKnowledgeSource(source)} disabled={isKnowledgeMutationBusy}>
                                <Trash2 size={14} aria-hidden="true" />
                                <span>Sil</span>
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>

              <section className="knowledge-bank-section" aria-labelledby="knowledge-documents-title">
                <div className="knowledge-bank-section-head">
                  <div>
                    <h3 id="knowledge-documents-title">{tenantConfig.uiWebKnowledgeDocumentsTableTitle}</h3>
                    <p className="section-subtitle">
                      {selectedKnowledgeSource ? `${selectedKnowledgeSource.name} kaynağına bağlı belgeler` : 'Tüm belgeler'}
                    </p>
                  </div>
                  <div className="document-section-actions">
                    {selectedKnowledgeSource?.sourceType === 'web_links' ? (
                      <button type="button" className="secondary-action-button" onClick={() => openNewWebDocumentDialog(selectedKnowledgeSource)} disabled={isKnowledgeMutationBusy}>
                        <Globe2 size={17} aria-hidden="true" />
                        <span>Yeni Web Belgesi</span>
                      </button>
                    ) : null}
                    {selectedKnowledgeSource?.sourceType === 'local_files' ? (
                      <button type="button" className="secondary-action-button" onClick={openNewLocalDocumentDialog} disabled={isKnowledgeMutationBusy}>
                        <FileUp size={17} aria-hidden="true" />
                        <span>Yeni Belge</span>
                      </button>
                    ) : null}
                  </div>
                </div>
                <div className="knowledge-table-scroll">
                  <table className="knowledge-table">
                    <thead>
                      <tr>
                        <th scope="col">Belge etkin</th>
                        <th scope="col">Arşivlensin</th>
                        <th scope="col">Belge adı</th>
                        <th scope="col">Belge adresi</th>
                        <th scope="col">Kaynak</th>
                        <th scope="col">Durum</th>
                        <th scope="col">Önizleme</th>
                        <th scope="col">Son güncelleme</th>
                        <th scope="col">Aksiyonlar</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleKnowledgeDocuments.map((document) => (
                        <tr key={document.id}>
                          <td>
                            <input
                              type="checkbox"
                              aria-label="Belge Etkin"
                              checked={document.isEnabled}
                              disabled={isKnowledgeMutationBusy}
                              onChange={(event) => {
                                const checkbox = event.currentTarget
                                const isEnabled = checkbox.checked
                                void runFastKnowledgeToggle(
                                  () => setKnowledgeDocumentEnabled(document.id, isEnabled),
                                  'Belge etkinliği güncellendi',
                                  () => {
                                    if (checkbox.isConnected) {
                                      checkbox.focus()
                                    }
                                  },
                                )
                              }}
                            />
                          </td>
                          <td>
                            <select
                              className="archive-select"
                              aria-label="Arşivlensin"
                              value={document.isArchived ? 'yes' : 'no'}
                              disabled={isKnowledgeMutationBusy}
                              onChange={(event) => void onDocumentArchiveSelection(document, event.target.value === 'yes', event.currentTarget)}
                            >
                              <option value="yes">Evet</option>
                              <option value="no">Hayır</option>
                            </select>
                          </td>
                          <th scope="row">{document.title}</th>
                          <td>
                            {isExternalUrl(document.url) ? (
                              <a href={document.url} target="_blank" rel="noreferrer">
                                <span>{document.url}</span>
                                <ExternalLink size={14} aria-hidden="true" />
                              </a>
                            ) : (
                              <span className="muted-address">{document.normalizedRelativePath || document.originalFileName || document.url}</span>
                            )}
                          </td>
                          <td>{document.sourceName}</td>
                          <td>
                            {isWarningStatus(document.statusCode) ? (
                              <button
                                className="warning-status-link"
                                type="button"
                                onClick={(event) => void openDocumentLogViewer(document, event.currentTarget)}
                              >
                                <AlertTriangle aria-hidden="true" size={16} />
                                <span>Uyarı</span>
                              </button>
                            ) : (
                              <span className={`status-badge status-${document.statusCode}`} aria-label={`Durum: ${document.statusLabel}. ${document.statusMessage}`}>
                                {document.statusLabel}
                              </span>
                            )}
                          </td>
                          <td className="preview-cell">{document.contentPreview}</td>
                          <td>{formatDate(document.lastCrawledAtUtc)}</td>
                          <td>
                            <div className="table-actions">
                              <button
                                type="button"
                                onClick={() => document.sourceType === 'local_files'
                                  ? void openKnowledgeDocumentEditor(document)
                                  : openWebDocumentLinkEditor(document)}
                                disabled={isKnowledgeMutationBusy}
                              >
                                <PencilLine size={14} aria-hidden="true" />
                                <span>{document.sourceType === 'local_files' ? 'Belgeyi Düzenle' : 'Bağlantıyı Düzenle'}</span>
                              </button>
                              <button type="button" onClick={() => void onRedownloadKnowledgeDocument(document)} disabled={isKnowledgeMutationBusy}>
                                <RefreshCw size={14} aria-hidden="true" />
                                <span>Yeniden İndir</span>
                              </button>
                              <button type="button" onClick={() => onDeleteKnowledgeDocument(document)} disabled={isKnowledgeMutationBusy}>
                                <Trash2 size={14} aria-hidden="true" />
                                <span>Sil</span>
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            </div>
          ) : null}
        </div>
      ) : null}

      {isNewSourceOpen ? (
        <ModalFrame
          dialogRef={newSourceDialogRef}
          className="document-editor-dialog source-form-dialog"
          labelledBy="new-source-dialog-title"
          describedBy="new-source-dialog-description"
        >
          <div className="document-editor-head">
            <div>
              <p className="eyebrow">Kaynak Ekle</p>
              <h2 id="new-source-dialog-title">Yeni bilgi kaynağı</h2>
            </div>
            <button type="button" className="back-button" onClick={() => setIsNewSourceOpen(false)} disabled={isKnowledgeMutationBusy}>
              <ArrowLeft size={18} aria-hidden="true" />
              <span>Geri</span>
            </button>
          </div>
          <p id="new-source-dialog-description" className="document-editor-meta">
            Eklenen kaynak kaydedildiği anda Bilgi Bankası ve aktif soru-cevap önbelleği güncellenir.
          </p>
          <form className="modal-form-grid" onSubmit={(event) => void onAddKnowledgeSource(event)}>
            <label htmlFor="new-source-type">
              <span>Kaynak Türü</span>
              <select
                id="new-source-type"
                value={newSourceType}
                onChange={(event) => setNewSourceType(event.target.value as KnowledgeSourceType)}
                disabled={isKnowledgeMutationBusy}
              >
                <option value="web_site">Web Sitesi</option>
                <option value="web_links">Web Bağlantıları</option>
                <option value="local_files">Yerel Dosyalar</option>
              </select>
            </label>
            <label htmlFor="new-source-name">
              <span>Kaynak ismi</span>
              <input
                id="new-source-name"
                value={newSourceName}
                onChange={(event) => setNewSourceName(event.target.value)}
                placeholder={newSourceType === 'local_files' ? 'Yerel Dosyalar' : newSourceType === 'web_links' ? 'Web Bağlantıları' : tenantConfig.uiWebBrandName}
                disabled={isKnowledgeMutationBusy}
              />
            </label>
            <label htmlFor="new-source-description">
              <span>Kaynak açıklaması</span>
              <textarea
                id="new-source-description"
                value={newSourceDescription}
                onChange={(event) => setNewSourceDescription(event.target.value)}
                placeholder="Bu kaynağın bilgi bankasında hangi amaçla kullanılacağını yazın."
                disabled={isKnowledgeMutationBusy}
              />
            </label>
            {newSourceType === 'web_site' ? (
              <label htmlFor="new-source-address">
                <span>Web Adresi</span>
                <input
                  id="new-source-address"
                  value={newSourceAddress}
                  onChange={(event) => setNewSourceAddress(event.target.value)}
                  placeholder="https://www.oyakdijital.com.tr"
                  disabled={isKnowledgeMutationBusy}
                />
              </label>
            ) : newSourceType === 'web_links' ? (
              <div className="local-source-note">
                <Globe2 size={18} aria-hidden="true" />
                <p>Web Bağlantıları kaynaklarında web sayfalarını Belgeler tablosundan tek tek eklersiniz; otomatik alt sayfa taraması yapılmaz.</p>
              </div>
            ) : (
              <div className="local-source-note">
                <HardDrive size={18} aria-hidden="true" />
                <p>Yerel dosya kaynağı oluşturulduktan sonra bu kaynağı seçip Belgeler bölümünden dosya veya klasör yükleyebilirsiniz.</p>
              </div>
            )}
            <div className="document-editor-actions">
              <button
                type="submit"
                className="primary-action-button"
                disabled={isKnowledgeMutationBusy || (newSourceType === 'web_site' && newSourceAddress.trim().length === 0)}
              >
                <Plus size={17} aria-hidden="true" />
                <span>Ekle</span>
              </button>
              <button type="button" className="secondary-action-button" onClick={() => setIsNewSourceOpen(false)} disabled={isKnowledgeMutationBusy}>
                İptal
              </button>
            </div>
          </form>
        </ModalFrame>
      ) : null}

      {editingSource ? (
        <ModalFrame
          dialogRef={sourceEditorDialogRef}
          className="document-editor-dialog source-form-dialog"
          labelledBy="source-editor-dialog-title"
          describedBy="source-editor-dialog-description"
        >
          <div className="document-editor-head">
            <div>
              <p className="eyebrow">Kaynak Düzenleme</p>
              <h2 id="source-editor-dialog-title">{editingSource.source.name}</h2>
            </div>
            <button type="button" className="back-button" onClick={() => setEditingSource(null)} disabled={isKnowledgeMutationBusy}>
              <ArrowLeft size={18} aria-hidden="true" />
              <span>Geri</span>
            </button>
          </div>
          <p id="source-editor-dialog-description" className="document-editor-meta">
            Kaynak bilgisi kaydedildiğinde veri tabanı ve aktif bilgi önbelleği güncellenir.
          </p>
          <form className="modal-form-grid" onSubmit={(event) => void saveEditingSource(event)}>
            <label htmlFor="edit-source-name">
              <span>Kaynak ismi</span>
              <input
                id="edit-source-name"
                value={editingSource.name}
                onChange={(event) => setEditingSource((current) => current ? { ...current, name: event.target.value } : current)}
                disabled={isKnowledgeMutationBusy}
              />
            </label>
            <label htmlFor="edit-source-description">
              <span>Kaynak açıklaması</span>
              <textarea
                id="edit-source-description"
                value={editingSource.description}
                onChange={(event) => setEditingSource((current) => current ? { ...current, description: event.target.value } : current)}
                disabled={isKnowledgeMutationBusy}
              />
            </label>
            {editingSource.source.sourceType === 'web_site' ? (
              <label htmlFor="edit-source-address">
                <span>Kaynak adresi</span>
                <input
                  id="edit-source-address"
                  value={editingSource.address}
                  onChange={(event) => setEditingSource((current) => current ? { ...current, address: event.target.value } : current)}
                  disabled={isKnowledgeMutationBusy}
                />
              </label>
            ) : editingSource.source.sourceType === 'web_links' ? (
              <div className="local-source-note">
                <Globe2 size={18} aria-hidden="true" />
                <p>Web Bağlantıları kaynaklarının adresi sistem tarafından yönetilir; web belgelerini Belgeler tablosundan ekleyip düzenleyebilirsiniz.</p>
              </div>
            ) : (
              <div className="local-source-note">
                <HardDrive size={18} aria-hidden="true" />
                <p>Yerel dosya kaynaklarının ham dosya arşivi backend tarafından yönetilir; kaynak adresi düzenlenmez.</p>
              </div>
            )}
            <div className="document-editor-actions">
              <button type="submit" className="primary-action-button" disabled={isKnowledgeMutationBusy}>
                <Save size={17} aria-hidden="true" />
                <span>Kaydet</span>
              </button>
              <button type="button" className="secondary-action-button" onClick={() => setEditingSource(null)} disabled={isKnowledgeMutationBusy}>
                İptal
              </button>
            </div>
          </form>
        </ModalFrame>
      ) : null}

      {webDocumentLinkEditor ? (
        <ModalFrame
          dialogRef={webDocumentLinkDialogRef}
          className="document-editor-dialog source-form-dialog"
          labelledBy="web-document-link-dialog-title"
          describedBy="web-document-link-dialog-description"
        >
          <div className="document-editor-head">
            <div>
              <p className="eyebrow">{webDocumentLinkEditor.mode === 'create' ? 'Web Belgesi Ekle' : 'Bağlantı Düzenleme'}</p>
              <h2 id="web-document-link-dialog-title">
                {webDocumentLinkEditor.mode === 'create' ? 'Yeni Web Belgesi' : 'Bağlantıyı Düzenle'}
              </h2>
            </div>
            <button type="button" className="back-button" onClick={() => setWebDocumentLinkEditor(null)} disabled={isWebDocumentLinkSaving}>
              <ArrowLeft size={18} aria-hidden="true" />
              <span>Geri</span>
            </button>
          </div>
          <p id="web-document-link-dialog-description" className="document-editor-meta">
            Web bağlantısı kaydedilmeden önce backend tarafından ziyaret edilir, render edilir, saf metne dönüştürülür ve Bilgi Bankası önbelleği güncellenir.
          </p>
          <form className="modal-form-grid" onSubmit={(event) => void saveWebDocumentLink(event)}>
            {webDocumentLinkEditor.mode === 'create' ? (
              <label htmlFor="web-document-title">
                <span>Belge başlığı</span>
                <input
                  id="web-document-title"
                  value={webDocumentLinkEditor.title}
                  onChange={(event) => setWebDocumentLinkEditor((current) => current ? { ...current, title: event.target.value } : current)}
                  placeholder="İsteğe bağlı"
                  disabled={isWebDocumentLinkSaving}
                />
              </label>
            ) : null}
            <label htmlFor="web-document-url">
              <span>Web Bağlantısı</span>
              <input
                id="web-document-url"
                value={webDocumentLinkEditor.url}
                onChange={(event) => setWebDocumentLinkEditor((current) => current ? { ...current, url: event.target.value } : current)}
                placeholder="https://www.oyakdijital.com.tr/..."
                disabled={isWebDocumentLinkSaving}
              />
            </label>
            {webDocumentLinkError ? <p className="error-banner">{webDocumentLinkError}</p> : null}
            <div className="document-editor-actions">
              <button type="submit" className="primary-action-button" disabled={isWebDocumentLinkSaving || webDocumentLinkEditor.url.trim().length === 0}>
                {isWebDocumentLinkSaving ? <Loader2 size={17} aria-hidden="true" /> : <Save size={17} aria-hidden="true" />}
                <span>{webDocumentLinkEditor.mode === 'create' ? 'Ekle' : 'Tamam'}</span>
              </button>
              <button type="button" className="secondary-action-button" onClick={() => setWebDocumentLinkEditor(null)} disabled={isWebDocumentLinkSaving}>
                İptal
              </button>
            </div>
          </form>
        </ModalFrame>
      ) : null}

      {isNewDocumentOpen ? (
        <div className="document-editor-overlay">
          <div ref={newDocumentDialogRef} className="local-file-dialog" role="dialog" aria-modal="true" aria-labelledby="local-file-dialog-title" tabIndex={-1}>
            <div className="document-editor-head">
              <div>
                <p className="eyebrow">Yerel Dosya Ekle</p>
                <h2 id="local-file-dialog-title">Yeni Dosya Ekle</h2>
              </div>
              <button type="button" className="back-button" onClick={closeNewLocalDocumentDialog} disabled={isPreviewingFiles || isImportingFiles}>
                İptal
              </button>
            </div>

            <p className="document-editor-meta">
              Desteklenen dosyalar backend tarafında parse edilip normalize edilir; kaydettiğiniz anda Bilgi Bankası ve aktif soru-cevap önbelleği güncellenir.
            </p>

            <div className="file-picker-row">
              <button type="button" className="secondary-action-button" onClick={() => localFilesInputRef.current?.click()} disabled={isPreviewingFiles || isImportingFiles}>
                <FileUp size={17} aria-hidden="true" />
                <span>Yerel Dosya Yükle</span>
              </button>
              <button type="button" className="secondary-action-button" onClick={() => localFolderInputRef.current?.click()} disabled={isPreviewingFiles || isImportingFiles}>
                <FolderOpen size={17} aria-hidden="true" />
                <span>Yerel Klasör Yükle</span>
              </button>
              <input
                ref={localFilesInputRef}
                className="sr-only"
                type="file"
                aria-label="Yerel dosya yükle"
                accept={supportedKnowledgeFileAccept}
                multiple
                onChange={(event) => {
                  void appendLocalFiles(Array.from(event.target.files ?? []))
                  event.currentTarget.value = ''
                }}
              />
              <input
                ref={(node) => {
                  localFolderInputRef.current = node
                  node?.setAttribute('webkitdirectory', '')
                  node?.setAttribute('directory', '')
                }}
                className="sr-only"
                type="file"
                aria-label="Yerel klasör yükle"
                accept={supportedKnowledgeFileAccept}
                multiple
                onChange={(event) => {
                  void appendLocalFiles(Array.from(event.target.files ?? []))
                  event.currentTarget.value = ''
                }}
              />
            </div>

            {isPreviewingFiles ? (
              <div className="source-loading">
                <Loader2 size={20} aria-hidden="true" />
                <p>Dosyalar okunuyor ve önizleme hazırlanıyor...</p>
              </div>
            ) : null}

            {localFileError ? <p className="error-banner">{localFileError}</p> : null}

            <div className="upload-card-grid" aria-label="Eklenecek dosyalar">
              {localUploadCards.map((card, index) => (
                <article key={card.id} className={`upload-card${card.errorMessage ? ' upload-card-error' : ''}`}>
                  <h3>{index + 1}. dosya:</h3>
                  <p><strong>Dosya Adı:</strong> {card.file.name}</p>
                  <label>
                    <span>Dosya Başlığı:</span>
                    <input
                      value={card.title}
                      onChange={(event) => setLocalUploadCards((current) => current.map((item) => (
                        item.id === card.id ? { ...item, title: event.target.value } : item
                      )))}
                      disabled={isImportingFiles}
                    />
                  </label>
                  <div>
                    <span className="upload-preview-label">Dosya Önizlemesi:</span>
                    <div className="upload-preview">{card.errorMessage || card.preview}</div>
                  </div>
                  <div className="upload-card-actions">
                    <label className="secondary-action-button">
                      <Upload size={15} aria-hidden="true" />
                      <span>Başka Yükle</span>
                      <input
                        className="sr-only"
                        type="file"
                        aria-label={`${card.file.name} yerine başka dosya yükle`}
                        accept={supportedKnowledgeFileAccept}
                        onChange={(event) => {
                          void replaceLocalUploadCard(card.id, event.target.files)
                          event.currentTarget.value = ''
                        }}
                      />
                    </label>
                    <button
                      type="button"
                      className="secondary-action-button"
                      onClick={() => setLocalUploadCards((current) => current.filter((item) => item.id !== card.id))}
                      disabled={isImportingFiles}
                    >
                      <Trash2 size={15} aria-hidden="true" />
                      <span>Kaldır</span>
                    </button>
                  </div>
                </article>
              ))}
            </div>

            <div className="document-editor-actions">
              <button type="button" className="primary-action-button" onClick={() => void importLocalUploadCards()} disabled={isPreviewingFiles || isImportingFiles || localUploadCards.length === 0}>
                {isImportingFiles ? <Loader2 size={17} aria-hidden="true" /> : <Save size={17} aria-hidden="true" />}
                <span>{isImportingFiles ? 'Ekleniyor' : 'Dosyaları Ekle'}</span>
              </button>
              <button type="button" className="secondary-action-button" onClick={closeNewLocalDocumentDialog} disabled={isPreviewingFiles || isImportingFiles}>
                İptal
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {editingDocument ? (
        <div className="document-editor-overlay">
          <div ref={documentEditorDialogRef} className="document-editor-dialog" role="dialog" aria-modal="true" aria-labelledby="document-editor-title" aria-describedby="document-editor-description" tabIndex={-1}>
            <div className="document-editor-head">
              <div>
                <p className="eyebrow">Belge Düzenleme</p>
                <h2 id="document-editor-title">{editingDocument.title}</h2>
              </div>
              <button type="button" className="back-button" onClick={closeKnowledgeDocumentEditor} disabled={isDocumentEditorSaving}>
                İptal
              </button>
            </div>
            <p id="document-editor-description" className="document-editor-meta">
              Bu içerik kaydedildiğinde veri tabanı, belge önizlemesi ve system instruction önbelleği güncellenir.
            </p>
            <div className="file-picker-row">
              <button type="button" className="secondary-action-button" onClick={() => documentUploadInputRef.current?.click()} disabled={isDocumentEditorSaving}>
                <FileUp size={17} aria-hidden="true" />
                <span>Dosya yükle</span>
              </button>
              <input
                ref={documentUploadInputRef}
                className="sr-only"
                type="file"
                aria-label="Belge yükle"
                accept={supportedKnowledgeFileAccept}
                onChange={(event) => void onDocumentEditorFileChange(event.target.files)}
              />
            </div>
            <label className="document-editor-field" htmlFor="document-content-editor">
              <span>Belge içeriği</span>
              <textarea
                id="document-content-editor"
                value={documentDraftContent}
                onChange={(event) => setDocumentDraftContent(event.target.value)}
                disabled={isDocumentEditorSaving}
              />
            </label>
            {documentEditorError ? <p className="error-banner">{documentEditorError}</p> : null}
            <div className="document-editor-actions">
              <button type="button" className="primary-action-button" onClick={() => void saveKnowledgeDocument()} disabled={isDocumentEditorSaving || documentDraftContent.trim().length === 0}>
                {isDocumentEditorSaving ? <Loader2 size={17} aria-hidden="true" /> : <Save size={17} aria-hidden="true" />}
                <span>{isDocumentEditorSaving ? 'Kaydediliyor' : 'Kaydet'}</span>
              </button>
              <button type="button" className="secondary-action-button" onClick={closeKnowledgeDocumentEditor} disabled={isDocumentEditorSaving}>
                İptal
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {logViewer ? (() => {
        const sourceDiagnostics = logViewer.source
        const documentDiagnostics = logViewer.document
        const currentSource = logViewer.kind === 'source'
          ? knowledgeSources.find((source) => source.id === logViewer.itemId)
          : documentDiagnostics
            ? knowledgeSources.find((source) => source.id === documentDiagnostics.document.sourceId)
            : undefined
        const currentDocument = logViewer.kind === 'document'
          ? knowledgeDocuments.find((document) => document.id === logViewer.itemId)
          : undefined
        const warningDocuments = logViewer.kind === 'source'
          ? sourceDiagnostics?.warningDocuments ?? getWarningDocumentsForSource(logViewer.itemId)
          : []
        const title = logViewer.kind === 'source' ? 'Kaynak Uyarı Günlüğü' : 'Belge Uyarı Günlüğü'

        return (
          <div ref={logViewerDialogRef} className="log-viewer" role="dialog" aria-modal="true" aria-labelledby="log-viewer-title" tabIndex={-1}>
            <div className="modal-backdrop" aria-hidden="true" />
            <section className="log-viewer-panel" aria-describedby="log-viewer-description">
              <header className="log-viewer-header">
                <button className="ghost-button" type="button" onClick={closeLogViewer}>
                  <ArrowLeft aria-hidden="true" size={16} />
                  Geri
                </button>
                <div>
                  <span className="eyebrow">Bilgi Bankası</span>
                  <h2 id="log-viewer-title">{title}</h2>
                  <p id="log-viewer-description">Hatalı ya da ulaşılamayan bilgi kaydı için durum, neden ve hızlı aksiyonları gösterir.</p>
                </div>
              </header>

              <div className="log-viewer-body">
                {logViewer.isLoading ? (
                  <div className="log-viewer-loading" role="status">
                    <Loader2 aria-hidden="true" className="spin" size={20} />
                    Uyarı bilgileri yükleniyor...
                  </div>
                ) : logViewer.error ? (
                  <div className="log-viewer-error" role="alert">
                    <AlertTriangle aria-hidden="true" size={18} />
                    <span>{logViewer.error}</span>
                  </div>
                ) : logViewer.kind === 'source' && currentSource ? (
                  <>
                    <section className="log-summary-card" aria-label="Kaynak özeti">
                      <div>
                        <span className="eyebrow">Kaynak</span>
                        <h3>{sourceDiagnostics?.source.name ?? currentSource.name}</h3>
                        <p>{sourceDiagnostics?.source.address ?? currentSource.address}</p>
                      </div>
                      <dl className="log-summary-grid">
                        <div>
                          <dt>Durum</dt>
                          <dd>{sourceDiagnostics?.source.statusLabel ?? currentSource.statusLabel}</dd>
                        </div>
                        <div>
                          <dt>Neden</dt>
                          <dd>{sourceDiagnostics?.source.statusMessage ?? currentSource.statusMessage}</dd>
                        </div>
                        <div>
                          <dt>Uyarı veren belge</dt>
                          <dd>{warningDocuments.length}</dd>
                        </div>
                      </dl>
                    </section>

                    <div className="log-action-bar" aria-label="Kaynak aksiyonları">
                      <button type="button" onClick={() => { closeLogViewer(); onEditKnowledgeSource(currentSource) }}>
                        <PencilLine aria-hidden="true" size={16} />
                        Kaynağı Düzenle
                      </button>
                      <button type="button" onClick={() => void onRedownloadSourceFromLog(currentSource)} disabled={isKnowledgeRedownloading}>
                        <RefreshCw aria-hidden="true" size={16} />
                        Kaynağı Yeniden İndir
                      </button>
                      <button type="button" onClick={() => void runFastKnowledgeToggle(() => setKnowledgeSourceEnabled(currentSource.id, false), 'Kaynak pasifleştirildi.', () => void reloadLogViewer())} disabled={!currentSource.isEnabled || isKnowledgeMutationBusy}>
                        Kaynağı Pasifleştir
                      </button>
                      <button type="button" onClick={() => void runFastKnowledgeToggle(() => setKnowledgeSourceArchived(currentSource.id, true), 'Kaynak arşivlendi.', () => void reloadLogViewer())} disabled={currentSource.isArchived || isKnowledgeMutationBusy}>
                        Kaynağı Arşivle
                      </button>
                      <button type="button" onClick={() => { closeLogViewer(); onDeleteKnowledgeSource(currentSource) }}>
                        <Trash2 aria-hidden="true" size={16} />
                        Kaynağı Sil
                      </button>
                    </div>

                    <section aria-labelledby="log-warning-documents-title">
                      <h3 id="log-warning-documents-title">Uyarı veren belgeler</h3>
                      {warningDocuments.length > 0 ? (
                        <div className="log-issue-list">
                          {warningDocuments.map((document) => {
                            const sourceDocument = knowledgeDocuments.find((item) => item.id === document.id)
                            return (
                              <article className="log-issue-card" key={document.id}>
                                <div>
                                  <h4>{document.title}</h4>
                                  {isExternalUrl(document.url) ? (
                                    <a className="log-resource-link" href={document.url} target="_blank" rel="noreferrer">
                                      <span>{document.url}</span>
                                      <ExternalLink size={14} aria-hidden="true" />
                                    </a>
                                  ) : (
                                    <p>{document.url}</p>
                                  )}
                                  <p><strong>Neden:</strong> {document.statusMessage}</p>
                                </div>
                                {sourceDocument ? (
                                  <div className="log-card-actions">
                                    <button
                                      type="button"
                                      onClick={() => {
                                        closeLogViewer()
                                        if (sourceDocument.sourceType === 'local_files') {
                                          void openKnowledgeDocumentEditor(sourceDocument)
                                        } else {
                                          openWebDocumentLinkEditor(sourceDocument)
                                        }
                                      }}
                                    >
                                      <PencilLine aria-hidden="true" size={16} />
                                      {sourceDocument.sourceType === 'web_site' || sourceDocument.sourceType === 'web_links' ? 'Belge Bağlantısını Düzenle' : 'Belgeyi Düzenle'}
                                    </button>
                                    <button type="button" onClick={() => void onRedownloadDocumentFromLog(sourceDocument)} disabled={isKnowledgeRedownloading}>
                                      <RefreshCw aria-hidden="true" size={16} />
                                      Belgeyi Yeniden İndir
                                    </button>
                                    <button type="button" onClick={() => void runFastKnowledgeToggle(() => setKnowledgeDocumentEnabled(sourceDocument.id, false), 'Belge pasifleştirildi.', () => void reloadLogViewer())} disabled={!sourceDocument.isEnabled || isKnowledgeMutationBusy}>
                                      Belgeyi Pasifleştir
                                    </button>
                                    <button type="button" onClick={() => void runFastKnowledgeToggle(() => setKnowledgeDocumentArchived(sourceDocument.id, true), 'Belge arşivlendi.', () => void reloadLogViewer())} disabled={sourceDocument.isArchived || isKnowledgeMutationBusy}>
                                      Belgeyi Arşivle
                                    </button>
                                    <button type="button" onClick={() => { closeLogViewer(); onDeleteKnowledgeDocument(sourceDocument) }}>
                                      <Trash2 aria-hidden="true" size={16} />
                                      Belgeyi Sil
                                    </button>
                                  </div>
                                ) : null}
                              </article>
                            )
                          })}
                        </div>
                      ) : (
                        <p className="empty-state">Bu kaynak için belge uyarısı bulunmuyor.</p>
                      )}
                    </section>
                  </>
                ) : logViewer.kind === 'document' && (currentDocument || documentDiagnostics) ? (() => {
                  const document = currentDocument
                  const diagnostics = documentDiagnostics?.document
                  return (
                    <>
                      <section className="log-summary-card" aria-label="Belge özeti">
                        <div>
                          <span className="eyebrow">Belge</span>
                          <h3>{diagnostics?.title ?? document?.title}</h3>
                          {isExternalUrl(diagnostics?.url ?? document?.url ?? '') ? (
                            <a className="log-resource-link" href={diagnostics?.url ?? document?.url} target="_blank" rel="noreferrer">
                              <span>{diagnostics?.url ?? document?.url}</span>
                              <ExternalLink size={14} aria-hidden="true" />
                            </a>
                          ) : (
                            <p>{diagnostics?.url ?? document?.url}</p>
                          )}
                        </div>
                        <dl className="log-summary-grid">
                          <div>
                            <dt>Kaynak</dt>
                            <dd>{diagnostics?.sourceName ?? document?.sourceName}</dd>
                          </div>
                          <div>
                            <dt>Durum</dt>
                            <dd>{diagnostics?.statusLabel ?? document?.statusLabel}</dd>
                          </div>
                          <div>
                            <dt>Neden</dt>
                            <dd>{diagnostics?.statusMessage ?? document?.statusMessage}</dd>
                          </div>
                        </dl>
                      </section>

                      {document ? (
                        <div className="log-action-bar" aria-label="Belge aksiyonları">
                          <button
                            type="button"
                            onClick={() => {
                              closeLogViewer()
                              if (document.sourceType === 'local_files') {
                                void openKnowledgeDocumentEditor(document)
                              } else {
                                openWebDocumentLinkEditor(document)
                              }
                            }}
                          >
                            <PencilLine aria-hidden="true" size={16} />
                            {document.sourceType === 'web_site' || document.sourceType === 'web_links' ? 'Belge Bağlantısını Düzenle' : 'Belgeyi Düzenle'}
                          </button>
                          <button type="button" onClick={() => void onRedownloadDocumentFromLog(document)} disabled={isKnowledgeRedownloading}>
                            <RefreshCw aria-hidden="true" size={16} />
                            Belgeyi Yeniden İndir
                          </button>
                          <button type="button" onClick={() => void runFastKnowledgeToggle(() => setKnowledgeDocumentEnabled(document.id, false), 'Belge pasifleştirildi.', () => void reloadLogViewer())} disabled={!document.isEnabled || isKnowledgeMutationBusy}>
                            Belgeyi Pasifleştir
                          </button>
                          <button type="button" onClick={() => void runFastKnowledgeToggle(() => setKnowledgeDocumentArchived(document.id, true), 'Belge arşivlendi.', () => void reloadLogViewer())} disabled={document.isArchived || isKnowledgeMutationBusy}>
                            Belgeyi Arşivle
                          </button>
                          <button type="button" onClick={() => { closeLogViewer(); onDeleteKnowledgeDocument(document) }}>
                            <Trash2 aria-hidden="true" size={16} />
                            Belgeyi Sil
                          </button>
                        </div>
                      ) : null}
                    </>
                  )
                })() : (
                  <p className="empty-state">Uyarı kaydı bulunamadı.</p>
                )}
              </div>
            </section>
          </div>
        )
      })() : null}

      {documentViewer ? (
        <div ref={documentViewerDialogRef} className="document-viewer" role="dialog" aria-modal="true" aria-labelledby="document-viewer-title" tabIndex={-1}>
          <div className="document-viewer-top">
            <button type="button" className="back-button" onClick={closeDocumentViewer}>
              <ArrowLeft size={18} aria-hidden="true" />
              <span>Geri</span>
            </button>
            <div>
              <p className="eyebrow">Belge Görüntüleyici</p>
              <h2 id="document-viewer-title">{documentViewer.content?.title ?? 'Belge yükleniyor'}</h2>
              {documentViewer.content ? (
                <p className="document-viewer-meta">
                  {documentViewer.content.sourceName}
                  {documentViewer.content.originalFileName ? ` - ${documentViewer.content.originalFileName}` : ''}
                </p>
              ) : null}
            </div>
          </div>

          <section className="document-viewer-body" aria-label="Belge içeriği">
            {documentViewer.isLoading ? (
              <div className="source-loading">
                <Loader2 size={22} aria-hidden="true" />
                <p>Belge içeriği yükleniyor...</p>
              </div>
            ) : null}
            {!documentViewer.isLoading && documentViewer.error ? (
              <p className="error-banner">{documentViewer.error}</p>
            ) : null}
            {!documentViewer.isLoading && documentViewer.content ? (
              <pre className="content-display">{documentViewer.content.content}</pre>
            ) : null}
          </section>
        </div>
      ) : null}

      {isSettingsOpen ? (
        <div ref={settingsDialogRef} className="source-dialog settings-dialog" role="dialog" aria-modal="true" aria-labelledby="settings-dialog-title" tabIndex={-1}>
          <div className="source-dialog-top">
            <button type="button" className="back-button" onClick={() => void closeSettingsPage()} disabled={isSavingSettings}>
              <ArrowLeft size={18} aria-hidden="true" />
              <span>Geri</span>
            </button>
            <div>
              <p className="eyebrow">{tenantConfig.uiWebSettingsPageTitle}</p>
              <h2 id="settings-dialog-title">{tenantConfig.uiWebSettingsHeaderTitle}</h2>
            </div>
          </div>

          <div className="settings-panel">
            {isSettingsLoading ? (
              <div className="source-loading">
                <Loader2 size={22} aria-hidden="true" />
                <p>Ayarlar yükleniyor...</p>
              </div>
            ) : null}

            {!isSettingsLoading && aiSettings ? (
              <form className="settings-form" aria-label="AI ayarları">
                <label>
                  <span>AI Tedarikçisi</span>
                  <select
                    value={draftProvider}
                    onChange={(event) => onDraftProviderChange(event.target.value)}
                    disabled={isSavingSettings}
                  >
                    {aiSettings.providers.map((provider) => (
                      <option key={provider.id} value={provider.id}>{provider.label}</option>
                    ))}
                  </select>
                </label>

                <label>
                  <span>AI Modeli</span>
                  <select
                    value={draftModel}
                    onChange={(event) => setDraftModel(event.target.value)}
                    disabled={isSavingSettings}
                  >
                    {(selectedDraftProvider?.models ?? []).map((model) => (
                      <option key={model.id} value={model.id} disabled={!model.isAvailable}>
                        {model.label}{model.isAvailable ? '' : ' (kullanılamıyor)'}
                      </option>
                    ))}
                  </select>
                </label>

                <div className="settings-section-title">
                  <MessageSquareText size={18} aria-hidden="true" />
                  <div>
                    <strong>Soru-Cevap Deneyimi</strong>
                    <p>Hazır sorular, öneriler ve kaynak görünürlüğü</p>
                  </div>
                </div>

                <label>
                  <span>Gösterilen hazır soru sayısı</span>
                  <select
                    value={draftDisplayedReadyQuestionCount}
                    onChange={(event) => setDraftDisplayedReadyQuestionCount(Number(event.target.value))}
                    disabled={isSavingSettings}
                  >
                    {Array.from({ length: 10 }, (_, index) => index + 1).map((value) => (
                      <option key={value} value={value}>{value}</option>
                    ))}
                  </select>
                </label>

                <label>
                  <span>Gösterilen önerilen soru sayısı</span>
                  <select
                    value={draftDisplayedSuggestedQuestionCount}
                    onChange={(event) => setDraftDisplayedSuggestedQuestionCount(Number(event.target.value))}
                    disabled={isSavingSettings}
                  >
                    {Array.from({ length: 10 }, (_, index) => index + 1).map((value) => (
                      <option key={value} value={value}>{value}</option>
                    ))}
                  </select>
                </label>

                <label>
                  <span>Hazır ve önerilen sorular otomatik gönderilsin mi?</span>
                  <select
                    value={draftAutoSubmitPromptButtons ? 'yes' : 'no'}
                    onChange={(event) => setDraftAutoSubmitPromptButtons(event.target.value === 'yes')}
                    disabled={isSavingSettings}
                  >
                    <option value="yes">Evet</option>
                    <option value="no">Hayır</option>
                  </select>
                </label>

                <label>
                  <span>Asistan cevaplarında kaynak belge adları yer alsın mı?</span>
                  <select
                    value={draftShowAnswerSourceDocumentNames ? 'yes' : 'no'}
                    onChange={(event) => setDraftShowAnswerSourceDocumentNames(event.target.value === 'yes')}
                    disabled={isSavingSettings}
                  >
                    <option value="yes">Evet</option>
                    <option value="no">Hayır</option>
                  </select>
                </label>

                <div className="settings-section-title">
                  <FileText size={18} aria-hidden="true" />
                  <div>
                    <strong>Bilgi Bankası</strong>
                    <p>Dosya Yükleme Limitleri</p>
                  </div>
                </div>

                <label>
                  <span>Maksimum dosya boyutu (MB)</span>
                  <input
                    type="number"
                    min={1}
                    max={500}
                    value={draftMaxFileSizeMb}
                    onChange={(event) => setDraftMaxFileSizeMb(Number(event.target.value))}
                    disabled={isSavingSettings}
                  />
                </label>

                <label>
                  <span>Maksimum dosya adedi</span>
                  <input
                    type="number"
                    min={1}
                    max={1000}
                    value={draftMaxBatchFileCount}
                    onChange={(event) => setDraftMaxBatchFileCount(Number(event.target.value))}
                    disabled={isSavingSettings}
                  />
                </label>

                <label>
                  <span>Maksimum toplu yükleme boyutu (MB)</span>
                  <input
                    type="number"
                    min={1}
                    max={2000}
                    value={draftMaxBatchSizeMb}
                    onChange={(event) => setDraftMaxBatchSizeMb(Number(event.target.value))}
                    disabled={isSavingSettings}
                  />
                </label>
              </form>
            ) : null}

            {settingsError ? <p className="error-banner">{settingsError}</p> : null}
          </div>
        </div>
      ) : null}

      {isHelpOpen ? <HelpPage onBack={closeHelpPage} tenantConfig={tenantConfig} /> : null}

      {floatingMessage ? (
        <MessageDialog
          message={floatingMessage}
          onClose={closeFloatingMessage}
          dialogRef={messageDialogRef}
        />
      ) : null}

      {confirmDialog ? (
        <ConfirmDialog
          {...confirmDialog}
          onCancel={() => setConfirmDialog(null)}
          dialogRef={confirmDialogRef}
        />
      ) : null}

      {activeProgressDialog ? (
        <ProgressDialog
          title={activeProgressDialog.title}
          steps={activeProgressDialog.steps}
          message={activeProgressDialog.message}
          error={activeProgressDialog.error}
          isDismissible={activeProgressDialog.isDismissible}
          onDismiss={activeProgressDialog.onDismiss}
          dialogRef={activeProgressDialog.dialogRef}
        />
      ) : null}

      {a11yAnnouncement && !isBlockingModalOpen ? (
        <div className="sr-only" aria-live="polite" aria-atomic="true">
          {a11yAnnouncement}
        </div>
      ) : null}
    </div>
  )
}

export default App
