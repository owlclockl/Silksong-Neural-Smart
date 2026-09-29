// =============================================================================
// SILKSONG NEURAL SMART — РЕЖИМ "ВЕЛИКАЯ АРЕНА" (GRAND ARENA)
// Отдельный режим: Хорнет и стая мобов всех архетипов обучаются ОДНОВРЕМЕННО
// на одной большой арене. Со своими слотами сохранений (localStorage),
// полностью повторяет логику C#-режима мода (src/Training/GrandArenaMode.cs).
// =============================================================================

const GRAND_ARCHETYPES = ['grunt', 'flying', 'knight', 'assassin'];

const GRAND_ARCHETYPE_TITLES = {
    grunt: 'Разведчик Мха',
    flying: 'Летающий Охотник',
    knight: 'Рыцарь Цитадели',
    assassin: 'Ассасин-Ткач'
};

const GRAND_ARCHETYPE_COLORS = {
    grunt: '#4cd137',
    flying: '#9c88ff',
    knight: '#fbc531',
    assassin: '#e84118'
};

function createArchetypeEntity(key) {
    switch (key) {
        case 'flying': return new FlyingHunterEntity();
        case 'knight': return new ShieldKnightEntity();
        case 'assassin': return new AssassinWeaverEntity();
        default: return new MossGruntEntity();
    }
}

// =============================================================================
// СОХРАНЕНИЯ РЕЖИМА — собственные слоты, не пересекаются с классическим додзё
// =============================================================================
const GrandArenaSaves = {
    SLOT_COUNT: 3,
    KEY_PREFIX: 'sns.grandArena.slot.',

    key(slot) {
        return this.KEY_PREFIX + slot;
    },

    exists(slot) {
        try { return !!localStorage.getItem(this.key(slot)); } catch (e) { return false; }
    },

    info(slot) {
        try {
            const raw = localStorage.getItem(this.key(slot));
            if (!raw) return { slot, exists: false };
            const d = JSON.parse(raw);
            return {
                slot,
                exists: true,
                name: d.name || ('Слот ' + slot),
                wave: d.wave || 1,
                generation: d.hornetGeneration || 1,
                totalEpisodes: d.totalEpisodes || 0,
                hornetWins: d.hornetWins || 0,
                packWins: d.packWins || 0,
                mobCount: d.mobCount || 4,
                playTime: d.playTime || 0,
                updated: d.updated || ''
            };
        } catch (e) {
            return { slot, exists: false };
        }
    },

    all() {
        const list = [];
        for (let i = 1; i <= this.SLOT_COUNT; i++) list.push(this.info(i));
        return list;
    },

    save(slot, arena) {
        try {
            localStorage.setItem(this.key(slot), JSON.stringify(arena.serialize()));
            return true;
        } catch (e) {
            console.error('[GrandArena] Ошибка сохранения слота', slot, e);
            return false;
        }
    },

    load(slot, arena) {
        try {
            const raw = localStorage.getItem(this.key(slot));
            if (!raw) return false;
            arena.deserialize(JSON.parse(raw));
            return true;
        } catch (e) {
            console.error('[GrandArena] Ошибка загрузки слота', slot, e);
            return false;
        }
    },

    remove(slot) {
        try { localStorage.removeItem(this.key(slot)); return true; } catch (e) { return false; }
    }
};

// =============================================================================
// СИМУЛЯЦИЯ БОЛЬШОЙ АРЕНЫ
// =============================================================================
class GrandArena {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.width = canvas.width;
        this.height = canvas.height;

        this.particles = [];
        this.projectiles = [];
        this.platforms = [];
        this.roomId = 4;

        this.hornet = new CombatEntity('Hornet', true);
        this.hornet.maxHealth = 120;
        this.hornet.health = 120;

        this.pack = [];
        this.focusArchetype = 'grunt';

        this.hornetTrainer = new GeneticTrainerJS(24, [24, 32, 32, 10]);
        this.mobTrainers = {};
        GRAND_ARCHETYPES.forEach(k => { this.mobTrainers[k] = new GeneticTrainerJS(24, [24, 32, 32, 10]); });

