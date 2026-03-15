// ── Results & Filters ───────────────────────────────────────────────────────
// ── Render results ────────────────────────────────────────────────────────────
let _lastResults = null;
const _ignoredIssues = new Set(); // "fileIdx-issueIdx"

// ── Active filters ────────────────────────────────────────────────────────────
const _filters = { severity: new Set(), category: new Set() };

function toggleFilter(type, value, el) {
    const set = _filters[type];
    set.has(value) ? set.delete(value) : set.add(value);
    el.classList.toggle('active', set.has(value));
    applyFilters();
}

function clearFilters() {
    _filters.severity.clear();
    _filters.category.clear();
    document.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('active'));
    applyFilters();
}

function isIssueVisible(issue, fileIdx, issueIdx) {
    // Ignorowane są widoczne (wyszarzone) — chyba że dodatkowo filtr je ukrywa
    if (_filters.severity.size > 0 && !_filters.severity.has(issue.severity)) return false;
    if (_filters.category.size > 0 && !_filters.category.has(issue.category)) return false;
    return true;
}

function isIssueCounted(issue, fileIdx, issueIdx) {
    // Do licznika ukrytych: ignorowane + przefiltrowane
    if (_ignoredIssues.has(`${fileIdx}-${issueIdx}`)) return false;
    return isIssueVisible(issue, fileIdx, issueIdx);
}

function applyFilters() {
    if (!_lastResults) return;
    _lastResults.files.forEach((file, fIdx) => {
        let anyActive = false;
        file.issues.forEach((issue, iIdx) => {
            const el = document.getElementById(`issue-${fIdx}-${iIdx}`);
            if (!el) return;
            const visible = isIssueVisible(issue, fIdx, iIdx);
            el.style.display = visible ? '' : 'none';
            if (visible && !_ignoredIssues.has(`${fIdx}-${iIdx}`)) anyActive = true;
        });
        const empty = document.getElementById(`empty-${fIdx}`);
        if (empty) empty.style.display = anyActive ? 'none' : '';
    });
    updateFilterCount();
}

function updateFilterCount() {
    if (!_lastResults) return;
    const allIssues = _lastResults.files.flatMap((f, fIdx) =>
        f.issues.map((issue, iIdx) => ({ issue, fIdx, iIdx })));
    const total = allIssues.length;
    const visible = allIssues.filter(({ issue, fIdx, iIdx }) => isIssueCounted(issue, fIdx, iIdx)).length;
    const hidden = total - visible;
    const countEl = document.getElementById('filter-count');
    if (countEl) countEl.textContent = hidden > 0 ? t('results.hidden', { hidden, total }) : '';
}

function ignoreIssue(cardIdx, issueIdx) {
    const key = `${cardIdx}-${issueIdx}`;
    const el = document.getElementById(`issue-${cardIdx}-${issueIdx}`);
    const btn = el?.querySelector('.ignore-btn');

    if (_ignoredIssues.has(key)) {
        _ignoredIssues.delete(key);
        if (el) el.classList.remove('ignored');
        if (btn) btn.textContent = t('btn.ignore');
    } else {
        _ignoredIssues.add(key);
        if (el) el.classList.add('ignored');
        if (btn) btn.textContent = t('btn.restore');
    }

    applyFilters();
    recalculateSummary();
}

function recalculateSummary() {
    if (!_lastResults) return;

    let critical = 0, warnings = 0, info = 0, totalScore = 0;
    const penalties = { Critical: 20, Warning: 8, Info: 2 };

    _lastResults.files.forEach((file, fIdx) => {
        const active = file.issues.filter((_, iIdx) => !_ignoredIssues.has(`${fIdx}-${iIdx}`));

        critical += active.filter(i => i.severity === 'Critical').length;
        warnings += active.filter(i => i.severity === 'Warning').length;
        info += active.filter(i => i.severity === 'Info').length;

        // Score = 100 minus kary za aktywne issues
        const penalty = active.reduce((s, i) => s + (penalties[i.severity] ?? 0), 0);
        const fileScore = Math.max(0, 100 - penalty);
        totalScore += fileScore;

        // Zaktualizuj score per plik
        const scoreEl = document.querySelector(`#card${fIdx} .file-score`);
        if (scoreEl) {
            const fc = fileScore >= 80 ? 'var(--green)' : fileScore >= 60 ? 'var(--yellow)' : 'var(--red)';
            scoreEl.textContent = `${fileScore}/100`;
            scoreEl.style.color = fc;
        }
    });

    const overallScore = _lastResults.files.length > 0
        ? Math.round(totalScore / _lastResults.files.length) : 0;
    const scoreColor = overallScore >= 80 ? 'var(--green)' : overallScore >= 60 ? 'var(--yellow)' : 'var(--red)';

    const stats = document.querySelectorAll('.stat .stat-value');
    if (stats[0]) stats[0].textContent = critical;
    if (stats[1]) stats[1].textContent = warnings;
    if (stats[2]) stats[2].textContent = info;

    const ringText = document.querySelector('.score-ring-text');
    const ringCircle = document.querySelector('.score-ring circle:last-child');
    if (ringText) { ringText.textContent = overallScore; ringText.style.color = scoreColor; }
    if (ringCircle) {
        const circ = Math.PI * 2 * 26;
        ringCircle.setAttribute('stroke-dashoffset', circ * (1 - overallScore / 100));
        ringCircle.setAttribute('stroke', scoreColor);
    }
}

