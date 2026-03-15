// ── Providers & Models ──────────────────────────────────────────────────────
// ── Provider & model management ───────────────────────────────────────────────
let currentProvider = 'Ollama';
let currentModel = '';
let _lastRepoInfo = null; // cache dla odświeżenia po zmianie języka

async function initProviders() {
    try {
        const r = await fetch(`${API}/ai/providers`);
        const d = await r.json();

        // Logika wyboru domyślnego providera
        const serverProvider = d.currentProvider ?? 'Ollama';

        // Sprawdź czy LocalAI ma zainstalowane modele
        const localAIModels = await fetch(`${API}/localai/models`)
            .then(r => r.json()).catch(() => ({ models: [] }));
        const hasLocalAIModels = localAIModels.models?.length > 0;

        if (hasLocalAIModels) {
            // LocalAI ma modele — ustaw jako aktywny z pierwszym modelem
            const firstModel = localAIModels.models[0].fileName;
            if (serverProvider !== 'LocalAI') {
                await fetch(`${API}/ai/provider`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ provider: 'LocalAI', model: firstModel })
                });
            }
            currentProvider = 'LocalAI';
            currentModel = d.currentProvider === 'LocalAI' ? (d.currentModel ?? firstModel) : firstModel;
        } else if (serverProvider === 'LocalAI') {
            // LocalAI wybrany ale brak modeli — pokaż onboarding modal
            currentProvider = 'LocalAI';
            currentModel = '';
            setTimeout(() => openLocalAIModal(), 800);
        } else {
            currentProvider = serverProvider;
            currentModel = d.currentModel ?? '';
        }

        renderProviderMenu(d.providers);
        updateProviderBtn();
        await loadModelsForProvider(currentProvider, currentModel);

        // Jeśli żaden provider nie jest dostępny i brak modeli LocalAI — pokaż onboarding
        if (!hasLocalAIModels && !d.providers?.find(p => p.available && p.name !== 'LocalAI')) {
            setTimeout(() => openLocalAIModal(), 800);
        }
    } catch {
        document.getElementById('ollamaDot').className = 'status-dot offline';
        document.getElementById('providerBtnText').textContent = t('provider.unavailable');
    }
}

function renderProviderMenu(providers) {
    const menu = document.getElementById('providerMenu');
    menu.innerHTML = providers.map(p => {
        const dotClass = p.available ? 'online' : (!p.hasConfig ? 'unknown' : 'offline');
        const isActive = p.name === currentProvider;
        const unavail = !p.available;
        return `<div class="provider-menu-item ${isActive ? 'active' : ''} ${unavail ? 'unavailable' : ''}"
                     onclick="selectProvider('${p.name}', ${p.available})">
            <div class="provider-item-left">
                <div class="provider-item-dot ${dotClass}"></div>
                <span class="provider-item-name">${p.label}</span>
            </div>
            <span class="provider-tag">${p.type === 'local' ? 'LOCAL' : 'CLOUD'}</span>
        </div>`;
    }).join('');
}

function updateProviderBtn() {
    const dot = document.getElementById('ollamaDot');
    const btn = document.getElementById('providerBtnText');

    const activeLabel = document.querySelector('.provider-menu-item.active .provider-item-name');
    btn.textContent = activeLabel ? activeLabel.textContent : currentProvider;

    const activeDot = document.querySelector('.provider-menu-item.active .provider-item-dot');
    dot.className = 'status-dot ' + (activeDot?.classList.contains('online') ? 'online' : 'offline');
}

function toggleProviderMenu() {
    const menu = document.getElementById('providerMenu');
    if (!menu.innerHTML.trim()) return; // nie otwieraj gdy puste (ładowanie)
    menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
}

async function selectProvider(name, available) {
    if (!available) return;
    closeProviderMenu();

    // LocalAI — otwórz modal zarządzania modelami
    if (name === 'LocalAI') {
        openLocalAIModal();
        return;
    }

    await fetch(`${API}/ai/provider`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ provider: name })
    });

    currentProvider = name;

    // Odśwież menu
    const r = await fetch(`${API}/ai/providers`);
    const d = await r.json();
    renderProviderMenu(d.providers);
    updateProviderBtn();

    // Załaduj modele dla nowego providera
    await loadModelsForProvider(name);
}

async function loadModelsForProvider(providerName, activeModel = '') {
    const isLocal = ['Ollama', 'LMStudio', 'LocalAI'].includes(providerName);
    const badge = isLocal ? 'LOCAL' : 'CLOUD';

    ['snippetModel', 'modelSelect'].forEach(id => {
        const sel = document.getElementById(id);
        if (sel) sel.innerHTML = '<option value="">' + t('model.loading') + '</option>';
    });
    ['snippetModelBadge', 'repoModelBadge'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.textContent = badge;
    });

    try {
        const r = await fetch(`${API}/ai/models?provider=${encodeURIComponent(providerName)}`);
        const d = await r.json();
        const models = d.models ?? [];

        // Zachowaj model aktualnie wybrany przez użytkownika (jeśli istnieje)
        const userSelected = (() => {
            const sel = document.getElementById('snippetModel');
            return sel && sel.value ? sel.value : null;
        })();
        const modelToSelect = userSelected && models.includes(userSelected)
            ? userSelected
            : (activeModel && models.includes(activeModel) ? activeModel : models[0] ?? '');

        const opts = models.length
            ? models.map(m => `<option value="${m}" ${m === modelToSelect ? 'selected' : ''}>${m}</option>`).join('')
            : providerName === 'LocalAI'
                ? `<option value="">${t('localai.noModels')}</option>`
                : `<option value="">${t('model.none')}</option>`;

        ['snippetModel', 'modelSelect'].forEach(id => {
            const sel = document.getElementById(id);
            if (sel) sel.innerHTML = opts;
        });
    } catch {
        ['snippetModel', 'modelSelect'].forEach(id => {
            const sel = document.getElementById(id);
            if (sel) sel.innerHTML = '<option value="">' + t('model.error') + '</option>';
        });
    }
}

function closeProviderMenu() {
    document.getElementById('providerMenu').style.display = 'none';
}

// Zamknij menu po kliknięciu poza nim
document.addEventListener('click', e => {
    if (!e.target.closest('.provider-dropdown')) closeProviderMenu();
});