import { expect, test } from '@playwright/test';
import { captura, escribirContrasena, iniciarSesion, usuarios } from './ayuda';

test('403: el ejecutor no puede abrir Usuarios', async ({ page }) => {
  test.skip(process.env.E2E_API_CAIDA === '1', 'Requiere la API en marcha.');
  await iniciarSesion(page, usuarios.ejecutor);
  await page.goto('/admin/users');
  await expect(page.getByText(/no tiene autorización|acceso denegado/i).first()).toBeVisible();
  await captura(page, '29-error-403', /acceso denegado/i);
});

test('404: grupo inexistente', async ({ page }) => {
  test.skip(process.env.E2E_API_CAIDA === '1', 'Requiere la API en marcha.');
  await iniciarSesion(page, usuarios.admin);
  const respuesta = await page.goto('/modulos/noexiste');
  expect(respuesta?.status()).toBe(404);
  await captura(page, '30-error-404', /no encontrad/i);
});

// Ejecutar aparte con la API detenida: docker compose stop api (y docker compose start api al terminar).
test('@api-caida: la página responde con aviso si la API no está disponible', async ({ page }) => {
  test.skip(process.env.E2E_API_CAIDA !== '1', 'Solo con la API detenida (E2E_API_CAIDA=1).');
  await page.goto('/account/login');
  await page.locator('#userNameOrEmail').fill(usuarios.admin);
  await escribirContrasena(page);
  await page.getByRole('button', { name: /iniciar sesión|entrar/i }).click();
  await captura(page, '31-error-api-caida', /servicio de autenticación no está disponible/i);
});
