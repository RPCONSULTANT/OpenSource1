// Convierte docs/entregable-parte-1/documento-tecnico.md en documento-tecnico.docx (sin portada, justificado, A4).
// Uso (desde tests/e2e): node scripts/entregable/md2docx.cjs [salida.docx]. Skill docx de Claude Code con docx-js.
const fs = require('fs');
const path = require('path');
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, AlignmentType, ImageRun, Table, TableRow, TableCell,
  WidthType, BorderStyle, PageOrientation, ShadingType, Footer, PageNumber, LevelFormat, TableLayoutType,
} = require('docx');

const DIR = path.resolve(__dirname, '../../../../docs/entregable-parte-1');
const md = fs.readFileSync(path.join(DIR, 'documento-tecnico.md'), 'utf8');
const LANG = { value: 'es-DO' };
const FONT = 'Calibri';
const CONTENT_DXA = 11906 - 2 * 1417; // A4 menos márgenes de 2,5 cm
const MAX_W = 605; // px a 96 ppp ≈ 16 cm
const MAX_H = 800; // px ≈ 21 cm
const faltan = [];

function pngSize(buf) { return { w: buf.readUInt32BE(16), h: buf.readUInt32BE(20) }; }

// Inline: **negrita**, *cursiva*, `código`
function runs(text, base = {}) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|`[^`]+`|\*[^*]+\*)/g;
  let last = 0, m;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(new TextRun({ text: text.slice(last, m.index), ...base, language: LANG }));
    const tok = m[0];
    if (tok.startsWith('**')) out.push(new TextRun({ text: tok.slice(2, -2), bold: true, ...base, language: LANG }));
    else if (tok.startsWith('`')) out.push(new TextRun({ text: tok.slice(1, -1), font: 'Consolas', size: (base.size || 22) - 2, ...Object.fromEntries(Object.entries(base).filter(([k]) => k !== 'font' && k !== 'size')), language: LANG }));
    else out.push(new TextRun({ text: tok.slice(1, -1), italics: true, ...base, language: LANG }));
    last = m.index + tok.length;
  }
  if (last < text.length) out.push(new TextRun({ text: text.slice(last), ...base, language: LANG }));
  return out;
}

const body = (text, opts = {}) => new Paragraph({ alignment: AlignmentType.JUSTIFIED, spacing: { after: 120, line: 276 }, children: runs(text), ...opts });

const border = { style: BorderStyle.SINGLE, size: 4, color: '808080' };
const borders = { top: border, bottom: border, left: border, right: border };

