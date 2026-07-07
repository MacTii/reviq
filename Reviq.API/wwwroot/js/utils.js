// ── Utilities ─────────────────────────────────────────────────────────────────
import { t } from './i18n.js';

export function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

export function detectLang(fileName) {
    const ext = fileName.split('.').pop().toLowerCase();
    const map = {
        cs: 'C#', ts: 'TypeScript', tsx: 'TypeScript', js: 'JavaScript',
        jsx: 'JavaScript', py: 'Python', java: 'Java', go: 'Go',
        rs: 'Rust', php: 'PHP'
    };
    return map[ext] ?? 'Unknown';
}

export function formatBytes(bytes) {
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

export function sevLabel(sev) {
    return { Critical: t('severity.critical'), Warning: t('severity.warning'), Info: t('severity.info') }[sev] ?? sev;
}

export function stripCodeFences(str) {
    if (!str) return '';
    return str.replace(/^```[\w]*\n?/m, '').replace(/\n?```$/m, '').trim();
}

export function showError(msg) {
    const b = document.getElementById('errorBox');
    b.textContent = '⚠ ' + msg;
    b.classList.add('active');
}
export function clearError() { document.getElementById('errorBox').classList.remove('active'); }
export function showSnippetError(msg) {
    const b = document.getElementById('snippetErrorBox');
    b.textContent = '⚠ ' + msg;
    b.classList.add('active');
}
export function clearSnippetError() { document.getElementById('snippetErrorBox').classList.remove('active'); }
