// =============================================================================
// SILKSONG NEURAL SMART - COMBAT ARENA & PHYSICS ENGINE
// =============================================================================

const SENSOR_INPUT_COUNT = 24;

class Particle {
    constructor(x, y, vx, vy, color, life, size = 3) {
        this.x = x;
        this.y = y;
        this.vx = vx;
        this.vy = vy;
        this.color = color;
        this.maxLife = life;
        this.life = life;
        this.size = size;
    }

    update(dt) {
        this.x += this.vx * dt * 60;
        this.y += this.vy * dt * 60;
        this.vy += 0.2 * dt * 60; // gravity
        this.life -= dt;
    }

    draw(ctx) {
        const alpha = Math.max(0, this.life / this.maxLife);
        ctx.save();
        ctx.globalAlpha = alpha;
        ctx.fillStyle = this.color;
        ctx.beginPath();
        ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
    }
}

class CombatEntity {
    constructor(name, isHornet = false) {
        this.name = name;
        this.isHornet = isHornet;
        this.x = 0;
        this.y = 0;
        this.vx = 0;
        this.vy = 0;
        this.width = 30;
        this.height = 48;
        this.facing = 1; // 1: right, -1: left
        this.grounded = false;

        this.health = 100;
        this.maxHealth = 100;
        this.silk = 1.0; // 0..1

        // Combat states
        this.isAttacking = false;
        this.isDashing = false;
        this.isPogoing = false;
        this.isParrying = false;
        this.isHealing = false;

        this.attackTimer = 0;
        this.dashTimer = 0;
        this.healTimer = 0;
        this.invulnTimer = 0;

        // Cumulative metrics
        this.damageDealt = 0;
        this.damageTaken = 0;
        this.pogos = 0;
        this.dodges = 0;
        this.attacksHit = 0;

        this.balancer = new BalancingEngineJS();
        this.brain = new NeuralNetwork([24, 32, 32, 10]);
        this.lastRawAction = null;
        this.lastSensors = new Float32Array(24);
    }

    reset(x, y) {
        this.x = x;
        this.y = y;
        this.vx = 0;
        this.vy = 0;
        this.health = this.maxHealth;
        this.silk = 1.0;
        this.grounded = false;
        this.isAttacking = false;
        this.isDashing = false;
        this.isPogoing = false;
        this.isParrying = false;
        this.isHealing = false;
        this.attackTimer = 0;
        this.dashTimer = 0;
        this.healTimer = 0;
        this.invulnTimer = 0;
        this.damageDealt = 0;
        this.damageTaken = 0;
        this.pogos = 0;
        this.dodges = 0;
        this.attacksHit = 0;
        this.balancer.reset();
    }

    takeDamage(amount, particles) {
        if (this.invulnTimer > 0 || this.isDashing) {
            this.dodges++;
            return false;
        }

        if (this.isParrying) {
            amount *= 0.15; // 85% block
            if (particles) {
                for (let i = 0; i < 8; i++) {
                    particles.push(new Particle(this.x, this.y, (Math.random() - 0.5) * 8, (Math.random() - 0.5) * 8, '#ffffff', 0.4, 4));
                }
            }
        }

        this.health = Math.max(0, this.health - amount);
        this.damageTaken += amount;
        this.invulnTimer = 0.25;

        if (particles) {
            const col = this.isHornet ? '#e84118' : '#44bd32';
            for (let i = 0; i < 12; i++) {
                particles.push(new Particle(this.x, this.y, (Math.random() - 0.5) * 6, (Math.random() - 1.0) * 6, col, 0.5, 3));
            }
        }
        return true;
    }

