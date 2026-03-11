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

// ── Ollama status ─────────────────────────────────────────────────────────────
async function checkOllama() {
    try {
        const r = await fetch(`${API}/ollama/status`);
        const d = await r.json();
        const dot = document.getElementById('ollamaDot');
        const txt = document.getElementById('ollamaStatusText');
        if (d.available) {
            dot.className = 'status-dot online';
            txt.textContent = `Ollama online · ${d.models.length} model${d.models.length !== 1 ? 'i' : ''}`;
            ['modelSelect', 'snippetModel'].forEach(id => {
                const sel = document.getElementById(id);
                if (!sel) return;
                sel.innerHTML = '';
                d.models.forEach(m => {
                    const opt = document.createElement('option');
                    opt.value = m;
                    opt.textContent = m;
                    sel.appendChild(opt);
                });
            });
        } else {
            dot.className = 'status-dot offline';
            txt.textContent = 'Ollama offline';
        }
    } catch {
        document.getElementById('ollamaDot').className = 'status-dot offline';
        document.getElementById('ollamaStatusText').textContent = 'API niedostępne';
    }
}

// ── Checkbox toggle ───────────────────────────────────────────────────────────
document.addEventListener('click', e => {
    const item = e.target.closest('.checkbox-item');
    if (!item) return;
    item.classList.toggle('active');
    item.querySelector('.check-icon').textContent = item.classList.contains('active') ? '✓' : '';
});

// ── Line counter ──────────────────────────────────────────────────────────────
function updateLineCount() {
    const code = document.getElementById('snippetCode').value;
    const lines = code ? code.split('\n').length : 0;
    document.getElementById('lineCount').textContent = `${lines} linii`;
    const ext = { 'C#': '.cs', 'TypeScript': '.ts', 'JavaScript': '.js', 'Python': '.py', 'Java': '.java', 'Go': '.go', 'Rust': '.rs', 'PHP': '.php' };
    const lang = document.getElementById('snippetLang').value;
    const fn = document.getElementById('snippetFileName');
    if (fn.value === 'snippet.cs' || fn.value.startsWith('snippet.')) {
        fn.value = 'snippet' + (ext[lang] || '.txt');
    }
}

document.getElementById('snippetLang').addEventListener('change', updateLineCount);

// ── File upload ───────────────────────────────────────────────────────────────
const uploadedFiles = [];

document.getElementById('fileInput').addEventListener('change', e => {
    Array.from(e.target.files).forEach(file => {
        const reader = new FileReader();
        reader.onload = ev => {
            const exists = uploadedFiles.findIndex(f => f.name === file.name);
            if (exists >= 0) uploadedFiles.splice(exists, 1);
            uploadedFiles.push({ name: file.name, content: ev.target.result });
            renderFileList();
        };
        reader.readAsText(file);
    });
    e.target.value = '';
});

function removeFile(name) {
    const idx = uploadedFiles.findIndex(f => f.name === name);
    if (idx >= 0) uploadedFiles.splice(idx, 1);
    renderFileList();
}

function renderFileList() {
    const list = document.getElementById('fileList');
    if (uploadedFiles.length === 0) {
        list.innerHTML = '';
        list.style.display = 'none';
        return;
    }
    list.style.display = 'flex';
    list.innerHTML = uploadedFiles.map(f => `
        <div class="file-tag">
            <span class="file-tag-name">${escapeHtml(f.name)}</span>
            <span class="file-tag-preview" onclick="previewFile('${escapeHtml(f.name)}')" title="Podgląd">👁</span>
            <span class="file-tag-remove" onclick="removeFile('${escapeHtml(f.name)}')" title="Usuń">✕</span>
        </div>`).join('');
}

function previewFile(name) {
    const file = uploadedFiles.find(f => f.name === name);
    if (!file) return;
    previewCode(file.name, file.content);
}

function previewCode(name, code) {
    const lines = code.split('\n').length;
    document.getElementById('previewModalTitle').textContent = name;
    document.getElementById('previewModalMeta').textContent = `${lines} linii`;
    document.getElementById('previewModalCode').textContent = code;
    document.getElementById('previewModal').style.display = 'flex';
    document.body.style.overflow = 'hidden';
}

function closePreviewModal() {
    document.getElementById('previewModal').style.display = 'none';
    document.body.style.overflow = '';
}

