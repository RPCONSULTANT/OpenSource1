// Genera docs/entregable-parte-1/presentacion.pptx (9 diapositivas, español). Skill pptx de Claude Code con pptxgenjs.
// Uso (desde tests/e2e): node scripts/entregable/deck.cjs
const fs = require('fs');
const pptxgen = require('pptxgenjs');
const D = require('path').resolve(__dirname, '../../../../docs/entregable-parte-1');
const XUNIT = process.env.XUNIT_TOTAL || '1684';

const NAVY = '14213D', BRAND = '2155D9', ICE = 'E8EEFC', INK = '1F2937', MUTED = '5B6475', WHITE = 'FFFFFF';
const HEAD = 'Cambria', BODY = 'Calibri';

const pres = new pptxgen();
pres.layout = 'LAYOUT_16x9'; // 10 x 5.625
pres.title = 'AxionERP — Entregable, Parte 1';
pres.lang = 'es-DO';

const title = (s, t, sub) => {
  s.addText(t, { x: 0.5, y: 0.3, w: 9, h: 0.6, fontFace: HEAD, fontSize: 28, bold: true, color: NAVY, margin: 0, isTextBox: true });
  if (sub) s.addText(sub, { x: 0.5, y: 0.88, w: 9, h: 0.35, fontFace: BODY, fontSize: 13, color: MUTED, margin: 0, isTextBox: true });
};
const num = (s, n, x, y, fill = BRAND) => {
  s.addShape(pres.shapes.OVAL, { x, y, w: 0.42, h: 0.42, fill: { color: fill }, line: { color: fill } });
  s.addText(String(n), { x, y, w: 0.42, h: 0.42, align: 'center', valign: 'middle', fontFace: BODY, fontSize: 13, bold: true, color: WHITE, margin: 0, isTextBox: true });
};
const card = (s, x, y, w, h) => s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.08, fill: { color: ICE }, line: { color: ICE } });
const img = (s, file, x, y, w, h) => s.addImage({ path: `${D}/${file}`, x, y, w, h, altText: file });
const pic = (file) => { const b = fs.readFileSync(`${D}/${file}`); return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) }; };
const fit = (file, bw, bh) => { const { w, h } = pic(file); const r = Math.min(bw / w, bh / h); return { w: w * r, h: h * r }; };
const caption = (s, t, x, y, w) => s.addText(t, { x, y, w, h: 0.3, fontFace: BODY, fontSize: 10.5, italic: true, color: MUTED, align: 'center', margin: 0, isTextBox: true });

// 1. Portada de la presentación
{
  const s = pres.addSlide(); s.background = { color: NAVY };
  s.addText('AxionERP', { x: 0.6, y: 1.35, w: 8.8, h: 1.0, fontFace: HEAD, fontSize: 54, bold: true, color: WHITE, margin: 0, isTextBox: true });
  s.addText('Sistema web de gestión empresarial — Entregable, Parte 1', { x: 0.6, y: 2.35, w: 8.8, h: 0.5, fontFace: BODY, fontSize: 20, color: 'CADCFC', margin: 0, isTextBox: true });
  s.addText([
    { text: 'Desarrollo de Software con Tecnologías Propietarias y Open Source I (ISO-615)', options: { breakLine: true } },
    { text: 'Repositorio OpenSource1 · rama Fix-Features' },
  ], { x: 0.6, y: 3.45, w: 8.8, h: 0.8, fontFace: BODY, fontSize: 14, color: WHITE, margin: 0, paraSpaceAfter: 4, isTextBox: true });
  s.addText('ASP.NET Core 10 · Blazor Static SSR · PostgreSQL 17', { x: 0.6, y: 4.75, w: 8.8, h: 0.35, fontFace: BODY, fontSize: 12, color: '8FA6E0', margin: 0, isTextBox: true });
  s.addNotes('Presentar el proyecto, la asignatura y la rama que se entrega.');
}

