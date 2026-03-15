// ── Local AI ─────────────────────────────────────────────────────────────────
// ── HuggingFace Search ────────────────────────────────────────────────────────
async function searchHuggingFace() {
    const q = document.getElementById('hfSearchInput').value.trim() || 'coder gguf';
    const results = document.getElementById('hfSearchResults');
    const repoPanel = document.getElementById('hfRepoFiles');

    results.innerHTML = `<div style="color:var(--text3);font-size:12px">🔍 ${escapeHtml(q)}...</div>`;
    repoPanel.style.display = 'none';

    try {
        const r = await fetch(`${API}/localai/hf/search?q=${encodeURIComponent(q)}&limit=20`);
        const data = await r.json();

        if (!data.length) {
            results.innerHTML = `<div style="color:var(--text3);font-size:12px">${t('localai.hfNoResults')}</div>`;
            return;
        }

        results.innerHTML = data.map(m => `
            <div style="display:flex;align-items:center;gap:10px;background:var(--surface2);border:1px solid var(--border);border-radius:6px;padding:8px 12px;cursor:pointer;transition:border-color .15s"
                 onmouseover="this.style.borderColor='var(--accent)'" onmouseout="this.style.borderColor='var(--border)'"
                 onclick="loadHfRepoFiles('${escapeHtml(m.id)}')">
                <div style="flex:1;min-width:0">
                    <div style="font-size:12px;font-family:var(--mono);color:var(--text);white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${escapeHtml(m.id)}</div>
                    <div style="font-size:10px;color:var(--text3);margin-top:2px" class="hf-downloads" data-downloads="${m.downloads || 0}" data-likes="${m.likes || 0}">⬇ ${(m.downloads || 0).toLocaleString()} ${t('localai.hfDownloads')} · ❤ ${m.likes || 0}</div>
                </div>
                <span style="font-size:10px;color:var(--accent)" class="hf-files-link">${t('localai.hfFiles')}</span>
            </div>`).join('');
    } catch {
        results.innerHTML = `<div style="color:var(--red);font-size:12px">${t('localai.hfSearchError')}</div>`;
    }
}

async function loadHfRepoFiles(repo) {
    const panel = document.getElementById('hfRepoFiles');
    const nameEl = document.getElementById('hfRepoName');
    const filesEl = document.getElementById('hfFilesList');

    panel.style.display = 'block';
    nameEl.textContent = repo;
    filesEl.innerHTML = `<div style="color:var(--text3);font-size:12px">${t('localai.hfLoadingFiles')}</div>`;

    try {
        const r = await fetch(`${API}/localai/hf/files?repo=${encodeURIComponent(repo)}`);
        const data = await r.json();

        if (!data.files.length) {
            filesEl.innerHTML = `<div style="color:var(--text3);font-size:12px">${t('localai.hfNoFiles')}</div>`;
            return;
        }

        filesEl.innerHTML = data.files.map(f => {
            // baseName — tylko nazwa pliku bez subfoldera (zapisywany i trackowany po tej nazwie)
            const baseName = f.fileName.includes('/') ? f.fileName.split('/').pop() : f.fileName;
            const id = baseName.replace(/\./g, '-');
            const isDownloading = !!_localAIDownloadPollers[baseName];
            const sizeStr = f.sizeMb > 0 ? Math.round(f.sizeMb) + ' MB' : '?';
            return `
            <div id="hf-row-${id}" style="padding:6px 8px;border-radius:5px;background:var(--surface3);margin-bottom:4px">
                <div style="display:flex;align-items:center;gap:8px">
                    <div style="flex:1;min-width:0">
                        <div style="font-size:11px;font-family:var(--mono);color:var(--text);white-space:nowrap;overflow:hidden;text-overflow:ellipsis" title="${escapeHtml(f.fileName)}">${escapeHtml(baseName)}</div>
                        <div style="font-size:10px;color:var(--text3)">${sizeStr}</div>
                    </div>
                    <div class="hf-btn-area">
                    ${f.isInstalled
                    ? `<button class="btn-sm btn-accent i18n-btn" data-i18n-key="localai.use" onclick="useLocalAIModel('${escapeHtml(baseName)}')">${t('localai.use')}</button>`
                    : isDownloading
                        ? `<button class="btn-sm i18n-btn" data-i18n-key="localai.cancel" style="background:rgba(239,68,68,.1);color:#ef4444" onclick="cancelLocalAIDownload('${escapeHtml(baseName)}','${escapeHtml(repo)}','hf')">${t('localai.cancel')}</button>`
                        : `<button class="btn-sm btn-accent i18n-btn" data-i18n-key="localai.download" onclick="startLocalAIDownload('${escapeHtml(repo)}','${escapeHtml(f.fileName)}','hf')">${t('localai.download')}</button>`
                }
                    </div>
                </div>
                <div id="hf-prog-${id}" style="display:${isDownloading ? 'block' : 'none'};margin-top:6px">
                    <div style="background:var(--surface2);border-radius:4px;height:4px;overflow:hidden">
                        <div class="dl-bar" style="background:var(--accent);height:4px;width:0%;transition:width .3s"></div>
                    </div>
                    <div class="dl-text" style="font-size:10px;color:var(--text3);margin-top:3px"></div>
                </div>
            </div>`;
        }).join('');

        // Wznów pollery dla aktywnych pobierań
        data.files.forEach(f => {
            const baseName = f.fileName.includes('/') ? f.fileName.split('/').pop() : f.fileName;
            if (_localAIDownloadPollers[baseName])
                startDownloadPoller(baseName, 'hf', repo);
        });
    } catch {
        filesEl.innerHTML = `<div style="color:var(--red);font-size:12px">${t('localai.hfFilesError')}</div>`;
    }
}

