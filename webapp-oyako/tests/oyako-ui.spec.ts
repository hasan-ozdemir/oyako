// Codex developer note: Explains the purpose and flow of webapp-oyako/tests/oyako-ui.spec.ts for maintainers.
import { expect, test, type Locator, type Page } from '@playwright/test'
import { mockOyakoApi } from './mockApi'

// Checks that responsive changes do not create page-level horizontal overflow.
async function expectNoBodyHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(2)
}

// Checks that the intended local container owns overflow instead of hiding content.
async function expectScrollable(locator: Locator, axis: 'x' | 'y') {
  await expect(locator).toBeVisible()
  const canScroll = await locator.evaluate((element, scrollAxis) => scrollAxis === 'x'
    ? element.scrollWidth > element.clientWidth
    : element.scrollHeight > element.clientHeight, axis)
  expect(canScroll).toBeTruthy()
}

// Checks that visible interactive controls provide a usable pointer target.
async function expectMinimumPointerTargets(root: Locator) {
  const undersizedTargets = await root
    .locator('button, textarea, select, input[type="checkbox"], input[type="text"], [role="button"], [role="menuitem"]')
    .evaluateAll((elements) => elements
      .filter((element) => {
        const style = window.getComputedStyle(element)
        const rect = element.getBoundingClientRect()
        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0
      })
      .map((element) => {
        const rect = element.getBoundingClientRect()
        return {
          label: element.getAttribute('aria-label') || element.textContent?.trim() || element.tagName,
          width: Math.round(rect.width),
          height: Math.round(rect.height),
        }
      })
      .filter((target) => target.width < 24 || target.height < 24))
  expect(undersizedTargets).toEqual([])
}

// Checks the UI behavior expected by this automated test.
test('main UI remains usable across core interactions', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('banner')).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('contentinfo')).toContainText('Uygulama Hazır')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('contentinfo')).toContainText('Bilgi Bankası (2 Kaynak 3 Belge)')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('button', { name: 'Yeni Sohbet' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('link', { name: 'Oyak Dijital' })).toBeVisible()

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByLabel('Kullanıcı: Ziyaretçi').click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('menu', { name: 'Kullanıcı işlemleri' })).toContainText('Giriş Yap')

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Ayarlar' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: 'Oyako çalışma ayarları' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Geri' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: 'Oyako çalışma ayarları' })).toBeHidden()
})

// Checks the UI behavior expected by this automated test.
test('visible interactive controls keep production-safe pointer target sizes', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await page.setViewportSize({ width: 390, height: 844 })
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await expectMinimumPointerTargets(page.locator('body'))
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: /Bilgi bankası/i }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await expectMinimumPointerTargets(page.getByRole('dialog', { name: /kullandığı bilgi kaynakları/i }))
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('row', { name: /Yerel Dosyalar/ }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page
    .getByRole('row', { name: /Kurumsal Dosya/ })
    .getByRole('button', { name: 'Belgeyi Düzenle' })
    .click()
  // Awaits the asynchronous frontend operation before continuing.
  await expectMinimumPointerTargets(page.getByRole('dialog', { name: 'Kurumsal Dosya' }))
})

// Checks the UI behavior expected by this automated test.
test('status bar reports offline application state when runtime endpoints are unreachable', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/runtime/status/stream**', async (route) => route.abort())
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/runtime/status**', async (route) => route.abort())
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/qna-experience-settings', async (route) => route.abort())
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/knowledge-health', async (route) => route.abort())
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/ready-questions**', async (route) => route.abort())
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('contentinfo')).toContainText('Uygulama Çevrimdışı')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('contentinfo')).not.toContainText('bağlantı sınırlı')
})

const overflowViewports = [
  { name: 'compact mobile', width: 360, height: 740 },
  { name: 'mobile', width: 390, height: 844 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'landscape tablet', width: 1024, height: 768 },
  { name: 'desktop', width: 1366, height: 768 },
  { name: 'large desktop', width: 1440, height: 900 },
  { name: 'full hd desktop', width: 1920, height: 1080 },
]