// ── Snippet review ────────────────────────────────────────────────────────────
async function startSnippetReview() {
    const singleCode = document.getElementById('snippetCode').value.trim();
    const language = document.getElementById('snippetLang').value;
    const fileName = document.getElementById('snippetFileName').value.trim() || 'snippet.cs';
    const model = document.getElementById('snippetModel').value;

    // Budujemy listę plików: wklejony kod + uploady
    const filesToReview = [];

    if (singleCode) {
        filesToReview.push({ code: singleCode, language, fileName });
    }

    uploadedFiles.forEach(f => {
        const lang = detectLang(f.name);
        filesToReview.push({ code: f.content, language: lang, fileName: f.name });
    });

    if (filesToReview.length === 0) return showSnippetError('Wklej kod lub dodaj plik do analizy.');
    clearSnippetError();

    const btn = document.getElementById('snippetBtn');
    btn.disabled = true;
    btn.textContent = `⏳ Analizuję ${filesToReview.length} plik${filesToReview.length > 1 ? 'i' : ''}...`;
    showLoader(`Analizuję ${filesToReview.length} plik${filesToReview.length > 1 ? 'ów' : ''}...`);

    try {
        const res = await fetch(`${API}/code/review-batch?model=${encodeURIComponent(model)}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                files: filesToReview.map(f => ({
                    code: f.code,
                    language: f.language,
                    fileName: f.fileName
                }))
            })
        });

        const data = await res.json();
        if (!res.ok) { showSnippetError(data.error || 'Błąd analizy.'); return; }

        renderResults(data);
    } catch (err) {
        showSnippetError(`Błąd: ${err.message}`);
    } finally {
        btn.disabled = false;
        btn.textContent = '⚡ Analizuj kod';
    }
}

function detectLang(fileName) {
    const ext = fileName.split('.').pop().toLowerCase();
    return { cs: 'C#', ts: 'TypeScript', js: 'JavaScript', py: 'Python', java: 'Java', go: 'Go', rs: 'Rust', php: 'PHP' }[ext] || 'Unknown';
}

function renderMultiResults(results) {
    // Sklejamy wyniki wielu plików w jeden widok
    const merged = {
        summary: {
            critical: results.reduce((s, r) => s + (r.summary?.critical ?? 0), 0),
            warnings: results.reduce((s, r) => s + (r.summary?.warnings ?? 0), 0),
            info: results.reduce((s, r) => s + (r.summary?.info ?? 0), 0),
            overallScore: Math.round(results.reduce((s, r) => s + (r.summary?.overallScore ?? 0), 0) / results.length),
            generalFeedback: `Przeanalizowano ${results.length} pliki.`
        },
        files: results.flatMap(r => r.files ?? [])
    };
    renderResults(merged);
}

// ── Repo review ───────────────────────────────────────────────────────────────
async function browseFolder() {
    // showDirectoryPicker nie zwraca pełnej ścieżki — tylko nazwę folderu
    // Używamy do podpowiedzi, pełną ścieżkę user wpisuje lub uzupełnia
    if (!window.showDirectoryPicker) {
        showError('Przeglądarka nie obsługuje wyboru folderu. Wpisz ścieżkę ręcznie.');
        return;
    }
    try {
        const dir = await window.showDirectoryPicker();
        const input = document.getElementById('repoPath');
        // Jeśli pole jest puste, wstaw nazwę jako podpowiedź
        if (!input.value.trim()) {
            input.value = dir.name;
            input.focus();
            input.select();
        } else {
            // Zastąp ostatni segment ścieżki wybraną nazwą
            const parts = input.value.replace(/\\/g, '/').split('/');
            parts[parts.length - 1] = dir.name;
            input.value = parts.join('\\');
        }
    } catch (e) {
        if (e.name !== 'AbortError') showError('Nie udało się otworzyć selektora folderu.');
    }
}

async function checkRepo() {
    const path = document.getElementById('repoPath').value.trim();
    if (!path) return showError('Podaj ścieżkę do repozytorium.');

    const preview = document.getElementById('repoPreview');

    // Jeśli już otwarty dla tej samej ścieżki — zamknij
    if (preview.style.display === 'block' && preview.dataset.path === path) {
        preview.style.display = 'none';
        return;
    }

    try {
        const r = await fetch(`${API}/git/info?path=${encodeURIComponent(path)}`);
        const d = await r.json();
        const content = document.getElementById('repoInfoContent');
        if (!d.isValid) {
            content.innerHTML = `<span style="color:var(--red)">${d.error || 'Nieprawidłowe repozytorium'}</span>`;
        } else {
            content.innerHTML = `
                <div style="display:flex;gap:8px;align-items:center">
                    <span style="color:var(--text3)">Branch:</span><span style="color:var(--accent)">${d.branch}</span>
                </div>
                <div style="display:flex;gap:8px;align-items:center">
                    <span style="color:var(--text3)">Commit:</span><span>${d.latestCommit}</span>
                    <span style="color:var(--text3);font-size:11px">${d.commitMessage}</span>
                </div>
                <div style="color:var(--text3);margin-top:4px">Pliki do analizy (${d.changedFiles.length}):</div>
                ${d.changedFiles.map(f => `<div style="color:var(--text2);padding-left:8px">• ${f}</div>`).join('')}
                ${d.changedFiles.length === 0 ? '<div style="color:var(--yellow)">Brak zmienionych plików.</div>' : ''}`;
        }
        preview.dataset.path = path;
        preview.style.display = 'block';
    } catch {
        showError('Nie można połączyć z API.');
    }
}

async function startReview() {
    const path = document.getElementById('repoPath').value.trim();
    const model = document.getElementById('modelSelect').value;
    if (!path) return showError('Podaj ścieżkę do repozytorium.');
    clearError();

    const btn = document.getElementById('reviewBtn');
    btn.disabled = true;
    btn.textContent = '⏳ Analizuję...';
    showLoader();

    try {
        const r = await fetch(`${API}/review?model=${encodeURIComponent(model)}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ repoPath: path, files: [], scope: 0 })
        });
        const data = await r.json();
        if (!r.ok) { showError(data.error || 'Błąd podczas analizy.'); return; }
        renderResults(data);
    } catch {
        showError('Nie można połączyć z API.');
    } finally {
        btn.disabled = false;
        btn.textContent = '⚡ Uruchom Review';
    }
}

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
    if (countEl) countEl.textContent = hidden > 0 ? `(ukryto ${hidden} z ${total})` : '';
}