function closeHfRepoFiles() {
    document.getElementById('hfRepoFiles').style.display = 'none';
}


let _localAIDownloadPollers = {};

function openLocalAIModal() {
    document.getElementById('localAIModal').style.display = 'flex';
    refreshLocalAIModal();
}

function closeLocalAIModal() {
    document.getElementById('localAIModal').style.display = 'none';
    // Zatrzymaj pollery
    Object.values(_localAIDownloadPollers).forEach(clearInterval);
    _localAIDownloadPollers = {};
}

async function refreshLocalAIModal() {
    await Promise.all([loadInstalledModels(), loadRecommendedModels()]);
}

async function loadInstalledModels() {
    const container = document.getElementById('localAIInstalled');
    try {
        const r = await fetch(`${API}/localai/models`);
        const data = await r.json();

        const dirEl = document.getElementById('localAIModelsDir');
        if (dirEl && data.modelsDir) dirEl.textContent = `📁 ${data.modelsDir}`;

        if (!data.models.length) {
            container.innerHTML = `<div style="color:var(--text3);font-size:12px">${t('localai.noModels')}</div>`;
            return;
        }

        container.innerHTML = data.models.map(m => {
            const isActive = currentProvider === 'LocalAI' && currentModel === m.fileName;
            return `
            <div style="display:flex;align-items:center;gap:10px;background:var(--surface2);border:1px solid ${isActive ? 'var(--accent)' : 'var(--border)'};border-radius:6px;padding:8px 12px;margin-bottom:6px">
                <div style="flex:1;min-width:0">
                    <div style="font-family:var(--mono);font-size:12px;color:var(--text);white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${escapeHtml(m.fileName)}</div>
                    <div style="font-size:11px;color:var(--text3);margin-top:2px">${m.sizeMb} MB</div>
                </div>
                ${isActive
                    ? `<span style="font-size:10px;padding:2px 8px;background:rgba(79,142,247,.15);color:var(--accent);border-radius:4px">${t('localai.active')}</span>`
                    : `<button class="btn-sm btn-accent" onclick="useLocalAIModel('${escapeHtml(m.fileName)}')">${t('localai.use')}</button>`
                }
                <button class="btn-sm" style="background:rgba(239,68,68,.1);color:#ef4444;border-color:rgba(239,68,68,.3)"
                        onclick="deleteLocalAIModel('${escapeHtml(m.fileName)}')">🗑</button>
            </div>`;
        }).join('');
    } catch {
        container.innerHTML = `<div style="color:var(--red);font-size:12px">${t('localai.loadingError')}</div>`;
    }
}

