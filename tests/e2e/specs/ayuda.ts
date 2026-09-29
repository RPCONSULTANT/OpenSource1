import { expect, type Locator, type Page } from '@playwright/test';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// El paquete es ESM ("type": "module"): sin __dirname; la ruta se resuelve desde este archivo (tests/e2e/specs).
export const CAPTURAS = fileURLToPath(new URL('../../../docs/entregable-parte-1/capturas/', import.meta.url));

export const usuarios = {
  admin: process.env.E2E_ADMIN_USER ?? 'admin',
  supervisor: process.env.E2E_SUPERVISOR_USER ?? 'supervisor',
  ejecutor: process.env.E2E_EJECUTOR_USER ?? 'ejecutor',
} as const;

function contrasena(): string {
  const valor = process.env.E2E_PASSWORD;
  if (!valor) throw new Error('Defina E2E_PASSWORD (o use scripts/con-credenciales.sh, que la lee de .env).');
  return valor;
}

// La contraseña se escribe con evaluate (no con fill): el reporte HTML guarda el valor de cada fill en el título del
// paso, y el reporte se versiona en docs/. Así el secreto no queda en ningún archivo.
export async function escribirContrasena(page: Page, valor: string = contrasena()): Promise<void> {
  await page.locator('#password').evaluate((el, v) => {
    const input = el as HTMLInputElement;
    input.value = v;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  }, valor);
}

export async function iniciarSesion(page: Page, usuario: string): Promise<void> {
  await page.goto('/account/login');
  await page.locator('#userNameOrEmail').fill(usuario);
  await escribirContrasena(page);
  await page.getByRole('button', { name: /iniciar sesión|entrar/i }).click();
  // El menú lateral también existe en /account/login: se espera a salir del login antes de seguir.
  await expect(page).not.toHaveURL(/\/account\/login/);
  await expect(page.getByTestId('nav-menu')).toBeVisible();
}

// R9: toda captura de evidencia va precedida de una aserción de texto visible, para que la imagen muestre lo que dice.
// Acepta un texto (se busca en toda la página) o un Locator ya acotado cuyo texto debe estar visible.
export async function captura(page: Page, nombre: string, textoVisible?: string | RegExp | Locator): Promise<void> {
  if (typeof textoVisible === 'string' || textoVisible instanceof RegExp) {
    await expect(page.getByText(textoVisible).first()).toBeVisible();
  } else if (textoVisible !== undefined) {
    await expect(textoVisible).toBeVisible();
  }
  // Tailwind (CDN) genera estilos tras cada parche del DOM: se espera a la red en reposo y a dos frames.
  await page.waitForLoadState('networkidle').catch(() => undefined);
  await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(() => r(null)))));
  // Imagen estable: sin el anillo de foco que Blazor pone en el <h1> tras navegar (FocusOnNavigate) y sin animaciones
  // de entrada a medias (diálogos, menús). El foco en otros controles (p. ej. la paleta) se conserva.
  await page.evaluate(() => {
    const activo = document.activeElement;
    if (activo instanceof HTMLElement && /^H[1-6]$/.test(activo.tagName)) activo.blur();
  });
  await page
    .waitForFunction(() => document.getAnimations().every((a) => a.playState !== 'running' || a.effect?.getTiming().iterations === Infinity), undefined, { timeout: 3_000 })
    .catch(() => undefined);
  await page.screenshot({ path: path.join(CAPTURAS, `${nombre}.png`) });
}

// Prefijo reconocible para los datos que crea la suite ("E2E ..."), con sufijo único por ejecución.
export const unico = (prefijo: string): string => `${prefijo}-${Date.now().toString(36).toUpperCase()}`;
