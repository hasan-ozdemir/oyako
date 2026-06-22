// Codex developer note: Explains the purpose and flow of webapp-oyako/tests/mockApi.ts for maintainers.
import type { Page } from '@playwright/test'

// Defines a reusable TypeScript type for frontend data flow.
type MockRuntimeStatus = {
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

// Defines a reusable frontend value used by the surrounding module.
const now = () => new Date().toISOString()

// Defines a reusable frontend value used by the surrounding module.
export const readyStatus = (): MockRuntimeStatus => ({
  operation: 'app',
  phase: 'ready_for_question',
  stepKey: 'ready',
  stepIndex: 1,
  stepCount: 1,
  isTerminal: true,
  message: 'Uygulama Hazır',
  severity: 'ready',
  icon: 'message',
  pageCount: 3,
  updatedAtUtc: now(),
})

// Implements a frontend function that supports Oyako user or API behavior.
export async function mockOyakoApi(page: Page) {
  let statuses: MockRuntimeStatus[] = [readyStatus()]
  const tenantConfig = {
    tenantId: '013dfb350ed64e324a805eae86646ddf',
    tenantOrderNumber: '1',
    tenantName: 'tenantdemo',
    tenantDisplayName: 'Tenant Demo',
    tenantAzureDomainName: 'oyako',
    tenantCustomDomainName: 'oyako.tenantdemo.example',
    tenantWebUrl: 'https://www.tenantdemo.example',
    tenantAdminEmail: 'admin@tenantdemo.example',
    tenantFeedbackEmail: 'iletisim@tenantdemo.example',
    primaryAiProvider: 'ollama-cloud',
    secondaryAiProvider: 'azure',
    uiWebBrandName: 'Tenant Demo',
    uiWebAssistantName: 'Oyako',
    uiWebTitle: 'Oyako: Tenant Demo Soru-Cevap Platformu',
    uiWebHeaderTitle: 'Tenant Demo soru-cevap platformu',
    uiWebBrandLogoUrl: '/icons/oyako-icon.svg',
    uiWebAssistantWelcomeMessage: 'Merhaba, ben dijital asistanınız Oyako. Tenant Demo ile ilgili merak ettiğiniz her şeyi bana sorabilirsiniz. Cevaplamak için hazırım.',
    uiWebAssistantHeaderTitle: 'Tenant Demo hakkında öğrenmek istediğinizi sorun:',
    uiWebMoreMenuBrandLink: 'Tenant Demo',
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
  let bank = {
    sourceCount: 2,
    documentCount: 3,
    sources: [
      {
        id: 1,
        sourceType: 'web_site',
        name: 'Tenant Demo',
        description: 'Tenant Demo web sitesi',
        address: 'https://www.tenantdemo.example',
        protocol: 'https',
        isEnabled: true,
        isArchived: false,
        statusCode: 'ok',
        statusLabel: 'Tamam',
        statusMessage: 'Kaynak kullanılabilir.',
        webPageAdditionMode: 'automatic',
        webPageAdditionModeLabel: 'Otomatik Eklenir',
        documentCount: 2,
        activeDocumentCount: 2,
        lastCheckedAtUtc: now(),
        updatedAtUtc: now(),
      },
      {
        id: 2,
        sourceType: 'local_files',
        name: 'Yerel Dosyalar',
        description: 'Test ortamı yerel dosya kaynağı',
        address: 'local-files://oyako-test',
        protocol: 'local-files',
        isEnabled: true,
        isArchived: false,
        statusCode: 'ok',
        statusLabel: 'Tamam',
        statusMessage: 'Kaynak kullanılabilir.',
        webPageAdditionMode: 'manual',
        webPageAdditionModeLabel: 'Kullanıcı Ekler',
        documentCount: 1,
        activeDocumentCount: 1,
        lastCheckedAtUtc: now(),
        updatedAtUtc: now(),
      },
    ],
    documents: [
      {
        id: 11,
        sourceId: 1,
        sourceName: 'Tenant Demo',
        sourceType: 'web_site',
        title: 'Dijital Dönüşüm ve Yapay Zekâ',
        url: 'https://www.tenantdemo.example/cozumler/dijital-donusum-ve-yapay-zeka',
        content: 'Tenant Demo, kurumların dijital dönüşüm yolculuğunu hızlandıran yapay zekâ ve veri odaklı çözümler geliştirir.',
        contentPreview: 'Tenant Demo, kurumların dijital dönüşüm yolculuğunu hızlandıran yapay zekâ ve veri odaklı çözümler geliştirir.',
        isEnabled: true,
        isArchived: false,
        statusCode: 'ok',
        statusLabel: 'Tamam',
        statusMessage: 'Belge kullanılabilir.',
        httpStatusCode: 200,
        previewStatus: 'deterministic',
        previewGeneratedAtUtc: now(),
        lastCheckedAtUtc: now(),
        lastCrawledAtUtc: now(),
      },
      {
        id: 12,
        sourceId: 1,
        sourceName: 'Tenant Demo',
        sourceType: 'web_site',
        title: 'Yönetilen Hizmetler',
        url: 'https://www.tenantdemo.example/cozumler/yonetilen-hizmetler',
        content: 'Profesyonel yönetilen hizmetler ile işletmelerin teknoloji operasyonları güvenli ve sürdürülebilir şekilde desteklenir.',
        contentPreview: 'Profesyonel yönetilen hizmetler ile işletmelerin teknoloji operasyonları güvenli ve sürdürülebilir şekilde desteklenir.',
        isEnabled: true,
        isArchived: false,
        statusCode: 'http404',
        statusLabel: 'http404',
        statusMessage: 'Sayfa HTTP 404 döndü.',
        httpStatusCode: 404,
        previewStatus: 'deterministic',
        previewGeneratedAtUtc: now(),
        lastCheckedAtUtc: now(),
        lastCrawledAtUtc: now(),
      },
      {
        id: 21,
        sourceId: 2,
        sourceName: 'Yerel Dosyalar',
        sourceType: 'local_files',
        title: 'Kurumsal Dosya',
        url: 'local-file://kurumsal-dosya.md',
        content: 'Yerel dosya içeriği Tenant Demo süreçlerini açıklar.',
        contentPreview: 'Yerel dosya içeriği Tenant Demo süreçlerini açıklar.',
        isEnabled: true,
        isArchived: false,
        statusCode: 'ok',
        statusLabel: 'Tamam',
        statusMessage: 'Belge kullanılabilir.',
        httpStatusCode: null,
        previewStatus: 'manual',
        previewGeneratedAtUtc: now(),
        lastCheckedAtUtc: now(),
        lastCrawledAtUtc: now(),
        originalFileName: 'kurumsal-dosya.md',
        normalizedRelativePath: 'kurumsal-dosya.md',
      },
    ],
  }
  let nextSourceId = 3
  let nextDocumentId = 30

  // Recomputes derived Knowledge Bank counters after mocked mutations.
  const syncBankCounters = () => {
    const sources = bank.sources.map((source) => {
      const sourceDocuments = bank.documents.filter((document) => document.sourceId === source.id)
      return {
        ...source,
        documentCount: sourceDocuments.length,
        activeDocumentCount: sourceDocuments.filter((document) => document.isEnabled && !document.isArchived).length,
        updatedAtUtc: now(),
      }
    })
    bank = {
      ...bank,
      sourceCount: sources.length,
      documentCount: bank.documents.length,
      sources,
    }
  }

  // Returns true when a mocked source or document should show a warning affordance.
  const isWarningStatus = (statusCode?: string | null) => (statusCode ?? '').trim().toLowerCase() !== 'ok'

  // Extracts the numeric path id from a mocked REST URL.
  const parseNumericId = (url: string) => Number(url.match(/\/(\d+)(?:\/|$)/)?.[1] ?? 0)

  // Builds the mocked source diagnostics payload expected by the frontend LogViewer.
  const buildSourceDiagnostics = (sourceId: number) => {
    const source = bank.sources.find((item) => item.id === sourceId)
    const warningDocuments = bank.documents.filter((document) => document.sourceId === sourceId && isWarningStatus(document.statusCode))
    return { itemType: 'source', source, warningDocuments }
  }

  // Builds the mocked document diagnostics payload expected by the frontend LogViewer.
  const buildDocumentDiagnostics = (documentId: number) => {
    const document = bank.documents.find((item) => item.id === documentId)
    return { itemType: 'document', document }
  }

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/runtime/status/stream**', async (route) => {
    // Awaits the asynchronous frontend operation before continuing.
    await route.abort()
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/runtime/status**', async (route) => {
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({ json: statuses.shift() ?? readyStatus() })
  })

  await page.route('**/api/tenant-config', async (route) => {
    await route.fulfill({ json: tenantConfig })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/knowledge-health', async (route) => {
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({
      json: {
        status: 'ready',
        database: 'ok',
        crawler: 'ok',
        scraper: 'ok',
        browser: 'ok',
        cache: 'ok',
        readyQuestions: 'ok',
        message: 'Uygulama Hazır',
        pageCount: 3,
        warningCount: 0,
        errorCount: 0,
        lastCrawlStatus: 'Completed',
        lastCrawlStartedAtUtc: now(),
        lastCrawlCompletedAtUtc: now(),
        lastCrawlErrorMessage: null,
        lastCrawlWarningMessage: null,
        sourceCount: 2,
        sourceFingerprint: 'fingerprint',
        readyQuestionsFingerprint: 'fingerprint',
        readyQuestionsFingerprintMatches: true,
        readyQuestionsCount: 100,
        readyQuestionsGeneratedAtUtc: now(),
        knowledgeCacheBuiltAtUtc: now(),
        checkedAtUtc: now(),
        components: [],
      },
    })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/ready-questions**', async (route) => {
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({
      json: {
        questions: [
          { text: 'Tenant Demo hangi hizmetleri sunar?' },
          { text: 'Tenant Demo yapay zeka alanında ne yapıyor?' },
          { text: 'Yönetilen hizmetler nelerdir?' },
          { text: 'Bilgi güvenliği yaklaşımı nedir?' },
        ],
        source: 'generated',
        generatedAtUtc: now(),
        totalAvailable: 100,
        sourceFingerprint: 'fingerprint',
        isRefreshing: false,
      },
    })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/knowledge-bank', async (route) => {
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({ json: bank })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/knowledge-files/preview', async (route) => {
    await route.fulfill({
      json: {
        items: [
          {
            clientFileId: 'mock-file-1',
            fileName: 'oyako-test.md',
            relativePath: 'oyako-test.md',
            defaultTitle: 'Oyako Test Belgesi',
            content: 'Oyako test dosyası Tenant Demo bilgi bankası için temiz metin içerir.',
            contentPreview: 'Oyako test dosyası Tenant Demo bilgi bankası için temiz metin içerir.',
            parseStatus: 'parsed',
            ocrStatus: 'not_required',
            errorMessage: null,
          },
        ],
        messages: [],
      },
    })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/knowledge-sources**', async (route) => {
    const request = route.request()
    const url = request.url()
    const sourceId = parseNumericId(url)

    if (request.method() === 'POST' && url.includes('/documents/import')) {
      const source = bank.sources.find((item) => item.id === sourceId)
      if (!source) {
        await route.fulfill({ status: 404, json: { message: 'Kaynak bulunamadı.' } })
        return
      }

      const documentTitle = 'Oyako Test Belgesi'
      bank = {
        ...bank,
        documents: [
          ...bank.documents,
          {
            id: nextDocumentId++,
            sourceId: source.id,
            sourceName: source.name,
            sourceType: source.sourceType,
            title: documentTitle,
            url: `local-file://${documentTitle.toLowerCase().replaceAll(' ', '-')}.md`,
            content: 'Oyako test dosyası Tenant Demo bilgi bankası için temiz metin içerir.',
            contentPreview: 'Oyako test dosyası Tenant Demo bilgi bankası için temiz metin içerir.',
            isEnabled: true,
            isArchived: false,
            statusCode: 'ok',
            statusLabel: 'Tamam',
            statusMessage: 'Belge kullanılabilir.',
            httpStatusCode: null,
            previewStatus: 'manual',
            previewGeneratedAtUtc: now(),
            lastCheckedAtUtc: now(),
            lastCrawledAtUtc: now(),
            originalFileName: 'oyako-test.md',
            normalizedRelativePath: 'oyako-test.md',
          },
        ],
      }
      syncBankCounters()
      await route.fulfill({
        json: {
          importedCount: 1,
          updatedCount: 0,
          skippedCount: 0,
          failedItems: [],
          knowledgeBank: bank,
        },
      })
      return
    }

    if (request.method() === 'POST' && url.includes('/web-documents')) {
      const source = bank.sources.find((item) => item.id === sourceId)
      const body = await request.postDataJSON() as { url?: string; title?: string; isEnabled?: boolean }
      if (!source) {
        await route.fulfill({ status: 404, json: { message: 'Kaynak bulunamadı.' } })
        return
      }

      const title = body.title?.trim() || 'Manuel Web Belgesi'
      bank = {
        ...bank,
        documents: [
          ...bank.documents,
          {
            id: nextDocumentId++,
            sourceId: source.id,
            sourceName: source.name,
            sourceType: source.sourceType,
            title,
            url: body.url || 'https://example.com/manual',
            content: `${title} Tenant Demo bilgi bankasına manuel web bağlantısı olarak eklendi.`,
            contentPreview: `${title} Tenant Demo bilgi bankasına manuel web bağlantısı olarak eklendi.`,
            isEnabled: body.isEnabled ?? true,
            isArchived: false,
            statusCode: 'ok',
            statusLabel: 'Tamam',
            statusMessage: 'Belge kullanılabilir.',
            httpStatusCode: 200,
            previewStatus: 'manual',
            previewGeneratedAtUtc: now(),
            lastCheckedAtUtc: now(),
            lastCrawledAtUtc: now(),
          },
        ],
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    if (request.method() === 'GET' && url.includes('/diagnostics')) {
      await route.fulfill({ json: buildSourceDiagnostics(sourceId) })
      return
    }

    if (request.method() === 'PATCH' && url.includes('/enabled')) {
      const body = await request.postDataJSON() as { isEnabled?: boolean }
      bank = {
        ...bank,
        sources: bank.sources.map((source) => source.id === sourceId ? { ...source, isEnabled: Boolean(body.isEnabled) } : source),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    if (request.method() === 'PATCH' && url.includes('/archive')) {
      const body = await request.postDataJSON() as { isArchived?: boolean }
      bank = {
        ...bank,
        sources: bank.sources.map((source) => source.id === sourceId ? { ...source, isArchived: Boolean(body.isArchived) } : source),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    if (request.method() === 'DELETE') {
      bank = {
        ...bank,
        sources: bank.sources.filter((source) => source.id !== sourceId),
        documents: bank.documents.filter((document) => document.sourceId !== sourceId),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    if (request.method() === 'PUT') {
      const body = await request.postDataJSON() as { sourceType?: string; name?: string; description?: string; address?: string; isEnabled?: boolean }
      bank = {
        ...bank,
        sources: bank.sources.map((source) => source.id === sourceId
          ? {
              ...source,
              sourceType: body.sourceType ?? source.sourceType,
              name: body.name || source.name,
              description: body.description ?? source.description,
              address: body.address || source.address,
              isEnabled: body.isEnabled ?? source.isEnabled,
              protocol: body.sourceType === 'local_files'
                ? 'local-files'
                : body.sourceType === 'web_links'
                  ? 'web-links'
                  : source.protocol,
              webPageAdditionMode: body.sourceType === 'web_site' ? 'automatic' : 'manual',
              webPageAdditionModeLabel: body.sourceType === 'web_site' ? 'Otomatik Eklenir' : 'Kullanıcı Ekler',
              statusCode: 'ok',
              statusLabel: 'Tamam',
              statusMessage: 'Kaynak kullanılabilir.',
              updatedAtUtc: now(),
            }
          : source),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    // Guards this branch so the UI handles the condition intentionally.
    if (request.method() === 'POST') {
      const body = await request.postDataJSON() as { sourceType?: string; name?: string; description?: string; address?: string }
      const sourceType = body.sourceType || 'web_site'
      const defaultName = sourceType === 'local_files'
        ? 'Yerel Dosyalar'
        : sourceType === 'web_links'
          ? 'Web Bağlantıları'
          : 'Yeni Kaynak'
      bank = {
        ...bank,
        sources: [
          ...bank.sources,
          {
            id: nextSourceId++,
            sourceType,
            name: body.name?.trim() || defaultName,
            description: body.description ?? '',
            address: sourceType === 'web_site'
              ? body.address || 'https://example.com'
              : sourceType === 'web_links'
                ? 'web-links://manual'
                : 'local-files://oyako-test',
            protocol: sourceType === 'web_site'
              ? 'https'
              : sourceType === 'web_links'
                ? 'web-links'
                : 'local-files',
            isEnabled: true,
            isArchived: false,
            statusCode: 'ok',
            statusLabel: 'Tamam',
            statusMessage: sourceType === 'web_site' ? 'Kaynak tarama için sıraya alındı.' : 'Kaynak kullanılabilir.',
            webPageAdditionMode: sourceType === 'web_site' ? 'automatic' : 'manual',
            webPageAdditionModeLabel: sourceType === 'web_site' ? 'Otomatik Eklenir' : 'Kullanıcı Ekler',
            documentCount: 0,
            activeDocumentCount: 0,
            lastCheckedAtUtc: now(),
            updatedAtUtc: now(),
          },
        ],
      }
      syncBankCounters()
    }

    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({ json: bank })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/knowledge-documents**', async (route) => {
    const request = route.request()
    const url = request.url()
    const documentId = parseNumericId(url)

    if (request.method() === 'GET' && url.includes('/diagnostics')) {
      await route.fulfill({ json: buildDocumentDiagnostics(documentId) })
      return
    }

    if (request.method() === 'PATCH' && url.includes('/enabled')) {
      const body = await request.postDataJSON() as { isEnabled?: boolean }
      bank = {
        ...bank,
        documents: bank.documents.map((document) => document.id === documentId ? { ...document, isEnabled: Boolean(body.isEnabled) } : document),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    if (request.method() === 'PATCH' && url.includes('/archive')) {
      const body = await request.postDataJSON() as { isArchived?: boolean }
      bank = {
        ...bank,
        documents: bank.documents.map((document) => document.id === documentId ? { ...document, isArchived: Boolean(body.isArchived) } : document),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    if (request.method() === 'DELETE') {
      bank = {
        ...bank,
        documents: bank.documents.filter((document) => document.id !== documentId),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    if (request.method() === 'GET' && url.includes('/content')) {
      const document = bank.documents.find((item) => item.id === documentId) ?? bank.documents[0]
      await route.fulfill({
        json: {
          id: document.id,
          sourceId: document.sourceId,
          sourceName: document.sourceName,
          sourceType: document.sourceType,
          title: document.title,
          url: document.url,
          content: document.content,
          originalFileName: document.originalFileName ?? '',
          lastCheckedAtUtc: document.lastCheckedAtUtc,
          lastCrawledAtUtc: document.lastCrawledAtUtc,
        },
      })
      return
    }

    if (request.method() === 'PATCH' && url.includes('/web-link')) {
      const body = await request.postDataJSON() as { url?: string }
      bank = {
        ...bank,
        documents: bank.documents.map((document) => document.id === documentId
          ? {
              ...document,
              url: body.url || document.url,
              content: `Güncellenmiş web bağlantısı ${body.url || document.url} Tenant Demo bilgi bankasında kullanılabilir.`,
              contentPreview: `Güncellenmiş web bağlantısı ${body.url || document.url} Tenant Demo bilgi bankasında kullanılabilir.`,
              statusCode: 'ok',
              statusLabel: 'Tamam',
              statusMessage: 'Belge bağlantısı güncellendi.',
              previewStatus: 'manual',
              previewGeneratedAtUtc: now(),
              lastCheckedAtUtc: now(),
              lastCrawledAtUtc: now(),
            }
          : document),
      }
      syncBankCounters()
      await route.fulfill({ json: bank })
      return
    }

    // Guards this branch so the UI handles the condition intentionally.
    if (request.method() === 'PUT') {
      const body = await request.postDataJSON() as { content?: string; title?: string; isEnabled?: boolean }
      bank = {
        ...bank,
        documents: bank.documents.map((document) => document.id === documentId
          ? {
              ...document,
              title: body.title ?? document.title,
              content: body.content ?? document.content,
              contentPreview: body.content ?? document.contentPreview,
              isEnabled: body.isEnabled ?? document.isEnabled,
              statusCode: 'ok',
              statusLabel: 'Tamam',
              statusMessage: 'Belge kullanıcı tarafından düzenlendi.',
              previewStatus: 'manual',
              previewGeneratedAtUtc: now(),
              lastCheckedAtUtc: now(),
              lastCrawledAtUtc: now(),
            }
          : document),
      }
      syncBankCounters()
    }

    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({ json: bank })
  })

  // Awaits the asynchronous frontend operation before continuing.
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/knowledge-source-redownload/*', async (route) => {
    await route.fulfill({
      json: {
        status: 'succeeded',
        backupSetId: '',
        startedAtUtc: now(),
        completedAtUtc: now(),
        pageCount: bank.documents.length,
        warningCount: 0,
        errorCount: 0,
        readyQuestionsCount: 100,
        sourceFingerprint: 'fingerprint',
        cacheBuiltAtUtc: now(),
        restoredFromBackup: false,
        message: 'Kaynak yeniden indirildi.',
        knowledgeBank: bank,
      },
    })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/source-document-redownload/*', async (route) => {
    await route.fulfill({
      json: {
        status: 'succeeded',
        backupSetId: '',
        startedAtUtc: now(),
        completedAtUtc: now(),
        pageCount: 1,
        warningCount: 0,
        errorCount: 0,
        readyQuestionsCount: 100,
        sourceFingerprint: 'fingerprint',
        cacheBuiltAtUtc: now(),
        restoredFromBackup: false,
        message: 'Belge yeniden indirildi.',
        knowledgeBank: bank,
      },
    })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/ai-settings', async (route) => {
    // Guards this branch so the UI handles the condition intentionally.
    if (route.request().method() === 'PUT') {
      // Awaits the asynchronous frontend operation before continuing.
      await route.fulfill({
        json: {
          activeProvider: 'azure',
          activeModel: 'deepseek-v4-flash',
          providers: [
            {
              id: 'azure',
              label: 'Azure',
              selectedModel: 'deepseek-v4-flash',
              isAvailable: true,
              models: [{ id: 'deepseek-v4-flash', label: 'DeepSeek V4 Flash', isAvailable: true }],
            },
          ],
        },
      })
      // Returns the value or JSX produced by this frontend workflow.
      return
    }

    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({
      json: {
        activeProvider: 'azure',
        activeModel: 'deepseek-v4-flash',
        providers: [
          {
            id: 'azure',
            label: 'Azure',
            selectedModel: 'deepseek-v4-flash',
            isAvailable: true,
            models: [{ id: 'deepseek-v4-flash', label: 'DeepSeek V4 Flash', isAvailable: true }],
          },
          {
            id: 'ollama',
            label: 'Ollama',
            selectedModel: 'minimax-m3:cloud',
            isAvailable: true,
            models: [{ id: 'minimax-m3:cloud', label: 'minimax-m3:cloud', isAvailable: true }],
          },
        ],
      },
    })
  })

  await page.route('**/api/knowledge-settings', async (route) => {
    await route.fulfill({
      json: {
        maxFileSizeMb: 50,
        maxBatchFileCount: 25,
        maxBatchSizeMb: 250,
        updatedAtUtc: now(),
      },
    })
  })

  await page.route('**/api/knowledge-refresh-settings', async (route) => {
    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON() as { refreshPeriodValue?: number; refreshPeriodUnit?: 'minute' | 'hour' | 'day' | 'week' }
      const value = body.refreshPeriodValue ?? 1
      const unit = body.refreshPeriodUnit ?? 'hour'
      const minutes = unit === 'minute' ? value : unit === 'hour' ? value * 60 : unit === 'day' ? value * 1440 : value * 10080
      await route.fulfill({
        json: {
          refreshPeriodValue: value,
          refreshPeriodUnit: unit,
          refreshPeriodMinutes: minutes,
          updatedAtUtc: now(),
        },
      })
      return
    }

    await route.fulfill({
      json: {
        refreshPeriodValue: 1,
        refreshPeriodUnit: 'hour',
        refreshPeriodMinutes: 60,
        updatedAtUtc: now(),
      },
    })
  })

  await page.route('**/api/qna-experience-settings', async (route) => {
    await route.fulfill({
      json: {
        displayedReadyQuestionCount: 4,
        displayedSuggestedQuestionCount: 4,
        autoSubmitPromptButtons: true,
        showAnswerSourceDocumentNames: true,
        updatedAtUtc: now(),
      },
    })
  })

  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/chat/stream', async (route) => {
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({
      contentType: 'text/event-stream',
      body: [
        'data: {"type":"phase","phase":"asking"}',
        '',
        'data: {"type":"answer","answer_content":"<p>Tenant Demo; dijital dönüşüm, yapay zekâ, kurumsal uygulama ve yönetilen hizmetler alanlarında hizmet sunar.</p>","suggested_questions":["Yönetilen hizmetler nelerdir?","Yapay zekâ hizmetleri nelerdir?"]}',
        '',
        'data: [DONE]',
        '',
      ].join('\n'),
    })
  })

  // Returns the value or JSX produced by this frontend workflow.
  return {
    setRuntimeStatuses(next: MockRuntimeStatus[]) {
      statuses = next
    },
  }
}
