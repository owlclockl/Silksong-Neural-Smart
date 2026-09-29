// =============================================================================
// SILKSONG NEURAL SMART - ARENA RENDERER & NEURAL VISUALIZER
// =============================================================================

class ArenaVisualizer {
    constructor(arena, netCanvas, chartCanvas) {
        this.arena = arena;
        this.ctx = arena.ctx;
        this.netCanvas = netCanvas;
        this.netCtx = netCanvas ? netCanvas.getContext('2d') : null;
        this.chartCanvas = chartCanvas;
        this.chartCtx = chartCanvas ? chartCanvas.getContext('2d') : null;

        this.showSensors = true;
        this.showHitboxes = false;
        this.activeBrainTarget = 'hornet'; // 'hornet' or 'mob'
    }

    render() {
        const ctx = this.ctx;
        const w = this.arena.width;
        const h = this.arena.height;

        // 1. Background
        const grad = ctx.createLinearGradient(0, 0, 0, h);
        grad.addColorStop(0, '#0a0d14');
        grad.addColorStop(0.5, '#121824');
        grad.addColorStop(1, '#080a10');
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, w, h);

        // Grid lines / Pharloom ambiance
        ctx.save();
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.03)';
        ctx.lineWidth = 1;
        for (let x = 0; x < w; x += 40) {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, h);
            ctx.stroke();
        }
        for (let y = 0; y < h; y += 40) {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(w, y);
            ctx.stroke();
        }
        ctx.restore();

        // 2. Platforms & Obstacles
        this.renderPlatforms();

        // 3. Sensory Raycasts
        if (this.showSensors) {
            this.renderSensors(this.arena.hornet, this.arena.mob, '#4bcffa');
            this.renderSensors(this.arena.mob, this.arena.hornet, '#ff5e57');
        }

        // 4. Entities
        this.renderEntity(this.arena.hornet);
        this.renderEntity(this.arena.mob);

        // 5. Particles
        for (const p of this.arena.particles) {
            p.draw(ctx);
        }

        // 6. Arena Top HUD
        this.renderArenaHUD();

        // 7. Live Neural Visualizer
        if (this.netCtx) {
            this.renderNeuralNetwork();
        }

        // 8. Fitness & Training Chart
        if (this.chartCtx) {
            this.renderTrainingChart();
        }
    }

    renderPlatforms() {
        const ctx = this.ctx;
        for (const p of this.arena.platforms) {
            if (p.isHazard) {
                // Spikes
                ctx.fillStyle = '#ff3838';
                ctx.fillRect(p.x, p.y + p.h - 4, p.w, 4);

                ctx.fillStyle = '#eb2f06';
                const count = Math.floor(p.w / 14);
                for (let i = 0; i < count; i++) {
                    const sx = p.x + i * 14;
                    ctx.beginPath();
                    ctx.moveTo(sx, p.y + p.h);
                    ctx.lineTo(sx + 7, p.y);
                    ctx.lineTo(sx + 14, p.y + p.h);
                    ctx.closePath();
                    ctx.fill();
                }
            } else {
                // Platform stone
                const pGrad = ctx.createLinearGradient(p.x, p.y, p.x, p.y + p.h);
                pGrad.addColorStop(0, '#2f3640');
                pGrad.addColorStop(1, '#1e272e');
                ctx.fillStyle = pGrad;
                ctx.fillRect(p.x, p.y, p.w, p.h);

                // Top glow edge
                ctx.fillStyle = '#718093';
                ctx.fillRect(p.x, p.y, p.w, 3);
            }
        }
    }

    renderSensors(entity, target, color) {
        const ctx = this.ctx;
        ctx.save();
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.globalAlpha = 0.35;

        // Line to target
        ctx.setLineDash([4, 4]);
        ctx.beginPath();
        ctx.moveTo(entity.x, entity.y);
        ctx.lineTo(target.x, target.y);
        ctx.stroke();

        // 4 Directional raycasts
        ctx.setLineDash([]);
        const rays = [
            { dx: -120, dy: 0 },
            { dx: 120, dy: 0 },
            { dx: 0, dy: 80 },
            { dx: 0, dy: -80 },
            { dx: entity.facing * 140, dy: -40 }
        ];

        for (const r of rays) {
            ctx.beginPath();
            ctx.moveTo(entity.x, entity.y);
            ctx.lineTo(entity.x + r.dx, entity.y + r.dy);
            ctx.stroke();
        }

        ctx.restore();
    }

    renderEntity(entity) {
        const ctx = this.ctx;
        ctx.save();
        ctx.translate(entity.x, entity.y);

        const isHornet = entity.isHornet;

        // Dash Trail / Ghosting
        if (entity.isDashing) {
            ctx.fillStyle = isHornet ? 'rgba(235, 47, 6, 0.4)' : 'rgba(76, 209, 55, 0.4)';
            ctx.beginPath();
            ctx.arc(-entity.facing * 20, 0, 18, 0, Math.PI * 2);
            ctx.fill();
        }

        // Silk Heal Aura
        if (entity.isHealing) {
            ctx.strokeStyle = '#fbc531';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.arc(0, 0, 32 + Math.sin(Date.now() * 0.02) * 4, 0, Math.PI * 2);
            ctx.stroke();
        }

        // Shield Guard Aura
        if (entity.isParrying) {
            ctx.strokeStyle = '#00d2d3';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.arc(entity.facing * 12, 0, 26, -Math.PI * 0.4, Math.PI * 0.4);
            ctx.stroke();
        }

        // Body
        if (isHornet) {
            // Hornet Red Cloak
            ctx.fillStyle = '#e84118';
            ctx.beginPath();
            ctx.ellipse(0, 4, 14, 20, 0, 0, Math.PI * 2);
            ctx.fill();

            // White Mask / Head
            ctx.fillStyle = '#f5f6fa';
            ctx.beginPath();
            ctx.ellipse(0, -14, 10, 12, 0, 0, Math.PI * 2);
            ctx.fill();

            // Black Horns
            ctx.fillStyle = '#2f3640';
            ctx.beginPath();
            ctx.moveTo(-6, -22);
            ctx.lineTo(-12, -38);
            ctx.lineTo(-2, -26);
            ctx.closePath();
            ctx.fill();

            ctx.beginPath();
            ctx.moveTo(6, -22);
            ctx.lineTo(12, -38);
            ctx.lineTo(2, -26);
            ctx.closePath();
            ctx.fill();

            // Eyes
            ctx.fillStyle = '#1e272e';
            ctx.beginPath();
            ctx.ellipse(entity.facing * 4, -14, 2, 4, 0, 0, Math.PI * 2);
            ctx.fill();

            // Needle Weapon
            ctx.strokeStyle = '#dcdde1';
            ctx.lineWidth = 3;
            ctx.beginPath();
            if (entity.isAttacking) {
                // Extended Needle Thrust
                ctx.moveTo(0, -4);
                ctx.lineTo(entity.facing * 42, -4);
                ctx.stroke();

                // Needle Slash Arc Effect
                ctx.strokeStyle = 'rgba(255, 255, 255, 0.8)';
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.arc(0, -4, 44, entity.facing > 0 ? -Math.PI * 0.3 : Math.PI * 0.7, entity.facing > 0 ? Math.PI * 0.3 : Math.PI * 1.3);
                ctx.stroke();
            } else if (entity.isPogoing) {
                // Downward Needle Strike
                ctx.moveTo(0, 10);
                ctx.lineTo(0, 42);
                ctx.stroke();
            } else {
                // Needle Sheathed / Angled
                ctx.moveTo(-entity.facing * 6, -10);
                ctx.lineTo(entity.facing * 18, 12);
                ctx.stroke();
            }
        } else {
            // Enemy Mob Sprite
            ctx.fillStyle = entity.color || '#44bd32';
            ctx.beginPath();
            ctx.ellipse(0, 0, 16, 22, 0, 0, Math.PI * 2);
            ctx.fill();

            // Mob Eyes (Glowing orange/red)
            ctx.fillStyle = '#e1b12c';
            ctx.beginPath();
            ctx.arc(entity.facing * 6, -6, 3, 0, Math.PI * 2);
            ctx.fill();

            // Enemy Attack Swipe
            if (entity.isAttacking) {
                ctx.strokeStyle = '#ff3838';
                ctx.lineWidth = 3;
                ctx.beginPath();
                ctx.arc(entity.facing * 10, 0, 36, -Math.PI * 0.3, Math.PI * 0.3);
                ctx.stroke();
            }
        }

        ctx.restore();

        // Health & Resource Bars above character
        this.renderCharacterBars(entity);
    }

    renderCharacterBars(entity) {
        const ctx = this.ctx;
        const barW = 44;
        const barH = 5;
        const bx = entity.x - barW / 2;
        const by = entity.y - entity.height / 2 - 20;

        // Health background
        ctx.fillStyle = '#1e272e';
        ctx.fillRect(bx, by, barW, barH);

        // Health fill
        const hpPct = Math.max(0, entity.health / entity.maxHealth);
        ctx.fillStyle = entity.isHornet ? '#e84118' : '#4cd137';
        ctx.fillRect(bx, by, barW * hpPct, barH);

        // Border
        ctx.strokeStyle = '#718093';
        ctx.lineWidth = 1;
        ctx.strokeRect(bx, by, barW, barH);

        // Sub-bar: Silk for Hornet / Stamina for Mob
        if (entity.isHornet) {
            ctx.fillStyle = '#f5f6fa';
            ctx.fillRect(bx, by + barH + 2, barW * entity.silk, 3);
        } else {
            ctx.fillStyle = '#fbc531';
            ctx.fillRect(bx, by + barH + 2, barW * entity.balancer.stamina, 3);
        }

        // Action Name Tag
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.font = '10px Inter, sans-serif';
        ctx.textAlign = 'center';
        const actionLabel = entity.lastRawAction ? entity.lastRawAction.actionName : 'Idle';
        ctx.fillText(actionLabel, entity.x, by - 6);
    }

    renderArenaHUD() {
        const ctx = this.ctx;
        const a = this.arena;

        ctx.save();
        // Top status card
        ctx.fillStyle = 'rgba(15, 20, 30, 0.85)';
        ctx.strokeStyle = '#2f3640';
        ctx.lineWidth = 1;
        ctx.fillRect(20, 20, 340, 68);
        ctx.strokeRect(20, 20, 340, 68);

        ctx.fillStyle = '#f5f6fa';
        ctx.font = 'bold 12px Inter, sans-serif';
        ctx.textAlign = 'left';
        ctx.fillText(`ROOM: ${a.roomId === 1 ? 'Moss Dojo' : a.roomId === 2 ? 'Vertical Chasm' : 'Citadel Trial'}`, 32, 40);
        ctx.fillText(`EPISODE: ${a.totalEpisodes} | GEN: ${a.hornetTrainer.generation}`, 32, 56);
        ctx.fillText(`TIME: ${a.episodeTime.toFixed(1)}s / ${a.maxEpisodeTime}s | SPEED: ${a.timeScale.toFixed(1)}x`, 32, 72);

        // Win Rate Pill on right
        ctx.fillStyle = 'rgba(15, 20, 30, 0.85)';
        ctx.fillRect(a.width - 280, 20, 260, 68);
        ctx.strokeRect(a.width - 280, 20, 260, 68);

        const hWr = a.totalEpisodes > 0 ? (a.hornetWins / a.totalEpisodes * 100).toFixed(1) : '0.0';
        const mWr = a.totalEpisodes > 0 ? (a.mobWins / a.totalEpisodes * 100).toFixed(1) : '0.0';

        ctx.fillStyle = '#4bcffa';
        ctx.fillText(`HORNET WINS: ${a.hornetWins} (${hWr}%)`, a.width - 268, 42);
        ctx.fillStyle = '#ff5e57';
        ctx.fillText(`MOB WINS:    ${a.mobWins} (${mWr}%)`, a.width - 268, 58);
        ctx.fillStyle = '#d2dae2';
        ctx.fillText(`DRAWS:       ${a.draws} | DECISIONS/S: ${a.decisionsCount}`, a.width - 268, 74);

        ctx.restore();
    }

    renderNeuralNetwork() {
        const ctx = this.netCtx;
        const w = this.netCanvas.width;
        const h = this.netCanvas.height;

        ctx.clearRect(0, 0, w, h);

        const target = this.activeBrainTarget === 'hornet' ? this.arena.hornet : this.arena.mob;
        const brain = target.brain;
        if (!brain) return;

        const layerSizes = brain.layerSizes;
        const numLayers = layerSizes.length;
        const layerSpacing = (w - 100) / (numLayers - 1);

        // Header
        ctx.fillStyle = this.activeBrainTarget === 'hornet' ? '#4bcffa' : '#ff5e57';
        ctx.font = 'bold 12px Inter, sans-serif';
        ctx.textAlign = 'left';
        ctx.fillText(`BRAIN: ${target.name.toUpperCase()} (${layerSizes.join(' → ')})`, 16, 20);

        // Draw Layer nodes
        const nodePositions = [];

        for (let l = 0; l < numLayers; l++) {
            const count = Math.min(layerSizes[l], 16); // sample up to 16
            const lx = 50 + l * layerSpacing;
            const nodeSpacing = (h - 60) / (count + 1);
            const colNodes = [];

            for (let n = 0; n < count; n++) {
                const ny = 40 + (n + 1) * nodeSpacing;
                colNodes.push({ x: lx, y: ny });
            }
            nodePositions.push(colNodes);
        }

        // Draw Synapses (Weights)
        ctx.save();
        for (let l = 0; l < numLayers - 1; l++) {
            const currentNodes = nodePositions[l];
            const nextNodes = nodePositions[l + 1];

            for (let i = 0; i < currentNodes.length; i++) {
                for (let j = 0; j < nextNodes.length; j++) {
                    const weightVal = (brain.weights[l] && brain.weights[l][j]) ? brain.weights[l][j][i] || 0 : 0;
                    ctx.strokeStyle = weightVal > 0 ? 'rgba(75, 207, 250, 0.12)' : 'rgba(255, 94, 87, 0.12)';
                    ctx.lineWidth = Math.min(2.5, Math.abs(weightVal));
                    ctx.beginPath();
                    ctx.moveTo(currentNodes[i].x, currentNodes[i].y);
                    ctx.lineTo(nextNodes[j].x, nextNodes[j].y);
                    ctx.stroke();
                }
            }
        }
        ctx.restore();

        // Draw Nodes
        for (let l = 0; l < numLayers; l++) {
            const col = nodePositions[l];
            for (let n = 0; n < col.length; n++) {
                const act = (brain.activations[l] && brain.activations[l][n] !== undefined)
                    ? Math.abs(brain.activations[l][n])
                    : 0;

                const nodeRadius = l === 0 || l === numLayers - 1 ? 5 : 4;
                ctx.beginPath();
                ctx.arc(col[n].x, col[n].y, nodeRadius, 0, Math.PI * 2);

                const glow = Math.min(1, act);
                ctx.fillStyle = `rgba(75, 207, 250, ${0.3 + glow * 0.7})`;
                ctx.fill();
                ctx.strokeStyle = '#ffffff';
                ctx.lineWidth = 1;
                ctx.stroke();
            }
        }
    }

    renderTrainingChart() {
        const ctx = this.chartCtx;
        const w = this.chartCanvas.width;
        const h = this.chartCanvas.height;

        ctx.clearRect(0, 0, w, h);

        const hHistory = this.arena.hornetTrainer.fitnessHistory;
        const mHistory = this.arena.mobTrainer.fitnessHistory;

        // Background
        ctx.fillStyle = 'rgba(15, 20, 30, 0.9)';
        ctx.fillRect(0, 0, w, h);

        ctx.fillStyle = '#a4b0be';
        ctx.font = '11px Inter, sans-serif';
        ctx.fillText('FITNESS EVOLUTION CURVE (GENERATIONS)', 14, 18);

        if (hHistory.length < 2 && mHistory.length < 2) {
            ctx.fillStyle = '#718093';
            ctx.fillText('Waiting for generation 2+ data...', 14, 40);
            return;
        }

        // Draw Hornet Fitness Curve
        this.drawLineSeries(hHistory, '#4bcffa', 30, h - 20, w - 50, h - 50);
        // Draw Mob Fitness Curve
        this.drawLineSeries(mHistory, '#ff5e57', 30, h - 20, w - 50, h - 50);
    }

    drawLineSeries(data, color, ox, oy, plotW, plotH) {
        if (!data || data.length === 0) return;
        const ctx = this.chartCtx;
        const maxVal = Math.max(...data, 100);
        const minVal = 0;

        ctx.save();
        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        ctx.beginPath();

        for (let i = 0; i < data.length; i++) {
            const x = ox + (i / Math.max(1, data.length - 1)) * plotW;
            const y = oy - ((data[i] - minVal) / (maxVal - minVal)) * plotH;

            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.stroke();
        ctx.restore();
    }
}
