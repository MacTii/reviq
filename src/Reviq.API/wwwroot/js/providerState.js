// ── Shared provider/model state ─────────────────────────────────────────────
// Wydzielone z providers.js, bo zarówno providers.js jak i localai.js
// muszą móc odczytywać ORAZ zapisywać ten stan (importowane bindingi ES
// są tylko-do-odczytu, więc mutacja musi iść przez settery).
export let currentProvider = 'LocalAI';
export let currentModel = '';

export function setCurrentProvider(p) { currentProvider = p; }
export function setCurrentModel(m) { currentModel = m; }
