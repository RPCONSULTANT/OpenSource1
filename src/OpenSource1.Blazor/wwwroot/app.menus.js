// app.menus.js — cierra los menús <details data-menu> (PageToolbar, TarjetaAcciones) al pulsar fuera o con Esc
// o al salir de él con Tab (Fix-Features B1). Sin este script los menús siguen abriéndose y cerrándose con su propio <summary>.
// Delegación en document (un solo juego de listeners, válido tras la navegación mejorada); en 'enhancedload' (vía
// Blazor.addEventListener) se cierran los que hubieran quedado abiertos.
(() => {
  'use strict';

  if (window.__axionMenus) return;
  window.__axionMenus = true;

  function cerrarTodos(excepto) {
    document.querySelectorAll('details[data-menu][open]').forEach((menu) => {
      if (menu !== excepto) menu.removeAttribute('open');
    });
  }

  document.addEventListener('click', (evento) => {
    const actual = evento.target instanceof Element ? evento.target.closest('details[data-menu]') : null;
    cerrarTodos(actual);
  });

  document.addEventListener('keydown', (evento) => {
    if (evento.key !== 'Escape') return;
    const abierto = document.querySelector('details[data-menu][open]');
    cerrarTodos(null);
    abierto?.querySelector('summary')?.focus();
  });

  // Tab fuera del menú: se cierra. Solo si el foco va a OTRO elemento (relatedTarget no nulo): un clic con ratón en
  // Safari no enfoca enlaces (relatedTarget null) y cerrar ahí impediría seguir el enlace; ese caso lo cubre 'click'.
  document.addEventListener('focusout', (evento) => {
    const menu = evento.target instanceof Element ? evento.target.closest('details[data-menu][open]') : null;
    const destino = evento.relatedTarget;
    if (menu && destino instanceof Node && !menu.contains(destino)) menu.removeAttribute('open');
  });

  if (window.Blazor?.addEventListener) {
    window.Blazor.addEventListener('enhancedload', () => cerrarTodos(null));
  }
})();