for (const viewport of overflowViewports) {
  // Checks the UI behavior expected by this automated test.
  test(`layout remains scroll-safe on ${viewport.name} viewport`, async ({ page }) => {
    // Awaits the asynchronous frontend operation before continuing.
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    // Awaits the asynchronous frontend operation before continuing.
    await mockOyakoApi(page)
    // Awaits the asynchronous frontend operation before continuing.
    await page.goto('/')

    // Awaits the asynchronous frontend operation before continuing.
    await page.getByLabel('Kullanıcı: Ziyaretçi').click()
    const userMenu = page.getByRole('menu', { name: 'Kullanıcı işlemleri' })
    // Awaits the asynchronous frontend operation before continuing.
    await expect(userMenu).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(userMenu.getByRole('menuitem', { name: 'Giriş Yap' })).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(userMenu.getByRole('menuitem', { name: 'Kayıt Ol' })).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expectNoBodyHorizontalOverflow(page)
    // Awaits the asynchronous frontend operation before continuing.
    await page.getByLabel('Kullanıcı: Ziyaretçi').click()

    // Awaits the asynchronous frontend operation before continuing.
    await page.getByRole('button', { name: 'Ayarlar' }).click()
    const settingsDialog = page.getByRole('dialog', { name: 'Oyako çalışma ayarları' })
    // Awaits the asynchronous frontend operation before continuing.
    await expect(settingsDialog).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(settingsDialog.getByLabel('AI Tedarikçisi')).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(settingsDialog.getByLabel('AI Modeli')).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(settingsDialog.getByRole('button', { name: 'Geri' })).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expectNoBodyHorizontalOverflow(page)
    // Awaits the asynchronous frontend operation before continuing.
    await settingsDialog.getByRole('button', { name: 'Geri' }).click()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(settingsDialog).toBeHidden()

    const longQuestion = Array.from({ length: 120 }, (_, index) => `Oyak Dijital hizmetleri hakkında çok uzun soru satırı ${index + 1}`).join('\n')
    // Awaits the asynchronous frontend operation before continuing.
    await page.getByRole('textbox', { name: 'Soru' }).fill(longQuestion)
    // Awaits the asynchronous frontend operation before continuing.
    await expectNoBodyHorizontalOverflow(page)
    // Awaits the asynchronous frontend operation before continuing.
    await expectScrollable(page.getByRole('textbox', { name: 'Soru' }), 'y')

    // Awaits the asynchronous frontend operation before continuing.
    await page.getByRole('button', { name: /Bilgi bankası/i }).click()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(page.getByRole('dialog', { name: /kullandığı bilgi kaynakları/i })).toBeVisible()
    const documentTableScroller = page
      .locator('section[aria-labelledby="knowledge-documents-title"] table.knowledge-table')
      .locator('xpath=ancestor::div[contains(@class, "knowledge-table-scroll")]')
    const tableMetrics = await documentTableScroller.evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }))
    if (tableMetrics.scrollWidth > tableMetrics.clientWidth + 2) {
      // Awaits the asynchronous frontend operation before continuing.
      await expectScrollable(documentTableScroller, 'x')
    } else {
      expect(tableMetrics.scrollWidth <= tableMetrics.clientWidth + 2).toBeTruthy()
    }
    // Awaits the asynchronous frontend operation before continuing.
    await documentTableScroller.evaluate((element) => {
      element.scrollLeft = element.scrollWidth
    })
    // Awaits the asynchronous frontend operation before continuing.
    await page.getByRole('row', { name: /Yerel Dosyalar/ }).click()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(page
      .getByRole('row', { name: /Kurumsal Dosya/ })
      .getByRole('button', { name: 'Belgeyi Düzenle' })).toBeVisible()

    // Awaits the asynchronous frontend operation before continuing.
    await page
      .getByRole('row', { name: /Kurumsal Dosya/ })
      .getByRole('button', { name: 'Belgeyi Düzenle' })
      .click()
    const editor = page.getByRole('dialog', { name: 'Kurumsal Dosya' })
    // Awaits the asynchronous frontend operation before continuing.
    await expect(editor).toBeVisible()
    const longDocument = Array.from({ length: 180 }, (_, index) => `Düzenlenebilir belge içeriği satır ${index + 1}`).join('\n')
    // Awaits the asynchronous frontend operation before continuing.
    await page.getByLabel('Belge içeriği').fill(longDocument)
    // Awaits the asynchronous frontend operation before continuing.
    await expectScrollable(page.getByLabel('Belge içeriği'), 'y')
    // Awaits the asynchronous frontend operation before continuing.
    await expect(editor.getByRole('button', { name: 'Kaydet' })).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(editor.locator('.document-editor-actions').getByRole('button', { name: 'İptal' })).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await editor.locator('.document-editor-actions').getByRole('button', { name: 'İptal' }).click()
    // Awaits the asynchronous frontend operation before continuing.
    await expect(editor).toBeHidden()

    // Awaits the asynchronous frontend operation before continuing.
    await page.getByRole('button', { name: 'Geri' }).click()
    // Awaits the asynchronous frontend operation before continuing.
    await page.getByRole('button', { name: 'Daha Fazla...' }).click()
    // Awaits the asynchronous frontend operation before continuing.
    await page.getByRole('menuitem', { name: 'Yardım' }).click()
    const helpDialog = page.getByRole('dialog', { name: 'Oyako nasıl kullanılır?' })
    // Awaits the asynchronous frontend operation before continuing.
    await expect(helpDialog).toBeVisible()
    // Awaits the asynchronous frontend operation before continuing.
    await expectNoBodyHorizontalOverflow(page)
    // Awaits the asynchronous frontend operation before continuing.
    await expect(helpDialog.getByRole('button', { name: 'Geri' })).toBeVisible()
  })
}