        // --- Настройки режима ---
        this.slot = 1;
        this.profileName = 'Великая Арена';
        this.baseMobCount = 4;
        this.mobCount = 4;
        this.maxEpisodeTime = 45;
        this.timeScale = 1;
        this.waveScaling = true;
        this.balancePackDamage = true;
        this.manualHornet = false;
        this.autoSaveEvery = 5;

        // --- Прогресс (сохраняется в слот) ---
        this.wave = 1;
        this.bestWave = 1;
        this.totalEpisodes = 0;
        this.hornetWins = 0;
        this.packWins = 0;
        this.draws = 0;
        this.mobsDefeated = 0;
        this.playTime = 0;
        this.hornetFitnessLog = [];
        this.packFitnessLog = [];

        // --- Телеметрия ---
        this.episodeTime = 0;
        this.isRunning = false;
        this.decisionsCount = 0;
        this.lastHornetFitness = 0;
        this.lastPackFitness = 0;
        this.statusMessage = '';
        this.statusTimer = 0;
        this._episodesSinceSave = 0;

        this.loadArenaLayout();
        this.rebuildPack();
    }

    // ----------------------------------------------------------- геометрия
    loadArenaLayout() {
        const W = this.width;
        const H = this.height;
        this.platforms = [];

        // Пол из трёх секций с ямами шипов между ними
        this.platforms.push({ x: 20, y: H - 40, w: 330, h: 30 });
        this.platforms.push({ x: 350, y: H - 22, w: 90, h: 22, isHazard: true });
        this.platforms.push({ x: 440, y: H - 40, w: W - 880, h: 30 });
        this.platforms.push({ x: W - 440, y: H - 22, w: 90, h: 22, isHazard: true });
        this.platforms.push({ x: W - 350, y: H - 40, w: 330, h: 30 });

        // Стены
        this.platforms.push({ x: 0, y: 0, w: 20, h: H });
        this.platforms.push({ x: W - 20, y: 0, w: 20, h: H });

        // Первый ярус
        this.platforms.push({ x: 70, y: H - 150, w: 170, h: 16, isPassThrough: true });
        this.platforms.push({ x: 300, y: H - 175, w: 150, h: 16, isPassThrough: true });
        this.platforms.push({ x: W - 450, y: H - 175, w: 150, h: 16, isPassThrough: true });
        this.platforms.push({ x: W - 240, y: H - 150, w: 170, h: 16, isPassThrough: true });

        // Второй ярус
        this.platforms.push({ x: 200, y: H - 270, w: 160, h: 16, isPassThrough: true });
        this.platforms.push({ x: W / 2 - 110, y: H - 300, w: 220, h: 16, isPassThrough: true });
        this.platforms.push({ x: W - 360, y: H - 270, w: 160, h: 16, isPassThrough: true });

        // Верхние насесты для летающих
        this.platforms.push({ x: 60, y: H - 380, w: 150, h: 14, isPassThrough: true });
        this.platforms.push({ x: W - 210, y: H - 380, w: 150, h: 14, isPassThrough: true });

        // Колонны-укрытия
        this.platforms.push({ x: W / 2 - 170, y: H - 110, w: 26, h: 70 });
        this.platforms.push({ x: W / 2 + 144, y: H - 110, w: 26, h: 70 });

        this.hornetSpawn = { x: W / 2, y: H - 90 };
        this.mobSpawns = [
            { x: 110, y: H - 80 },
            { x: W - 110, y: H - 80 },
            { x: 320, y: H - 210 },
            { x: W - 320, y: H - 210 },
            { x: 260, y: H - 310 },
            { x: W - 260, y: H - 310 },
            { x: 60, y: H - 80 },
            { x: W - 60, y: H - 80 }
        ];
    }

    mobSpawnAt(index) {
        return this.mobSpawns[index % this.mobSpawns.length];
    }

    // ------------------------------------------------------------- стая
    rebuildPack() {
        this.pack = [];
        const perArchetype = {};
        GRAND_ARCHETYPES.forEach(k => { perArchetype[k] = 0; });

        for (let i = 0; i < this.mobCount; i++) {
            const key = GRAND_ARCHETYPES[i % GRAND_ARCHETYPES.length];
            const entity = createArchetypeEntity(key);
            entity.color = GRAND_ARCHETYPE_COLORS[key];

            this.pack.push({
                entity,
                key,
                genomeOffset: perArchetype[key],
                countedDead: false
            });
            perArchetype[key]++;
        }

        this.resetEpisode();
    }

    setMobCount(count) {
        this.baseMobCount = Math.max(1, Math.min(8, count));
        this.mobCount = this.baseMobCount;
        this.rebuildPack();
    }

    genomeFor(trainer, offset) {
        const idx = (trainer.currentGenomeIndex + offset) % trainer.population.length;
        return trainer.population[idx];
    }

    resetEpisode() {
        this.episodeTime = 0;

        this.hornet.brain = this.hornetTrainer.getCurrentGenome().brain;
        this.hornet.reset(this.hornetSpawn.x, this.hornetSpawn.y);

        this.pack.forEach((m, i) => {
            const trainer = this.mobTrainers[m.key];
            m.entity.brain = this.genomeFor(trainer, m.genomeOffset).brain;
            const spawn = this.mobSpawnAt(i);
            m.entity.reset(spawn.x, spawn.y);
            m.countedDead = false;
        });
    }

    get aliveMobs() {
        return this.pack.filter(m => m.entity.health > 0);
    }

    nearestAliveMob() {
        let best = null;
        let bestDist = Infinity;
        for (const m of this.pack) {
            if (m.entity.health <= 0) continue;
            const d = Math.hypot(m.entity.x - this.hornet.x, m.entity.y - this.hornet.y);
            if (d < bestDist) { bestDist = d; best = m; }
        }
        return best;
    }

    // Совместимость с общим визуализатором нейросети
    get mob() {
        const nearest = this.nearestAliveMob();
        if (nearest) return nearest.entity;
        return this.pack.length ? this.pack[0].entity : this.hornet;
    }

    get mobTrainer() {
        return this.mobTrainers[this.focusArchetype] || this.mobTrainers.grunt;
    }

    spawnSilkBurst(x, y) {
        for (let i = 0; i < 18; i++) {
            const angle = Math.random() * Math.PI * 2;
            const spd = 2 + Math.random() * 5;
            this.particles.push(new Particle(x, y, Math.cos(angle) * spd, Math.sin(angle) * spd, '#ffffff', 0.6, 2.5));
        }
    }

    showStatus(text) {
        this.statusMessage = text;
        this.statusTimer = 4;
    }

    // ----------------------------------------------------------- сенсоры
    getSensorVector(entity, target, allyDistance) {
        const obs = new Float32Array(24);
        const dx = target.x - entity.x;
        const dy = target.y - entity.y;
        const dist = Math.sqrt(dx * dx + dy * dy);

        obs[0] = Math.max(-1, Math.min(1, dx / (this.width * 0.5)));
        obs[1] = Math.max(-1, Math.min(1, dy / (this.height * 0.5)));
        obs[2] = Math.min(1, dist / this.width);
        obs[3] = Math.atan2(dy, dx) / Math.PI;

        obs[4] = entity.vx / 15.0;
        obs[5] = entity.vy / 20.0;
        obs[6] = target.vx / 15.0;
        obs[7] = target.vy / 20.0;

        obs[8] = entity.health / entity.maxHealth;
        obs[9] = target.health / target.maxHealth;
        obs[10] = entity.grounded ? 1 : 0;
        obs[11] = target.grounded ? 1 : 0;

        obs[12] = entity.balancer.cooldownTimer <= 0 ? 1 : 0;
        obs[13] = target.isAttacking ? 1 : 0;
        obs[14] = target.isDashing ? 1 : 0;
        obs[15] = target.isParrying ? 1 : 0;
        obs[16] = entity.silk;

        obs[17] = Math.min(1, entity.x / 400);
        obs[18] = Math.min(1, (this.width - entity.x) / 400);
        obs[19] = Math.min(1, (this.height - entity.y) / 400);
        obs[20] = this.isOverSpikes(entity) ? 1 : 0;
        obs[21] = this.isOverSpikes({ x: entity.x + entity.facing * 60, y: entity.y }) ? 1 : 0;
        obs[22] = Math.min(1, (allyDistance !== undefined ? allyDistance : 400) / 400);
        obs[23] = entity.facing;

        return obs;
    }

    isOverSpikes(point) {
        for (const p of this.platforms) {
            if (!p.isHazard) continue;
            if (point.x > p.x - 10 && point.x < p.x + p.w + 10 && point.y < p.y) return true;
        }
        return false;
    }

    nearestAllyDistance(member) {
        let best = 400;
        for (const other of this.pack) {
            if (other === member || other.entity.health <= 0) continue;
            const d = Math.hypot(other.entity.x - member.entity.x, other.entity.y - member.entity.y);
            if (d < best) best = d;
        }
        return best;
    }

    // ------------------------------------------------------ шаг симуляции
    stepSimulation(dt, keys) {
        this.episodeTime += dt;
        if (this.statusTimer > 0) this.statusTimer -= dt;

        const now = performance.now() / 1000;
        const target = this.nearestAliveMob();
        const targetEntity = target ? target.entity : this.hornet;

        // --- 1. Решение Хорнет ---
        let hornetAction;
        if (this.manualHornet && keys) {
            hornetAction = {
                moveX: (keys['ArrowRight'] || keys['KeyD'] ? 1 : 0) - (keys['ArrowLeft'] || keys['KeyA'] ? 1 : 0),
                jump: keys['KeyK'] || keys['Space'] || keys['ArrowUp'] || keys['KeyW'],
                dash: keys['KeyL'] || keys['ShiftLeft'],
                attack: keys['KeyJ'],
                pogo: (keys['KeyI'] || keys['ArrowDown'] || keys['KeyS']) && (keys['KeyJ'] || keys['KeyI']),
                parry: keys['KeyO'],
                heal: keys['KeyU'],
                actionName: 'Игрок',
                confidence: 1.0
            };
        } else {
            const obs = this.getSensorVector(this.hornet, targetEntity, 400);
            this.hornet.lastSensors = obs;
            this.hornet.balancer.push(obs, now);
            const perceived = this.hornet.balancer.getPerceived(now, obs);
            const raw = decodeAction(this.hornet.brain.forward(perceived));
            hornetAction = this.hornet.balancer.filterAction(raw);
            this.hornet.lastRawAction = raw;
            this.decisionsCount++;
        }

        this.hornet.balancer.update(dt);
        this.hornet.applyAction(hornetAction, dt, this);

        // --- 2. Решения всей стаи ---
        for (const m of this.pack) {
            if (m.entity.health <= 0) continue;

            const obs = this.getSensorVector(m.entity, this.hornet, this.nearestAllyDistance(m));
            m.entity.lastSensors = obs;
            m.entity.balancer.push(obs, now);
            const perceived = m.entity.balancer.getPerceived(now, obs);
            const raw = decodeAction(m.entity.brain.forward(perceived));
            const action = m.entity.balancer.filterAction(raw);
            m.entity.lastRawAction = raw;
            this.decisionsCount++;

            m.entity.balancer.update(dt);
            m.entity.applyAction(action, dt, this);
        }

        // --- 3. Физика ---
        this.hornet.updatePhysics(dt, this);
        for (const m of this.pack) {
            if (m.entity.health <= 0) continue;
            m.entity.updatePhysics(dt, this);
        }

        // --- 4. Бой: Хорнет против всей стаи ---
        const aliveCount = Math.max(1, this.aliveMobs.length);
        const packScale = this.balancePackDamage ? 1 / Math.sqrt(aliveCount) : 1;

        for (const m of this.pack) {
            const e = m.entity;
            if (e.health <= 0) {
                if (!m.countedDead) {
                    m.countedDead = true;
                    this.mobsDefeated++;
                }
                continue;
            }

            const dist = Math.hypot(this.hornet.x - e.x, this.hornet.y - e.y);

            if (this.hornet.isAttacking && dist < 68) {
                const dmg = (this.hornet.isPogoing ? 32 : 24) * dt * 4;
                if (e.takeDamage(dmg, this.particles)) {
                    this.hornet.damageDealt += dmg;
                    this.hornet.silk = Math.min(1, this.hornet.silk + 0.05);
                    if (this.hornet.isPogoing) {
                        this.hornet.pogos++;
                        this.hornet.vy = -11;
                    }
                }
            }

            if (e.isAttacking && dist < 62) {
                const dmg = 20 * dt * 4 * packScale;
                if (this.hornet.takeDamage(dmg, this.particles)) {
                    e.damageDealt += dmg;
                    e.attacksHit++;
                }
            }
        }

        // --- 5. Частицы ---
        for (let i = this.particles.length - 1; i >= 0; i--) {
            this.particles[i].update(dt);
            if (this.particles[i].life <= 0) this.particles.splice(i, 1);
        }

        // --- 6. Конец эпизода ---
        const hornetDead = this.hornet.health <= 0;
        const packDead = this.aliveMobs.length === 0;
        const timeout = this.episodeTime >= this.maxEpisodeTime;

        if (hornetDead || packDead || timeout) {
            this.finishEpisode(hornetDead, packDead, timeout);
        }
    }

    // ------------------------------------------------- завершение эпизода
    finishEpisode(hornetDead, packDead) {
        this.totalEpisodes++;
        this._episodesSinceSave++;

        const packSize = this.pack.length;
        const defeated = this.pack.filter(m => m.entity.health <= 0).length;
        const packDamageTotal = this.pack.reduce((sum, m) => sum + m.entity.damageDealt, 0);

        const hornetWon = packDead && !hornetDead;
        const packWon = hornetDead;

        if (hornetWon) this.hornetWins++;
        else if (packWon) this.packWins++;
        else this.draws++;

        // --- Фитнес Хорнет (бой против численного перевеса) ---
        const outnumbered = Math.max(1, packSize);
        let hFit = this.hornet.damageDealt * 3.0
            - this.hornet.damageTaken * (2.4 / Math.sqrt(outnumbered))
            + this.hornet.pogos * 20
            + this.hornet.dodges * 7
            + defeated * 70
            + Math.min(this.episodeTime, 90) * (0.6 * outnumbered)
            + (this.hornet.health / this.hornet.maxHealth) * 60;

        if (hornetWon) hFit += 240 + this.wave * 40 + Math.max(0, 60 - this.episodeTime) * 4;
        hFit = Math.max(0, hFit);

        this.hornetTrainer.getCurrentGenome().fitness = hFit;
        this.lastHornetFitness = hFit;
        this.pushLog(this.hornetFitnessLog, hFit);

        // --- Фитнес каждого бойца стаи (параллельная оценка геномов) ---
        const evaluated = {};
        let packFitSum = 0;

        for (const m of this.pack) {
            const e = m.entity;
            const share = packDamageTotal > 0.01 ? e.damageDealt / packDamageTotal : 0;

            let fit = e.damageDealt * 4.0
                - e.damageTaken * 1.4
                + e.attacksHit * 10
                + e.dodges * 6
                + Math.min(1, share) * 60
                + Math.min(this.episodeTime, 90) * 0.25
                + (e.health / e.maxHealth) * 25;

            if (packWon) fit += 150 + Math.max(0, 60 - this.episodeTime) * 3 + Math.max(0, 8 - packSize) * 10;
            fit = Math.max(0, fit);

            const trainer = this.mobTrainers[m.key];
            this.genomeFor(trainer, m.genomeOffset).fitness = fit;
            packFitSum += fit;

            evaluated[m.key] = (evaluated[m.key] || 0) + 1;
        }

        this.lastPackFitness = packSize ? packFitSum / packSize : 0;
        this.pushLog(this.packFitnessLog, this.lastPackFitness);

        // --- Продвижение популяций ---
        this.hornetTrainer.advance();
        Object.keys(evaluated).forEach(key => {
            const trainer = this.mobTrainers[key];
            for (let i = 0; i < evaluated[key]; i++) trainer.advance();
        });

        // --- Волны ---
        let rebuilt = false;
        if (hornetWon) {
            this.wave++;
            if (this.wave > this.bestWave) this.bestWave = this.wave;
            this.showStatus(`Хорнет зачистила волну ${this.wave - 1}! Стая усиливается.`);
        } else if (packWon) {
            this.wave = Math.max(1, this.wave - 1);
            this.showStatus('Стая победила Хорнет — эволюция продолжается.');
        }

        if (this.waveScaling) {
            const desired = Math.max(1, Math.min(8, this.baseMobCount + Math.floor((this.wave - 1) / 2)));
            if (desired !== this.mobCount) {
                this.mobCount = desired;
                this.rebuildPack();
                rebuilt = true;
            }
        }

        // --- Автосохранение слота ---
        if (this.autoSaveEvery > 0 && this._episodesSinceSave >= this.autoSaveEvery) {
            this._episodesSinceSave = 0;
            if (GrandArenaSaves.save(this.slot, this)) {
                this.showStatus(`Автосохранение в слот ${this.slot}`);
            }
        }

        if (!rebuilt) this.resetEpisode();
    }

    pushLog(log, value) {
        log.push(value);
        if (log.length > 400) log.shift();
    }

    // ------------------------------------------------------- сохранения
    serialize() {
        const mobs = {};
        GRAND_ARCHETYPES.forEach(key => {
            const t = this.mobTrainers[key];
            const best = t.bestGenomeEver && t.bestGenomeEver.fitness > -9000
                ? t.bestGenomeEver
                : t.getCurrentGenome();
            mobs[key] = {
                generation: t.generation,
                bestFitness: Math.max(0, best.fitness || 0),
                brain: best.brain.toJSON()
            };
        });

        const hornetBest = this.hornetTrainer.bestGenomeEver && this.hornetTrainer.bestGenomeEver.fitness > -9000
            ? this.hornetTrainer.bestGenomeEver
            : this.hornetTrainer.getCurrentGenome();

        return {
            version: 1,
            mode: 'GrandArena',
            name: this.profileName,
            updated: new Date().toISOString(),
            playTime: this.playTime,
            wave: this.wave,
            bestWave: this.bestWave,
            mobCount: this.mobCount,
            baseMobCount: this.baseMobCount,
            totalEpisodes: this.totalEpisodes,
            hornetWins: this.hornetWins,
            packWins: this.packWins,
            draws: this.draws,
            mobsDefeated: this.mobsDefeated,
            timeScale: this.timeScale,
            maxEpisodeTime: this.maxEpisodeTime,
            waveScaling: this.waveScaling,
            balancePackDamage: this.balancePackDamage,
            hornetGeneration: this.hornetTrainer.generation,
            hornetBestFitness: Math.max(0, hornetBest.fitness || 0),
            hornetBrain: hornetBest.brain.toJSON(),
            mobs,
            hornetFitnessLog: this.hornetFitnessLog.slice(-200),
            packFitnessLog: this.packFitnessLog.slice(-200)
        };
    }

    deserialize(data) {
        if (!data) return;

        this.profileName = data.name || this.profileName;
        this.playTime = data.playTime || 0;
        this.wave = data.wave || 1;
        this.bestWave = data.bestWave || this.wave;
        this.baseMobCount = data.baseMobCount || 4;
        this.mobCount = data.mobCount || this.baseMobCount;
        this.totalEpisodes = data.totalEpisodes || 0;
        this.hornetWins = data.hornetWins || 0;
        this.packWins = data.packWins || 0;
        this.draws = data.draws || 0;
        this.mobsDefeated = data.mobsDefeated || 0;
        this.timeScale = data.timeScale || 1;
        this.maxEpisodeTime = data.maxEpisodeTime || 45;
        this.waveScaling = data.waveScaling !== false;
        this.balancePackDamage = data.balancePackDamage !== false;
        this.hornetFitnessLog = (data.hornetFitnessLog || []).slice();
        this.packFitnessLog = (data.packFitnessLog || []).slice();

        if (data.hornetBrain) {
            const brain = NeuralNetwork.fromJSON(data.hornetBrain);
            this.reseedTrainer(this.hornetTrainer, brain, data.hornetGeneration || 1, data.hornetBestFitness || 0);
        }

        if (data.mobs) {
            GRAND_ARCHETYPES.forEach(key => {
                const saved = data.mobs[key];
                if (!saved || !saved.brain) return;
                const brain = NeuralNetwork.fromJSON(saved.brain);
                this.reseedTrainer(this.mobTrainers[key], brain, saved.generation || 1, saved.bestFitness || 0);
            });
        }

        this.rebuildPack();
    }

    /** Восстанавливает популяцию из сохранённого лучшего мозга (элита + мутации). */
    reseedTrainer(trainer, brain, generation, bestFitness) {
        trainer.generation = Math.max(1, generation);
        trainer.currentGenomeIndex = 0;
        trainer.bestGenomeEver = { brain: brain.clone(), fitness: bestFitness };

        const pop = [{ brain: brain.clone(), fitness: 0, wins: 0, losses: 0, damageDealt: 0, damageTaken: 0, pogos: 0, dodges: 0 }];
        pop.push({ brain: brain.clone(), fitness: 0, wins: 0, losses: 0, damageDealt: 0, damageTaken: 0, pogos: 0, dodges: 0 });

        while (pop.length < trainer.popSize) {
            const child = brain.clone();
            child.mutate(Math.max(0.02, trainer.mutationRate), trainer.mutationStrength);
            pop.push({ brain: child, fitness: 0, wins: 0, losses: 0, damageDealt: 0, damageTaken: 0, pogos: 0, dodges: 0 });
        }

        trainer.population = pop;
    }

    resetProgress() {
        this.wave = 1;
        this.bestWave = 1;
        this.totalEpisodes = 0;
        this.hornetWins = 0;
        this.packWins = 0;
        this.draws = 0;
        this.mobsDefeated = 0;
        this.playTime = 0;
        this.hornetFitnessLog = [];
        this.packFitnessLog = [];
        this.mobCount = this.baseMobCount;

        this.hornetTrainer = new GeneticTrainerJS(24, [24, 32, 32, 10]);
        GRAND_ARCHETYPES.forEach(k => { this.mobTrainers[k] = new GeneticTrainerJS(24, [24, 32, 32, 10]); });

        this.rebuildPack();
    }
}

