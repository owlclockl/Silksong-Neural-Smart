// =============================================================================
// SILKSONG NEURAL SMART - APPLICATION ORCHESTRATION & EVENT SYSTEM
// =============================================================================

window.addEventListener('DOMContentLoaded', () => {
    const arenaCanvas = document.getElementById('arenaCanvas');
    const netCanvas = document.getElementById('netCanvas');
    const chartCanvas = document.getElementById('chartCanvas');

    // Instantiate simulation & visualizer
    const arena = new TrainingArena(arenaCanvas);
    const visualizer = new ArenaVisualizer(arena, netCanvas, chartCanvas);

    // Keyboard state tracking
    const keysDown = {};
    window.addEventListener('keydown', (e) => {
        keysDown[e.code] = true;
    });
    window.addEventListener('keyup', (e) => {
        keysDown[e.code] = false;
    });

    // UI Elements
    const btnStart = document.getElementById('btnStartTraining');
    const btnPause = document.getElementById('btnPauseTraining');
    const btnReset = document.getElementById('btnResetEpisode');
    const speedButtons = document.querySelectorAll('.speed-btn');
    const roomButtons = document.querySelectorAll('.room-btn');
    const mobButtons = document.querySelectorAll('.mob-btn');
    const controlButtons = document.querySelectorAll('.control-btn');
    const toggleSensorsBtn = document.getElementById('toggleSensorsBtn');
    const playerGuideBox = document.getElementById('playerGuideBox');
    const roomNameLabel = document.getElementById('roomNameLabel');

    const targetHornetBrainBtn = document.getElementById('targetHornetBrain');
    const targetMobBrainBtn = document.getElementById('targetMobBrain');
    const actionProbList = document.getElementById('actionProbList');

    const statGen = document.getElementById('statGen');
    const statHornetWr = document.getElementById('statHornetWr');
    const statHornetWins = document.getElementById('statHornetWins');
    const statMobWr = document.getElementById('statMobWr');
    const statMobWins = document.getElementById('statMobWins');
    const statDps = document.getElementById('statDps');

    const sliderReactionDelay = document.getElementById('sliderReactionDelay');
    const valReactionDelay = document.getElementById('valReactionDelay');
    const sliderErrorMargin = document.getElementById('sliderErrorMargin');
    const valErrorMargin = document.getElementById('valErrorMargin');
    const sliderMutationRate = document.getElementById('sliderMutationRate');
    const valMutationRate = document.getElementById('valMutationRate');

    const btnExportBrain = document.getElementById('btnExportBrain');
    const btnLoadPretrained = document.getElementById('btnLoadPretrained');

    // Training Lifecycle Controls
    btnStart.addEventListener('click', () => {
        arena.isTrainingActive = true;
        btnStart.style.display = 'none';
        btnPause.style.display = 'inline-flex';
    });

    btnPause.addEventListener('click', () => {
        arena.isTrainingActive = false;
        btnPause.style.display = 'none';
        btnStart.style.display = 'inline-flex';
    });

    btnReset.addEventListener('click', () => {
        arena.resetEpisode();
    });

    // Simulation Speed
    speedButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            speedButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            arena.timeScale = parseFloat(btn.dataset.speed);
        });
    });

    // Rooms
    const roomNames = {
        1: "Комната 1: Арена Мха (Moss Duel Dojo)",
        2: "Комната 2: Вертикальный Разлом (Vertical Chasm with Spikes)",
        3: "Комната 3: Испытание Цитадели (Citadel Hazards & Pillars)"
    };

    roomButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            roomButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            const roomId = parseInt(btn.dataset.room);
            arena.loadRoom(roomId);
            roomNameLabel.innerText = roomNames[roomId];
        });
    });

    // Mob Selection
    mobButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            mobButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            arena.setMobType(btn.dataset.mob);
        });
    });

    // Control Mode (AI vs Manual Player)
    controlButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            controlButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            arena.manualHornetControl = (btn.dataset.mode === 'manual');
            playerGuideBox.style.display = arena.manualHornetControl ? 'block' : 'none';
        });
    });

    // Toggle Sensor Raycasts
    toggleSensorsBtn.addEventListener('click', () => {
        visualizer.showSensors = !visualizer.showSensors;
        toggleSensorsBtn.innerText = `👁 Лучи сенсоров: ${visualizer.showSensors ? 'ВКЛ' : 'ВЫКЛ'}`;
    });

    // Brain Target Toggle
    targetHornetBrainBtn.addEventListener('click', () => {
        targetHornetBrainBtn.classList.add('active');
        targetMobBrainBtn.classList.remove('active');
        visualizer.activeBrainTarget = 'hornet';
    });

    targetMobBrainBtn.addEventListener('click', () => {
        targetMobBrainBtn.classList.add('active');
        targetHornetBrainBtn.classList.remove('active');
        visualizer.activeBrainTarget = 'mob';
    });

    // Sliders
    sliderReactionDelay.addEventListener('input', (e) => {
        const ms = parseFloat(e.target.value);
        valReactionDelay.innerText = `${ms} ms`;
        arena.hornet.balancer.reactionDelay = ms / 1000.0;
        arena.mob.balancer.reactionDelay = ms / 1000.0;
    });

    sliderErrorMargin.addEventListener('input', (e) => {
        const pct = parseFloat(e.target.value);
        valErrorMargin.innerText = `${pct}%`;
        arena.hornet.balancer.errorMargin = pct / 100.0;
        arena.mob.balancer.errorMargin = pct / 100.0;
    });

    sliderMutationRate.addEventListener('input', (e) => {
        const pct = parseFloat(e.target.value);
        valMutationRate.innerText = `${pct}%`;
        arena.hornetTrainer.mutationRate = pct / 100.0;
        arena.mobTrainer.mutationRate = pct / 100.0;
    });

    // Export Brain JSON
    btnExportBrain.addEventListener('click', () => {
        const target = visualizer.activeBrainTarget === 'hornet' ? arena.hornet : arena.mob;
        const json = JSON.stringify(target.brain.toJSON(), null, 2);
        const blob = new Blob([json], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${target.name.toLowerCase().replace(/ /g, '_')}_neural.brain.json`;
        a.click();
        URL.revokeObjectURL(url);
    });

    // Load Pre-trained weights
    btnLoadPretrained.addEventListener('click', async () => {
        try {
            const res = await fetch('/PretrainedBrains/hornet_master.brain.json');
            if (res.ok) {
                const data = await res.json();
                const net = NeuralNetwork.fromJSON(data);
                arena.hornet.brain = net;
                arena.hornetTrainer.bestGenomeEver = { brain: net.clone(), fitness: 500 };
                alert("Pre-trained Master Brain loaded successfully!");
            }
        } catch (err) {
            console.error("Could not fetch pretrained brain:", err);
        }
    });

    // Render Action Probabilities
    function updateActionProbabilities() {
        const target = visualizer.activeBrainTarget === 'hornet' ? arena.hornet : arena.mob;
        const brain = target.brain;
        if (!brain || !brain.activations || brain.activations.length === 0) return;

        const outLayer = brain.activations[brain.activations.length - 1];
        let html = '';

        for (let i = 0; i < outLayer.length && i < ACTION_NAMES.length; i++) {
            const probPct = (outLayer[i] * 100).toFixed(1);
            const isHighest = target.lastRawAction && target.lastRawAction.primaryAction === i;
            const barCol = isHighest ? 'var(--accent-green)' : 'var(--accent-blue)';

            html += `
                <div class="prob-item">
                    <span style="width: 105px; ${isHighest ? 'font-weight: 700; color: #fff;' : 'color: var(--text-muted);'}">${ACTION_NAMES[i]}</span>
                    <div class="prob-bar-bg">
                        <div class="prob-bar-fill" style="width: ${probPct}%; background: ${barCol};"></div>
                    </div>
                    <span style="width: 40px; text-align: right; ${isHighest ? 'font-weight: 700; color: var(--accent-green);' : ''}">${probPct}%</span>
                </div>
            `;
        }

        actionProbList.innerHTML = html;
    }

    // Auto-start simulation in training mode on page load
    arena.isTrainingActive = true;
    btnStart.style.display = 'none';
    btnPause.style.display = 'inline-flex';

    // Main Animation Loop
    let lastTime = performance.now();
    let dpsCounter = 0;
    let dpsTimer = 0;

    function mainLoop(currentTime) {
        const dt = Math.min(0.1, (currentTime - lastTime) / 1000.0);
        lastTime = currentTime;

        // Run sub-steps based on timeScale
        if (arena.isTrainingActive) {
            const steps = Math.ceil(arena.timeScale);
            const stepDt = (dt * arena.timeScale) / steps;
            for (let s = 0; s < steps; s++) {
                arena.stepSimulation(stepDt, keysDown);
                dpsCounter += 2;
            }
        }

        dpsTimer += dt;
        if (dpsTimer >= 0.5) {
            arena.decisionsCount = Math.round(dpsCounter / dpsTimer);
            dpsCounter = 0;
            dpsTimer = 0;

            // Update stats
            statGen.innerHTML = `Gen ${arena.hornetTrainer.generation} <span style="font-size: 13px; color: var(--text-muted);">(24 агента)</span>`;
            const hWr = arena.totalEpisodes > 0 ? (arena.hornetWins / arena.totalEpisodes * 100).toFixed(1) : '0.0';
            const mWr = arena.totalEpisodes > 0 ? (arena.mobWins / arena.totalEpisodes * 100).toFixed(1) : '0.0';
            statHornetWr.innerHTML = `${hWr}% <span style="font-size: 13px; color: var(--text-muted);">(${arena.hornetWins})</span>`;
            statMobWr.innerHTML = `${mWr}% <span style="font-size: 13px; color: var(--text-muted);">(${arena.mobWins})</span>`;
            statDps.innerHTML = `${arena.decisionsCount} <span style="font-size: 12px; color: var(--text-muted);">(~0.015 ms)</span>`;

            updateActionProbabilities();
        }

        // Render frame
        visualizer.render();

        requestAnimationFrame(mainLoop);
    }

    requestAnimationFrame(mainLoop);
});
