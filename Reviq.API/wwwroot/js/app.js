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

    const ext = {
        'C#': '.cs', 'TypeScript': '.ts', 'JavaScript': '.js',
        'Python': '.py', 'Java': '.java', 'Go': '.go', 'Rust': '.rs', 'PHP': '.php'
    };
    const lang = document.getElementById('snippetLang').value;
    const fn = document.getElementById('snippetFileName');
    if (fn.value === 'snippet.cs' || fn.value.startsWith('snippet.')) {
        fn.value = 'snippet' + (ext[lang] || '.txt');
    }
}

document.getElementById('snippetLang').addEventListener('change', updateLineCount);

// ── Snippet review ────────────────────────────────────────────────────────────
async function startSnippetReview() {
    const code = document.getElementById('snippetCode').value.trim();
    const language = document.getElementById('snippetLang').value;
    const fileName = document.getElementById('snippetFileName').value.trim() || 'snippet.cs';
    const model = document.getElementById('snippetModel').value;

    if (!code) return showSnippetError('Wklej kod do analizy.');
    clearSnippetError();

    const btn = document.getElementById('snippetBtn');
    btn.disabled = true;
    btn.textContent = '⏳ Analizuję...';
    showLoader();

    try {
        const r = await fetch(`${API}/code/review?model=${encodeURIComponent(model)}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code, language, fileName })
        });

        const data = await r.json();
        if (!r.ok) { showSnippetError(data.error || 'Błąd analizy.'); return; }
        renderResults(data);
    } catch {
        showSnippetError('Nie można połączyć z API.');
    } finally {
        btn.disabled = false;
        btn.textContent = '⚡ Analizuj kod';
        hideLoader();
    }
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
                    <span style="color:var(--text3)">Branch:</span>
                    <span style="color:var(--accent)">${d.branch}</span>
                </div>
                <div style="display:flex;gap:8px;align-items:center">
                    <span style="color:var(--text3)">Commit:</span>
                    <span>${d.latestCommit}</span>
                    <span style="color:var(--text3);font-size:11px">${d.commitMessage}</span>
                </div>
                <div style="color:var(--text3);margin-top:4px">Pliki do analizy (${d.changedFiles.length}):</div>
                ${d.changedFiles.map(f => `<div style="color:var(--text2);padding-left:8px">• ${f}</div>`).join('')}
                ${d.changedFiles.length === 0 ? '<div style="color:var(--yellow)">Brak zmienionych plików.</div>' : ''}
            `;
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
        hideLoader();
    }
}

// ── Render results ────────────────────────────────────────────────────────────
function renderResults(data) {
    const area = document.getElementById('resultsArea');
    const scoreColor = data.summary.overallScore >= 80 ? 'var(--green)'
        : data.summary.overallScore >= 60 ? 'var(--yellow)' : 'var(--red)';
    const circ = Math.PI * 2 * 26;
    const offset = circ * (1 - data.summary.overallScore / 100);

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
                <div class="score-ring-text" style="color:${scoreColor}">${data.summary.overallScore}</div>
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
                        <span class="file-lang">${file.language}</span>
                        <span class="file-path">${file.filePath}</span>
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
                    ${file.issues.map(issue => `
                        <div class="issue-item">
                            <div class="issue-severity"><div class="sev-bar ${issue.severity}"></div></div>
                            <div class="issue-content">
                                <div class="issue-title-row">
                                    <span class="issue-title">${issue.title}</span>
                                    <span class="issue-cat">${issue.category}</span>
                                    ${issue.line ? `<span class="issue-line">L${issue.line}</span>` : ''}
                                </div>
                                <div class="issue-desc">${issue.description}</div>
                                ${issue.suggestion ? `<div class="issue-suggestion">${issue.suggestion}</div>` : ''}
                            </div>
                        </div>`).join('')}
                    ${file.issues.length === 0
                ? `<div style="padding:14px 16px;font-size:12px;color:var(--green)">✓ Brak problemów.</div>`
                : ''}
                </div>
            </div>`;
    });

    area.innerHTML = html;
}

function toggleCard(idx) {
    document.getElementById(`card${idx}`).classList.toggle('open');
}

// ── Loader ────────────────────────────────────────────────────────────────────
function showLoader() {
    document.getElementById('resultsArea').innerHTML = `
        <div class="panel">
            <div class="panel-header"><div class="dot"></div>Wyniki analizy</div>
            <div class="loader active">
                <div class="spinner"></div>
                <div class="loader-text">Analizuję kod lokalnie (Ollama)...</div>
                <div style="font-size:11px;color:var(--text3)">Może zająć 1–3 min</div>
            </div>
        </div>`;
}

function hideLoader() { }

// ── Error helpers ─────────────────────────────────────────────────────────────
function showError(msg) { const b = document.getElementById('errorBox'); b.textContent = '⚠ ' + msg; b.classList.add('active'); }
function clearError() { document.getElementById('errorBox').classList.remove('active'); }
function showSnippetError(msg) { const b = document.getElementById('snippetErrorBox'); b.textContent = '⚠ ' + msg; b.classList.add('active'); }
function clearSnippetError() { document.getElementById('snippetErrorBox').classList.remove('active'); }

// ── Init ──────────────────────────────────────────────────────────────────────
checkOllama();
setInterval(checkOllama, 15000);