// =============================================================================
// SILKSONG NEURAL SMART - JAVASCRIPT NEURAL ENGINE & GENETIC ALGORITHM
// 100% Binary/Formula Compatibility with C# BepInEx Core
// =============================================================================

class NeuralNetwork {
    constructor(layerSizes) {
        this.layerSizes = layerSizes || [24, 32, 32, 10];
        this.activations = [];
        this.rawZ = [];
        this.weights = [];
        this.biases = [];

        this.initLayers();
        this.initHe();
    }

    initLayers() {
        const numLayers = this.layerSizes.length;
        this.activations = new Array(numLayers);
        this.rawZ = new Array(numLayers);

        for (let i = 0; i < numLayers; i++) {
            this.activations[i] = new Float32Array(this.layerSizes[i]);
            this.rawZ[i] = new Float32Array(this.layerSizes[i]);
        }

        this.weights = new Array(numLayers - 1);
        this.biases = new Array(numLayers - 1);

        for (let l = 0; l < numLayers - 1; l++) {
            const outNodes = this.layerSizes[l + 1];
            const inNodes = this.layerSizes[l];

            this.weights[l] = new Array(outNodes);
            this.biases[l] = new Float32Array(outNodes);

            for (let j = 0; j < outNodes; j++) {
                this.weights[l][j] = new Float32Array(inNodes);
            }
        }
    }

    initHe() {
        for (let l = 0; l < this.weights.length; l++) {
            const inCount = this.layerSizes[l];
            const stdDev = Math.sqrt(2.0 / inCount);

            for (let j = 0; j < this.weights[l].length; j++) {
                this.biases[l][j] = 0.01;
                for (let i = 0; i < this.weights[l][j].length; i++) {
                    this.weights[l][j][i] = this.gaussianRandom(0, stdDev);
                }
            }
        }
    }