function table(rows) {
  const parse = (l) => l.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map((c) => c.trim());
  const header = parse(rows[0]);
  const data = rows.slice(2).map(parse);
  const n = header.length;
  const lens = header.map((h, i) => Math.max(h.length, ...data.map((r) => (r[i] || '').replace(/[`*]/g, '').length)));
  const word = (s) => Math.max(0, ...s.replace(/[`*]/g, '').split(/\s+/).map((w) => w.length));
  const minw = header.map((h, i) => Math.max(word(h), ...data.map((r) => word(r[i] || ''))) + 2);
  const weights = lens.map((l, i) => Math.max(Math.min(Math.max(l, 6), 60), minw[i]));
  const total = weights.reduce((a, b) => a + b, 0);
  const minDxa = minw.map((c) => Math.min(c, 20) * 125 + 160);
  const resto = CONTENT_DXA - minDxa.reduce((a, b) => a + b, 0);
  const extra = lens.map((l) => Math.min(l, 60));
  const totalExtra = extra.reduce((a, b) => a + b, 0);
  const widths = resto > 0
    ? minDxa.map((m, i) => Math.floor(m + resto * (extra[i] / totalExtra)))
    : weights.map((w) => Math.floor((w / total) * CONTENT_DXA));
  widths[n - 1] += CONTENT_DXA - widths.reduce((a, b) => a + b, 0);
  const cell = (text, i, head) => new TableCell({
    borders, width: { size: widths[i], type: WidthType.DXA },
    shading: head ? { fill: 'DCE4F7', type: ShadingType.CLEAR, color: 'auto' } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    children: [new Paragraph({ alignment: AlignmentType.LEFT, children: runs(text, { size: 20, bold: head || undefined }) })],
  });
  return new Table({
    width: { size: CONTENT_DXA, type: WidthType.DXA }, columnWidths: widths, layout: TableLayoutType.FIXED,
    rows: [new TableRow({ tableHeader: true, children: header.map((h, i) => cell(h, i, true)) }),
      ...data.map((r) => new TableRow({ children: header.map((_, i) => cell(r[i] || '', i, false)) }))],
  });
}

let children = [];
const secciones = []; // {landscape, children}
let cerrarTrasPie = false;
const cortar = (landscape) => { secciones.push({ landscape, children }); children = []; };
const lines = md.split('\n');
let i = 0;
while (i < lines.length) {
  const line = lines[i];
  if (!line.trim()) { i++; continue; }
  let m;
  if ((m = line.match(/^(#{1,4}) (.*)$/))) {
    const lvl = m[1].length;
    children.push(new Paragraph({
      alignment: AlignmentType.JUSTIFIED,
      heading: [null, HeadingLevel.TITLE, HeadingLevel.HEADING_1, HeadingLevel.HEADING_2, HeadingLevel.HEADING_3][lvl],
      children: runs(m[2]),
    }));
    i++; continue;
  }
  if ((m = line.match(/^!\[([^\]]*)\]\(([^)]+)\)$/))) {
    const file = path.join(DIR, m[2]);
    if (fs.existsSync(file)) {
      const buf = fs.readFileSync(file);
      const { w, h } = pngSize(buf);
      const ancha = w / h > 3;
      if (ancha) { cortar(false); cerrarTrasPie = true; }
      const maxW = ancha ? 880 : MAX_W, maxH = ancha ? 520 : MAX_H;
      // Los PNG de diagramas se renderizan a 2x: no se amplían por encima de su tamaño natural (w / 2 px CSS).
      let W = m[2].startsWith('diagramas/') ? Math.min(maxW, Math.round(w / 2)) : maxW, H = Math.round((h / w) * W);
      if (H > maxH) { H = maxH; W = Math.round((w / h) * H); }
      children.push(new Paragraph({ alignment: AlignmentType.CENTER, keepNext: true, spacing: { before: 120, after: 60 },
        children: [new ImageRun({ type: 'png', data: buf, transformation: { width: W, height: H }, altText: { title: m[1], description: m[1], name: path.basename(file) } })] }));
    } else {
      faltan.push(m[2]);
      children.push(new Paragraph({ alignment: AlignmentType.CENTER, keepNext: true, children: [new TextRun({ text: `[Imagen pendiente: ${m[2]}]`, color: 'C00000', language: LANG })] }));
    }
    i++; continue;
  }
  if ((m = line.match(/^\*(Figura \d+\..*)\*$/))) {
    children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 240 }, children: runs(m[1], { italics: true, size: 20, color: '404040' }) }));
    if (cerrarTrasPie) { cortar(true); cerrarTrasPie = false; }
    i++; continue;
  }
  if (line.startsWith('|')) {
    const rows = [];
    while (i < lines.length && lines[i].startsWith('|')) rows.push(lines[i++]);
    children.push(table(rows));
    children.push(new Paragraph({ spacing: { after: 120 }, children: [] }));
    continue;
  }
  if (line.startsWith('> ')) {
    const buf = [];
    while (i < lines.length && lines[i].startsWith('>')) buf.push(lines[i++].replace(/^>\s?/, ''));
    children.push(body(buf.join(' '), { indent: { left: 567 }, border: { left: { style: BorderStyle.SINGLE, size: 12, color: '2155D9', space: 8 } } }));
    continue;
  }
  if ((m = line.match(/^(- |\d+\. )(.*)$/))) {
    const ordered = /^\d/.test(m[1]);
    let text = m[2]; i++;
    while (i < lines.length && /^ {2,}\S/.test(lines[i])) text += ' ' + lines[i++].trim();
    children.push(new Paragraph({ alignment: AlignmentType.JUSTIFIED, spacing: { after: 80, line: 276 },
      numbering: { reference: ordered ? 'numeros' : 'vinetas', level: 0 }, children: runs(text) }));
    continue;
  }
  if (line.startsWith('```')) { i++; while (i < lines.length && !lines[i].startsWith('```')) i++; i++; continue; }
  if (/^\*\*[^*]+:\*\*/.test(line)) { children.push(body(line.trim(), { spacing: { after: 40 }, alignment: AlignmentType.LEFT })); i++; continue; }
  const buf = [];
  while (i < lines.length && lines[i].trim() && !/^\*\*[^*]+:\*\*/.test(lines[i]) && !/^(#|!\[|\||> |- |\d+\. |\*Figura|```)/.test(lines[i])) buf.push(lines[i++].trim());
  if (buf.length === 0) { buf.push(lines[i++].trim()); }
  children.push(body(buf.join(' ')));
}

// Párrafos que siguen a los metadatos del título (líneas **Asignatura:** ...) se dejan justificados igualmente.
const doc = new Document({
  creator: 'AxionERP', title: 'AxionERP — Documento técnico (Entregable, Parte 1)', language: 'es-DO',
  styles: {
    default: { document: { run: { font: FONT, size: 22, language: LANG } } },
    paragraphStyles: [
      { id: 'Title', name: 'Title', basedOn: 'Normal', next: 'Normal', run: { font: FONT, size: 32, bold: true, color: '1F3A8A' }, paragraph: { spacing: { after: 200 } } },
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: FONT, size: 30, bold: true, color: '2155D9' }, paragraph: { spacing: { before: 360, after: 160 }, outlineLevel: 0, keepNext: true } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: FONT, size: 25, bold: true, color: '1F3A8A' }, paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: 1, keepNext: true } },
      { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: FONT, size: 22, bold: true, color: '1F3A8A' }, paragraph: { spacing: { before: 200, after: 80 }, outlineLevel: 2, keepNext: true } },
    ],
  },
  numbering: { config: [
    { reference: 'vinetas', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 567, hanging: 283 } } } }] },
    { reference: 'numeros', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 567, hanging: 283 } } } }] },
  ] },
  sections: (cortar(false), secciones).filter((s) => s.children.length).map((s) => ({
    properties: { page: { size: { width: 11906, height: 16838, orientation: s.landscape ? PageOrientation.LANDSCAPE : PageOrientation.PORTRAIT }, margin: { top: 1417, bottom: 1417, left: 1417, right: 1417 } } },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [
      new TextRun({ text: 'Página ', size: 18 }), new TextRun({ children: [PageNumber.CURRENT], size: 18 }),
      new TextRun({ text: ' de ', size: 18 }), new TextRun({ children: [PageNumber.TOTAL_PAGES], size: 18 })] })] }) },
    children: s.children,
  })),
});

Packer.toBuffer(doc).then((b) => {
  const out = process.argv[2] || path.join(DIR, 'documento-tecnico.docx');
  fs.writeFileSync(out, b);
  console.log(`docx escrito: ${out} (${(b.length / 1048576).toFixed(1)} MB); imágenes pendientes: ${faltan.length}`);
  faltan.forEach((f) => console.log('  falta', f));
});