async function loadRecommendedModels() {
    const container = document.getElementById('localAIRecommended');
    try {
        const r = await fetch(`${API}/localai/recommended`);
        const data = await r.json();

        container.innerHTML = data.map(m => `
            <div id="rec-${m.fileName.replace(/\./g, '-')}"
                 style="display:flex;align-items:center;gap:10px;background:var(--surface2);border:1px solid var(--border);border-radius:6px;padding:10px 12px">
                <div style="flex:1;min-width:0">
                    <div style="font-size:13px;font-weight:600;color:var(--text)">${escapeHtml(m.name)}</div>
                    <div style="font-size:11px;color:var(--text3);margin-top:2px">${t(m.description)} — ${m.sizeMb} MB</div>
                    <div id="prog-${m.fileName.replace(/\./g, '-')}" style="display:none;margin-top:6px">
                        <div style="background:var(--surface3);border-radius:4px;height:4px;overflow:hidden">
                            <div class="dl-bar" style="background:var(--accent);height:4px;width:0%;transition:width .3s"></div>
                        </div>
                        <div class="dl-text" style="font-size:10px;color:var(--text3);margin-top:3px"></div>
                    </div>
                </div>
                ${m.isInstalled
                ? `<button class="btn-sm btn-accent i18n-btn" data-i18n-key="localai.use" onclick="useLocalAIModel('${m.fileName}')">${t('localai.use')}</button>`
                : m.isDownloading
                    ? `<button class="btn-sm i18n-btn" data-i18n-key="localai.cancel" style="background:rgba(239,68,68,.1);color:#ef4444" onclick="cancelLocalAIDownload('${m.fileName}','${m.repo}','recommended')">${t('localai.cancel')}</button>`
                    : `<button class="btn-sm btn-accent i18n-btn" data-i18n-key="localai.download" onclick="startLocalAIDownload('${m.repo}','${m.fileName}')">${t('localai.download')}</button>`
            }
            </div>`).join('');

        // Wznów pollery dla aktywnych pobierań
        data.filter(m => m.isDownloading).forEach(m => startDownloadPoller(m.fileName));

    } catch {
        container.innerHTML = `<div style="color:var(--red);font-size:12px">${t('localai.listError')}</div>`;
    }
}

async function useLocalAIModel(fileName) {
    await fetch(`${API}/ai/provider`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ provider: 'LocalAI', model: fileName })
    });
    currentProvider = 'LocalAI';
    currentModel = fileName;
    closeLocalAIModal();

    // Zaktualizuj select bezpośrednio — bez dodatkowego request do API
    ['snippetModel', 'modelSelect'].forEach(id => {
        const sel = document.getElementById(id);
        if (!sel) return;
        // Dodaj opcję jeśli nie istnieje
        if (!Array.from(sel.options).find(o => o.value === fileName)) {
            sel.innerHTML = `<option value="${fileName}" selected>${fileName}</option>`;
        } else {
            sel.value = fileName;
        }
    });

    // Odśwież badge i przycisk providera
    ['snippetModelBadge', 'repoModelBadge'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.textContent = 'LOCAL';
    });
    const r = await fetch(`${API}/ai/providers`);
    const d = await r.json();
    renderProviderMenu(d.providers);
    updateProviderBtn();
}

async function startLocalAIDownload(repo, fileName, source = 'recommended') {
    try {
        await fetch(`${API}/localai/download`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ repo, fileName })
        });

        // Zarejestruj poller
        const baseName = fileName.includes('/') ? fileName.split('/').pop() : fileName;
        startDownloadPoller(baseName, source, repo);

        if (source === 'hf') {
            // Podmień przycisk inline — bez pełnego przeładowania (które robi HEAD requesty)
            const id = baseName.replace(/\./g, '-');
            const row = document.getElementById(`hf-row-${id}`);
            if (row) {
                row.querySelector('.hf-btn-area').innerHTML =
                    `<button class="btn-sm" style="background:rgba(239,68,68,.1);color:#ef4444"
                        onclick="cancelLocalAIDownload('${baseName}','${escapeHtml(repo)}','hf')">${t('localai.cancel')}</button>`;
                const progEl = document.getElementById(`hf-prog-${id}`);
                if (progEl) progEl.style.display = 'block';
            }
        } else {
            await loadRecommendedModels();
        }
    } catch { /* ignore */ }
}

