import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, unico, usuarios } from './ayuda';

test('categorías: agregar, modificar y eliminar', async ({ page }) => {
  const codigo = unico('E2E').slice(0, 20);
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/categorias-producto/nuevo');
  await page.locator('[name="SaveInput.Codigo"]').fill(codigo);
  await page.locator('[name="SaveInput.Nombre"]').fill(`E2E Categoría ${codigo}`);
  await captura(page, '21-categoria-alta', /nueva categoría/i);
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);
  await page.goto(`/categorias-producto?codigo=${codigo}`);
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('accion-editar').click();
  await page.locator('[name="UpdateInput.Nombre"]').fill(`E2E Categoría ${codigo} editada`);
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=updated/);
  await expect(page.getByText(`E2E Categoría ${codigo} editada`).first()).toBeVisible();
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('accion-eliminar').click();
  await page.getByRole('button', { name: 'Sí, eliminar' }).click();
  await expect(page).toHaveURL(/ok=deleted/);
});

test('usuarios: alta en página propia y ficha para rol/estado', async ({ page }) => {
  const correo = `${unico('e2e').toLowerCase()}@e2e.local`;
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/admin/users');
  await page.getByTestId('accion-nuevo').click();
  await page.locator('[name="CreateInput.FullName"]').fill('E2E Usuario');
  await page.locator('[name="CreateInput.Email"]').fill(correo);
  await page.locator('[name="CreateInput.Password"]').fill('E2e#Clave123');
  await captura(page, '22-usuario-alta', /nuevo usuario/i);
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/\/admin\/users\?ok=/);
  await captura(page, '23-usuarios-listado', correo);

  // Ficha del usuario (rol y estado se gestionan aquí) y limpieza del usuario E2E.
  const tarjeta = page.getByText(correo).first().locator('xpath=ancestor::div[.//a[contains(., "Ver Perfil")]][1]');
  await tarjeta.getByRole('link', { name: 'Ver Perfil' }).click();
  await expect(page).toHaveURL(/\/admin\/users\/[0-9a-f-]{36}/);
  await expect(page.getByText('Estado de la cuenta')).toBeVisible();
  await page.goto(`${new URL(page.url()).pathname}?confirm=delete`);
  await page.getByRole('button', { name: 'Sí, eliminar' }).click();
  await expect(page).toHaveURL(/\/admin\/users(\?|$)/);
  await expect(page.getByText(correo)).toHaveCount(0);
});