    applyAction(action, dt, arena) {
        // Horizontal movement
        const speed = this.isHornet ? 6.5 : (this.archetypeSpeed || 5.0);
        if (action.moveX !== 0) {
            this.vx = action.moveX * speed;
            this.facing = action.moveX > 0 ? 1 : -1;
        } else {
            this.vx *= 0.8; // friction
        }

        // Jump
        if (action.jump && this.grounded) {
            this.vy = -12.5;
            this.grounded = false;
        }

        // Dash
        if (action.dash && this.dashTimer <= 0) {
            this.isDashing = true;
            this.dashTimer = 0.25;
            this.vx = this.facing * 16.0;
            this.vy = 0;
        }

        // Attack
        if (action.attack && this.attackTimer <= 0) {
            this.isAttacking = true;
            this.attackTimer = 0.22;
        }

        // Pogo
        if (action.pogo && !this.grounded) {
            this.isPogoing = true;
            this.isAttacking = true;
            this.attackTimer = 0.3;
        }

        // Parry
        this.isParrying = action.parry;

        // Silk Heal
        if (this.isHornet && action.heal && this.silk >= 0.33 && this.grounded && this.healTimer <= 0) {
            this.isHealing = true;
            this.healTimer = 0.6;
            this.silk -= 0.33;
            this.health = Math.min(this.maxHealth, this.health + 25);
            arena.spawnSilkBurst(this.x, this.y);
        }
    }

    updatePhysics(dt, arena) {
        if (this.attackTimer > 0) {
            this.attackTimer -= dt;
            if (this.attackTimer <= 0) {
                this.isAttacking = false;
                this.isPogoing = false;
            }
        }

        if (this.dashTimer > 0) {
            this.dashTimer -= dt;
            if (this.dashTimer <= 0) this.isDashing = false;
        }

        if (this.healTimer > 0) {
            this.healTimer -= dt;
            if (this.healTimer <= 0) this.isHealing = false;
        }

        if (this.invulnTimer > 0) this.invulnTimer -= dt;

        // Gravity
        if (!this.isFlying) {
            this.vy += 22.0 * dt;
        }

        this.x += this.vx * dt * 60 * 0.16;
        this.y += this.vy * dt * 60 * 0.16;

        // Room Collisions
        this.grounded = false;
        for (const plat of arena.platforms) {
            if (plat.isHazard) {
                // Spikes check
                if (this.x + this.width / 2 > plat.x && this.x - this.width / 2 < plat.x + plat.w &&
                    this.y + this.height / 2 > plat.y && this.y - this.height / 2 < plat.y + plat.h) {
                    this.takeDamage(35, arena.particles);
                    this.vy = -10; // bounce off spikes
                }
                continue;
            }

            // Floor & Platform top collision
            if (this.x + this.width / 2 > plat.x && this.x - this.width / 2 < plat.x + plat.w) {
                // Landing on platform top
                const prevY = this.y - this.vy * dt * 60 * 0.16;
                if (prevY + this.height / 2 <= plat.y + 4 && this.y + this.height / 2 >= plat.y) {
                    this.y = plat.y - this.height / 2;
                    this.vy = 0;
                    this.grounded = true;
                }
            }

            // Solid wall collision
            if (!plat.isPassThrough) {
                if (this.y + this.height / 2 > plat.y && this.y - this.height / 2 < plat.y + plat.h) {
                    if (this.x - this.width / 2 < plat.x + plat.w && this.x > plat.x + plat.w - 10) {
                        this.x = plat.x + plat.w + this.width / 2;
                        this.vx = 0;
                    } else if (this.x + this.width / 2 > plat.x && this.x < plat.x + 10) {
                        this.x = plat.x - this.width / 2;
                        this.vx = 0;
                    }
                }
            }
        }

        // Room outer boundary bounds
        if (this.x < 30) { this.x = 30; this.vx = 0; }
        if (this.x > arena.width - 30) { this.x = arena.width - 30; this.vx = 0; }
        if (this.y < 30) { this.y = 30; this.vy = 0; }
        if (this.y > arena.height - 30) {
            this.y = arena.height - 30;
            this.vy = 0;
            this.grounded = true;
        }
    }
}

// 4 Distinct Enemy Archetypes
class MossGruntEntity extends CombatEntity {
    constructor() {
        super("Moss Scout Grunt", false);
        this.archetypeSpeed = 4.8;
        this.color = "#4cd137";
        this.accent = "#44bd32";
    }
}

class FlyingHunterEntity extends CombatEntity {
    constructor() {
        super("Flying Silk Hunter", false);
        this.isFlying = true;
        this.archetypeSpeed = 5.8;
        this.color = "#9c88ff";
        this.accent = "#8c7ae6";
    }

