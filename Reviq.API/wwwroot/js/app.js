const API = 'http://localhost:5000/api';

// ── Tabs ──────────────────────────────────────────────────────────────────────
function switchTab(tab) {
    document.getElementById('tabSnippet').classList.toggle('active', tab === 'snippet');
    document.getElementById('tabRepo').classList.toggle('active', tab === 'repo');
    document.getElementById('contentSnippet').classList.toggle('active', tab === 'snippet');
    document.getElementById('contentRepo').classList.toggle('active', tab === 'repo');
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
                    opt.textContent = m + (m.includes('deepseek-coder-v2') ? ' (zalecany)' : '');
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
document.querySelectorAll('.checkbox-item').forEach(item => {
    item.addEventListener('click', () => {
        item.classList.toggle('active');
        item.querySelector('.check-icon').textContent = item.classList.contains('active') ? '✓' : '';
    });
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
            <span class="file-tag-remove" onclick="removeFile('${escapeHtml(f.name)}')" title="Usuń">✕</span>
        </div>`).join('');
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
        // Wysyłamy każdy plik osobno i zbieramy wyniki
        const allResults = [];
        for (const f of filesToReview) {
            const r = await fetch(`${API}/code/review?model=${encodeURIComponent(model)}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ code: f.code, language: f.language, fileName: f.fileName })
            });
            const data = await r.json();
            if (!r.ok) { showSnippetError(data.error || 'Błąd analizy.'); return; }
            allResults.push(data);
        }

        if (allResults.length === 1) {
            renderResults(allResults[0]);
        } else {
            renderMultiResults(allResults);
        }
    } catch {
        showSnippetError('Nie można połączyć z API.');
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
async function checkRepo() {
    const path = document.getElementById('repoPath').value.trim();
    if (!path) return showError('Podaj ścieżkę do repozytorium.');
    try {
        const r = await fetch(`${API}/git/info?path=${encodeURIComponent(path)}`);
        const d = await r.json();
        const preview = document.getElementById('repoPreview');
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
function renderResults(data) {
    const area = document.getElementById('resultsArea');
    const score = data.summary.overallScore;
    const scoreColor = score >= 80 ? 'var(--green)' : score >= 60 ? 'var(--yellow)' : 'var(--red)';
    const circ = Math.PI * 2 * 26;
    const offset = circ * (1 - score / 100);

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

    data.files.forEach((file, idx) => {
        const crit = file.issues.filter(i => i.severity === 'Critical').length;
        const warn = file.issues.filter(i => i.severity === 'Warning').length;
        const info = file.issues.filter(i => i.severity === 'Info').length;
        const fc = file.score >= 80 ? 'var(--green)' : file.score >= 60 ? 'var(--yellow)' : 'var(--red)';

        html += `
            <div class="file-card" id="card${idx}">
                <div class="file-card-header" onclick="toggleCard(${idx})">
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
                        <span class="file-score" style="color:${fc}">${file.score}/100</span>
                        <span class="chevron">▼</span>
                    </div>
                </div>
                <div class="file-issues">
                    ${file.issues.map((issue, iIdx) => renderIssue(issue, idx, iIdx)).join('')}
                    ${file.issues.length === 0 ? `<div style="padding:14px 16px;font-size:12px;color:var(--green)">✓ Brak problemów.</div>` : ''}
                </div>
            </div>`;
    });

    area.innerHTML = html;
}

function renderIssue(issue, cardIdx, issueIdx) {
    const sevClass = issue.severity; // Critical | Warning | Info
    const diffId = `diff-${cardIdx}-${issueIdx}`;
    const hasDiff = issue.codeBefore || issue.codeAfter;

    return `
        <div class="issue-item">
            <div class="issue-severity"><div class="sev-bar ${sevClass}"></div></div>
            <div class="issue-content">
                <div class="issue-title-row">
                    <span class="issue-badge ${sevClass.toLowerCase()}">${sevLabel(issue.severity)}</span>
                    <span class="issue-title">${escapeHtml(issue.title)}</span>
                    <span class="issue-cat">${escapeHtml(issue.category)}</span>
                    ${issue.line ? `<span class="issue-line">L${issue.line}</span>` : ''}
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
                        <pre class="diff-code before">${escapeHtml(issue.codeBefore)}</pre>
                    </div>` : ''}
                    ${issue.codeAfter ? `
                    <div class="diff-section">
                        <div class="diff-label after">✅ Po</div>
                        <pre class="diff-code after">${escapeHtml(issue.codeAfter)}</pre>
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

function toggleCard(idx) {
    document.getElementById(`card${idx}`).classList.toggle('open');
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

// ── Init ──────────────────────────────────────────────────────────────────────
checkOllama();
setInterval(checkOllama, 15000);