function restoreIgnored() {
    _ignoredIssues.clear();
    applyFilters();
}

function renderResultsInto(data, area) {
    // Unikalny prefix żeby ID nie kolidowały z głównym widokiem
    const prefix = 'h' + Math.random().toString(36).slice(2, 7);
    _renderResults(data, area, false, prefix);
}

function renderResults(data) {
    _lastResults = data;
    _ignoredIssues.clear();
    const area = document.getElementById('resultsArea');
    _renderResults(data, area, true, '');
}

function _renderResults(data, area, isMain, prefix) {
    const score = data.summary.overallScore;
    const scoreColor = score >= 80 ? 'var(--green)' : score >= 60 ? 'var(--yellow)' : 'var(--red)';
    const circ = Math.PI * 2 * 26;
    const offset = circ * (1 - score / 100);

    // Zbierz unikalne kategorie z wyników
    const categories = [...new Set(data.files.flatMap(f => f.issues.map(i => i.category)))];

    let html = `
        <div class="summary-bar">
            <div class="stat"><div class="stat-value" style="color:var(--red)">${data.summary.critical}</div><div class="stat-label">Krytyczne</div></div>
            <div class="stat"><div class="stat-value" style="color:var(--yellow)">${data.summary.warnings}</div><div class="stat-label">Ostrzeżenia</div></div>
            <div class="stat"><div class="stat-value" style="color:var(--accent)">${data.summary.info}</div><div class="stat-label">Informacje</div></div>
            <div class="stat"><div class="stat-value" style="color:var(--text2)">${data.files.length}</div><div class="stat-label">Pliki</div></div>
            <div class="score-ring">
                <svg width="64" height="64" viewBox="0 0 64 64">
                    <circle cx="32" cy="32" r="26" fill="none" stroke="var(--border)" stroke-width="4"/>
                    <circle cx="32" cy="32" r="26" fill="none" stroke="${scoreColor}"
                        stroke-width="4" stroke-linecap="round"
                        stroke-dasharray="${circ}" stroke-dashoffset="${offset}"
                        style="transition:stroke-dashoffset 1s ease"/>
                </svg>
                <div class="score-ring-text" style="color:${scoreColor}">${score}</div>
            </div>
        </div>`;

    if (data.summary.generalFeedback)
        html += `<div class="general-feedback">💬 ${data.summary.generalFeedback}</div>`;

    // Pasek filtrów
    html += `
        <div class="filter-bar">
            <div class="filter-group">
                <span class="filter-group-label">Ważność:</span>
                <button class="filter-chip critical" onclick="toggleFilter('severity','Critical',this)">⚠ Krytyczne</button>
                <button class="filter-chip warning"  onclick="toggleFilter('severity','Warning',this)">! Ostrzeżenia</button>
                <button class="filter-chip info"     onclick="toggleFilter('severity','Info',this)">i Info</button>
            </div>
            ${categories.length > 0 ? `
            <div class="filter-group">
                <span class="filter-group-label">Kategoria:</span>
                ${categories.map(c => `<button class="filter-chip cat" onclick="toggleFilter('category','${c}',this)">${escapeHtml(c)}</button>`).join('')}
            </div>` : ''}
            <div class="filter-actions">
                <span class="filter-count" id="filter-count"></span>
                <button class="filter-clear" onclick="clearFilters()">Wyczyść filtry</button>
                <button class="filter-clear" onclick="restoreIgnored()" title="Przywróć zignorowane">↩ Przywróć</button>
            </div>
        </div>`;

    data.files.forEach((file, idx) => {
        const crit = file.issues.filter(i => i.severity === 'Critical').length;
        const warn = file.issues.filter(i => i.severity === 'Warning').length;
        const info = file.issues.filter(i => i.severity === 'Info').length;
        const fc = file.score >= 80 ? 'var(--green)' : file.score >= 60 ? 'var(--yellow)' : 'var(--red)';
        const cardId = `${prefix}card${idx}`;

        html += `
            <div class="file-card" id="${cardId}">
                <div class="file-card-header" onclick="toggleCard('${cardId}')">
                    <div class="file-info">
                        <span class="file-lang">${escapeHtml(file.language)}</span>
                        <span class="file-path">${escapeHtml(file.filePath)}</span>
                    </div>
                    <div class="file-meta">
                        <div class="issue-counts">
                            ${crit > 0 ? `<span class="badge critical">⚠ ${crit}</span>` : ''}
                            ${warn > 0 ? `<span class="badge warning">! ${warn}</span>` : ''}
                            ${info > 0 ? `<span class="badge info">i ${info}</span>` : ''}
                        </div>
                        ${file.originalCode ? `<button class="file-preview-btn" onclick="event.stopPropagation();previewCode('${escapeHtml(file.filePath)}',${JSON.stringify(file.originalCode)})" title="Podgląd kodu">👁</button>` : ''}
                        <span class="file-score" style="color:${fc}">${file.score}/100</span>
                        <span class="chevron">▼</span>
                    </div>
                </div>
                <div class="file-issues">
                    ${file.issues.map((issue, iIdx) => renderIssue(issue, `${prefix}${idx}`, iIdx, isMain)).join('')}
                    <div id="${prefix}empty-${idx}" class="no-issues-msg" style="display:none">
                        ✓ Wszystkie issues ukryte lub przefiltrowane.
                    </div>
                    ${file.issues.length === 0 ? `<div style="padding:14px 16px;font-size:12px;color:var(--green)">✓ ${t('results.noIssues')}</div>` : ''}
                </div>
            </div>`;
    });

    area.innerHTML = html;

    if (isMain) {
        document.getElementById('exportHtmlBtn').disabled = false;
        document.getElementById('exportPdfBtn').disabled = false;
    }
}