// 2. Problema y objetivos
{
  const s = pres.addSlide(); s.background = { color: WHITE };
  title(s, 'Problema y objetivos', 'Paso 1 — de hojas de cálculo dispersas a una sola fuente de datos');
  card(s, 0.5, 1.45, 4.2, 3.7);
  s.addText('Situación actual', { x: 0.75, y: 1.6, w: 3.8, h: 0.4, fontFace: HEAD, fontSize: 17, bold: true, color: NAVY, margin: 0, isTextBox: true });
  s.addText([
    { text: 'Clientes, productos, existencias y saldos en hojas de cálculo separadas', options: { bullet: true, breakLine: true } },
    { text: 'Sin control de acceso ni registro de quién cambió qué', options: { bullet: true, breakLine: true } },
    { text: 'Ventas que no descuentan inventario; cobros en otro archivo', options: { bullet: true, breakLine: true } },
    { text: 'Conciliación manual al cierre de cada período', options: { bullet: true } },
  ], { x: 0.75, y: 2.05, w: 3.75, h: 2.9, fontFace: BODY, fontSize: 13.5, color: INK, paraSpaceAfter: 8, valign: 'top', isTextBox: true });
  const obj = [
    ['Mantenimientos', 'Clientes, productos, categorías y usuarios'],
    ['Inventario y ventas', 'Libro de inventario, facturas y notas'],
    ['Cuentas por cobrar', 'Cobros aplicados a facturas pendientes'],
    ['Búsqueda y reportes', 'Filtros por criterio, búsqueda global, PDF y Excel'],
    ['Trazabilidad', 'Documentos append-only y bitácora'],
  ];
  obj.forEach(([h, t], i) => {
    const y = 1.5 + i * 0.73;
    num(s, i + 1, 5.1, y);
    s.addText(h, { x: 5.7, y: y - 0.04, w: 3.8, h: 0.3, fontFace: BODY, fontSize: 14, bold: true, color: NAVY, margin: 0, isTextBox: true });
    s.addText(t, { x: 5.7, y: y + 0.25, w: 3.8, h: 0.3, fontFace: BODY, fontSize: 12, color: MUTED, margin: 0, isTextBox: true });
  });
}

// 3. Usuarios y permisos
{
  const s = pres.addSlide(); s.background = { color: WHITE };
  title(s, 'Usuarios y permisos', 'Tres roles de ASP.NET Core Identity; las mismas políticas en la interfaz y en la API');
  const H = (t) => ({ text: t, options: { bold: true, color: WHITE, fill: { color: NAVY }, align: 'center' } });
  const C = (t) => ({ text: t, options: { align: 'center', color: t === 'Sí' ? '166534' : '9F1239', bold: true } });
  const rows = [
    [H('Rol'), H('Consultar'), H('Agregar'), H('Modificar'), H('Eliminar'), H('Configurar')],
    [{ text: 'Administrador', options: { bold: true } }, C('Sí'), C('Sí'), C('Sí'), C('Sí'), C('Sí')],
    [{ text: 'Supervisor', options: { bold: true } }, C('Sí'), C('No'), C('Sí'), C('No'), C('No')],
    [{ text: 'Ejecutor', options: { bold: true } }, C('Sí'), C('Sí'), C('No'), C('No'), C('No')],
  ];
  s.addTable(rows, { x: 0.5, y: 1.5, w: 9, colW: [2.0, 1.4, 1.4, 1.4, 1.4, 1.4], rowH: 0.45, fontFace: BODY, fontSize: 14, color: INK, border: { type: 'solid', pt: 0.75, color: 'C7D2EA' }, fill: { color: WHITE } });
  const notas = [
    ['Políticas', 'CanConsult, CanAdd, CanModify, CanDelete y CanAdministrar, derivadas del rol.'],
    ['Interfaz', 'Una acción sin permiso no se genera en el HTML: el usuario solo ve lo que puede hacer.'],
    ['API', 'Decide siempre: un POST forzado recibe un 403 real que la interfaz muestra como mensaje.'],
  ];
  notas.forEach(([h, t], i) => {
    const x = 0.5 + i * 3.05;
    card(s, x, 3.6, 2.85, 1.5);
    s.addText(h, { x: x + 0.2, y: 3.72, w: 2.5, h: 0.35, fontFace: HEAD, fontSize: 15, bold: true, color: BRAND, margin: 0, isTextBox: true });
    s.addText(t, { x: x + 0.2, y: 4.08, w: 2.5, h: 0.95, fontFace: BODY, fontSize: 12, color: INK, margin: 0, valign: 'top', isTextBox: true });
  });
}

