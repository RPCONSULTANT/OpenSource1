(() => {
  const instances = new Map();

  function destroyAll() {
    for (const chart of instances.values()) {
      chart.destroy();
    }
    instances.clear();
  }

  function themeColors() {
    const isDark = document.documentElement.classList.contains('dark');
    return {
      text: isDark ? '#cbd5e1' : '#475569',
      grid: isDark ? 'rgba(148,163,184,0.15)' : 'rgba(100,116,139,0.12)'
    };
  }

  function renderCharts() {
    if (typeof Chart === 'undefined') return;
    destroyAll();

    const colors = themeColors();

    document.querySelectorAll('canvas[data-chart]').forEach((canvas) => {
      const dataEl = document.getElementById(canvas.id + '-data');
      if (!dataEl) return;

      let payload;
      try {
        payload = JSON.parse(dataEl.textContent);
      } catch {
        return;
      }

      const type = canvas.dataset.chart;
      const chart = new Chart(canvas, {
        type,
        data: {
          labels: payload.labels,
          datasets: payload.datasets
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              display: type === 'doughnut' || payload.datasets.length > 1,
              labels: { color: colors.text, boxWidth: 12, font: { size: 11 } }
            }
          },
          scales: type === 'doughnut' ? undefined : {
            x: { ticks: { color: colors.text, font: { size: 10 } }, grid: { color: colors.grid } },
            y: { ticks: { color: colors.text, font: { size: 10 } }, grid: { color: colors.grid }, beginAtZero: true }
          }
        }
      });

      instances.set(canvas.id, chart);
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', renderCharts);
  } else {
    renderCharts();
  }

  if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
    window.Blazor.addEventListener('enhancedload', renderCharts);
  }
})();