// =============================================================================
// РЕНДЕР БОЛЬШОЙ АРЕНЫ (наследует общий визуализатор)
// =============================================================================
class GrandArenaVisualizer extends ArenaVisualizer {
    render() {
        const ctx = this.ctx;
        const a = this.arena;
        const w = a.width;
        const h = a.height;

        // Фон
        const grad = ctx.createLinearGradient(0, 0, 0, h);
        grad.addColorStop(0, '#0b0e16');
        grad.addColorStop(0.45, '#141a28');
        grad.addColorStop(1, '#070910');
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, w, h);

        ctx.save();
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.03)';
        ctx.lineWidth = 1;
        for (let x = 0; x < w; x += 40) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke(); }
        for (let y = 0; y < h; y += 40) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke(); }
        ctx.restore();

        this.renderPlatforms();

        // Лучи сенсоров: Хорнет к ближайшему врагу и каждый моб к Хорнет
        const nearest = a.nearestAliveMob();
        if (this.showSensors) {
            if (nearest) this.renderSensors(a.hornet, nearest.entity, '#4bcffa');
            for (const m of a.pack) {
                if (m.entity.health <= 0) continue;
                this.renderSensors(m.entity, a.hornet, 'rgba(255, 94, 87, 0.5)');
            }
        }

        // Сущности
        for (const m of a.pack) {
            if (m.entity.health <= 0) {
                this.renderDefeated(m.entity);
                continue;
            }
            this.renderEntity(m.entity);
            this.renderArchetypeTag(m);
        }
        this.renderEntity(a.hornet);

        for (const p of a.particles) p.draw(ctx);

        this.renderGrandHUD();

        if (this.netCtx) this.renderNeuralNetwork();
        if (this.chartCtx) this.renderTrainingChart();
    }

    renderDefeated(entity) {
        const ctx = this.ctx;
        ctx.save();
        ctx.globalAlpha = 0.28;
        ctx.fillStyle = '#57606f';
        ctx.beginPath();
        ctx.ellipse(entity.x, entity.y + 14, 18, 7, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
    }

    renderArchetypeTag(member) {
        const ctx = this.ctx;
        ctx.save();
        ctx.fillStyle = GRAND_ARCHETYPE_COLORS[member.key];
        ctx.font = 'bold 9px Inter, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(GRAND_ARCHETYPE_TITLES[member.key], member.entity.x, member.entity.y - 42);
        ctx.restore();
    }

    renderGrandHUD() {
        const ctx = this.ctx;
        const a = this.arena;

        ctx.save();
        ctx.fillStyle = 'rgba(10, 14, 22, 0.88)';
        ctx.strokeStyle = '#3d4657';
        ctx.lineWidth = 1;
        ctx.fillRect(20, 16, 330, 76);
        ctx.strokeRect(20, 16, 330, 76);

        ctx.fillStyle = '#fbc531';
        ctx.font = 'bold 12px Inter, sans-serif';
        ctx.textAlign = 'left';
        ctx.fillText(`ВЕЛИКАЯ АРЕНА · СЛОТ ${a.slot} · ВОЛНА ${a.wave}`, 32, 36);

        ctx.fillStyle = '#f5f6fa';
        ctx.font = '11px Inter, sans-serif';
        ctx.fillText(`Эпизод ${a.totalEpisodes} · ${a.episodeTime.toFixed(1)}s / ${a.maxEpisodeTime}s · ${a.timeScale.toFixed(1)}x`, 32, 54);
        ctx.fillText(`Мобов живо: ${a.aliveMobs.length} / ${a.pack.length} · Поколение Хорнет: ${a.hornetTrainer.generation}`, 32, 72);
        ctx.fillText(`Повержено мобов всего: ${a.mobsDefeated}`, 32, 88);

        // Правая панель
        ctx.fillStyle = 'rgba(10, 14, 22, 0.88)';
        ctx.fillRect(a.width - 280, 16, 260, 76);
        ctx.strokeRect(a.width - 280, 16, 260, 76);

        ctx.fillStyle = '#4bcffa';
        ctx.fillText(`Победы Хорнет: ${a.hornetWins}`, a.width - 268, 36);
        ctx.fillStyle = '#ff6b6b';
        ctx.fillText(`Победы стаи:   ${a.packWins}`, a.width - 268, 54);
        ctx.fillStyle = '#d2dae2';
        ctx.fillText(`Ничьи: ${a.draws} · Решений/с: ${a.decisionsCount}`, a.width - 268, 72);
        ctx.fillText(`Рекорд волны: ${a.bestWave}`, a.width - 268, 88);

        if (a.statusTimer > 0 && a.statusMessage) {
            ctx.fillStyle = 'rgba(251, 197, 49, 0.92)';
            ctx.font = 'bold 13px Inter, sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(a.statusMessage, a.width / 2, 40);
        }

        ctx.restore();
    }
}