function stripCodeFences(str) {
    if (!str) return '';
    return str.replace(/^```[\w]*\n?/m, '').replace(/\n?```$/m, '').trim();
}

function renderIssue(issue, cardIdx, issueIdx, isMain = true) {
    const sevClass = issue.severity;
    const diffId = `diff-${cardIdx}-${issueIdx}`;
    const hasDiff = issue.codeBefore || issue.codeAfter;

    return `
        <div class="issue-item" id="issue-${cardIdx}-${issueIdx}">
            <div class="issue-severity"><div class="sev-bar ${sevClass}"></div></div>
            <div class="issue-content">
                <div class="issue-title-row">
                    <span class="issue-badge ${sevClass.toLowerCase()}">${sevLabel(issue.severity)}</span>
                    <span class="issue-title">${escapeHtml(issue.title)}</span>
                    <span class="issue-cat">${escapeHtml(issue.category)}</span>
                    ${issue.line ? `<span class="issue-line">L${issue.line}</span>` : ''}
                    ${isMain ? `<button class="ignore-btn" onclick="ignoreIssue('${cardIdx}',${issueIdx})" title="Oznacz jako false positive">${t('btn.ignore')}</button>` : ''}
                </div>
                <div class="issue-desc">${escapeHtml(issue.description)}</div>
                ${issue.suggestion ? `<div class="issue-suggestion">${escapeHtml(issue.suggestion)}</div>` : ''}
                ${hasDiff ? `
                <div class="diff-toggle" onclick="toggleDiff('${diffId}')">
                    <span>⟨/⟩ Pokaż kod do zmiany</span>
                </div>
                <div class="diff-block" id="${diffId}" style="display:none">
                    ${issue.codeBefore ? `
                    <div class="diff-section">
                        <div class="diff-label before">❌ Przed</div>
                        <pre class="diff-code before">${escapeHtml(stripCodeFences(issue.codeBefore))}</pre>
                    </div>` : ''}
                    ${issue.codeAfter ? `
                    <div class="diff-section">
                        <div class="diff-label after">✅ Po</div>
                        <pre class="diff-code after">${escapeHtml(stripCodeFences(issue.codeAfter))}</pre>
                    </div>` : ''}
                </div>` : ''}
            </div>
        </div>`;
}

function sevLabel(sev) {
    return { Critical: t('severity.critical'), Warning: t('severity.warning'), Info: t('severity.info') }[sev] ?? sev;
}

function toggleDiff(id) {
    const el = document.getElementById(id);
    const btn = el.previousElementSibling;
    if (el.style.display === 'none') {
        el.style.display = 'block';
        btn.querySelector('span').textContent = t('btn.hideCode');
    } else {
        el.style.display = 'none';
        btn.querySelector('span').textContent = t('btn.showCode');
    }
}

function toggleCard(cardId) {
    document.getElementById(cardId).classList.toggle('open');
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// ── Loader ────────────────────────────────────────────────────────────────────
let _lastLoaderMsg = null;

function showLoader(msg = t('btn.analyzing')) {
    _lastLoaderMsg = msg;
    _renderLoader(msg);
}

function _renderLoader(msg) {
    document.getElementById('resultsArea').innerHTML = `
        <div class="panel">
            <div class="panel-header"><div class="dot"></div>${t('panel.results')}</div>
            <div class="loader active">
                <div class="spinner"></div>
                <div class="loader-text">${escapeHtml(msg)}</div>
                <div style="font-size:11px;color:var(--text3)">${t('analyzing.hint')}</div>
            </div>
        </div>`;
}