    applyAction(action, dt, arena) {
        super.applyAction(action, dt, arena);
        // Vertical flying movement
        if (action.jump) this.vy = -6.0;
        else if (action.pogo) this.vy = 8.0;
        else this.vy = Math.sin(Date.now() * 0.005) * 2.0; // gentle hover
    }
}

class ShieldKnightEntity extends CombatEntity {
    constructor() {
        super("Citadel Shield Knight", false);
        this.archetypeSpeed = 3.6;
        this.maxHealth = 140;
        this.health = 140;
        this.color = "#fbc531";
        this.accent = "#e1b12c";
    }
}

class AssassinWeaverEntity extends CombatEntity {
    constructor() {
        super("Silk Assassin Weaver", false);
        this.archetypeSpeed = 7.5;
        this.maxHealth = 75;
        this.health = 75;
        this.color = "#e84118";
        this.accent = "#c23616";
    }
}

// Main Training Arena Environment
class TrainingArena {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.width = canvas.width;
        this.height = canvas.height;

        this.particles = [];
        this.projectiles = [];
        this.roomId = 1;
        this.platforms = [];

        this.hornet = new CombatEntity("Hornet", true);
        this.hornet.maxHealth = 100;
        this.hornet.health = 100;

        this.mob = new MossGruntEntity();
        this.mobType = "grunt";

        this.hornetTrainer = new GeneticTrainerJS(24, [24, 32, 32, 10]);
        this.mobTrainer = new GeneticTrainerJS(24, [24, 32, 32, 10]);

        this.isTrainingActive = false;
        this.manualHornetControl = false;
        this.timeScale = 1.0;

        this.totalEpisodes = 0;
        this.hornetWins = 0;
        this.mobWins = 0;
        this.draws = 0;
        this.episodeTime = 0;
        this.maxEpisodeTime = 30.0;

        this.decisionsCount = 0;
        this.decisionsPerSec = 0;
        this.fpsTimer = 0;

