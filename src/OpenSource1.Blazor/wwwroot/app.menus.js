// app.menus.js — cierra los menús <details data-menu> (PageToolbar, TarjetaAcciones) al pulsar fuera o con Esc
// (Fix-Features B1). Sin este script los menús siguen abriéndose y cerrándose con su propio <summary>.
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

  if (window.Blazor?.addEventListener) {
    window.Blazor.addEventListener('enhancedload', () => cerrarTodos(null));
  }
})();
