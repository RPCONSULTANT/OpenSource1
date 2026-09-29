#!/usr/bin/env node
// Verifica la estructura de docs/entregable-parte-1/documento-tecnico.md (Fix-Features D1/D4).
// Uso: node tests/e2e/scripts/verificar-documento.mjs [--completo]
import { readFile } from 'node:fs/promises';

const ruta = new URL('../../../docs/entregable-parte-1/documento-tecnico.md', import.meta.url);
const texto = await readFile(ruta, 'utf8');
const completo = process.argv.includes('--completo');
const fallos = [];

const secciones = [
  '## 1. Planteamiento del problema',
  '## 2. Tecnología y lenguaje',
  '## 3. Arquitectura del sistema',
  '## 4. Diseño de la base de datos',
  '## 5. Persistencia de datos',
  '## 6. Mantenimientos (CRUD)',
  '## 7. Búsquedas y consultas',
  '## 8. Menú y navegación',
  '## 9. Pruebas',
  '## 10. Entrega',
];
for (const s of secciones) if (!texto.includes(s)) fallos.push(`Falta la sección "${s}"`);
if (!texto.startsWith('# AxionERP')) fallos.push('El documento debe empezar por el título (sin portada).');
for (const termino of ['AxionERP', 'Administrador', 'Supervisor', 'Ejecutor', 'Blazor', 'PostgreSQL', 'MediatR', 'Dapper', 'SociosNegocio', 'AspNetUsers']) {
  if (!texto.includes(termino)) fallos.push(`No aparece "${termino}"`);
}
for (const imagen of ['diagramas/arquitectura.png', 'diagramas/modelo-er.png']) {
  if (!texto.includes(imagen)) fallos.push(`No se referencia ${imagen}`);
}
if (completo) {
  if (texto.includes('<!-- D4 -->')) fallos.push('Quedan secciones pendientes de D4.');
  if (!/capturas\/[\w-]+\.png/.test(texto)) fallos.push('No hay capturas referenciadas.');
}

if (fallos.length) {
  console.error(fallos.map((f) => `✗ ${f}`).join('\n'));
  process.exit(1);
}
console.log(`✓ documento-tecnico.md (${completo ? 'completo' : 'pasos 1-5'})`);
