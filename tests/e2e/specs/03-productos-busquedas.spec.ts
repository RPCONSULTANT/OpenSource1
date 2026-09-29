import { expect, test, type Page } from '@playwright/test';
import { captura, eliminarPrimeraFila, iniciarSesion, mostrarColumnaFinal, unico, usuarios } from './ayuda';

// Alta de producto desde /productos/nuevo; si se indica, captura el formulario lleno antes de guardar.
async function crearProducto(
  page: Page,
  datos: { codigo: string; nombre: string; precio: string; categoriaCodigo?: string },
  capturaAlta?: string,
): Promise<void> {
  await page.goto('/productos/nuevo');
  await page.locator('[name="Input.Codigo"]').fill(datos.codigo);
  await page.locator('[name="Input.Nombre"]').fill(datos.nombre);
  await page.locator('[name="Input.PrecioVentaTexto"]').fill(datos.precio);
  if (datos.categoriaCodigo) {
    const opcion = page.locator('[name="Input.CategoriaId"] option', { hasText: datos.categoriaCodigo });
    await page.locator('[name="Input.CategoriaId"]').selectOption((await opcion.first().getAttribute('value')) ?? '');
  }
  await expect(page.locator('.validation-message')).toHaveCount(0);
  if (capturaAlta) await captura(page, capturaAlta, page.getByTestId('entity-form-page').getByText(/nuevo producto/i).first());
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);
}

test('productos: agregar, buscar (código, nombre, categoría, precio, estado), modificar y eliminar', async ({ page }) => {
  // Dos productos E2E de la misma ejecución: A coincide con cada búsqueda y B no (salvo el estado de existencia, que
  // ninguno tiene), para que la evidencia muestre que el filtro excluye registros.
  const etiqueta = unico('E2E').slice(0, 12);
  const a = { codigo: `${etiqueta}-A`, nombre: `E2E Producto ${etiqueta} A`, precio: '125.50' };
  const b = { codigo: `${etiqueta}-B`, nombre: `E2E Producto ${etiqueta} B`, precio: '99.00' };
  const categoriaB = `${etiqueta}-C`;
  await iniciarSesion(page, usuarios.admin);

  // Categoría propia para B (A queda en la categoría por defecto, GENERAL).
  await page.goto('/categorias-producto/nuevo');
  await page.locator('[name="SaveInput.Codigo"]').fill(categoriaB);
  await page.locator('[name="SaveInput.Nombre"]').fill(`E2E Categoría ${categoriaB}`);
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=created/);

  await crearProducto(page, a, '33-producto-alta');
  await crearProducto(page, { ...b, categoriaCodigo: categoriaB });

  const busquedas: [string, string, boolean][] = [
    ['16-producto-busqueda-codigo', `/productos?view=list&showFilters=true&filters=codigo&codigo=${a.codigo}`, true],
    ['17-producto-busqueda-nombre', `/productos?view=list&showFilters=true&nombre=${encodeURIComponent(a.nombre)}`, true],
    ['18-producto-busqueda-categoria', `/productos?view=list&showFilters=true&filters=categoriaCodigo&categoriaCodigo=GENERAL&nombre=${etiqueta}`, true],
    ['19-producto-busqueda-precio', `/productos?view=list&showFilters=true&filters=precioVenta&precioVenta=125.50&nombre=${etiqueta}`, true],
    // Ninguno de los dos tiene existencia: los dos coinciden con "Sin existencia".
    ['20-producto-busqueda-estado', `/productos?view=list&showFilters=true&filters=estado&estado=without&nombre=${etiqueta}`, false],
  ];
  for (const [nombre, url, excluyeB] of busquedas) {
    await page.goto(url);
    await expect(page.getByRole('cell', { name: a.codigo, exact: true })).toBeVisible();
    if (excluyeB) await expect(page.getByRole('cell', { name: b.codigo, exact: true })).toHaveCount(0);
    else await expect(page.getByRole('cell', { name: b.codigo, exact: true })).toBeVisible();
    await mostrarColumnaFinal(page);
    await captura(page, nombre, page.getByRole('cell', { name: a.codigo, exact: true }));
  }

  // Modificar A: nuevo precio de venta.
  await page.goto(`/productos?view=list&filters=codigo&codigo=${a.codigo}`);
  await page.getByTestId('seleccionar-fila').first().click();
  await expect(page).toHaveURL(/sel=/);
  await page.getByTestId('accion-editar').click();
  await expect(page.getByTestId('entity-form-page')).toBeVisible();
  await page.locator('[name="Input.PrecioVentaTexto"]').fill('130.00');
  await page.getByTestId('guardar').click();
  await expect(page).toHaveURL(/ok=updated/);
  await page.evaluate(() => window.scrollTo(0, 0));
  await captura(page, '34-producto-modificado', page.getByRole('cell', { name: '130.00', exact: true }));

  // Eliminar A (con captura de la confirmación) y, como limpieza, B y su categoría.
  await eliminarPrimeraFila(page, '35-producto-eliminar-confirmacion');
  await page.goto(`/productos?view=list&filters=codigo&codigo=${b.codigo}`);
  await eliminarPrimeraFila(page);
  await page.goto(`/categorias-producto?codigo=${categoriaB}`);
  await eliminarPrimeraFila(page);
});
