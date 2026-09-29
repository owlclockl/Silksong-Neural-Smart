// =============================================================================
// SILKSONG NEURAL SMART — ГЛАВНОЕ МЕНЮ И ЭКРАН РЕЖИМА «ВЕЛИКАЯ АРЕНА»
// Точная веб-копия того, что мод добавляет в главное меню самой игры.
// =============================================================================

window.SNS_ACTIVE_SCREEN = 'menu';

window.addEventListener('DOMContentLoaded', () => {
    // ---------------------------------------------------------------- экраны
    const screens = {
        menu: document.getElementById('menuScreen'),
        grand: document.getElementById('grandScreen'),
        classic: document.getElementById('classicScreen')
    };

    function showScreen(name) {
        Object.keys(screens).forEach(k => {
            if (screens[k]) screens[k].classList.toggle('active', k === name);
        });
        window.SNS_ACTIVE_SCREEN = name;
        window.scrollTo(0, 0);
    }

    showScreen('menu');

    // ------------------------------------------------------------ симуляция
    const grandCanvas = document.getElementById('grandCanvas');
    const grandNetCanvas = document.getElementById('grandNetCanvas');
    const grandChartCanvas = document.getElementById('grandChartCanvas');

    const arena = new GrandArena(grandCanvas);
    const viz = new GrandArenaVisualizer(arena, grandNetCanvas, grandChartCanvas);
    window.SNS_GRAND_ARENA = arena;

    const keysDown = {};
    window.addEventListener('keydown', e => { keysDown[e.code] = true; });
    window.addEventListener('keyup', e => { keysDown[e.code] = false; });

    // ------------------------------------------------------- главное меню UI
    const menuSlots = document.getElementById('menuSlots');
    const slotList = document.getElementById('slotList');
    const paramMobsValue = document.getElementById('paramMobsValue');
    const paramTimeValue = document.getElementById('paramTimeValue');
    const paramWaves = document.getElementById('paramWaves');
    const paramBalance = document.getElementById('paramBalance');

    function renderSlotList() {
        const slots = GrandArenaSaves.all();
        slotList.innerHTML = slots.map(info => {
            if (!info.exists) {
                return `
                <div class="slot-row">
                    <div class="slot-info">
                        <b>СЛОТ ${info.slot}</b> — <span style="color: var(--text-muted);">пусто</span>
                        <div>Новая нейросеть · волна 1 · чистая статистика</div>
                    </div>
                    <div class="slot-actions">
                        <button data-action="new" data-slot="${info.slot}" class="primary">▶ Начать</button>
                    </div>
                </div>`;
            }

            const mins = Math.round(info.playTime / 60);
            return `
                <div class="slot-row filled">
                    <div class="slot-info">
                        <b>СЛОТ ${info.slot}</b> — ${info.name}
                        <div>Волна ${info.wave} · поколение ${info.generation} · эпизодов ${info.totalEpisodes} · стая ${info.mobCount}</div>
                        <div>Победы Хорнет: ${info.hornetWins} · победы стаи: ${info.packWins} · в режиме ${mins} мин</div>
                    </div>
                    <div class="slot-actions">
                        <button data-action="continue" data-slot="${info.slot}" class="primary">▶ Продолжить</button>
                        <button data-action="new" data-slot="${info.slot}">✚ Заново</button>
                        <button data-action="delete" data-slot="${info.slot}">🗑</button>
                    </div>
                </div>`;
        }).join('');

        slotList.querySelectorAll('button').forEach(btn => {
            btn.addEventListener('click', () => {
                const slot = parseInt(btn.dataset.slot, 10);
                const action = btn.dataset.action;

                if (action === 'delete') {
                    GrandArenaSaves.remove(slot);
                    renderSlotList();
                    return;
                }

                startGrandArena(slot, action === 'new');
            });
        });
    }

    function syncMenuParams() {
        paramMobsValue.textContent = arena.baseMobCount;
        paramTimeValue.textContent = `${arena.maxEpisodeTime} с`;
        paramWaves.textContent = arena.waveScaling ? 'ВКЛ' : 'ВЫКЛ';
        paramWaves.classList.toggle('active', arena.waveScaling);
        paramBalance.textContent = arena.balancePackDamage ? 'ВКЛ' : 'ВЫКЛ';
        paramBalance.classList.toggle('active', arena.balancePackDamage);
    }

    document.getElementById('menuGrandArena').addEventListener('click', () => {
        menuSlots.classList.add('active');
        renderSlotList();
        syncMenuParams();
        menuSlots.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    });

    document.getElementById('menuSlotsBack').addEventListener('click', () => {
        menuSlots.classList.remove('active');
    });

    document.getElementById('menuClassic').addEventListener('click', () => {
        arena.isRunning = false;
        showScreen('classic');
    });

    document.getElementById('paramMobsMinus').addEventListener('click', () => {
        arena.setMobCount(arena.baseMobCount - 1);
        syncMenuParams();
    });
    document.getElementById('paramMobsPlus').addEventListener('click', () => {
        arena.setMobCount(arena.baseMobCount + 1);
        syncMenuParams();
    });
    document.getElementById('paramTimeMinus').addEventListener('click', () => {
        arena.maxEpisodeTime = Math.max(15, arena.maxEpisodeTime - 15);
        syncMenuParams();
    });
    document.getElementById('paramTimePlus').addEventListener('click', () => {
        arena.maxEpisodeTime = Math.min(180, arena.maxEpisodeTime + 15);
        syncMenuParams();
    });
    paramWaves.addEventListener('click', () => {
        arena.waveScaling = !arena.waveScaling;
        syncMenuParams();
    });
    paramBalance.addEventListener('click', () => {
        arena.balancePackDamage = !arena.balancePackDamage;
        syncMenuParams();
    });

    const backToMenuBtn = document.getElementById('btnBackToMenu');
    if (backToMenuBtn) {
        backToMenuBtn.addEventListener('click', () => showScreen('menu'));
    }

    // ------------------------------------------------------- запуск режима
    function startGrandArena(slot, isNew) {
        arena.slot = slot;

        if (isNew) {
            GrandArenaSaves.remove(slot);
            arena.resetProgress();
            arena.profileName = 'Слот ' + slot;
            arena.showStatus(`Новая тренировка начата (слот ${slot})`);
        } else if (GrandArenaSaves.exists(slot)) {
            GrandArenaSaves.load(slot, arena);
            arena.showStatus(`Загружен слот ${slot}: волна ${arena.wave}, поколение ${arena.hornetTrainer.generation}`);
        } else {
            arena.resetProgress();
            arena.profileName = 'Слот ' + slot;
        }

        arena.isRunning = true;
        menuSlots.classList.remove('active');
        showScreen('grand');
        syncGrandUI();
    }

    // -------------------------------------------------- экран Великой Арены
    const grandPlay = document.getElementById('grandPlay');
    const grandPause = document.getElementById('grandPause');
    const grandSlotBadge = document.getElementById('grandSlotBadge');
    const grandWaveBadge = document.getElementById('grandWaveBadge');
    const grandMobsValue = document.getElementById('grandMobsValue');
    const grandHornetMode = document.getElementById('grandHornetMode');
    const grandGuide = document.getElementById('grandGuide');
    const grandSensorsBtn = document.getElementById('grandSensorsBtn');
    const grandWavesToggle = document.getElementById('grandWavesToggle');
    const grandBalanceToggle = document.getElementById('grandBalanceToggle');
    const gArchetypeList = document.getElementById('gArchetypeList');
    const gActionProbList = document.getElementById('gActionProbList');

    grandPlay.addEventListener('click', () => {
        arena.isRunning = true;
        grandPlay.style.display = 'none';
        grandPause.style.display = 'inline-flex';
    });

    grandPause.addEventListener('click', () => {
        arena.isRunning = false;
        grandPause.style.display = 'none';
        grandPlay.style.display = 'inline-flex';
    });

    document.getElementById('grandResetEpisode').addEventListener('click', () => arena.resetEpisode());

    document.getElementById('grandSave').addEventListener('click', () => {
        if (GrandArenaSaves.save(arena.slot, arena)) {
            arena.showStatus(`Сохранено в слот ${arena.slot}`);
        }
    });

    document.getElementById('grandExit').addEventListener('click', () => {
        GrandArenaSaves.save(arena.slot, arena);
        arena.isRunning = false;
        showScreen('menu');
        renderSlotList();
    });

    document.querySelectorAll('.gspeed-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.gspeed-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            arena.timeScale = parseFloat(btn.dataset.speed);
        });
    });

    document.querySelectorAll('.gslot-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.gslot-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            arena.slot = parseInt(btn.dataset.slot, 10);
            syncGrandUI();
        });
    });

    document.getElementById('grandLoadSlot').addEventListener('click', () => {
        if (GrandArenaSaves.load(arena.slot, arena)) {
            arena.showStatus(`Загружен слот ${arena.slot}`);
        } else {
            arena.showStatus(`Слот ${arena.slot} пуст`);
        }
        syncGrandUI();
    });

    document.getElementById('grandMobsMinus').addEventListener('click', () => {
        arena.setMobCount(arena.baseMobCount - 1);
        syncGrandUI();
    });
    document.getElementById('grandMobsPlus').addEventListener('click', () => {
        arena.setMobCount(arena.baseMobCount + 1);
        syncGrandUI();
    });

    grandHornetMode.addEventListener('click', () => {
        arena.manualHornet = !arena.manualHornet;
        grandHornetMode.textContent = arena.manualHornet ? '🎮 Хорнет: Игрок' : '🤖 Хорнет: ИИ';
        grandHornetMode.classList.toggle('active', !arena.manualHornet);
        grandGuide.style.display = arena.manualHornet ? 'block' : 'none';
    });

    grandSensorsBtn.addEventListener('click', () => {
        viz.showSensors = !viz.showSensors;
        grandSensorsBtn.textContent = `👁 Лучи: ${viz.showSensors ? 'ВКЛ' : 'ВЫКЛ'}`;
    });

    grandWavesToggle.addEventListener('click', () => {
        arena.waveScaling = !arena.waveScaling;
        grandWavesToggle.textContent = `🌊 Волны: ${arena.waveScaling ? 'ВКЛ' : 'ВЫКЛ'}`;
    });

    grandBalanceToggle.addEventListener('click', () => {
        arena.balancePackDamage = !arena.balancePackDamage;
        grandBalanceToggle.textContent = `⚖ Баланс: ${arena.balancePackDamage ? 'ВКЛ' : 'ВЫКЛ'}`;
    });

    document.getElementById('gBrainHornet').addEventListener('click', () => {
        viz.activeBrainTarget = 'hornet';
        document.getElementById('gBrainHornet').classList.add('active');
        document.getElementById('gBrainMob').classList.remove('active');
    });

    document.getElementById('gBrainMob').addEventListener('click', () => {
        viz.activeBrainTarget = 'mob';
        document.getElementById('gBrainMob').classList.add('active');
        document.getElementById('gBrainHornet').classList.remove('active');
    });

    document.getElementById('grandExportBrains').addEventListener('click', () => {
        const payload = arena.serialize();
        const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `grand_arena_slot${arena.slot}.brains.json`;
        a.click();
        URL.revokeObjectURL(url);
    });

    // ------------------------------------------------------ обновление UI
    function syncGrandUI() {
        grandSlotBadge.textContent = `Слот ${arena.slot}`;
        grandWaveBadge.textContent = `Волна ${arena.wave}`;
        grandMobsValue.textContent = arena.mobCount;

        document.querySelectorAll('.gslot-btn').forEach(b => {
            b.classList.toggle('active', parseInt(b.dataset.slot, 10) === arena.slot);
        });

        // скорость симуляции могла прийти из слота или с клавиш PageUp/PageDown:
        // подсвечиваем ближайшую кнопку, не меняя саму скорость
        let nearest = null;
        let nearestDiff = Infinity;
        document.querySelectorAll('.gspeed-btn').forEach(b => {
            const diff = Math.abs(parseFloat(b.dataset.speed) - arena.timeScale);
            if (diff < nearestDiff) { nearestDiff = diff; nearest = b; }
        });
        document.querySelectorAll('.gspeed-btn').forEach(b => b.classList.toggle('active', b === nearest));
        if (nearest && nearestDiff > 0.01) {
            nearest.title = `Текущая скорость: ${arena.timeScale.toFixed(2)}x`;
        }

        grandWavesToggle.textContent = `🌊 Волны: ${arena.waveScaling ? 'ВКЛ' : 'ВЫКЛ'}`;
        grandBalanceToggle.textContent = `⚖ Баланс: ${arena.balancePackDamage ? 'ВКЛ' : 'ВЫКЛ'}`;
        grandHornetMode.textContent = arena.manualHornet ? '🎮 Хорнет: Игрок' : '🤖 Хорнет: ИИ';
        grandHornetMode.classList.toggle('active', !arena.manualHornet);
        grandGuide.style.display = arena.manualHornet ? 'block' : 'none';

        grandPlay.style.display = arena.isRunning ? 'none' : 'inline-flex';
        grandPause.style.display = arena.isRunning ? 'inline-flex' : 'none';

        updateGrandStats();
    }

    function updateGrandStats() {
        const total = Math.max(1, arena.totalEpisodes);
        const hWr = (arena.hornetWins / total * 100).toFixed(1);
        const pWr = (arena.packWins / total * 100).toFixed(1);

        document.getElementById('gStatWave').innerHTML =
            `${arena.wave} <span style="font-size:13px; color: var(--text-muted);">(рекорд ${arena.bestWave})</span>`;
        document.getElementById('gStatHornet').innerHTML =
            `${arena.hornetWins} <span style="font-size:13px; color: var(--text-muted);">(${hWr}%)</span>`;
        document.getElementById('gStatPack').innerHTML =
            `${arena.packWins} <span style="font-size:13px; color: var(--text-muted);">(${pWr}%)</span>`;
        document.getElementById('gStatMobs').innerHTML =
            `${arena.mobsDefeated} <span style="font-size:13px; color: var(--text-muted);">(${arena.decisionsPerSec || 0}/с)</span>`;

        grandWaveBadge.textContent = `Волна ${arena.wave}`;
        grandMobsValue.textContent = arena.mobCount;

        // Архетипы
        gArchetypeList.innerHTML = GRAND_ARCHETYPES.map(key => {
            const t = arena.mobTrainers[key];
            const alive = arena.pack.filter(m => m.key === key && m.entity.health > 0).length;
            const total = arena.pack.filter(m => m.key === key).length;
            const best = t.bestGenomeEver && t.bestGenomeEver.fitness > -9000 ? Math.round(t.bestGenomeEver.fitness) : 0;
            return `
                <div class="archetype-row ${arena.focusArchetype === key ? 'active' : ''}" data-key="${key}">
                    <span><span class="dot" style="background:${GRAND_ARCHETYPE_COLORS[key]}"></span>${GRAND_ARCHETYPE_TITLES[key]}</span>
                    <span class="meta">пок. ${t.generation} · фитнес ${best} · живых ${alive}/${total}</span>
                </div>`;
        }).join('');

        gArchetypeList.querySelectorAll('.archetype-row').forEach(row => {
            row.addEventListener('click', () => {
                arena.focusArchetype = row.dataset.key;
            });
        });

        // Вероятности действий
        const target = viz.activeBrainTarget === 'hornet' ? arena.hornet : arena.mob;
        const brain = target && target.brain;
        if (brain && brain.activations && brain.activations.length) {
            const out = brain.activations[brain.activations.length - 1];
            let html = '';
            for (let i = 0; i < out.length && i < ACTION_NAMES.length; i++) {
                const pct = (out[i] * 100).toFixed(1);
                const isTop = target.lastRawAction && target.lastRawAction.primaryAction === i;
                html += `
                    <div class="prob-item">
                        <span style="width:105px; ${isTop ? 'font-weight:700; color:#fff;' : 'color: var(--text-muted);'}">${ACTION_NAMES[i]}</span>
                        <div class="prob-bar-bg"><div class="prob-bar-fill" style="width:${pct}%; background:${isTop ? 'var(--accent-green)' : 'var(--accent-blue)'};"></div></div>
                        <span style="width:40px; text-align:right;">${pct}%</span>
                    </div>`;
            }
            gActionProbList.innerHTML = html;
        }
    }

    // ------------------------------------------- горячие клавиши (как в моде)
    window.addEventListener('keydown', e => {
        // F6 — Великая Арена: из меню открыть панель слотов, из режима — выйти в меню
        if (e.code === 'F6') {
            e.preventDefault();
            if (window.SNS_ACTIVE_SCREEN === 'grand') {
                GrandArenaSaves.save(arena.slot, arena);
                arena.isRunning = false;
                showScreen('menu');
                renderSlotList();
            } else {
                showScreen('menu');
                menuSlots.classList.add('active');
                renderSlotList();
                syncMenuParams();
            }
            return;
        }

        if (window.SNS_ACTIVE_SCREEN !== 'grand') return;

        // F5 — быстрое сохранение слота
        if (e.code === 'F5') {
            e.preventDefault();
            if (GrandArenaSaves.save(arena.slot, arena)) {
                arena.showStatus(`Быстрое сохранение: слот ${arena.slot}`);
            }
        } else if (e.code === 'F8') {           // F8 — ИИ / игрок
            e.preventDefault();
            arena.manualHornet = !arena.manualHornet;
            syncGrandUI();
        } else if (e.code === 'F9') {           // F9 — перезапуск раунда
            e.preventDefault();
            arena.resetEpisode();
        } else if (e.code === 'PageUp') {       // ускорение
            e.preventDefault();
            arena.timeScale = Math.min(50, arena.timeScale * 1.5);
            syncGrandUI();
        } else if (e.code === 'PageDown') {     // замедление
            e.preventDefault();
            arena.timeScale = Math.max(0.25, arena.timeScale / 1.5);
            syncGrandUI();
        }
    });

    // ------------------------------------------------------------ главный цикл
    let last = performance.now();
    let decisionAccum = 0;
    let uiTimer = 0;

    function loop(now) {
        const dt = Math.max(0, Math.min(0.1, (now - last) / 1000)); // защита от скачков времени
        last = now;

        if (window.SNS_ACTIVE_SCREEN === 'grand') {
            if (arena.isRunning) {
                arena.playTime += dt;
                const steps = Math.ceil(arena.timeScale);
                const stepDt = (dt * arena.timeScale) / steps;
                const before = arena.decisionsCount;
                for (let s = 0; s < steps; s++) {
                    arena.stepSimulation(stepDt, keysDown);
                }
                decisionAccum += arena.decisionsCount - before;
            }

            uiTimer += dt;
            if (uiTimer >= 0.5) {
                arena.decisionsPerSec = Math.round(decisionAccum / uiTimer);
                decisionAccum = 0;
                uiTimer = 0;
                updateGrandStats();
            }

            viz.render();
        }

        requestAnimationFrame(loop);
    }

    requestAnimationFrame(loop);
});
