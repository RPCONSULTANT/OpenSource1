(() => {
  const overlayId = 'global-loading-overlay';

  function overlay() {
    return document.getElementById(overlayId);
  }

  function showLoading() {
    const el = overlay();
    if (!el) return;
    el.classList.add('is-visible');
    el.setAttribute('aria-hidden', 'false');
  }

  function hideLoading() {
    const el = overlay();
    if (!el) return;
    el.classList.remove('is-visible');
    el.setAttribute('aria-hidden', 'true');
  }

  document.addEventListener('submit', (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;
    if (form.dataset.noLoading === 'true') return;

    const submitter = event.submitter;
    if (submitter instanceof HTMLElement) {
      submitter.classList.add('is-loading');
      submitter.setAttribute('aria-busy', 'true');
    }

    window.setTimeout(showLoading, 80);
  }, true);

  document.addEventListener('click', (event) => {
    const target = event.target instanceof Element ? event.target.closest('a') : null;
    if (!target || target.dataset.noLoading === 'true') return;
    const href = target.getAttribute('href');
    if (!href || href.startsWith('#') || href.startsWith('javascript:') || target.target === '_blank') return;
    window.setTimeout(showLoading, 80);
  }, true);

  window.addEventListener('pageshow', (event) => {
    hideLoading();
    if (event.persisted) {
      window.location.reload();
    }
  });

  window.addEventListener('load', hideLoading);
  document.addEventListener('enhancedload', hideLoading);

  if ('caches' in window) {
    window.OpenSource1ClearClientCaches = async () => {
      const keys = await caches.keys();
      await Promise.all(keys.map((key) => caches.delete(key)));
    };
  }
})();
