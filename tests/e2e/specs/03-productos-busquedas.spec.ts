import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, unico, usuarios } from './ayuda';

test('productos: alta y búsquedas por código, nombre, categoría, precio y estado', async ({ page }) => {
  const codigo = unico('E2E').slice(0, 14);
  await iniciarSesion(page, usuarios.admin);

  await page.goto('/productos/nuevo');
  await page.locator('[name="Input.Codigo"]').fill(codigo);
  await page.locator('[name="Input.Nombre"]').fill(`E2E Producto ${codigo}`);
  await page.locator('[name="Input.PrecioVentaTexto"]').fill('125.50');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);

  const busquedas: [string, string][] = [
    ['16-producto-busqueda-codigo', `/productos?view=list&showFilters=true&filters=codigo&codigo=${codigo}`],
    ['17-producto-busqueda-nombre', `/productos?view=list&nombre=${encodeURIComponent(`E2E Producto ${codigo}`)}`],
    ['18-producto-busqueda-categoria', `/productos?view=list&showFilters=true&filters=categoriaCodigo&categoriaCodigo=GENERAL&nombre=${codigo}`],
    ['19-producto-busqueda-precio', `/productos?view=list&showFilters=true&filters=precioVenta&precioVenta=125.50&nombre=${codigo}`],
    ['20-producto-busqueda-estado', `/productos?view=list&showFilters=true&filters=estado&estado=without&nombre=${codigo}`],
  ];
  for (const [nombre, url] of busquedas) {
    await page.goto(url);
    await captura(page, nombre, codigo);
  }

  // Limpieza: el producto E2E no tiene movimientos, así que se elimina desde la propia lista.
  await page.goto(`/productos?view=list&showFilters=true&filters=codigo&codigo=${codigo}`);
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('accion-eliminar').click();
  await page.getByRole('button', { name: 'Sí, eliminar' }).click();
  await expect(page).toHaveURL(/ok=deleted/);
});