// Checks the UI behavior expected by this automated test.
test('long assistant answers remain inside the answer panel scroll area', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await page.setViewportSize({ width: 390, height: 844 })
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/chat/stream', async (route) => {
    const rows = Array.from({ length: 80 }, (_, index) => `<p>Uzun yanıt satırı ${index + 1}: Oyak Dijital bilgi bankası içeriği panel içinde okunur.</p>`).join('')
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({
      contentType: 'text/event-stream',
      body: [
        'data: {"type":"phase","phase":"asking"}',
        '',
        `data: ${JSON.stringify({ type: 'answer', answer_content: rows, suggested_questions: ['Uzun yanıt nasıl okunur?'] })}`,
        '',
        'data: [DONE]',
        '',
      ].join('\n'),
    })
  })
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('textbox', { name: 'Soru' }).fill('Oyak Dijital hangi hizmetleri sunar?')
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Sor', exact: true }).click()

  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByText('Uzun yanıt satırı 80')).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expectScrollable(page.locator('.messages'), 'y')
  // Awaits the asynchronous frontend operation before continuing.
  await expectNoBodyHorizontalOverflow(page)
})

// Checks the UI behavior expected by this automated test.
test('floating surfaces close with Escape and restore focus to their trigger', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  const knowledgeButton = page.getByRole('button', { name: /Bilgi bankası/i })
  // Awaits the asynchronous frontend operation before continuing.
  await knowledgeButton.click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: /kullandığı bilgi kaynakları/i })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await page.keyboard.press('Escape')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: /kullandığı bilgi kaynakları/i })).toBeHidden()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(knowledgeButton).toBeFocused()

  const settingsButton = page.getByRole('button', { name: 'Ayarlar' })
  // Awaits the asynchronous frontend operation before continuing.
  await settingsButton.click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: 'Oyako çalışma ayarları' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await page.keyboard.press('Escape')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: 'Oyako çalışma ayarları' })).toBeHidden()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(settingsButton).toBeFocused()

  const moreButton = page.getByRole('button', { name: 'Daha Fazla...' })
  // Awaits the asynchronous frontend operation before continuing.
  await moreButton.click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('menu', { name: 'Daha Fazla...' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await page.keyboard.press('Escape')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('menu', { name: 'Daha Fazla...' })).toBeHidden()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(moreButton).toBeFocused()
})

