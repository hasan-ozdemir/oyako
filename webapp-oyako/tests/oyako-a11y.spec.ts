// Codex developer note: Explains the purpose and flow of webapp-oyako/tests/oyako-a11y.spec.ts for maintainers.
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { mockOyakoApi } from './mockApi'

// Checks the UI behavior expected by this automated test.
test('main screen has no critical accessibility violations', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Defines a reusable frontend value used by the surrounding module.
  const results = await new AxeBuilder({ page })
    .exclude('.assistant-html')
    .analyze()

  // Checks the UI behavior expected by this automated test.
  expect(results.violations.filter((violation) => violation.impact === 'critical' || violation.impact === 'serious')).toEqual([])
})

// Checks the UI behavior expected by this automated test.
test('knowledge bank and settings dialogs have no critical accessibility violations', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: /Bilgi bankası/i }).click()
  // Creates the browser or application object needed for this step.
  let results = await new AxeBuilder({ page }).analyze()
  // Checks the UI behavior expected by this automated test.
  expect(results.violations.filter((violation) => violation.impact === 'critical' || violation.impact === 'serious')).toEqual([])

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Geri' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Ayarlar' }).click()
  // Creates the browser or application object needed for this step.
  results = await new AxeBuilder({ page }).analyze()
  // Checks the UI behavior expected by this automated test.
  expect(results.violations.filter((violation) => violation.impact === 'critical' || violation.impact === 'serious')).toEqual([])
})

// Checks the UI behavior expected by this automated test.
test('help and document editor dialogs have no serious accessibility violations', async ({ page }) => {
  // Awaits the asynchronous frontend operation before continuing.
  await mockOyakoApi(page)
  // Awaits the asynchronous frontend operation before continuing.
  await page.goto('/')

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Daha Fazla...' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('menuitem', { name: 'Yardım' }).click()
  // Creates the browser or application object needed for this step.
  let results = await new AxeBuilder({ page }).analyze()
  // Checks the UI behavior expected by this automated test.
  expect(results.violations.filter((violation) => violation.impact === 'critical' || violation.impact === 'serious')).toEqual([])

  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: 'Geri' }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('button', { name: /Bilgi bankası/i }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page.getByRole('row', { name: /Yerel Dosyalar/ }).click()
  // Awaits the asynchronous frontend operation before continuing.
  await page
    .getByRole('row', { name: /Kurumsal Dosya/ })
    .getByRole('button', { name: 'Belgeyi Düzenle' })
    .click()
  // Creates the browser or application object needed for this step.
  results = await new AxeBuilder({ page }).analyze()
  // Checks the UI behavior expected by this automated test.
  expect(results.violations.filter((violation) => violation.impact === 'critical' || violation.impact === 'serious')).toEqual([])
})