    gaussianRandom(mean = 0, stdDev = 1) {
        let u1 = 1.0 - Math.random();
        let u2 = 1.0 - Math.random();
        let randStdNormal = Math.sqrt(-2.0 * Math.log(u1)) * Math.sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    forward(inputs) {
        if (!inputs || inputs.length !== this.layerSizes[0]) {
            throw new Error(`Input size mismatch. Expected ${this.layerSizes[0]}, got ${inputs?.length}`);
        }

        // Copy input
        for (let i = 0; i < inputs.length; i++) {
            this.activations[0][i] = inputs[i];
        }

        for (let l = 0; l < this.weights.length; l++) {
            const prev = this.activations[l];
            const current = this.activations[l + 1];
            const z = this.rawZ[l + 1];
            const w = this.weights[l];
            const b = this.biases[l];
            const isOutput = (l === this.weights.length - 1);

            for (let j = 0; j < current.length; j++) {
                let sum = b[j];
                const row = w[j];
                for (let i = 0; i < prev.length; i++) {
                    sum += prev[i] * row[i];
                }
                z[j] = sum;

                if (isOutput) {
                    current[j] = sum;
                } else {
                    // LeakyReLU
                    current[j] = sum > 0 ? sum : sum * 0.01;
                }
            }

            if (isOutput) {
                this.applySoftmax(current);
            }
        }

        return this.activations[this.activations.length - 1];
    }

    applySoftmax(values) {
        let max = values[0];
        for (let i = 1; i < values.length; i++) {
            if (values[i] > max) max = values[i];
        }

        let sum = 0;
        for (let i = 0; i < values.length; i++) {
            const clamped = Math.max(-20, Math.min(20, values[i] - max));
            values[i] = Math.exp(clamped);
            sum += values[i];
        }

        if (sum < 1e-7) sum = 1e-7;
        for (let i = 0; i < values.length; i++) {
            values[i] /= sum;
        }
    }

    mutate(rate, strength) {
        for (let l = 0; l < this.weights.length; l++) {
            for (let j = 0; j < this.weights[l].length; j++) {
                if (Math.random() < rate) {
                    this.biases[l][j] += this.gaussianRandom(0, strength * 0.5);
                }
                for (let i = 0; i < this.weights[l][j].length; i++) {
                    if (Math.random() < rate) {
                        if (Math.random() < 0.05) {
                            this.weights[l][j][i] = this.gaussianRandom(0, 0.5);
                        } else {
                            this.weights[l][j][i] += this.gaussianRandom(0, strength);
                        }
                    }
                }
            }
        }
    }

    clone() {
        const copy = new NeuralNetwork(this.layerSizes);
        for (let l = 0; l < this.weights.length; l++) {
            copy.biases[l].set(this.biases[l]);
            for (let j = 0; j < this.weights[l].length; j++) {
                copy.weights[l][j].set(this.weights[l][j]);
            }
        }
        return copy;
    }

    static crossover(parentA, parentB) {
        const child = new NeuralNetwork(parentA.layerSizes);
        for (let l = 0; l < child.weights.length; l++) {
            for (let j = 0; j < child.weights[l].length; j++) {
                child.biases[l][j] = Math.random() < 0.5 ? parentA.biases[l][j] : parentB.biases[l][j];
                for (let i = 0; i < child.weights[l][j].length; i++) {
                    child.weights[l][j][i] = Math.random() < 0.5 ? parentA.weights[l][j][i] : parentB.weights[l][j][i];
                }
            }
        }
        return child;
    }

    toJSON() {
        const biasesArr = [];
        for (let l = 0; l < this.biases.length; l++) {
            biasesArr.push(Array.from(this.biases[l]));
        }

        const weightsArr = [];
        for (let l = 0; l < this.weights.length; l++) {
            const layerArr = [];
            for (let j = 0; j < this.weights[l].length; j++) {
                layerArr.push(Array.from(this.weights[l][j]));
            }
            weightsArr.push(layerArr);
        }

        return {
            layerSizes: this.layerSizes,
            biases: biasesArr,
            weights: weightsArr
        };
    }

    static fromJSON(obj) {
        if (typeof obj === 'string') obj = JSON.parse(obj);
        const net = new NeuralNetwork(obj.layerSizes);

        for (let l = 0; l < net.biases.length && l < obj.biases.length; l++) {
            for (let j = 0; j < net.biases[l].length && j < obj.biases[l].length; j++) {
                net.biases[l][j] = obj.biases[l][j];
            }
        }

        for (let l = 0; l < net.weights.length && l < obj.weights.length; l++) {
            for (let j = 0; j < net.weights[l].length && j < obj.weights[l].length; j++) {
                for (let i = 0; i < net.weights[l][j].length && i < obj.weights[l][j].length; i++) {
                    net.weights[l][j][i] = obj.weights[l][j][i];
                }
            }
        }

        return net;
    }
}

// Neuroevolution Genetic Trainer
class GeneticTrainerJS {
    constructor(popSize, layerSizes) {
        this.popSize = Math.max(4, popSize);
        this.generation = 1;
        this.currentGenomeIndex = 0;
        this.mutationRate = 0.08;
        this.mutationStrength = 0.25;
        this.eliteCount = 2;

        this.population = [];
        for (let i = 0; i < this.popSize; i++) {
            this.population.push({
                brain: new NeuralNetwork(layerSizes),
                fitness: 0,
                wins: 0,
                losses: 0,
                damageDealt: 0,
                damageTaken: 0,
                pogos: 0,
                dodges: 0
            });
        }

        this.bestGenomeEver = {
            brain: this.population[0].brain.clone(),
            fitness: -9999
        };
        this.fitnessHistory = [];
    }

    getCurrentGenome() {
        return this.population[this.currentGenomeIndex];
    }

    advance() {
        this.currentGenomeIndex++;
        if (this.currentGenomeIndex >= this.population.length) {
            this.evolve();
            this.currentGenomeIndex = 0;
            return true; // Generation finished
        }
        return false;
    }

    evolve() {
        this.population.sort((a, b) => b.fitness - a.fitness);

        const genBest = this.population[0].fitness;
        this.fitnessHistory.push(genBest);

        if (genBest > this.bestGenomeEver.fitness) {
            this.bestGenomeEver = {
                brain: this.population[0].brain.clone(),
                fitness: genBest
            };
        }

        const nextPop = [];
        // Elitism
        for (let i = 0; i < this.eliteCount && i < this.population.length; i++) {
            nextPop.push({
                brain: this.population[i].brain.clone(),
                fitness: 0,
                wins: 0,
                losses: 0,
                damageDealt: 0,
                damageTaken: 0,
                pogos: 0,
                dodges: 0
            });
        }

        // Selection & crossover
        while (nextPop.length < this.popSize) {
            const pA = this.tournament(4);
            const pB = this.tournament(4);

            const childBrain = NeuralNetwork.crossover(pA.brain, pB.brain);
            childBrain.mutate(this.mutationRate, this.mutationStrength);

            nextPop.push({
                brain: childBrain,
                fitness: 0,
                wins: 0,
                losses: 0,
                damageDealt: 0,
                damageTaken: 0,
                pogos: 0,
                dodges: 0
            });
        }

        this.population = nextPop;
        this.generation++;
    }

