// ── i18n ─────────────────────────────────────────────────────────────────────
// ── i18n ──────────────────────────────────────────────────────────────────────
let _locale = {};
let _localeFallback = {};  // zawsze pl jako fallback
let _lang = localStorage.getItem('lang') || 'pl';

async function loadLocale(lang) {
    try {
        const v = '?v=4';
        const fb = await fetch('/locales/pl.json' + v);
        _localeFallback = await fb.json();

        const r = await fetch(`/locales/${lang}.json` + v);
        _locale = await r.json();
        _lang = lang;
        localStorage.setItem('lang', lang);
        applyTranslations();
        const btnText = document.getElementById('providerBtnText');
        if (btnText && !currentProvider) btnText.textContent = t('provider.checking');
    } catch {
        console.warn(`Failed to load locale: ${lang}`);
    }
}

function t(key, vars = {}) {
    let str = _locale[key] ?? _localeFallback[key] ?? key;
    for (const [k, v] of Object.entries(vars))
        str = str.replace(`{${k}}`, v);
    return str;
}

function applyTranslations() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        const attr = el.getAttribute('data-i18n-attr');
        if (attr) el.setAttribute(attr, t(key));
        else el.textContent = t(key);
    });
    document.documentElement.lang = _lang;

    // Odśwież lineCount
    const lineCountEl = document.getElementById('lineCount');
    if (lineCountEl) {
        const n = parseInt(lineCountEl.textContent) || 0;
        lineCountEl.textContent = t('lines', { n });
    }

    // Odśwież providerBtnText tylko jeśli jest w stanie "Sprawdzanie"
    const btnText = document.getElementById('providerBtnText');
    if (btnText) {
        const cur = btnText.textContent.trim();
        if (cur === 'Sprawdzanie...' || cur === 'Checking...' || cur === t('provider.checking', {}))
            btnText.textContent = t('provider.checking');
    }

    // Wyczyść error boxy — przy zmianie języka stary komunikat byłby w złym języku
    ['errorBox', 'snippetErrorBox'].forEach(id => {
        const b = document.getElementById(id);
        if (b) { b.textContent = ''; b.classList.remove('active'); }
    });

    // Odśwież loader jeśli jest aktywny
    if (_lastLoaderMsg !== null && document.querySelector('#resultsArea .loader.active'))
        _renderLoader(t('btn.analyzing'));

    // Odśwież repo info jeśli jest widoczny
    const preview = document.getElementById('repoPreview');
    if (_lastRepoInfo && preview && preview.style.display === 'block')
        renderRepoInfo(_lastRepoInfo.d, _lastRepoInfo.diffScope);

    // Odśwież puste selecty modeli (gdy brak modeli)
    ['snippetModel', 'modelSelect'].forEach(id => {
        const sel = document.getElementById(id);
        if (sel && sel.options.length === 1 && sel.options[0].value === '') {
            const key = currentProvider === 'LocalAI' ? 'localai.noModels' : 'model.none';
            sel.options[0].textContent = t(key);
        }
    });

    // Odśwież przyciski z data-i18n-key w dynamicznie generowanym HTML
    document.querySelectorAll('.i18n-btn[data-i18n-key]').forEach(el => {
        el.textContent = t(el.dataset.i18nKey);
    });

    // Odśwież wyniki HF search — podmień teksty bezpośrednio bez re-fetcha
    document.querySelectorAll('#hfSearchResults .hf-downloads').forEach(el => {
        const downloads = el.dataset.downloads || '0';
        const likes = el.dataset.likes || '0';
        el.textContent = `⬇ ${parseInt(downloads).toLocaleString()} ${t('localai.hfDownloads')} · ❤ ${likes}`;
    });
    document.querySelectorAll('#hfSearchResults .hf-files-link').forEach(el => {
        el.textContent = t('localai.hfFiles');
    });

    // Odśwież historię jeśli jest widoczna
    const historyPage = document.getElementById('pageHistory');
    if (historyPage && historyPage.style.display !== 'none')
        loadHistory();
}

function setLang(lang) {
    loadLocale(lang);
    document.querySelectorAll('.lang-btn').forEach(b =>
        b.classList.toggle('active', b.dataset.lang === lang));
}

// Ustaw aktywny przycisk języka przy starcie
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.lang-btn').forEach(b =>
        b.classList.toggle('active', b.dataset.lang === _lang));
});