// 4. Tecnología
{
  const s = pres.addSlide(); s.background = { color: WHITE };
  title(s, 'Tecnología', 'Paso 2 — por qué ASP.NET Core 10 con Blazor Static SSR y PostgreSQL');
  const items = [
    ['ASP.NET Core 10 (LTS)', 'C# y net10.0; soporte a largo plazo, Kestrel eficiente y DTOs tipados compartidos entre API e interfaz.'],
    ['Blazor Static SSR', 'HTML completo desde el servidor, componentes reutilizables y formularios POST con antiforgery; sin WebSocket ni WebAssembly.'],
    ['PostgreSQL 17', 'Dos bases (negocio e identidad), tipos numeric exactos y concurrencia optimista con xmin.'],
    ['EF Core + Dapper + MediatR', 'EF Core para escrituras y 38 migraciones; Dapper para lecturas; MediatR con validación y transacción.'],
  ];
  items.forEach(([h, t], i) => {
    const x = 0.5 + (i % 2) * 4.6, y = 1.5 + Math.floor(i / 2) * 1.85;
    card(s, x, y, 4.4, 1.65);
    num(s, i + 1, x + 0.2, y + 0.2);
    s.addText(h, { x: x + 0.8, y: y + 0.2, w: 3.4, h: 0.42, fontFace: HEAD, fontSize: 16, bold: true, color: NAVY, margin: 0, valign: 'middle', isTextBox: true });
    s.addText(t, { x: x + 0.8, y: y + 0.68, w: 3.4, h: 0.85, fontFace: BODY, fontSize: 12, color: INK, margin: 0, valign: 'top', isTextBox: true });
  });
}

// 5. Arquitectura
{
  const s = pres.addSlide(); s.background = { color: WHITE };
  title(s, 'Arquitectura', 'Paso 3 — capas Onion; el JWT se queda en el servidor');
  // Flujo vertical con formas nativas (legible), resumen de diagramas/arquitectura.png.
  const pasos = [
    ['Usuario (navegador)', 'GET/POST HTML · cookie HttpOnly'],
    ['OpenSource1.Blazor', 'Static SSR · cliente HTTP tipado'],
    ['OpenSource1.Api', 'Controladores REST · JWT · políticas'],
    ['OpenSource1.Application', 'MediatR · validación · transacción'],
    ['OpenSource1.Infrastructure', 'EF Core (escritura) · Dapper (lectura)'],
    ['PostgreSQL 17', 'AxionERP_App · AxionERP_Identity'],
  ];
  const f = { w: 3.6 };
  pasos.forEach(([h, d], i) => {
    const y = 1.35 + i * 0.66;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 0.5, y, w: 3.6, h: 0.5, rectRadius: 0.06, fill: { color: i === 5 ? NAVY : ICE }, line: { color: i === 5 ? NAVY : 'C7D2EA' } });
    s.addText([{ text: h, options: { bold: true, breakLine: true } }, { text: d, options: { fontSize: 10, color: i === 5 ? 'CADCFC' : MUTED } }],
      { x: 0.6, y, w: 3.4, h: 0.5, fontFace: BODY, fontSize: 12, color: i === 5 ? WHITE : NAVY, margin: 0, valign: 'middle', align: 'center', isTextBox: true });
    if (i < 5) s.addShape(pres.shapes.LINE, { x: 2.3, y: y + 0.5, w: 0, h: 0.16, line: { color: BRAND, width: 1.5, endArrowType: 'triangle' } });
  });
  const cols = [
    ['Flujo', 'Blazor → cliente HTTP tipado → controlador → MediatR (Logging, Validation, Transaction) → EF Core / Dapper → PostgreSQL.'],
    ['Capas', 'Core · Application · Infrastructure · Api · Blazor. Las dependencias apuntan al dominio; el controlador delega en MediatR.'],
    ['Seguridad', 'Cookie HttpOnly y SameSite=Strict con sesión de servidor; el navegador nunca ve el token. La API decide con 401/403.'],
  ];
  const x = 0.5 + f.w + 0.4, w = 9.5 - x;
  cols.forEach(([h, t], i) => {
    const y = 1.35 + i * 1.3;
    card(s, x, y, w, 1.15);
    s.addText(h, { x: x + 0.2, y: y + 0.1, w: w - 0.4, h: 0.32, fontFace: HEAD, fontSize: 15, bold: true, color: BRAND, margin: 0, isTextBox: true });
    s.addText(t, { x: x + 0.2, y: y + 0.45, w: w - 0.4, h: 0.62, fontFace: BODY, fontSize: 12, color: INK, margin: 0, valign: 'top', isTextBox: true });
  });
}

