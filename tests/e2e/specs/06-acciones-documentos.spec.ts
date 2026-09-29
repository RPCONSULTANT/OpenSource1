import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, unico, usuarios } from './ayuda';

test('desde un cliente: Crear ▾ Factura abre la nueva factura con el cliente precargado', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/clientes?view=list');
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('menu-crear').locator('summary').click();
  await page.getByTestId('menu-crear').getByRole('link', { name: 'Factura', exact: true }).click();
  await expect(page).toHaveURL(/\/facturas-venta\/nueva\?.*socioId=/);
  await expect(page.getByTestId('cliente-precargado')).toBeVisible();
  await captura(page, '26-nueva-factura-precargada', /nueva factura/i);
});

test('facturas filtradas por cliente y ficha de producto con acciones', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  // Producto E2E propio (el catálogo puede estar vacío); se elimina al final.
  const codigo = unico('E2E').slice(0, 14);
  await page.goto('/productos/nuevo');
  await page.locator('[name="Input.Codigo"]').fill(codigo);
  await page.locator('[name="Input.Nombre"]').fill(`E2E Producto ${codigo}`);
  await page.locator('[name="Input.PrecioVentaTexto"]').fill('80.00');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);
  await page.goto(`/productos?view=list&filters=codigo&codigo=${codigo}`);
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('menu-ver').locator('summary').click();
  await expect(page.getByTestId('menu-ver').getByRole('link', { name: 'Existencias' })).toBeVisible();
  await captura(page, '27-producto-menu-ver', page.getByTestId('menu-ver').getByRole('link', { name: 'Movimientos de producto' }));
  await page.getByTestId('menu-ver').locator('summary').click();
  await page.getByTestId('accion-eliminar').click();
  await page.getByRole('button', { name: 'Sí, eliminar' }).click();
  await expect(page).toHaveURL(/ok=deleted/);

  // Cliente con alguna factura posteada (el de la primera factura del listado), para que el filtro muestre resultados.
  await page.goto('/facturas-venta');
  const filaFactura = page.getByTestId('tabla-facturas').locator('tbody tr').filter({ has: page.getByTestId('seleccionar-fila') }).first();
  const codigoCliente = (await filaFactura.count()) > 0
    ? (await filaFactura.locator('td').nth(2).innerText()).trim().split(/\s+/)[0]
    : '';
  await page.goto(codigoCliente ? `/clientes?view=list&filters=codigo&codigo=${encodeURIComponent(codigoCliente)}` : '/clientes?view=list');
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('menu-ver').locator('summary').click();
  await page.getByTestId('menu-ver').getByRole('link', { name: 'Facturas del cliente' }).click();
  await expect(page).toHaveURL(/\/facturas-venta\?.*socioId=/);
  await expect(page.getByTestId('filtro-cliente')).toBeVisible();
  if (codigoCliente) await expect(page.getByTestId('tabla-facturas').getByText(codigoCliente).first()).toBeVisible();
  await captura(page, '28-facturas', page.getByTestId('filtro-cliente'));
});
