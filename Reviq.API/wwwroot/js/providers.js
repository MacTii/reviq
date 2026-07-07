// ── Providers & Models ────────────────────────────────────────────────────────
import { API } from './api.js';
import { t } from './i18n.js';
import { showError } from './utils.js';
import { openLocalAIModal } from './localai.js';
import { currentProvider, currentModel, setCurrentProvider, setCurrentModel } from './providerState.js';

export async function initProviders() {
    try {
        const r = await fetch(`${API}/ai/providers`);
        const d = await r.json();

        setCurrentProvider(d.currentProvider ?? 'LocalAI');
        setCurrentModel(d.currentModel ?? '');

        // Sprawdź dostępność wszystkich providerów równolegle
        const statusResults = await Promise.allSettled(
            d.providers.map(p => {
                if (p.name === 'LocalAI')
                    return Promise.resolve({ name: p.name, available: true, hasConfig: true });
                return fetch(`${API}/ai/providers/${p.name}/status`)
                    .then(r => r.json())
                    .then(s => ({ name: p.name, available: s.available, hasConfig: p.hasConfig }))
                    .catch(() => ({ name: p.name, available: false, hasConfig: p.hasConfig }));
            })
        );

        const providersWithStatus = d.providers.map(p => {
            const result = statusResults.find(r => r.status === 'fulfilled' && r.value.name === p.name);
            const available = result?.value?.available ?? false;
            return { ...p, available };
        });

        renderProviderMenu(providersWithStatus);
        updateProviderBtn();

        // Załaduj modele i sprawdź czy otworzyć modal
        await loadModelsForProvider(currentProvider, currentModel);
        _checkLocalAIModal();

    } catch {
        document.getElementById('ollamaDot').className = 'status-dot offline';
        document.getElementById('providerBtnText').textContent = t('provider.unavailable');
    }
}

function _checkLocalAIModal() {
    if (currentProvider !== 'LocalAI') return;
    // Sprawdź czy modelSelect ma jakieś realne opcje
    const sel = document.getElementById('modelSelect') || document.getElementById('snippetModel');
    const hasModels = sel && sel.options.length > 0 && sel.options[0].value !== '';
    if (!hasModels) setTimeout(() => openLocalAIModal(), 300);
}

export function renderProviderMenu(providers) {
    const menu = document.getElementById('providerMenu');
    menu.innerHTML = providers.map(p => {
        const dotClass = p.available ? 'online' : (!p.hasConfig ? 'unknown' : 'offline');
        const isActive = p.name === currentProvider;
        const unavail = !p.available;
        return `<div class="provider-menu-item ${isActive ? 'active' : ''} ${unavail ? 'unavailable' : ''}"
                     data-name="${p.name}"
                     onclick="selectProvider('${p.name}', ${p.available})">
            <div class="provider-item-left">
                <div class="provider-item-dot ${dotClass}"></div>
                <span class="provider-item-name">${p.label}</span>
            </div>
            <span class="provider-tag">${p.type === 'local' ? 'LOCAL' : 'CLOUD'}</span>
        </div>`;
    }).join('');
}

export function updateProviderBtn() {
    const dot = document.getElementById('ollamaDot');
    const btn = document.getElementById('providerBtnText');
    const activeItem = document.querySelector('.provider-menu-item.active');

    btn.textContent = activeItem
        ? activeItem.querySelector('.provider-item-name').textContent
        : currentProvider;

    const activeDot = activeItem?.querySelector('.provider-item-dot');
    if (dot && activeDot)
        dot.className = activeDot.className.replace('provider-item-dot', 'status-dot');
}

function toggleProviderMenu() {
    const menu = document.getElementById('providerMenu');
    if (!menu.innerHTML.trim()) return;
    menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
}

function closeProviderMenu() {
    document.getElementById('providerMenu').style.display = 'none';
}

document.addEventListener('click', e => {
    if (!e.target.closest('.provider-dropdown')) closeProviderMenu();
});

async function selectProvider(name, available) {
    if (!available) return;
    closeProviderMenu();

    try {
        const r = await fetch(`${API}/ai/provider`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ provider: name })
        });
        const d = await r.json();

        setCurrentProvider(d.provider);
        setCurrentModel(d.model ?? '');

        document.querySelectorAll('.provider-menu-item').forEach(el =>
            el.classList.toggle('active', el.dataset.name === name));
        updateProviderBtn();
        await loadModelsForProvider(name, currentModel);
        _checkLocalAIModal();

    } catch (e) {
        console.error('selectProvider failed:', e);
        showError(t('error.providerSwitch') ?? 'Failed to switch provider.');
    }
}

export async function loadModelsForProvider(providerName, activeModel = '') {
    const isLocal = ['Ollama', 'LMStudio', 'LocalAI'].includes(providerName);
    const badge = isLocal ? 'LOCAL' : 'CLOUD';

    ['snippetModel', 'modelSelect'].forEach(id => {
        const sel = document.getElementById(id);
        if (sel) sel.innerHTML = `<option value="">${t('model.loading')}</option>`;
    });
    ['snippetModelBadge', 'repoModelBadge'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.textContent = badge;
    });

    try {
        const r = await fetch(`${API}/ai/models?provider=${encodeURIComponent(providerName)}`);
        const d = await r.json();
        const models = d.models ?? [];

        const modelToSelect = activeModel && models.includes(activeModel)
            ? activeModel
            : models[0] ?? '';

        const opts = models.length
            ? models.map(m =>
                `<option value="${m}" ${m === modelToSelect ? 'selected' : ''}>${m}</option>`
            ).join('')
            : `<option value="">${t('model.none')}</option>`;

        ['snippetModel', 'modelSelect'].forEach(id => {
            const sel = document.getElementById(id);
            if (sel) sel.innerHTML = opts;
        });

        if (modelToSelect) setCurrentModel(modelToSelect);

    } catch {
        ['snippetModel', 'modelSelect'].forEach(id => {
            const sel = document.getElementById(id);
            if (sel) sel.innerHTML = `<option value="">${t('model.error')}</option>`;
        });
    }
}

export async function pollProviderStatus() {
    try {
        const r = await fetch(`${API}/ai/providers/${currentProvider}/status`);
        if (!r.ok) return;
        const d = await r.json();
        const dot = document.getElementById('ollamaDot');
        if (dot) dot.className = `status-dot ${d.available ? 'online' : 'offline'}`;
    } catch { /* ignore */ }
}

window.selectProvider = selectProvider;
window.toggleProviderMenu = toggleProviderMenu;
