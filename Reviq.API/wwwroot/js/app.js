const API = '/api';

// ── Tabs ──────────────────────────────────────────────────────────────────────
function switchTab(tab) {
    document.getElementById('tabSnippet').classList.toggle('active', tab === 'snippet');
    document.getElementById('tabRepo').classList.toggle('active', tab === 'repo');
    document.getElementById('contentSnippet').classList.toggle('active', tab === 'snippet');
    document.getElementById('contentRepo').classList.toggle('active', tab === 'repo');
}

function showPage(page) {
    document.getElementById('pageAnalyze').style.display = page === 'analyze' ? '' : 'none';
    document.getElementById('pageHistory').style.display = page === 'history' ? '' : 'none';
    document.getElementById('navAnalyze').classList.toggle('active', page === 'analyze');
    document.getElementById('navHistory').classList.toggle('active', page === 'history');
    if (page === 'history') loadHistory();
}


// ── Init ──────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.lang-btn').forEach(b =>
        b.classList.toggle('active', b.dataset.lang === _lang));
});

loadLocale(_lang).then(() => initProviders());

setInterval(pollProviderStatus, 30000);