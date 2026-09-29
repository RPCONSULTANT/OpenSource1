import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, unico, usuarios } from './ayuda';

test('clientes: agregar, consultar, modificar, eliminar y validación', async ({ page }) => {
  const nombre = unico('E2E Cliente');
  await iniciarSesion(page, usuarios.admin);

  await page.goto('/clientes');
  await page.getByTestId('accion-nuevo').click();
  await expect(page.getByTestId('entity-form-page')).toBeVisible();
  await page.getByTestId('guardar').click();
  const primerError = page.locator('.validation-message').first();
  await primerError.scrollIntoViewIfNeeded();
  await expect(primerError).not.toBeEmpty();
  await captura(page, '10-cliente-validacion', primerError);

  await page.locator('[name="Input.NombreComercial"]').fill(nombre);
  await page.locator('[name="Input.Email"]').fill(`${nombre.replace(/\s/g, '').toLowerCase()}@e2e.local`);
  await page.locator('[name="Input.NombreComercial"]').scrollIntoViewIfNeeded();
  await captura(page, '11-cliente-alta', page.getByTestId('entity-form-page').getByText(/nuevo cliente/i).first());
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/\/clientes\?.*ok=created/);

  await page.goto(`/clientes?view=list&nombre=${encodeURIComponent(nombre)}`);
  await expect(page.getByRole('cell', { name: nombre })).toBeVisible();
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await captura(page, '12-cliente-seleccion-acciones', nombre);
  await page.getByTestId('menu-crear').locator('summary').click();
  await expect(page.getByTestId('menu-crear').getByRole('link', { name: 'Factura', exact: true })).toBeVisible();
  await captura(page, '13-cliente-menu-crear', page.getByTestId('menu-crear').getByRole('link', { name: 'Nota de crédito' }));

  await page.getByTestId('menu-crear').locator('summary').click();
  await page.getByTestId('accion-editar').click();
  await expect(page.getByTestId('entity-form-page')).toBeVisible();
  await page.locator('[name="Input.Telefono"]').fill('809-555-0101');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=updated/);
  // Tras volver del editor largo la página conserva el desplazamiento: se sube para mostrar la barra y la fila.
  await page.evaluate(() => window.scrollTo(0, 0));
  await captura(page, '14-cliente-modificado', page.getByRole('cell', { name: '809-555-0101' }));

  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('accion-eliminar').click();
  await captura(page, '15-cliente-eliminar-confirmacion', 'Sí, eliminar');
  await page.getByRole('button', { name: 'Sí, eliminar' }).click();
  await expect(page).toHaveURL(/ok=deleted/);
  await expect(page.getByRole('cell', { name: nombre })).toHaveCount(0);
});