// Checks the UI behavior expected by this automated test.
test('composer keyboard shortcuts send questions and preserve multiline drafting', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  const composer = page.getByRole('textbox', { name: 'Soru' })
  // Awaits the asynchronous frontend operation before continuing.
  await composer.click()
  // Awaits the asynchronous frontend operation before continuing.
  await composer.pressSequentially('İlk satır')
  // Awaits the asynchronous frontend operation before continuing.
  await page.keyboard.press('Shift+Enter')
  // Awaits the asynchronous frontend operation before continuing.
  await composer.pressSequentially('ikinci satır')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(composer).toHaveValue('İlk satır\nikinci satır')
  // Awaits the asynchronous frontend operation before continuing.
  await page.keyboard.press('Enter')

  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('heading', { name: 'Siz' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('heading', { name: 'Oyako' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByText('dijital dönüşüm, yapay zekâ')).toBeVisible()
})

// Checks the UI behavior expected by this automated test.
test('knowledge bank renders source and document tables with actions', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: /Bilgi bankası/i }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('heading', { name: "Oyako'nun kullandığı bilgi kaynakları", level: 2, exact: true })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('heading', { name: "Oyako'nun cevap üretirken kullanabileceği belgeler", level: 3, exact: true })).toBeVisible()
  const sourceTable = page.locator('section[aria-labelledby="knowledge-sources-title"] table.knowledge-table')
  const documentTable = page.locator('section[aria-labelledby="knowledge-documents-title"] table.knowledge-table')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(sourceTable).toContainText('Tamam')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(documentTable).toContainText('OYAK Dijital')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('columnheader', { name: 'Arşivlensin' }).first()).toBeVisible()
  const sourceRow = sourceTable.getByRole('row', { name: /Oyak Dijital/ })
  // Awaits the asynchronous frontend operation before continuing.
  await sourceRow.getByLabel('Arşivlensin').selectOption('yes')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(sourceRow.getByLabel('Arşivlensin')).toHaveValue('yes')
  // Awaits the asynchronous frontend operation before continuing.
  await sourceRow.getByLabel('Arşivlensin').selectOption('no')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(sourceRow.getByLabel('Arşivlensin')).toHaveValue('no')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('button', { name: 'Arşivle' })).toHaveCount(0)
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('button', { name: 'Arşivden geri al' })).toHaveCount(0)
  // Awaits the asynchronous frontend operation before continuing.
  await documentTable.getByRole('row', { name: /Yönetilen Hizmetler/ }).getByRole('button', { name: 'Uyarı' }).click()
  const documentLogViewer = page.getByRole('dialog', { name: 'Belge Uyarı Günlüğü' })
  // Awaits the asynchronous frontend operation before continuing.
  await expect(documentLogViewer).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(documentLogViewer.getByRole('heading', { name: 'Yönetilen Hizmetler' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(documentLogViewer.getByText('Sayfa HTTP 404 döndü.')).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await documentLogViewer.getByRole('button', { name: 'Geri' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await sourceRow.getByRole('button', { name: 'Uyarı' }).click()
  const sourceLogViewer = page.getByRole('dialog', { name: 'Kaynak Uyarı Günlüğü' })
  // Awaits the asynchronous frontend operation before continuing.
  await expect(sourceLogViewer).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(sourceLogViewer.getByRole('heading', { name: 'Uyarı veren belgeler' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(sourceLogViewer.getByRole('button', { name: 'Kaynağı Arşivle' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await sourceLogViewer.getByRole('button', { name: 'Geri' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Yeni kaynak' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByLabel('Web Adresi').fill('https://example.com')
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Ekle' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('rowheader', { name: 'Yeni Kaynak' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('row', { name: /Yerel Dosyalar/ }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page
    .getByRole('row', { name: /Kurumsal Dosya/ })
    .getByRole('button', { name: 'Belgeyi Düzenle' })
    .click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: 'Kurumsal Dosya' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByLabel('Belge içeriği').fill('Güncellenmiş belge içeriği OYAK Dijital hizmetlerini açıklar.')
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Kaydet' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('dialog', { name: 'Kurumsal Dosya' })).toBeHidden()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByText('Güncellenmiş belge içeriği OYAK Dijital hizmetlerini açıklar.')).toBeVisible()
})

// Checks that modal forms do not lose focus while the user types and expose primary actions first in the DOM.
test('knowledge bank modal forms preserve typing focus and expose primary actions first', async ({ page }) => {
  await mockOyakoApi(page)
  await page.goto('/')

  await page.getByRole('button', { name: /Bilgi bankası/i }).click()
  await page.getByRole('button', { name: 'Yeni kaynak' }).click()
  const newSourceDialog = page.getByRole('dialog', { name: 'Yeni bilgi kaynağı' })
  await expect(newSourceDialog).toBeVisible()
  await expect(newSourceDialog.locator('.document-editor-actions button').first()).toContainText('Ekle')
  const newSourceName = newSourceDialog.getByLabel('Kaynak ismi')
  await newSourceName.click()
  await newSourceName.pressSequentially('Odak Testi')
  await expect(newSourceName).toBeFocused()
  await expect(newSourceName).toHaveValue('Odak Testi')
  await newSourceDialog.locator('.document-editor-actions').getByRole('button', { name: 'İptal' }).click()

  const sourceRow = page.locator('section[aria-labelledby="knowledge-sources-title"] table.knowledge-table').getByRole('row', { name: /Oyak Dijital/ })
  await sourceRow.getByRole('button', { name: 'Kaynağı Düzenle' }).click()
  const sourceEditor = page.getByRole('dialog', { name: 'Oyak Dijital' })
  await expect(sourceEditor.locator('.document-editor-actions button').first()).toContainText('Kaydet')
  const sourceName = sourceEditor.getByLabel('Kaynak ismi')
  await sourceName.fill('')
  await sourceName.pressSequentially('Oyak Dijital Platform')
  await expect(sourceName).toBeFocused()
  await expect(sourceName).toHaveValue('Oyak Dijital Platform')
  await sourceEditor.locator('.document-editor-actions').getByRole('button', { name: 'İptal' }).click()

  const webDocumentRow = page.locator('section[aria-labelledby="knowledge-documents-title"] table.knowledge-table').getByRole('row', { name: /Dijital Dönüşüm ve Yapay Zekâ/ })
  await webDocumentRow.getByRole('button', { name: 'Bağlantıyı Düzenle' }).click()
  const linkEditor = page.getByRole('dialog', { name: 'Bağlantıyı Düzenle' })
  await expect(linkEditor.locator('.document-editor-actions button').first()).toContainText('Tamam')
  const linkInput = linkEditor.getByLabel('Web Bağlantısı')
  await linkInput.fill('')
  await linkInput.pressSequentially('https://www.oyakdijital.com.tr/focus-test')
  await expect(linkInput).toBeFocused()
  await expect(linkInput).toHaveValue('https://www.oyakdijital.com.tr/focus-test')
  await linkEditor.locator('.document-editor-actions').getByRole('button', { name: 'İptal' }).click()

  await page.getByRole('row', { name: /Yerel Dosyalar/ }).click()
  await page
    .getByRole('row', { name: /Kurumsal Dosya/ })
    .getByRole('button', { name: 'Belgeyi Düzenle' })
    .click()
  const documentEditor = page.getByRole('dialog', { name: 'Kurumsal Dosya' })
  await expect(documentEditor.locator('.document-editor-actions button').first()).toContainText('Kaydet')
  const documentContent = documentEditor.getByLabel('Belge içeriği')
  await documentContent.fill('')
  await documentContent.pressSequentially(' Yeni içerik satırı.')
  await expect(documentContent).toBeFocused()
  await documentEditor.locator('.document-editor-actions').getByRole('button', { name: 'İptal' }).click()
})

// Checks that manual and local source mutations stay lightweight while explicit redownload actions keep the progress dialog.
test('knowledge bank source lifecycles separate lightweight mutations from redownload progress', async ({ page }) => {
  await mockOyakoApi(page)
  await page.goto('/')

  await page.getByRole('button', { name: /Bilgi bankası/i }).click()
  const sourceTable = page.locator('section[aria-labelledby="knowledge-sources-title"] table.knowledge-table')
  const documentTable = page.locator('section[aria-labelledby="knowledge-documents-title"] table.knowledge-table')

  await page.getByRole('button', { name: 'Yeni kaynak' }).click()
  const sourceDialog = page.getByRole('dialog', { name: 'Yeni bilgi kaynağı' })
  await sourceDialog.getByLabel('Kaynak Türü').selectOption('web_links')
  await sourceDialog.getByLabel('Kaynak ismi').fill('Manuel Web Bağlantıları')
  await sourceDialog.getByRole('button', { name: 'Ekle' }).click()
  await expect(sourceDialog).toBeHidden()
  await expect(sourceTable.getByRole('rowheader', { name: 'Manuel Web Bağlantıları' })).toBeVisible()
  await expect(page.getByRole('dialog', { name: /Bilgi Bankası/ })).toHaveCount(0)

  await sourceTable.getByRole('row', { name: /Manuel Web Bağlantıları/ }).click()
  await page.getByRole('button', { name: 'Yeni Web Belgesi' }).click()
  const webDocumentDialog = page.getByRole('dialog', { name: 'Yeni Web Belgesi' })
  await webDocumentDialog.getByLabel('Belge başlığı').fill('Manuel Web Test Belgesi')
  await webDocumentDialog.getByLabel('Web Bağlantısı').fill('https://example.com/manual-test')
  await webDocumentDialog.getByRole('button', { name: 'Ekle' }).click()
  await expect(webDocumentDialog).toBeHidden()
  await expect(documentTable.getByRole('row', { name: /Manuel Web Test Belgesi/ })).toBeVisible()
  await expect(page.getByRole('dialog', { name: /Bilgi Bankası/ })).toHaveCount(0)

  await documentTable
    .getByRole('row', { name: /Manuel Web Test Belgesi/ })
    .getByRole('button', { name: 'Yeniden İndir' })
    .click()
  const redownloadProgress = page.getByRole('dialog', { name: /Bilgi Bankası/ })
  await expect(redownloadProgress).toBeVisible()
  await redownloadProgress.getByRole('button', { name: 'Tamam, Teşekkürler' }).click()
  await expect(redownloadProgress).toBeHidden()

  await page.getByRole('button', { name: 'Yeni kaynak' }).click()
  const localSourceDialog = page.getByRole('dialog', { name: 'Yeni bilgi kaynağı' })
  await localSourceDialog.getByLabel('Kaynak Türü').selectOption('local_files')
  await localSourceDialog.getByLabel('Kaynak ismi').fill('Yerel Test Dosyaları')
  await localSourceDialog.getByRole('button', { name: 'Ekle' }).click()
  await expect(localSourceDialog).toBeHidden()
  await expect(sourceTable.getByRole('rowheader', { name: 'Yerel Test Dosyaları' })).toBeVisible()
  await expect(page.getByRole('dialog', { name: /Bilgi Bankası/ })).toHaveCount(0)

  await sourceTable.getByRole('row', { name: /Yerel Test Dosyaları/ }).click()
  await page.getByRole('button', { name: 'Yeni Belge' }).click()
  const localDocumentDialog = page.getByRole('dialog', { name: 'Yeni Dosya Ekle' })
  const fileChooserPromise = page.waitForEvent('filechooser')
  await localDocumentDialog.getByRole('button', { name: 'Yerel Dosya Yükle', exact: true }).click()
  const fileChooser = await fileChooserPromise
  await fileChooser.setFiles({
    name: 'oyako-test.md',
    mimeType: 'text/markdown',
    buffer: Buffer.from('Oyako test dosyası OYAK Dijital bilgi bankası için temiz metin içerir.'),
  })
  await expect(localDocumentDialog.getByRole('textbox', { name: 'Dosya Başlığı:' })).toHaveValue('Oyako Test Belgesi')
  await localDocumentDialog.getByRole('button', { name: 'Dosyaları Ekle' }).click()
  await expect(page.getByRole('dialog', { name: 'Dosyalar ekleniyor' })).toHaveCount(0)
  await expect(localDocumentDialog).toBeHidden()
  await expect(documentTable.getByRole('row', { name: /Oyako Test Belgesi/ })).toBeVisible()
})

// Checks the UI behavior expected by this automated test.
test('chat flow renders answer and latest suggestions', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('textbox', { name: 'Soru' }).fill('Oyak Dijital hangi hizmetleri sunar?')
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Sor', exact: true }).click()

  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('heading', { name: 'Siz' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('heading', { name: 'Oyako' })).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByText('dijital dönüşüm, yapay zekâ')).toBeVisible()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByLabel('Son cevabın önerilen soruları')).toContainText('Yönetilen hizmetler nelerdir?')
})

// Checks the UI behavior expected by this automated test.
test('chat flow renders actionable contact links from assistant answers', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/chat/stream', async (route) => {
    const answerContent = [
      '<p>OYAK Dijital iletişim bilgileri:</p>',
      '<ul>',
      '<li>E-posta: <a class="oyako-action-link" href="mailto:iletisim@oyakdijital.com.tr" aria-label="E-posta gönder: iletisim@oyakdijital.com.tr">iletisim@oyakdijital.com.tr</a></li>',
      '<li>Telefon: <a class="oyako-action-link" href="tel:+903124441552" aria-label="Telefonla ara: 0 (312) 444 15 52">0 (312) 444 15 52</a></li>',
      '<li>SMS: <a class="oyako-action-link" href="sms:+905321112233" aria-label="SMS gönder: 0532 111 22 33">0532 111 22 33</a></li>',
      '<li>WhatsApp: <a class="oyako-action-link" href="https://wa.me/905321112233" target="_blank" rel="noopener noreferrer" aria-label="WhatsApp ile yaz: 0532 111 22 33">0532 111 22 33</a></li>',
      '<li>Web: <a class="oyako-action-link" href="https://oyakdijital.com.tr/iletisim" target="_blank" rel="noopener noreferrer" aria-label="Bağlantıyı aç: oyakdijital.com.tr/iletisim">oyakdijital.com.tr/iletisim</a></li>',
      '<li>Adres: <a class="oyako-action-link" href="https://www.google.com/maps/search/?api=1&amp;query=Maslak%20Mah.%20Ta%C5%9Fyoncas%C4%B1%20Sk.%20No%3A%201%20Sar%C4%B1yer%20%C4%B0stanbul" target="_blank" rel="noopener noreferrer" aria-label="Haritada aç: Maslak Mah. Taşyoncası Sk. No: 1 Sarıyer İstanbul">Maslak Mah. Taşyoncası Sk. No: 1 Sarıyer İstanbul</a></li>',
      '</ul>',
    ].join('')
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({
      contentType: 'text/event-stream',
      body: [
        'data: {"type":"phase","phase":"asking"}',
        '',
        `data: ${JSON.stringify({ type: 'answer', answer_content: answerContent, suggested_questions: ['OYAK Dijital ile nasıl iletişime geçebilirim?'], source_attributions: [] })}`,
        '',
        'data: [DONE]',
        '',
      ].join('\n'),
    })
  })
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('textbox', { name: 'Soru' }).fill("OYAK Dijital'in iletişim bilgileri nelerdir?")
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Sor', exact: true }).click()

  // Defines a reusable frontend value used by the surrounding module.
  const emailLink = page.getByRole('link', { name: 'E-posta gönder: iletisim@oyakdijital.com.tr' })
  // Defines a reusable frontend value used by the surrounding module.
  const phoneLink = page.getByRole('link', { name: 'Telefonla ara: 0 (312) 444 15 52' })
  // Defines a reusable frontend value used by the surrounding module.
  const smsLink = page.getByRole('link', { name: 'SMS gönder: 0532 111 22 33' })
  // Defines a reusable frontend value used by the surrounding module.
  const whatsAppLink = page.getByRole('link', { name: 'WhatsApp ile yaz: 0532 111 22 33' })
  // Defines a reusable frontend value used by the surrounding module.
  const webLink = page.getByRole('link', { name: 'Bağlantıyı aç: oyakdijital.com.tr/iletisim' })
  // Defines a reusable frontend value used by the surrounding module.
  const addressLink = page.getByRole('link', { name: 'Haritada aç: Maslak Mah. Taşyoncası Sk. No: 1 Sarıyer İstanbul' })

  // Awaits the asynchronous frontend operation before continuing.
  await expect(emailLink).toHaveAttribute('href', 'mailto:iletisim@oyakdijital.com.tr')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(phoneLink).toHaveAttribute('href', 'tel:+903124441552')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(smsLink).toHaveAttribute('href', 'sms:+905321112233')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(whatsAppLink).toHaveAttribute('href', 'https://wa.me/905321112233')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(whatsAppLink).toHaveAttribute('rel', 'noopener noreferrer')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(webLink).toHaveAttribute('href', 'https://oyakdijital.com.tr/iletisim')
  // Awaits the asynchronous frontend operation before continuing.
  await expect(addressLink).toHaveAttribute('href', /google\.com\/maps\/search/)
  // Awaits the asynchronous frontend operation before continuing.
  await emailLink.focus()
  // Awaits the asynchronous frontend operation before continuing.
  await expect(emailLink).toBeFocused()
})

// Checks that dismissed blocking messages do not remain pinned to the main screen or live regions.
test('dismissed message dialogs remove their visual and live-region copy', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.route('**/api/chat/stream', async (route) => {
    // Awaits the asynchronous frontend operation before continuing.
    await route.fulfill({
      contentType: 'text/event-stream',
      body: [
        'data: {"type":"phase","phase":"asking"}',
        '',
        'data: {"type":"error","content":"failed to fetch"}',
        '',
        'data: [DONE]',
        '',
      ].join('\n'),
    })
  })
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Defines a reusable frontend value used by the surrounding module.
  const friendlyMessage = 'Oyako servislerine şu anda ulaşılamıyor. Lütfen bağlantınızı kontrol edip tekrar deneyin.'
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('textbox', { name: 'Soru' }).fill('OYAK Dijital iletişim bilgileri nelerdir?')
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Sor', exact: true }).click()

  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('alertdialog', { name: 'İşlem tamamlanamadı' })).toContainText(friendlyMessage)
  // Verifies the expected behavior for this test scenario.
  await expect(page.locator('[role="status"]').filter({ hasText: friendlyMessage })).toHaveCount(0)
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Mesajı kapat' }).click()

  // Awaits the asynchronous frontend operation before continuing.
  await expect(page.getByRole('alertdialog', { name: 'İşlem tamamlanamadı' })).toBeHidden()
  // Verifies the expected behavior for this test scenario.
  await expect(page.locator('body')).not.toContainText(friendlyMessage)
  // Verifies the expected behavior for this test scenario.
  await expect(page.locator('[role="status"]').filter({ hasText: friendlyMessage })).toHaveCount(0)
})

