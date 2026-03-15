// ── Utilities ─────────────────────────────────────────────────────────────────
function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function detectLang(fileName) {
    const ext = fileName.split('.').pop().toLowerCase();
    const map = {
        cs: 'C#', ts: 'TypeScript', tsx: 'TypeScript', js: 'JavaScript',
        jsx: 'JavaScript', py: 'Python', java: 'Java', go: 'Go',
        rs: 'Rust', php: 'PHP'
    };
    return map[ext] ?? 'Unknown';
}

function formatBytes(bytes) {
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

function sevLabel(sev) {
    return { Critical: t('severity.critical'), Warning: t('severity.warning'), Info: t('severity.info') }[sev] ?? sev;
}

function stripCodeFences(str) {
    if (!str) return '';
    return str.replace(/^```[\w]*\n?/, '').replace(/\n?```$/, '').trim();
}

function showError(msg) {
    const b = document.getElementById('errorBox');
    b.textContent = '⚠ ' + msg;
    b.classList.add('active');
}
function clearError() { document.getElementById('errorBox').classList.remove('active'); }
function showSnippetError(msg) {
    const b = document.getElementById('snippetErrorBox');
    b.textContent = '⚠ ' + msg;
    b.classList.add('active');
}
function clearSnippetError() { document.getElementById('snippetErrorBox').classList.remove('active'); }