        this.loadRoom(1);
    }

    loadRoom(id) {
        this.roomId = id;
        this.platforms = [];

        if (id === 1) {
            // Room 1: Moss Duel Dojo (Ground + 2 platforms)
            this.platforms.push({ x: 20, y: this.height - 40, w: this.width - 40, h: 30, isHazard: false });
            this.platforms.push({ x: 140, y: this.height - 150, w: 180, h: 16, isPassThrough: true });
            this.platforms.push({ x: this.width - 320, y: this.height - 150, w: 180, h: 16, isPassThrough: true });
            this.hornetSpawn = { x: 120, y: this.height - 80 };
            this.mobSpawn = { x: this.width - 120, y: this.height - 80 };
        } else if (id === 2) {
            // Room 2: The Vertical Chasm (Spikes in middle + Tiered towers)
            this.platforms.push({ x: 20, y: this.height - 40, w: 220, h: 30, isHazard: false });
            this.platforms.push({ x: this.width - 240, y: this.height - 40, w: 220, h: 30, isHazard: false });
            this.platforms.push({ x: 240, y: this.height - 25, w: this.width - 480, h: 25, isHazard: true }); // SPIKES!

            this.platforms.push({ x: 200, y: this.height - 140, w: 140, h: 16, isPassThrough: true });
            this.platforms.push({ x: this.width - 340, y: this.height - 140, w: 140, h: 16, isPassThrough: true });
            this.platforms.push({ x: this.width / 2 - 100, y: this.height - 240, w: 200, h: 16, isPassThrough: true });

            this.hornetSpawn = { x: 100, y: this.height - 80 };
            this.mobSpawn = { x: this.width - 100, y: this.height - 80 };
        } else {
            // Room 3: Citadel Hazard Trial (Pillars & Floating Isles)
            this.platforms.push({ x: 20, y: this.height - 40, w: this.width - 40, h: 30, isHazard: false });
            this.platforms.push({ x: 260, y: this.height - 180, w: 40, h: 140, isHazard: false }); // Pillar
            this.platforms.push({ x: this.width - 300, y: this.height - 180, w: 40, h: 140, isHazard: false }); // Pillar
            this.platforms.push({ x: this.width / 2 - 120, y: this.height - 200, w: 240, h: 16, isPassThrough: true });

            this.hornetSpawn = { x: 100, y: this.height - 80 };
            this.mobSpawn = { x: this.width - 100, y: this.height - 80 };
        }

        this.resetEpisode();
    }

    setMobType(type) {
        this.mobType = type.toLowerCase();
        let newMob;
        if (this.mobType === 'flying') newMob = new FlyingHunterEntity();
        else if (this.mobType === 'knight') newMob = new ShieldKnightEntity();
        else if (this.mobType === 'assassin') newMob = new AssassinWeaverEntity();
        else newMob = new MossGruntEntity();

        newMob.brain = this.mobTrainer.getCurrentGenome().brain;
        this.mob = newMob;
        this.resetEpisode();
    }

    resetEpisode() {
        this.episodeTime = 0;
        this.hornet.brain = this.hornetTrainer.getCurrentGenome().brain;
        this.hornet.reset(this.hornetSpawn.x, this.hornetSpawn.y);

        this.mob.brain = this.mobTrainer.getCurrentGenome().brain;
        this.mob.reset(this.mobSpawn.x, this.mobSpawn.y);
    }

    spawnSilkBurst(x, y) {
        for (let i = 0; i < 20; i++) {
            const angle = Math.random() * Math.PI * 2;
            const spd = 2 + Math.random() * 5;
            this.particles.push(new Particle(x, y, Math.cos(angle) * spd, Math.sin(angle) * spd, '#ffffff', 0.6, 2.5));
        }
    }

    getSensorVector(entity, target) {
        const obs = new Float32Array(24);
        const dx = (target.x - entity.x);
        const dy = (target.y - entity.y);
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
        obs[10] = entity.grounded ? 1.0 : 0.0;
        obs[11] = target.grounded ? 1.0 : 0.0;

        obs[12] = entity.balancer.cooldownTimer <= 0 ? 1.0 : 0.0;
        obs[13] = target.isAttacking ? 1.0 : 0.0;
        obs[14] = target.isDashing ? 1.0 : 0.0;
        obs[15] = target.isParrying ? 1.0 : 0.0;
        obs[16] = entity.silk;

        // Proximity sensors
        obs[17] = Math.min(1, entity.x / 400); // dist to left wall
        obs[18] = Math.min(1, (this.width - entity.x) / 400); // dist to right wall
        obs[19] = Math.min(1, (this.height - entity.y) / 400); // dist to floor
        obs[20] = (this.roomId === 2 && entity.x > 240 && entity.x < this.width - 240) ? 1.0 : 0.0; // spikes below
        obs[21] = 0.0;
        obs[22] = 1.0;
        obs[23] = entity.facing;

        return obs;
    }

    stepSimulation(dt, manualKeys = null) {
        this.episodeTime += dt;
        const now = performance.now() / 1000;

        // 1. Hornet Decision
        let hornetAction;
        if (this.manualHornetControl && manualKeys) {
            hornetAction = {
                moveX: (manualKeys['ArrowRight'] || manualKeys['KeyD'] ? 1 : 0) - (manualKeys['ArrowLeft'] || manualKeys['KeyA'] ? 1 : 0),
                jump: manualKeys['KeyK'] || manualKeys['Space'] || manualKeys['ArrowUp'] || manualKeys['KeyW'],
                dash: manualKeys['KeyL'] || manualKeys['ShiftLeft'],
                attack: manualKeys['KeyJ'],
                pogo: (manualKeys['KeyI'] || manualKeys['ArrowDown'] || manualKeys['KeyS']) && (manualKeys['KeyJ'] || manualKeys['KeyI']),
                parry: manualKeys['KeyO'],
                heal: manualKeys['KeyU'],
                actionName: "Manual Player Control",
                confidence: 1.0
            };
        } else {
            const hObs = this.getSensorVector(this.hornet, this.mob);
            this.hornet.lastSensors = hObs;
            this.hornet.balancer.push(hObs, now);
            const pObs = this.hornet.balancer.getPerceived(now, hObs);
            const logits = this.hornet.brain.forward(pObs);
            const rawAct = decodeAction(logits);
            hornetAction = this.hornet.balancer.filterAction(rawAct);
            this.hornet.lastRawAction = rawAct;
            this.decisionsCount++;
        }

        this.hornet.balancer.update(dt);
        this.hornet.applyAction(hornetAction, dt, this);

        // 2. Mob Decision
        const mObs = this.getSensorVector(this.mob, this.hornet);
        this.mob.lastSensors = mObs;
        this.mob.balancer.push(mObs, now);
        const mpObs = this.mob.balancer.getPerceived(now, mObs);
        const mLogits = this.mob.brain.forward(mpObs);
        const mRawAct = decodeAction(mLogits);
        const mobAction = this.mob.balancer.filterAction(mRawAct);
        this.mob.lastRawAction = mRawAct;
        this.decisionsCount++;

        this.mob.balancer.update(dt);
        this.mob.applyAction(mobAction, dt, this);

        // 3. Update physics
        this.hornet.updatePhysics(dt, this);
        this.mob.updatePhysics(dt, this);

        // 4. Combat collisions & Hitbox checks
        const dist = Math.sqrt((this.hornet.x - this.mob.x) ** 2 + (this.hornet.y - this.mob.y) ** 2);

        // Hornet striking Mob
        if (this.hornet.isAttacking && dist < 65) {
            const dmg = this.hornet.isPogoing ? 32 : 24;
            const landed = this.mob.takeDamage(dmg * dt * 4, this.particles);
            if (landed) {
                this.hornet.damageDealt += dmg * dt * 4;
                this.hornet.silk = Math.min(1.0, this.hornet.silk + 0.08);
                if (this.hornet.isPogoing) {
                    this.hornet.pogos++;
                    this.hornet.vy = -11.0; // Pogo bounce recoil!
                }
            }
        }

        // Mob striking Hornet
        if (this.mob.isAttacking && dist < 60) {
            const dmg = 20;
            const landed = this.hornet.takeDamage(dmg * dt * 4, this.particles);
            if (landed) {
                this.mob.damageDealt += dmg * dt * 4;
                this.mob.attacksHit++;
            }
        }

        // 5. Update Particles
        for (let i = this.particles.length - 1; i >= 0; i--) {
            this.particles[i].update(dt);
            if (this.particles[i].life <= 0) {
                this.particles.splice(i, 1);
            }
        }

        // 6. Check Episode End
        const hornetDead = this.hornet.health <= 0;
        const mobDead = this.mob.health <= 0;
        const timeout = this.episodeTime >= this.maxEpisodeTime;

        if (hornetDead || mobDead || timeout) {
            this.finishEpisode(hornetDead, mobDead, timeout);
        }
    }

    finishEpisode(hornetDead, mobDead, timeout) {
        this.totalEpisodes++;
        const hornetWon = mobDead && !hornetDead;
        const mobWon = hornetDead && !mobDead;

        if (hornetWon) this.hornetWins++;
        else if (mobWon) this.mobWins++;
        else this.draws++;

        // Fitness calculation
        let hFit = this.hornet.damageDealt * 3.5 - this.hornet.damageTaken * 2.0 + this.hornet.pogos * 20 + this.hornet.dodges * 8;
        if (hornetWon) hFit += 250 + (this.maxEpisodeTime - this.episodeTime) * 6;
        hFit += (this.hornet.health / this.hornet.maxHealth) * 50;

        let mFit = this.mob.damageDealt * 3.5 - this.mob.damageTaken * 1.8 + this.mob.attacksHit * 15 + this.mob.dodges * 8;
        if (mobWon) mFit += 220 + (this.maxEpisodeTime - this.episodeTime) * 5;
        mFit += (this.mob.health / this.mob.maxHealth) * 40;

        this.hornetTrainer.getCurrentGenome().fitness = Math.max(0, hFit);
        this.mobTrainer.getCurrentGenome().fitness = Math.max(0, mFit);

        this.hornetTrainer.advance();
        this.mobTrainer.advance();

        this.resetEpisode();
    }
}
