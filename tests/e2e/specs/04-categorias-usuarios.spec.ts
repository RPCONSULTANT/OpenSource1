import { expect, test } from '@playwright/test';
import { captura, eliminarPrimeraFila, iniciarSesion, unico, usuarios } from './ayuda';

test('categorías: agregar, consultar, modificar y eliminar', async ({ page }) => {
  const codigo = unico('E2E').slice(0, 20);
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/categorias-producto/nuevo');
  await page.locator('[name="SaveInput.Codigo"]').fill(codigo);
  await page.locator('[name="SaveInput.Nombre"]').fill(`E2E Categoría ${codigo}`);
  await captura(page, '21-categoria-alta', /nueva categoría/i);
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);
  await page.goto(`/categorias-producto?codigo=${codigo}`);
  await captura(page, '36-categorias-listado', page.getByRole('cell', { name: codigo, exact: true }));
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('accion-editar').click();
  await page.locator('[name="UpdateInput.Nombre"]').fill(`E2E Categoría ${codigo} editada`);
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=updated/);
  await captura(page, '37-categoria-modificada', page.getByText(`E2E Categoría ${codigo} editada`));
  await eliminarPrimeraFila(page, '38-categoria-eliminar-confirmacion');
  await expect(page.getByRole('cell', { name: codigo, exact: true })).toHaveCount(0);
});

test('usuarios: alta, ficha, desactivar y eliminar', async ({ page }) => {
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

  // Ficha del usuario: rol y estado se gestionan aquí (los usuarios no tienen Eliminar en la barra, R13).
  const tarjeta = page.getByText(correo).first().locator('xpath=ancestor::div[.//a[contains(., "Ver Perfil")]][1]');
  await tarjeta.getByRole('link', { name: 'Ver Perfil' }).click();
  await expect(page).toHaveURL(/\/admin\/users\/[0-9a-f-]{36}/);
  await expect(page.getByText('Estado de la cuenta')).toBeVisible();
  await captura(page, '39-usuario-perfil', page.getByText(correo).first());

  // Desactivar la cuenta (confirmación) y comprobar el nuevo estado.
  await page.getByRole('link', { name: 'Desactivar cuenta' }).click();
  await page.getByRole('button', { name: 'Sí, cambiar estado' }).click();
  const inactiva = page.getByText('La cuenta está', { exact: false }).filter({ hasText: 'inactiva' });
  await expect(inactiva).toBeVisible();
  await inactiva.scrollIntoViewIfNeeded();
  await captura(page, '40-usuario-rol-o-estado-cambiado', inactiva);

  // Eliminar el usuario E2E desde su ficha (confirmación) como limpieza.
  await page.locator('a[href$="?confirm=delete"]').click();
  const confirmar = page.getByRole('button', { name: 'Sí, eliminar' });
  await captura(page, '41-usuario-eliminar-o-desactivar', confirmar);
  await confirmar.click();
  await expect(page).toHaveURL(/\/admin\/users(\?|$)/);
  await expect(page.getByText(correo)).toHaveCount(0);
});
