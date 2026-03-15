// ── History ─────────────────────────────────────────────────────────────────
async function loadHistory() {
    const container = document.getElementById('historyList');
    container.innerHTML = `<div class="history-loading">${t('history.loading')}</div>`;

    try {
        const res = await fetch(`${API}/history`);
        const data = await res.json();

        if (!data.length) {
            container.innerHTML = `<div class="history-empty">${t('history.empty')}</div>`;
            return;
        }

        container.innerHTML = data.map(item => {
            const date = new Date(item.createdAt).toLocaleString(_lang === 'pl' ? 'pl-PL' : 'en-GB');
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
        container.innerHTML = `<div class="history-empty" style="color:var(--red)">${t('history.errorLoad')}</div>`;
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

        detail.innerHTML = '';
        renderResultsInto(dto, detail);
    } catch {
        detail.innerHTML = `<div class="history-empty" style="color:var(--red)">${t('history.errorDetail')}</div>`;
    }
}

function closeHistoryDetail() { }