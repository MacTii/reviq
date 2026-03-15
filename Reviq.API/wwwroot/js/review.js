// ── Review ──────────────────────────────────────────────────────────────────
// ── Model sync before review ───────────────────────────────────────────────────
async function syncModelBeforeReview(scope) {
    const model = document.getElementById(scope === 'snippet' ? 'snippetModel' : 'modelSelect').value;
    if (model) {
        await fetch(`${API}/ai/model`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ model })
        });
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
    document.getElementById('lineCount').textContent = t('lines', { n: lines });
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
    document.getElementById('previewModalMeta').textContent = t('lines', { n: lines });
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
    await syncModelBeforeReview('snippet');
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

    if (filesToReview.length === 0) return showSnippetError(t('error.noCode'));
    if (!model) return showSnippetError(t('error.noModel'));
    clearSnippetError();

    const btn = document.getElementById('snippetBtn');
    btn.disabled = true;
    btn.textContent = t('analyzing.files', { n: filesToReview.length, files: filesToReview.length > 1 ? t('files.countPlural') : t('files.count') });
    showLoader(t('analyzing.files', { n: filesToReview.length, files: filesToReview.length > 1 ? t('files.countPlural') : t('files.count') }));

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
        if (!res.ok) { showSnippetError(data.error || t('error.analysisError')); return; }

        renderResults(data);
    } catch (err) {
        showSnippetError(`${t('error.analysisError')} ${err.message}`);
    } finally {
        btn.disabled = false;
        btn.textContent = t('btn.analyze');
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
// ── Path history ──────────────────────────────────────────────────────────────
const _recentPaths = JSON.parse(sessionStorage.getItem('recentPaths') || '[]');

function saveRecentPath(path) {
    if (!path) return;
    const idx = _recentPaths.indexOf(path);
    if (idx >= 0) _recentPaths.splice(idx, 1);
    _recentPaths.unshift(path);
    if (_recentPaths.length > 8) _recentPaths.pop();
    sessionStorage.setItem('recentPaths', JSON.stringify(_recentPaths));
    updatePathDatalist();
}

function updatePathDatalist() {
    const dl = document.getElementById('pathHistory');
    if (!dl) return;
    dl.innerHTML = _recentPaths.map(p => `<option value="${escapeHtml(p)}">`).join('');
}

function getSelectedCategories() {
    const scopeMap = { bugs: 'Bug', security: 'Security', bestpractice: 'BestPractice', refactor: 'Refactor' };
    return [...document.querySelectorAll('#scopeChecks .checkbox-item.active')]
        .map(el => scopeMap[el.dataset.scope])
        .filter(Boolean);
}

document.addEventListener('DOMContentLoaded', updatePathDatalist);

function renderRepoInfo(d, diffScope) {
    const content = document.getElementById('repoInfoContent');
    if (!content) return;
    const scopeLabels = [
        t('diffScope.lastCommit'),
        t('diffScope.sinceLastPush'),
        t('diffScope.uncommitted'),
        t('diffScope.allFiles')
    ];
    content.innerHTML = `
        <div style="display:flex;gap:8px;align-items:center">
            <span style="color:var(--text3)">${t('repo.branch')}:</span>
            <span style="color:var(--accent)">${escapeHtml(d.branch)}</span>
        </div>
        <div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap">
            <span style="color:var(--text3)">${t('repo.lastCommit')}:</span>
            <span style="font-family:var(--mono)">${escapeHtml(d.latestCommit)}</span>
            <span style="color:var(--text3);font-size:11px">${escapeHtml(d.commitMessage)}</span>
        </div>
        <div style="color:var(--text3);margin-top:4px">
            ${t('repo.files')} — <span style="color:var(--accent2)">${scopeLabels[diffScope]}</span>
            <span style="color:var(--text)">(${d.changedFiles.length})</span>:
        </div>
        <div style="display:flex;flex-direction:column;gap:4px;margin-top:4px;max-height:260px;overflow-y:auto;padding-right:4px">
            ${d.changedFiles.map(f => {
        const parts = f.replace(/\\/g, '/').split('/');
        const file = parts.pop();
        const dir = parts.join('/');
        return `<div style="display:flex;align-items:center;gap:8px;background:var(--surface2);border:1px solid var(--border);border-radius:6px;padding:5px 10px">
                    <span style="color:var(--text3);font-size:10px;flex-shrink:0">•</span>
                    <div style="min-width:0">
                        <div style="font-family:var(--mono);font-size:12px;color:var(--text);white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${escapeHtml(file)}</div>
                        ${dir ? `<div style="font-family:var(--mono);font-size:10px;color:var(--text3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${escapeHtml(dir)}</div>` : ''}
                    </div>
                </div>`;
    }).join('')}
            ${d.changedFiles.length === 0 ? `<div style="color:var(--yellow)">${t('repo.noFiles')}</div>` : ''}
        </div>`;
}

async function checkRepo() {
    const path = document.getElementById('repoPath').value.trim();
    if (!path) return showError(t('error.noRepoPath'));

    const preview = document.getElementById('repoPreview');
    const diffScope = parseInt(document.getElementById('diffScope')?.value ?? '0');

    // Toggle — ta sama ścieżka i ten sam zakres → zamknij
    if (preview.style.display === 'block' && preview.dataset.path === path && parseInt(preview.dataset.scope ?? '-1') === diffScope) {
        preview.style.display = 'none';
        return;
    }

    try {
        const r = await fetch(`${API}/git/info?path=${encodeURIComponent(path)}&diffScope=${diffScope}`);
        const d = await r.json();
        const content = document.getElementById('repoInfoContent');
        if (!d.isValid) {
            content.innerHTML = `<span style="color:var(--red)">${d.error || t('repo.error.invalid')}</span>`;
        } else {
            _lastRepoInfo = { d, diffScope };
            renderRepoInfo(d, diffScope);
        }
        preview.dataset.path = path;
        preview.dataset.scope = diffScope;
        preview.style.display = 'block';
    } catch {
        showError(t('error.noConnection'));
    }
}

async function startReview() {
    await syncModelBeforeReview('repo');
    const path = document.getElementById('repoPath').value.trim();
    const model = document.getElementById('modelSelect').value;
    const diffScope = parseInt(document.getElementById('diffScope')?.value ?? '0');
    if (!path) return showError(t('error.noRepoPath'));
    if (!model) return showError(t('error.noModel'));
    clearError();

    const btn = document.getElementById('reviewBtn');
    btn.disabled = true;
    btn.textContent = t('btn.analyzing');
    showLoader();

    try {
        const r = await fetch(`${API}/review?model=${encodeURIComponent(model)}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ repoPath: path, files: [], categories: getSelectedCategories(), diffScope })
        });
        const data = await r.json();
        if (!r.ok) { showError(data.error || t('error.analysisErrorRepo')); return; }
        saveRecentPath(path);
        renderResults(data);
    } catch {
        showError(t('error.noConnection'));
    } finally {
        btn.disabled = false;
        btn.textContent = t('btn.runReview');
    }
}