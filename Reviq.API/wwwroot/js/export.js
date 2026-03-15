// ── Export ───────────────────────────────────────────────────────────────────
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
                    ${issue.codeBefore ? `<div style="background:#1a0f0f;padding:4px 10px;font-size:10px;color:#f87171;font-weight:700;border-bottom:1px solid #252d42">❌ ${t('diff.before')}</div><pre style="margin:0;padding:10px;font-family:monospace;font-size:11px;background:#120a0a;color:#c8c8c8;overflow-x:auto;white-space:pre">${escapeHtml(stripCodeFences(issue.codeBefore))}</pre>` : ''}
                    ${issue.codeAfter ? `<div style="background:#0a1a10;padding:4px 10px;font-size:10px;color:#34d399;font-weight:700;border-top:1px solid #252d42;border-bottom:1px solid #252d42">✅ ${t('diff.after')}</div><pre style="margin:0;padding:10px;font-family:monospace;font-size:11px;background:#080f0a;color:#c8c8c8;overflow-x:auto;white-space:pre">${escapeHtml(stripCodeFences(issue.codeAfter))}</pre>` : ''}
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
                ${issueRows || `<div style="padding:14px 16px;font-size:12px;color:#34d399">✓ ${t('results.noIssues')}</div>`}            </div>`;
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