// 6. Base de datos
{
  const s = pres.addSlide(); s.background = { color: WHITE };
  title(s, 'Base de datos', 'Paso 4 — 42 tablas generadas con migraciones; entidades del enunciado → tablas reales');
  const dominios = [
    ['Usuarios y roles', 'AspNetUsers · AspNetRoles · AspNetUserRoles', 'Base AxionERP_Identity (ASP.NET Core Identity)'],
    ['Clientes y CxC', 'SociosNegocio · MovimientosCliente · MovimientosClienteDetalle', 'Saldo derivado de cargos, cobros y aplicaciones'],
    ['Productos e inventario', 'Productos · CategoriasProducto · Almacenes · MovimientosProducto · MovimientosValor', 'Existencia y costo derivados de libros append-only'],
    ['Ventas y facturas', 'FacturasVentaBorrador → FacturasVenta · LineasFacturaVenta · LineasIvaFacturaVenta', 'Borrador editable; documento posteado inmutable'],
  ];
  dominios.forEach(([h, tablas, nota], i) => {
    const x = 0.5 + (i % 2) * 3.2, y = 1.35 + Math.floor(i / 2) * 2.0;
    card(s, x, y, 3.0, 1.9);
    s.addText(h, { x: x + 0.2, y: y + 0.12, w: 2.6, h: 0.32, fontFace: HEAD, fontSize: 15, bold: true, color: NAVY, margin: 0, isTextBox: true });
    s.addText(tablas, { x: x + 0.2, y: y + 0.46, w: 2.6, h: 0.95, fontFace: BODY, fontSize: 11, bold: true, color: BRAND, margin: 0, valign: 'top', isTextBox: true });
    s.addText(nota, { x: x + 0.2, y: y + 1.42, w: 2.6, h: 0.42, fontFace: BODY, fontSize: 10.5, italic: true, color: MUTED, margin: 0, valign: 'top', isTextBox: true });
  });
  const claves = [
    ['PK', 'uuid en maestros; Numero (texto) en documentos posteados; bigint en libros'],
    ['Importes', 'numeric(18,4); cantidades numeric(18,6)'],
    ['Concurrencia', 'xmin de PostgreSQL como token optimista'],
    ['Detalle', 'Seis vistas ER por dominio y diccionario de datos en el documento (§4)'],
  ];
  claves.forEach(([h, t], i) => {
    const y = 1.4 + i * 0.95;
    num(s, i + 1, 7.05, y);
    s.addText(h, { x: 7.6, y: y - 0.04, w: 1.9, h: 0.3, fontFace: BODY, fontSize: 13, bold: true, color: NAVY, margin: 0, isTextBox: true });
    s.addText(t, { x: 7.6, y: y + 0.26, w: 1.9, h: 0.6, fontFace: BODY, fontSize: 10.5, color: INK, margin: 0, valign: 'top', isTextBox: true });
  });
}

// 7. Mantenimientos
{
  const s = pres.addSlide(); s.background = { color: WHITE };
  title(s, 'Mantenimientos y patrón de página', 'Pasos 6 y 8 — agregar, consultar, modificar, eliminar y limpiar campos con el mismo patrón');
  img(s, 'capturas/12-cliente-seleccion-acciones.png', 0.5, 1.4, 4.4, 2.75);
  img(s, 'capturas/13-cliente-menu-crear.png', 5.1, 1.4, 4.4, 2.75);
  caption(s, 'Fila seleccionada (?sel=): Editar y Eliminar habilitados', 0.5, 4.2, 4.4);
  caption(s, 'Crear ▾ desde un cliente: factura, nota de crédito, cobro', 5.1, 4.2, 4.4);
  s.addText([
    { text: 'PageToolbar', options: { bold: true, color: BRAND } }, { text: ': + Nuevo, Crear ▾, Ver ▾, Editar, Eliminar según permisos.   ' },
    { text: 'EntityFormPage', options: { bold: true, color: BRAND } }, { text: ': Guardar, Limpiar campos y Cancelar, sin JavaScript.' },
  ], { x: 0.5, y: 4.6, w: 9, h: 0.55, fontFace: BODY, fontSize: 12.5, color: INK, margin: 0, isTextBox: true });
}

