(() => {
  const overlayId = 'global-loading-overlay';
  const loadingDelayMs = 180;
  const failsafeHideMs = 4000;
  let showTimer = 0;
  let hideTimer = 0;
  let pendingFetches = 0;
  let hardNavigationPending = false;

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
    showTimer = window.setTimeout(() => {
      if (pendingFetches > 0 || hardNavigationPending) {
        showLoading();
      }
    }, loadingDelayMs);
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
    hardNavigationPending = false;
    cancelPendingShow();
    clearTimer(hideTimer);
    hideTimer = 0;

    const el = overlay();
    if (!el) return;

    el.classList.remove('is-visible');
    el.setAttribute('aria-hidden', 'true');
  }

  function beginTrackedActivity() {
    scheduleShowLoading();
    scheduleFailsafeHide();
  }

  function beginFetchActivity() {
    pendingFetches += 1;
    beginTrackedActivity();
  }

  function endFetchActivity() {
    pendingFetches = Math.max(0, pendingFetches - 1);
    if (pendingFetches === 0 && !hardNavigationPending) {
      hideLoading();
    }
  }

  function isTrackedRequest(input, init) {
    try {
      const request = input instanceof Request ? input : new Request(input, init);
      const url = new URL(request.url, window.location.href);

      if (url.origin !== window.location.origin) return false;

      const method = (request.method || 'GET').toUpperCase();
      if (method !== 'GET' && method !== 'POST') return false;

      const accept = request.headers.get('accept') || '';
      return accept.includes('text/html') || request.mode === 'navigate' || method === 'POST';
    } catch {
      return false;
    }
  }

  const originalFetch = window.fetch.bind(window);
  window.fetch = async (input, init) => {
    const tracked = isTrackedRequest(input, init);

    if (tracked) {
      beginFetchActivity();
    }

    try {
      return await originalFetch(input, init);
    } finally {
      if (tracked) {
        endFetchActivity();
      }
    }
  };

  document.addEventListener('submit', (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;
    if (form.dataset.noLoading === 'true') return;

    const submitter = event.submitter;
    if (submitter instanceof HTMLElement) {
      submitter.classList.add('is-loading');
      submitter.setAttribute('aria-busy', 'true');
    }

    if (!form.hasAttribute('data-enhance')) {
      hardNavigationPending = true;
      beginTrackedActivity();
    }
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

    hardNavigationPending = true;
    beginTrackedActivity();
  }, true);

  window.addEventListener('pageshow', (event) => {
    hideLoading();
  });

  window.addEventListener('beforeunload', () => {
    if (pendingFetches > 0 || hardNavigationPending) {
      showLoading();
    }
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

  const main = document.querySelector('main');
  if (main && 'MutationObserver' in window) {
    const observer = new MutationObserver(() => {
      if (pendingFetches === 0) {
        hideLoading();
      }
    });

    observer.observe(main, { childList: true, subtree: true });
  }

  if ('caches' in window) {
    window.OpenSource1ClearClientCaches = async () => {
      const keys = await caches.keys();
      await Promise.all(keys.map((key) => caches.delete(key)));
    };
  }
})();