function ignoreIssue(cardIdx, issueIdx) {
    const key = `${cardIdx}-${issueIdx}`;
    const el = document.getElementById(`issue-${cardIdx}-${issueIdx}`);
    const btn = el?.querySelector('.ignore-btn');

    if (_ignoredIssues.has(key)) {
        _ignoredIssues.delete(key);
        if (el) el.classList.remove('ignored');
        if (btn) btn.textContent = '✕ ignoruj';
    } else {
        _ignoredIssues.add(key);
        if (el) el.classList.add('ignored');
        if (btn) btn.textContent = '↩ przywróć';
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
                    ${file.issues.length === 0 ? `<div style="padding:14px 16px;font-size:12px;color:var(--green)">✓ Brak problemów.</div>` : ''}
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
                    ${isMain ? `<button class="ignore-btn" onclick="ignoreIssue('${cardIdx}',${issueIdx})" title="Oznacz jako false positive">✕ ignoruj</button>` : ''}
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
    return { Critical: 'KRYTYCZNY', Warning: 'OSTRZEŻENIE', Info: 'INFO' }[sev] ?? sev;
}

function toggleDiff(id) {
    const el = document.getElementById(id);
    const btn = el.previousElementSibling;
    if (el.style.display === 'none') {
        el.style.display = 'block';
        btn.querySelector('span').textContent = '⟨/⟩ Ukryj kod';
    } else {
        el.style.display = 'none';
        btn.querySelector('span').textContent = '⟨/⟩ Pokaż kod do zmiany';
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
function showLoader(msg = 'Analizuję kod lokalnie (Ollama)...') {
    document.getElementById('resultsArea').innerHTML = `
        <div class="panel">
            <div class="panel-header"><div class="dot"></div>Wyniki analizy</div>
            <div class="loader active">
                <div class="spinner"></div>
                <div class="loader-text">${escapeHtml(msg)}</div>
                <div style="font-size:11px;color:var(--text3)">Może zająć 1–3 min</div>
            </div>
        </div>`;
}

// ── Error helpers ─────────────────────────────────────────────────────────────
function showError(msg) { const b = document.getElementById('errorBox'); b.textContent = '⚠ ' + msg; b.classList.add('active'); }
function clearError() { document.getElementById('errorBox').classList.remove('active'); }
function showSnippetError(msg) { const b = document.getElementById('snippetErrorBox'); b.textContent = '⚠ ' + msg; b.classList.add('active'); }
function clearSnippetError() { document.getElementById('snippetErrorBox').classList.remove('active'); }

// ── Export ────────────────────────────────────────────────────────────────────
function buildReportHTML(data) {
    // Użyj aktywnych (niezignorowanych) issues do raportu
    const allActive = data.files.flatMap((f, fIdx) =>
        f.issues.filter((_, iIdx) => !_ignoredIssues.has(`${fIdx}-${iIdx}`)));
    const critical = allActive.filter(i => i.severity === 'Critical').length;
    const warnings = allActive.filter(i => i.severity === 'Warning').length;
    const info = allActive.filter(i => i.severity === 'Info').length;
    const score = data.summary.overallScore;
    const scoreColor = score >= 80 ? '#34d399' : score >= 60 ? '#fbbf24' : '#f87171';
    const now = new Date().toLocaleString('pl-PL');
    const ignoredCount = _ignoredIssues.size;

    const issuesHTML = data.files.map((file, fIdx) => {
        const activeIssues = file.issues.filter((_, iIdx) => !_ignoredIssues.has(`${fIdx}-${iIdx}`));
        const fc = file.score >= 80 ? '#34d399' : file.score >= 60 ? '#fbbf24' : '#f87171';
        const issueRows = activeIssues.map(issue => {
            const sevColor = { Critical: '#f87171', Warning: '#fbbf24', Info: '#4f8ef7' }[issue.severity] ?? '#8892a4';
            const sevLabel = { Critical: 'KRYTYCZNY', Warning: 'OSTRZEŻENIE', Info: 'INFO' }[issue.severity] ?? issue.severity;
            const diff = (issue.codeBefore || issue.codeAfter) ? `
                <div style="margin-top:10px;border-radius:6px;overflow:hidden;border:1px solid #252d42">
                    ${issue.codeBefore ? `<div style="background:#1a0f0f;padding:4px 10px;font-size:10px;color:#f87171;font-weight:700;border-bottom:1px solid #252d42">❌ PRZED</div><pre style="margin:0;padding:10px;font-family:monospace;font-size:11px;background:#120a0a;color:#c8c8c8;overflow-x:auto;white-space:pre">${escapeHtml(stripCodeFences(issue.codeBefore))}</pre>` : ''}
                    ${issue.codeAfter ? `<div style="background:#0a1a10;padding:4px 10px;font-size:10px;color:#34d399;font-weight:700;border-top:1px solid #252d42;border-bottom:1px solid #252d42">✅ PO</div><pre style="margin:0;padding:10px;font-family:monospace;font-size:11px;background:#080f0a;color:#c8c8c8;overflow-x:auto;white-space:pre">${escapeHtml(stripCodeFences(issue.codeAfter))}</pre>` : ''}
                </div>` : '';
            return `
                <div style="padding:14px 16px;border-top:1px solid #1e2435">
                    <div style="display:flex;align-items:center;gap:8px;margin-bottom:6px;flex-wrap:wrap">
                        <span style="font-size:9px;font-weight:700;padding:2px 7px;border-radius:4px;background:${sevColor}22;color:${sevColor};border:1px solid ${sevColor}44">${sevLabel}</span>
                        <strong style="font-size:13px;color:#e2e8f0">${escapeHtml(issue.title)}</strong>
                        <span style="font-size:10px;color:#4a5568;text-transform:uppercase">${escapeHtml(issue.category)}</span>
                        ${issue.line ? `<span style="font-size:10px;background:#161923;border:1px solid #1e2435;padding:1px 6px;border-radius:3px;color:#4a5568">L${issue.line}</span>` : ''}
                    </div>
                    <p style="font-size:12px;color:#8892a4;margin:0 0 6px;line-height:1.6">${escapeHtml(issue.description)}</p>
                    ${issue.suggestion ? `<div style="font-size:11px;color:#34d399;background:rgba(52,211,153,0.06);border-left:2px solid #34d399;padding:6px 10px;border-radius:0 4px 4px 0">→ ${escapeHtml(issue.suggestion)}</div>` : ''}
                    ${diff}
                </div>`;
        }).join('');

        return `
            <div style="background:#10121a;border:1px solid #1e2435;border-radius:12px;overflow:hidden;margin-bottom:16px">
                <div style="padding:12px 16px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #1e2435">
                    <div style="display:flex;align-items:center;gap:10px">
                        <span style="font-size:10px;padding:2px 7px;border-radius:4px;background:rgba(79,142,247,0.1);border:1px solid rgba(79,142,247,0.2);color:#4f8ef7;font-weight:600">${escapeHtml(file.language)}</span>
                        <span style="font-size:13px;color:#e2e8f0">${escapeHtml(file.filePath)}</span>
                    </div>
                    <span style="font-family:sans-serif;font-size:14px;font-weight:800;color:${fc}">${file.score}/100</span>
                </div>
                ${issueRows || '<div style="padding:14px 16px;font-size:12px;color:#34d399">✓ Brak problemów.</div>'}            </div>`;
    }).join('');

    return `<!DOCTYPE html>
<html lang="pl">
<head>
<meta charset="UTF-8"/>
<title>Reviq — Raport Code Review</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { background: #0a0b0e; color: #e2e8f0; font-family: 'JetBrains Mono', monospace, monospace; padding: 32px; }
  @media print {
    body { padding: 16px; background: #fff; color: #111; }
    .no-print { display: none !important; }
    pre { white-space: pre-wrap; }
  }
</style>
</head>
<body>
<div style="max-width:900px;margin:0 auto">

  <!-- Header -->
  <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:32px;padding-bottom:20px;border-bottom:1px solid #1e2435">
    <div style="display:flex;align-items:center;gap:12px">
      <div style="width:36px;height:36px;background:linear-gradient(135deg,#4f8ef7,#7c6af7);border-radius:8px;display:flex;align-items:center;justify-content:center;font-size:18px">⚡</div>
      <div>
        <div style="font-family:sans-serif;font-size:20px;font-weight:800">Code<span style="color:#4f8ef7">Review</span> AI</div>
        <div style="font-size:11px;color:#4a5568">Raport wygenerowany: ${now}</div>
      </div>
    </div>
    <div style="text-align:center">
      <div style="font-family:sans-serif;font-size:48px;font-weight:800;color:${scoreColor};line-height:1">${score}</div>
      <div style="font-size:10px;color:#4a5568;text-transform:uppercase;letter-spacing:0.1em">Ogólny wynik</div>
    </div>
  </div>

    <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin-bottom:24px">
    <div style="background:#10121a;border:1px solid #1e2435;border-radius:12px;padding:18px;text-align:center">
      <div style="font-family:sans-serif;font-size:32px;font-weight:800;color:#f87171">${critical}</div>
      <div style="font-size:10px;color:#4a5568;text-transform:uppercase;letter-spacing:0.08em;margin-top:4px">Krytyczne</div>
    </div>
    <div style="background:#10121a;border:1px solid #1e2435;border-radius:12px;padding:18px;text-align:center">
      <div style="font-family:sans-serif;font-size:32px;font-weight:800;color:#fbbf24">${warnings}</div>
      <div style="font-size:10px;color:#4a5568;text-transform:uppercase;letter-spacing:0.08em;margin-top:4px">Ostrzeżenia</div>
    </div>
    <div style="background:#10121a;border:1px solid #1e2435;border-radius:12px;padding:18px;text-align:center">
      <div style="font-family:sans-serif;font-size:32px;font-weight:800;color:#4f8ef7">${info}</div>
      <div style="font-size:10px;color:#4a5568;text-transform:uppercase;letter-spacing:0.08em;margin-top:4px">Informacje</div>
    </div>
  </div>
  ${ignoredCount > 0 ? `<div style="font-size:11px;color:#4a5568;margin-bottom:16px;font-style:italic">* Pominięto ${ignoredCount} issue${ignoredCount > 1 ? 's' : ''} oznaczonych jako false positive.</div>` : ''}

  ${data.summary.generalFeedback ? `<div style="background:#10121a;border:1px solid #1e2435;border-left:3px solid #7c6af7;border-radius:0 12px 12px 0;padding:14px 18px;font-size:13px;color:#8892a4;line-height:1.7;margin-bottom:24px">💬 ${escapeHtml(data.summary.generalFeedback)}</div>` : ''}

  <!-- Files -->
  ${issuesHTML}

</div>
</body>
</html>`;
}

function exportHTML() {
    if (!_lastResults) return;
    const html = buildReportHTML(_lastResults);
    const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `reviq-report-${new Date().toISOString().slice(0, 10)}.html`;
    a.click();
    URL.revokeObjectURL(a.href);
}

function exportPDF() {
    if (!_lastResults) return;
    const html = buildReportHTML(_lastResults);
    const win = window.open('', '_blank');
    win.document.write(html);
    win.document.close();
    win.focus();
    setTimeout(() => { win.print(); }, 500);
}

// ── Init ──────────────────────────────────────────────────────────────────────
checkOllama();
setInterval(checkOllama, 15000);

// ── History ───────────────────────────────────────────────────────────────────
async function loadHistory() {
    const container = document.getElementById('historyList');
    container.innerHTML = `<div class="history-loading">Ładowanie historii...</div>`;

    try {
        const res = await fetch(`${API}/history`);
        const data = await res.json();

        if (!data.length) {
            container.innerHTML = `<div class="history-empty">📭 Brak poprzednich analiz w tej sesji.</div>`;
            return;
        }

        container.innerHTML = data.map(item => {
            const date = new Date(item.createdAt).toLocaleString('pl-PL');
            const score = item.overallScore;
            const sc = score >= 80 ? 'var(--green)' : score >= 60 ? 'var(--yellow)' : 'var(--red)';
            const srcIcon = item.source === 'repo' ? '📁' : '📄';
            const badges = [
                item.critical > 0 ? `<span class="badge critical">⚠ ${item.critical}</span>` : '',
                item.warnings > 0 ? `<span class="badge warning">! ${item.warnings}</span>` : '',
                item.info > 0 ? `<span class="badge info">i ${item.info}</span>` : '',
            ].join('');
            const filesLabel = `${item.fileCount} plik${item.fileCount === 1 ? '' : item.fileCount < 5 ? 'i' : 'ów'}`;

            return `
            <div class="history-entry" id="hentry-${item.reviewId}">
                <div class="history-item" onclick="toggleHistoryItem('${item.reviewId}')">
                    <div class="history-item-left">
                        <span class="history-source">${srcIcon}</span>
                        <div class="history-meta">
                            <div class="history-label">${escapeHtml(item.label || item.reviewId)}</div>
                            <div class="history-date">${date} &middot; ${filesLabel}</div>
                        </div>
                    </div>
                    <div class="history-item-right">
                        <div class="history-badges">${badges}</div>
                        <div class="history-score" style="color:${sc}">${score}/100</div>
                        <span class="history-chevron" id="hchev-${item.reviewId}">▶</span>
                    </div>
                </div>
                <div class="history-detail" id="hdetail-${item.reviewId}" style="display:none">
                    <div class="history-detail-loading">Ładowanie wyników...</div>
                </div>
            </div>`;
        }).join('');
    } catch {
        container.innerHTML = `<div class="history-empty" style="color:var(--red)">⚠ Błąd ładowania historii.</div>`;
    }
}

async function toggleHistoryItem(id) {
    const detail = document.getElementById(`hdetail-${id}`);
    const chev = document.getElementById(`hchev-${id}`);
    const open = detail.style.display !== 'none';

    if (open) {
        detail.style.display = 'none';
        chev.textContent = '▶';
        return;
    }

    detail.style.display = '';
    chev.textContent = '▼';

    // Załaduj tylko raz
    if (detail.dataset.loaded) return;
    detail.dataset.loaded = '1';

    try {
        const res = await fetch(`${API}/history/${id}`);
        const data = await res.json();

        const dto = {
            reviewId: data.reviewId,
            summary: {
                overallScore: data.summary.overallScore,
                critical: data.summary.critical,
                warnings: data.summary.warnings,
                info: data.summary.info,
                generalFeedback: data.summary.generalFeedback
            },
            files: (data.files || []).map(f => ({
                filePath: f.filePath,
                language: f.language,
                score: f.score,
                issues: (f.issues || []).map(i => ({
                    severity: i.severity,
                    category: i.category,
                    title: i.title,
                    description: i.description,
                    suggestion: i.suggestion,
                    line: i.line,
                    codeBefore: i.codeBefore,
                    codeAfter: i.codeAfter
                }))
            }))
        };

        // Wyrenderuj wyniki bezpośrednio w panelu historii
        detail.innerHTML = '';
        renderResultsInto(dto, detail);
    } catch {
        detail.innerHTML = `<div class="history-empty" style="color:var(--red)">⚠ Nie udało się załadować analizy.</div>`;
    }
}

function closeHistoryDetail() { }  // zachowane dla kompatybilności z HTML