    tournament(k = 4) {
        let best = null;
        for (let i = 0; i < k; i++) {
            const rand = this.population[Math.floor(Math.random() * this.population.length)];
            if (!best || rand.fitness > best.fitness) {
                best = rand;
            }
        }
        return best;
    }
}

// Action Decoding Enum & Maps
const CombatAction = {
    MoveLeft: 0,
    MoveRight: 1,
    IdleSpacing: 2,
    Jump: 3,
    Dash: 4,
    AttackSlash: 5,
    SpecialSkill: 6,
    PogoDownSlash: 7,
    ParryBlock: 8,
    SilkHeal: 9
};

const ACTION_NAMES = [
    "Move Left",
    "Move Right",
    "Idle / Space",
    "Jump / Leap",
    "Dash Evade",
    "Needle Slash",
    "Silk Skill / Dive",
    "Pogo Down-Slash",
    "Shield / Parry",
    "Silk Heal (Focus)"
];

function decodeAction(actionProbabilities) {
    let bestIdx = 0;
    let maxProb = actionProbabilities[0];

    for (let i = 1; i < actionProbabilities.length; i++) {
        if (actionProbabilities[i] > maxProb) {
            maxProb = actionProbabilities[i];
            bestIdx = i;
        }
    }

    const action = {
        primaryAction: bestIdx,
        actionName: ACTION_NAMES[bestIdx],
        confidence: maxProb,
        moveX: 0,
        jump: false,
        dash: false,
        attack: false,
        special: false,
        pogo: false,
        parry: false,
        heal: false
    };

    switch (bestIdx) {
        case CombatAction.MoveLeft: action.moveX = -1.0; break;
        case CombatAction.MoveRight: action.moveX = 1.0; break;
        case CombatAction.IdleSpacing: action.moveX = 0.0; break;
        case CombatAction.Jump: action.jump = true; break;
        case CombatAction.Dash: action.dash = true; break;
        case CombatAction.AttackSlash: action.attack = true; break;
        case CombatAction.SpecialSkill: action.special = true; break;
        case CombatAction.PogoDownSlash: action.pogo = true; break;
        case CombatAction.ParryBlock: action.parry = true; break;
        case CombatAction.SilkHeal: action.heal = true; break;
    }

    return action;
}

// Balance Latency Buffer
class BalancingEngineJS {
    constructor() {
        this.reactionDelay = 0.11; // seconds (110ms)
        this.errorMargin = 0.05;
        this.stamina = 1.0;
        this.staminaRegen = 0.35;
        this.cooldownTimer = 0.0;
        this.buffer = [];
    }

    reset() {
        this.stamina = 1.0;
        this.cooldownTimer = 0.0;
        this.buffer = [];
    }

    update(dt) {
        if (this.cooldownTimer > 0) this.cooldownTimer -= dt;
        this.stamina = Math.min(1.0, this.stamina + this.staminaRegen * dt);
    }

    push(obs, time) {
        this.buffer.push({ time, obs: new Float32Array(obs) });
    }

    getPerceived(time, fallback) {
        if (this.buffer.length === 0) return fallback;
        const targetTime = time - this.reactionDelay;
        let selected = this.buffer[0];

        while (this.buffer.length > 1) {
            if (this.buffer[1].time <= targetTime) {
                selected = this.buffer.shift();
            } else {
                break;
            }
        }
        return selected.obs || fallback;
    }

    filterAction(action) {
        const filtered = Object.assign({}, action);

        // Human mistake jitter
        if (Math.random() < this.errorMargin) {
            filtered.moveX *= 0.5;
        }

        // Attacks cooldown and stamina budget
        if (filtered.attack || filtered.special || filtered.pogo) {
            if (this.cooldownTimer > 0 || this.stamina < 0.20) {
                filtered.attack = false;
                filtered.special = false;
                filtered.pogo = false;
            } else {
                this.stamina -= 0.22;
                this.cooldownTimer = 0.35;
            }
        }

        // Dash stamina
        if (filtered.dash) {
            if (this.stamina < 0.15) {
                filtered.dash = false;
            } else {
                this.stamina -= 0.18;
            }
        }

        return filtered;
    }
}
