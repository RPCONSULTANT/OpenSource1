#!/usr/bin/env node
// Renderiza un .mmd a SVG y PNG con mermaid + el chromium de Playwright (sin mmdc ni graphviz). Fix-Features D2.
// Uso: node scripts/render-mermaid.mjs <entrada.mmd> <salida-sin-extension>
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { chromium } from '@playwright/test';

const require = createRequire(import.meta.url);
const [, , entrada, salida] = process.argv;
if (!entrada || !salida) {
  console.error('Uso: node scripts/render-mermaid.mjs <entrada.mmd> <salida-sin-extension>');
  process.exit(2);
}

const codigo = await readFile(entrada, 'utf8');
const navegador = await chromium.launch();
try {
  const pagina = await navegador.newPage({ deviceScaleFactor: 2, viewport: { width: 1800, height: 1200 } });
  await pagina.setContent('<!doctype html><html><body style="margin:0;background:#ffffff"><div id="d"></div></body></html>');
  await pagina.addScriptTag({ path: require.resolve('mermaid/dist/mermaid.min.js') });
  const svg = await pagina.evaluate(async (texto) => {
    window.mermaid.initialize({
      startOnLoad: false, theme: 'neutral', securityLevel: 'strict',
      er: { useMaxWidth: false }, flowchart: { useMaxWidth: false, htmlLabels: true },
    });
    const { svg: resultado } = await window.mermaid.render('diagrama', texto);
    document.getElementById('d').innerHTML = resultado;
    return resultado;
  }, codigo);
  await writeFile(`${salida}.svg`, svg, 'utf8');
  await pagina.locator('#d svg').screenshot({ path: `${salida}.png` });
  console.log(`OK ${salida}.svg y ${salida}.png`);
} finally {
  await navegador.close();
}