// 8. Búsqueda
{
  const s = pres.addSlide(); s.background = { color: WHITE };
  title(s, 'Búsqueda global y paleta Ctrl+K', 'Paso 7 — módulos y registros; funciona también sin JavaScript');
  img(s, 'capturas/24-paleta-modulos.png', 0.5, 1.4, 4.4, 2.75);
  img(s, 'capturas/25-buscar-resultados.png', 5.1, 1.4, 4.4, 2.75);
  caption(s, 'Paleta Ctrl+K: módulos sin acentos ni mayúsculas', 0.5, 4.2, 4.4);
  caption(s, '/buscar?q=cliente: módulos, clientes y facturas', 5.1, 4.2, 4.4);
  s.addText('Registros vía GET /api/busqueda (2–100 caracteres, ILIKE escapado); la paleta pasa por el servidor Blazor, así que el JWT no llega al navegador. Filtros por listado con *, || y &&.', { x: 0.5, y: 4.6, w: 9, h: 0.55, fontFace: BODY, fontSize: 12.5, color: INK, margin: 0, isTextBox: true });
}

// 9. Pruebas y cierre
{
  const s = pres.addSlide(); s.background = { color: NAVY };
  s.addText('Pruebas y resultados', { x: 0.5, y: 0.3, w: 9, h: 0.6, fontFace: HEAD, fontSize: 28, bold: true, color: WHITE, margin: 0, isTextBox: true });
  s.addText('Paso 9 — la misma versión del código que se muestra en las capturas', { x: 0.5, y: 0.88, w: 9, h: 0.35, fontFace: BODY, fontSize: 13, color: 'CADCFC', margin: 0, isTextBox: true });
  const stats = [
    [XUNIT, 'pruebas xUnit en verde', 'unitarias, API contra PostgreSQL real y SSR'],
    ['15 / 15', 'pruebas E2E Playwright', '14 en la ejecución principal + API caída aparte'],
    ['0', 'avisos de compilación', 'dotnet build test.slnx'],
  ];
  stats.forEach(([n, l, d], i) => {
    const x = 0.5 + i * 3.05;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 1.5, w: 2.85, h: 2.1, rectRadius: 0.08, fill: { color: '1F3160' }, line: { color: '1F3160' } });
    s.addText(n, { x, y: 1.6, w: 2.85, h: 0.95, align: 'center', fontFace: HEAD, fontSize: 44, bold: true, color: WHITE, margin: 0, isTextBox: true });
    s.addText(l, { x: x + 0.15, y: 2.6, w: 2.55, h: 0.35, align: 'center', fontFace: BODY, fontSize: 14, bold: true, color: 'CADCFC', margin: 0, isTextBox: true });
    s.addText(d, { x: x + 0.15, y: 2.97, w: 2.55, h: 0.5, align: 'center', fontFace: BODY, fontSize: 11, color: 'A9B8DE', margin: 0, isTextBox: true });
  });
  s.addText([
    { text: 'Cubierto: ', options: { bold: true } },
    { text: 'altas, modificaciones, eliminaciones, validaciones, búsquedas y errores 403, 404 y API caída.', options: { breakLine: true } },
    { text: 'Entrega: ', options: { bold: true } },
    { text: 'documento-tecnico.docx, diagramas/, capturas/, evidencias/, guion-demo.md.' },
  ], { x: 0.5, y: 3.9, w: 9, h: 0.8, fontFace: BODY, fontSize: 13, color: WHITE, margin: 0, paraSpaceAfter: 6, isTextBox: true });
  s.addText('Gracias. Siguiente: demostración en vivo.', { x: 0.5, y: 4.85, w: 9, h: 0.4, fontFace: HEAD, fontSize: 16, italic: true, color: '8FA6E0', margin: 0, isTextBox: true });
}

pres.writeFile({ fileName: `${D}/presentacion.pptx` }).then((f) => console.log('pptx escrito:', f));
