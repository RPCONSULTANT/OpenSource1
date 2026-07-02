(() => {
  const overlayId = 'global-loading-overlay';
  const loadingDelayMs = 220;
  const failsafeHideMs = 6000;
  let showTimer = 0;
  let hideTimer = 0;

  function overlay() {
    return document.getElementById(overlayId);
  }

  function clearTimer(timerId) {
    if (timerId) {
      window.clearTimeout(timerId);
    }
  }

  function cancelPendingShow() {
    clearTimer(showTimer);
    showTimer = 0;
  }

  function scheduleShowLoading() {
    cancelPendingShow();
    showTimer = window.setTimeout(showLoading, loadingDelayMs);
  }

  function scheduleFailsafeHide() {
    clearTimer(hideTimer);
    hideTimer = window.setTimeout(hideLoading, failsafeHideMs);
  }

  function showLoading() {
    const el = overlay();
    if (!el) return;
    el.classList.add('is-visible');
    el.setAttribute('aria-hidden', 'false');
  }

  function hideLoading() {
    cancelPendingShow();
    clearTimer(hideTimer);
    hideTimer = 0;

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

    scheduleShowLoading();
    scheduleFailsafeHide();
  }, true);

  document.addEventListener('click', (event) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

    const target = event.target instanceof Element ? event.target.closest('a') : null;
    if (!target || target.dataset.noLoading === 'true') return;
    if (target.hasAttribute('download') || target.target === '_blank') return;

    const href = target.getAttribute('href');
    if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;

    const targetUrl = new URL(href, window.location.href);
    const currentUrl = new URL(window.location.href);

    if (targetUrl.origin !== currentUrl.origin) return;
    if (targetUrl.href === currentUrl.href) return;

    scheduleShowLoading();
    scheduleFailsafeHide();
  }, true);

  window.addEventListener('pageshow', (event) => {
    hideLoading();
  });

  window.addEventListener('focus', hideLoading);
  window.addEventListener('popstate', hideLoading);
  document.addEventListener('readystatechange', () => {
    if (document.readyState === 'interactive' || document.readyState === 'complete') {
      hideLoading();
    }
  });
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      hideLoading();
    }
  });
  document.addEventListener('DOMContentLoaded', hideLoading);
  window.addEventListener('load', hideLoading);
  document.addEventListener('enhancedload', hideLoading);

  if ('caches' in window) {
    window.OpenSource1ClearClientCaches = async () => {
      const keys = await caches.keys();
      await Promise.all(keys.map((key) => caches.delete(key)));
    };
  }
})();
