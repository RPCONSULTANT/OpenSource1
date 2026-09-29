// app.search.js — Paleta de búsqueda Ctrl/Cmd+K o "/" (Fix-Features A4). Mejora progresiva: sin este script la caja sigue
// siendo un <form method="get" action="/buscar">. Módulos: isla JSON #modulos-data (solo los visibles para el usuario,
// renderizada en servidor). Registros: GET /buscar/sugerencias?q= (el host llama a la API con la sesión; el JWT no sale).
(() => {
  'use strict';

  const DEBOUNCE_MS = 250;
  const MIN_CARACTERES = 2;
  const MAX_MODULOS = 8;
  const ERROR_REGISTROS = 'No fue posible buscar registros en este momento.';

  let modulos = [];
  let opciones = [];
  let activo = -1;
  let temporizador = 0;
  let controlador = null;
  let ultimaConsulta = '';

  const porId = (id) => document.getElementById(id);
  const normalizar = (texto) => (texto || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();

  function elementos() {
    const input = porId('busqueda-global');
    const panel = porId('busqueda-paleta');
    const lista = porId('busqueda-paleta-lista');
    return input && panel && lista ? { input, panel, lista } : null;
  }

  function leerModulos() {
    const isla = porId('modulos-data');
    if (!isla) return [];
    try {
      const datos = JSON.parse(isla.textContent || '[]');
      return Array.isArray(datos) ? datos : [];
    } catch {
      return [];
    }
  }

  function filtrarModulos(consulta) {
    const q = normalizar(consulta);
    if (!q) return modulos.slice(0, MAX_MODULOS);
    return modulos
      .filter((m) => normalizar(`${m.titulo} ${m.grupo} ${m.palabras}`).includes(q))
      .slice(0, MAX_MODULOS);
  }

  function estaAbierta() {
    const el = elementos();
    return !!el && !el.panel.hidden;
  }

  function abrir() {
    const el = elementos();
    if (!el) return;
    el.panel.hidden = false;
    el.input.setAttribute('aria-expanded', 'true');
    el.input.focus();
    actualizar(el.input.value);
  }

  function cerrar() {
    const el = elementos();
    if (!el) return;
    el.panel.hidden = true;
    el.input.setAttribute('aria-expanded', 'false');
    el.input.removeAttribute('aria-activedescendant');
    activo = -1;
    window.clearTimeout(temporizador);
    if (controlador) controlador.abort();
  }

  function encabezado(lista, texto) {
    const li = document.createElement('li');
    li.setAttribute('role', 'presentation');
    li.className = 'px-3 pt-3 pb-1 text-[10px] font-bold uppercase tracking-widest text-slate-400';
    li.textContent = texto;
    lista.appendChild(li);
  }

  function aviso(lista, texto) {
    const li = document.createElement('li');
    li.setAttribute('role', 'presentation');
    li.className = 'px-3 py-2 text-xs text-slate-500';
    li.textContent = texto;
    lista.appendChild(li);
  }

  function opcion(lista, datos) {
    const indice = opciones.length;
    opciones.push(datos);
    const li = document.createElement('li');
    li.id = `busqueda-opcion-${indice}`;
    li.setAttribute('role', 'option');
    li.setAttribute('aria-selected', 'false');
    li.className = 'mx-1 rounded-lg';
    const a = document.createElement('a');
    a.href = datos.href;
    a.tabIndex = -1;
    a.className = 'flex flex-col rounded-lg px-3 py-2 text-sm text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-700/60';
    const titulo = document.createElement('span');
    titulo.className = 'font-semibold';
    titulo.textContent = datos.texto;
    a.appendChild(titulo);
    if (datos.detalle) {
      const detalle = document.createElement('span');
      detalle.className = 'text-xs text-slate-400';
      detalle.textContent = datos.detalle;
      a.appendChild(detalle);
    }
    li.appendChild(a);
    lista.appendChild(li);
  }

  function pintar(consulta, grupos, error, cargando) {
    const el = elementos();
    if (!el) return;
    el.lista.replaceChildren();
    opciones = [];
    activo = -1;
    el.input.removeAttribute('aria-activedescendant');

    const encontrados = filtrarModulos(consulta);
    encabezado(el.lista, 'Módulos');
    if (encontrados.length === 0) aviso(el.lista, 'Ningún módulo coincide.');
    encontrados.forEach((m) => opcion(el.lista, { href: m.ruta, texto: m.titulo, detalle: m.grupo }));

    if (consulta.trim().length < MIN_CARACTERES) return;
    if (cargando) { encabezado(el.lista, 'Registros'); aviso(el.lista, 'Buscando…'); return; }
    if (error) { encabezado(el.lista, 'Registros'); aviso(el.lista, error); return; }
    (grupos || []).filter((g) => Array.isArray(g.items) && g.items.length > 0).forEach((g) => {
      encabezado(el.lista, g.titulo);
      g.items.forEach((r) => opcion(el.lista, { href: r.ruta, texto: r.titulo, detalle: r.subtitulo }));
    });
  }

  function marcar(indice) {
    const el = elementos();
    if (!el || opciones.length === 0) return;
    activo = (indice + opciones.length) % opciones.length;
    el.lista.querySelectorAll('[role="option"]').forEach((li, i) => {
      const seleccionada = i === activo;
      li.setAttribute('aria-selected', seleccionada ? 'true' : 'false');
      li.classList.toggle('bg-brand-50', seleccionada);
      if (seleccionada) li.scrollIntoView({ block: 'nearest' });
    });
    el.input.setAttribute('aria-activedescendant', `busqueda-opcion-${activo}`);
  }

  function actualizar(consulta) {
    ultimaConsulta = consulta;
    window.clearTimeout(temporizador);
    if (controlador) controlador.abort();
    if (consulta.trim().length < MIN_CARACTERES) { pintar(consulta, [], null, false); return; }
    pintar(consulta, [], null, true);
    temporizador = window.setTimeout(() => buscarRegistros(consulta), DEBOUNCE_MS);
  }

  async function buscarRegistros(consulta) {
    controlador = new AbortController();
    try {
      const respuesta = await fetch(`/buscar/sugerencias?q=${encodeURIComponent(consulta.trim())}`, {
        headers: { Accept: 'application/json' },
        credentials: 'same-origin',
        signal: controlador.signal,
      });
      if (consulta !== ultimaConsulta) return;
      if (!respuesta.ok) { pintar(consulta, [], ERROR_REGISTROS, false); return; }
      const datos = await respuesta.json();
      pintar(consulta, datos.grupos || [], null, false);
    } catch (error) {
      if (error && error.name === 'AbortError') return;
      if (consulta === ultimaConsulta) pintar(consulta, [], ERROR_REGISTROS, false);
    }
  }

  function esCampoEditable(objetivo) {
    if (!(objetivo instanceof HTMLElement)) return false;
    return objetivo.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(objetivo.tagName);
  }

  function inicializar() {
    modulos = leerModulos();
    const el = elementos();
    if (!el || el.input.dataset.paletaLista === 'true') return;
    el.input.dataset.paletaLista = 'true';

    el.input.addEventListener('focus', () => { if (el.panel.hidden) abrir(); });
    el.input.addEventListener('input', () => { if (el.panel.hidden) abrir(); else actualizar(el.input.value); });
    el.input.addEventListener('keydown', (evento) => {
      if (evento.key === 'ArrowDown') { evento.preventDefault(); if (!estaAbierta()) abrir(); marcar(activo + 1); }
      else if (evento.key === 'ArrowUp') { evento.preventDefault(); marcar(activo - 1); }
      else if (evento.key === 'Escape') { evento.preventDefault(); cerrar(); }
      else if (evento.key === 'Enter' && estaAbierta() && activo >= 0 && opciones[activo]) {
        evento.preventDefault();
        const enlace = el.lista.querySelector(`#busqueda-opcion-${activo} a`);
        cerrar();
        if (enlace) enlace.click();
      }
      // Enter sin selección: el <form method="get" action="/buscar"> se envía con normalidad.
    });
    el.lista.addEventListener('mousedown', (evento) => evento.preventDefault());
    el.lista.addEventListener('click', () => cerrar());
  }

  document.addEventListener('keydown', (evento) => {
    const tecla = (evento.key || '').toLowerCase();
    if ((evento.ctrlKey || evento.metaKey) && tecla === 'k') { evento.preventDefault(); abrir(); }
    else if (tecla === '/' && !evento.ctrlKey && !evento.metaKey && !evento.altKey && !esCampoEditable(evento.target)) {
      evento.preventDefault();
      abrir();
    }
  });

  document.addEventListener('click', (evento) => {
    const el = elementos();
    if (el && estaAbierta() && !el.panel.contains(evento.target) && evento.target !== el.input) cerrar();
  });

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', inicializar);
  else inicializar();

  // Navegación mejorada de Blazor: el DOM se parchea sin recargar; se relee la isla y se cablea un input nuevo si lo hay.
  if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
    window.Blazor.addEventListener('enhancedload', () => { cerrar(); inicializar(); });
  }
})();