async function cancelLocalAIDownload(fileName, repo = null, source = 'recommended') {
    // Wyczyść UI natychmiast
    const id = fileName.replace(/\./g, '-');
    const progEl = document.getElementById(`hf-prog-${id}`) || document.getElementById(`prog-${id}`);
    if (progEl) progEl.style.display = 'none';

    const btnArea = document.getElementById(`hf-row-${id}`)?.querySelector('.hf-btn-area');
    if (btnArea) btnArea.innerHTML = `<span style="font-size:10px;color:var(--text3)">${t('localai.cancelled')}</span>`;

    stopDownloadPoller(fileName);
    await fetch(`${API}/localai/download/${encodeURIComponent(fileName)}/cancel`, { method: 'POST' });

    await new Promise(r => setTimeout(r, 600));

    if (source === 'hf' && repo) await loadHfRepoFiles(repo);
    else await refreshLocalAIModal();
}

async function deleteLocalAIModel(fileName) {
    if (!confirm(`${t('localai.delete')} ${fileName}?`)) return;
    await fetch(`${API}/localai/models/${encodeURIComponent(fileName)}`, { method: 'DELETE' });

    // Jeśli usunięto aktywny model — wyczyść select
    if (currentProvider === 'LocalAI' && currentModel === fileName) {
        currentModel = '';
        ['snippetModel', 'modelSelect'].forEach(id => {
            const sel = document.getElementById(id);
            if (sel) sel.innerHTML = `<option value="">${t('localai.noModels')}</option>`;
        });
    }
    await refreshLocalAIModal();
    // Odśwież pełną listę modeli
    await loadModelsForProvider('LocalAI', currentModel);
}

function setLocalAICustomModel() {
    const path = document.getElementById('localAICustomPath').value.trim();
    if (!path) return;
    // Ustaw LocalAI jako aktywny provider z tym modelem
    fetch(`${API}/ai/provider`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ provider: 'LocalAI', model: path })
    }).then(() => {
        closeLocalAIModal();
        initProviders();
    });
}

function startDownloadPoller(fileName, source = 'recommended', repo = null) {
    if (_localAIDownloadPollers[fileName]) return;
    _localAIDownloadPollers[fileName] = setInterval(
        () => pollDownloadStatus(fileName, source, repo), 1000
    );
}

function stopDownloadPoller(fileName) {
    clearInterval(_localAIDownloadPollers[fileName]);
    delete _localAIDownloadPollers[fileName];
}

async function pollDownloadStatus(fileName, source = 'recommended', repo = null) {
    try {
        const r = await fetch(`${API}/localai/download/${encodeURIComponent(fileName)}/status`);
        const data = await r.json();
        const id = fileName.replace(/\./g, '-');

        // Progress w sekcji Polecane
        const progEl = document.getElementById(`prog-${id}`);
        if (progEl) {
            progEl.style.display = 'block';
            progEl.querySelector('.dl-bar').style.width = `${data.progress}%`;
            progEl.querySelector('.dl-text').textContent =
                t('localai.progress', { done: formatBytes(data.downloadedBytes), total: formatBytes(data.totalBytes) });
        }

        // Progress w panelu HF files
        const hfProgEl = document.getElementById(`hf-prog-${id}`);
        if (hfProgEl) {
            hfProgEl.style.display = 'block';
            hfProgEl.querySelector('.dl-bar').style.width = `${data.progress}%`;
            hfProgEl.querySelector('.dl-text').textContent =
                `${data.progress}% · ${formatBytes(data.downloadedBytes)} / ${formatBytes(data.totalBytes)}`;
        }

        if (!data.isRunning) {
            stopDownloadPoller(fileName);
            if (data.error) {
                // Cancelled lub błąd — wyczyść progress bar natychmiast
                const id2 = fileName.replace(/\./g, '-');
                const pEl = document.getElementById(`hf-prog-${id2}`) || document.getElementById(`prog-${id2}`);
                if (pEl) pEl.style.display = 'none';
            }
            if (source === 'hf' && repo) await loadHfRepoFiles(repo);
            else await refreshLocalAIModal();
        }
    } catch { stopDownloadPoller(fileName); }
}

function formatBytes(bytes) {
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

// ── Error helpers ─────────────────────────────────────────────────────────────