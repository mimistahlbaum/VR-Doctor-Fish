// VR Doctor Fish – browser version.
//
// A Three.js port of the Unity experience in Assets/Scripts/DoctorFish.
// The staged sequence (Welcome -> Small fish -> Big fish -> Jellyfish ->
// Calm -> Finished), the creature choreography, the water surface and the
// audio all mirror the Unity implementation. Haptics are not available in
// the browser; the experience runs with visuals and audio only, exactly
// like the Unity build without the VibraForge server.
//
// Coordinates: the Unity scene is mirrored on Z (Unity looks down +Z,
// Three.js looks down -Z), so the pool sits at z = -0.55 in front of the
// seated viewer at the origin.

import * as THREE from './vendor/three.module.min.js';

// ---------------------------------------------------------------------------
// Layout constants (DoctorFishBootstrap).

const POOL_CENTRE = new THREE.Vector3(0, 0, -0.55);
const TUB_RADIUS = 0.5;
const TUB_WALL_HEIGHT = 0.36;
const WATER_LEVEL = 0.3;
const SEATED_EYE_HEIGHT = 1.1;
// Desktop (non-VR) start view: leaning a little forward over the tub and
// looking steeply down, so the whole leg is in frame from the knees down
// the shins to the toes and the shins never hide the feet.
const DESKTOP_EYE = new THREE.Vector3(0, SEATED_EYE_HEIGHT + 0.1, -0.3);
const DESKTOP_PITCH_DEG = -68;

const Stage = {
  Welcome: 'Welcome',
  SmallFish: 'SmallFish',
  BigFish: 'BigFish',
  Jellyfish: 'Jellyfish',
  Calm: 'Calm',
  Finished: 'Finished',
};

const Leg = { Left: 'Left', Right: 'Right', Both: 'Both' };

// ---------------------------------------------------------------------------
// Small helpers.

// Unity material colours are linear values; keep them linear here too so
// the browser version matches the Unity look.
function col(r, g, b) {
  return new THREE.Color().setRGB(r, g, b, THREE.LinearSRGBColorSpace);
}

const rand = (a, b) => a + Math.random() * (b - a);

function insideUnitCircle() {
  const angle = Math.random() * Math.PI * 2;
  const radius = Math.sqrt(Math.random());
  return new THREE.Vector2(Math.cos(angle) * radius, Math.sin(angle) * radius);
}

function insideUnitSphere() {
  const v = new THREE.Vector3();
  do {
    v.set(rand(-1, 1), rand(-1, 1), rand(-1, 1));
  } while (v.lengthSq() > 1);
  return v;
}

function moveTowards(current, target, maxDelta) {
  const dx = target.x - current.x;
  const dy = target.y - current.y;
  const dz = target.z - current.z;
  const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);
  if (dist <= maxDelta || dist === 0) {
    current.copy(target);
    return current;
  }
  const k = maxDelta / dist;
  current.x += dx * k;
  current.y += dy * k;
  current.z += dz * k;
  return current;
}

const smoothStep = (t) => {
  const k = THREE.MathUtils.clamp(t, 0, 1);
  return k * k * (3 - 2 * k);
};

// Quaternion looking along `direction` (+Z forward, like Unity's
// Quaternion.LookRotation).
const _looker = new THREE.Object3D();
function lookRotation(direction, out) {
  _looker.lookAt(direction.x, direction.y, direction.z);
  out.copy(_looker.quaternion);
  return out;
}

// Global clock shared by every update, mirroring Unity's Time.time /
// Time.deltaTime.
const time = { now: 0, delta: 0 };

// A minimal coroutine runner so the Unity coroutines port one to one.
// Generators yield once per frame.
class CoroutineRunner {
  constructor() {
    this.routines = new Set();
  }

  start(generator) {
    const routine = { generator, done: false };
    this.routines.add(routine);
    return routine;
  }

  stop(routine) {
    if (routine) {
      routine.done = true;
      this.routines.delete(routine);
    }
  }

  update() {
    for (const routine of [...this.routines]) {
      if (routine.done) continue;
      if (routine.generator.next().done) {
        routine.done = true;
        this.routines.delete(routine);
      }
    }
  }
}

const runner = new CoroutineRunner();

function* waitSeconds(seconds) {
  let elapsed = 0;
  while (elapsed < seconds) {
    elapsed += time.delta;
    yield;
  }
}

// ---------------------------------------------------------------------------
// Materials and creature building (CreatureBuilder).

function creatureMaterial(color, smoothness = 0.6) {
  return new THREE.MeshStandardMaterial({
    color,
    roughness: 1 - smoothness,
    metalness: 0,
  });
}

function transparentMaterial(color, opacity, extra = {}) {
  return new THREE.MeshStandardMaterial({
    color,
    transparent: true,
    opacity,
    roughness: 0.15,
    metalness: 0,
    depthWrite: false,
    ...extra,
  });
}

// Shared unit geometries matching Unity's primitives (sphere diameter 1,
// cube 1x1x1, cylinder height 2 radius 0.5, capsule height 2 radius 0.5);
// meshes are scaled non-uniformly just like Unity's localScale.
const GEO = {
  sphere: new THREE.SphereGeometry(0.5, 20, 14),
  box: new THREE.BoxGeometry(1, 1, 1),
  cylinder: new THREE.CylinderGeometry(0.5, 0.5, 2, 28),
  capsule: new THREE.CapsuleGeometry(0.5, 1, 6, 16),
};

function primitive(geometry, name, parent, position, scale, material) {
  const mesh = new THREE.Mesh(geometry, material);
  mesh.name = name;
  mesh.position.copy(position);
  mesh.scale.copy(scale);
  parent.add(mesh);
  return mesh;
}

// A little fish facing +Z: stretched sphere body, tail on a pivot and two
// dark eyes. The tail pivot is stored as `.userData.tailPivot`.
function buildFish(name, parent, length, bodyColor) {
  const root = new THREE.Group();
  root.name = name;
  parent.add(root);

  const body = creatureMaterial(bodyColor);
  const dark = creatureMaterial(bodyColor.clone().lerp(col(0, 0, 0), 0.65));
  const eye = creatureMaterial(col(0.08, 0.08, 0.1), 0.9);

  primitive(GEO.sphere, 'Body', root, new THREE.Vector3(),
    new THREE.Vector3(length * 0.36, length * 0.42, length), body);

  const tailPivot = new THREE.Group();
  tailPivot.name = 'TailPivot';
  tailPivot.position.set(0, 0, -length * 0.46);
  root.add(tailPivot);
  primitive(GEO.sphere, 'Tail', tailPivot,
    new THREE.Vector3(0, 0, -length * 0.22),
    new THREE.Vector3(length * 0.06, length * 0.34, length * 0.42), dark);

  primitive(GEO.sphere, 'DorsalFin', root,
    new THREE.Vector3(0, length * 0.2, -length * 0.1),
    new THREE.Vector3(length * 0.05, length * 0.22, length * 0.35), dark);

  const eyeScale = new THREE.Vector3().setScalar(length * 0.09);
  primitive(GEO.sphere, 'EyeL', root,
    new THREE.Vector3(-length * 0.15, length * 0.08, length * 0.36),
    eyeScale, eye);
  primitive(GEO.sphere, 'EyeR', root,
    new THREE.Vector3(length * 0.15, length * 0.08, length * 0.36),
    eyeScale, eye);

  root.userData.tailPivot = tailPivot;
  return root;
}

// A jellyfish: translucent pulsing bell plus swaying line tentacles.
function buildJellyfish(name, parent, bellRadius, tint, emission, tentacles) {
  const root = new THREE.Group();
  root.name = name;
  parent.add(root);

  const bellMaterial = transparentMaterial(tint, 0.4, {
    emissive: emission.clone(),
    emissiveIntensity: 1,
  });
  const bell = primitive(GEO.sphere, 'Bell', root, new THREE.Vector3(),
    new THREE.Vector3(bellRadius * 2, bellRadius * 1.3, bellRadius * 2),
    bellMaterial);

  const tentacleList = [];
  const tentacleMaterial = new THREE.LineBasicMaterial({
    color: tint,
    transparent: true,
    opacity: 0.45,
    depthWrite: false,
  });
  for (let i = 0; i < tentacles; i++) {
    const points = 6;
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position',
      new THREE.BufferAttribute(new Float32Array(points * 3), 3));
    const line = new THREE.Line(geometry, tentacleMaterial);
    line.name = `Tentacle${i}`;
    const angle = (i * Math.PI * 2) / tentacles;
    line.position.set(
      Math.cos(angle) * bellRadius * 0.55,
      -bellRadius * 0.4,
      Math.sin(angle) * bellRadius * 0.55);
    root.add(line);
    tentacleList.push(line);
  }

  root.userData.bell = bell;
  root.userData.tentacles = tentacleList;
  return root;
}

// ---------------------------------------------------------------------------
// Water surface (WaterSurface): a disc mesh riding three sine waves.

class WaterSurface {
  constructor(radius, segments = 28, rings = 10) {
    this.radius = radius;
    this.choppiness = 1;
    this.baseWaveHeight = 0.004;

    const vertexCount = 1 + segments * rings;
    this.baseVertices = new Float32Array(vertexCount * 3);
    const positions = new Float32Array(vertexCount * 3);
    for (let r = 0; r < rings; r++) {
      const distance = (radius * (r + 1)) / rings;
      for (let s = 0; s < segments; s++) {
        const angle = (s * Math.PI * 2) / segments;
        const index = (1 + r * segments + s) * 3;
        this.baseVertices[index] = Math.cos(angle) * distance;
        this.baseVertices[index + 2] = Math.sin(angle) * distance;
      }
    }
    positions.set(this.baseVertices);

    const triangles = [];
    for (let s = 0; s < segments; s++) {
      const next = (s + 1) % segments;
      triangles.push(0, 1 + s, 1 + next);
    }
    for (let r = 0; r < rings - 1; r++) {
      for (let s = 0; s < segments; s++) {
        const next = (s + 1) % segments;
        const a = 1 + r * segments + s;
        const b = 1 + r * segments + next;
        const c = 1 + (r + 1) * segments + s;
        const d = 1 + (r + 1) * segments + next;
        triangles.push(a, c, d, a, d, b);
      }
    }

    this.geometry = new THREE.BufferGeometry();
    this.geometry.setAttribute('position',
      new THREE.BufferAttribute(positions, 3));
    this.geometry.setIndex(triangles);
    this.geometry.computeVertexNormals();

    this.material = new THREE.MeshStandardMaterial({
      color: col(0.3, 0.66, 0.72),
      transparent: true,
      opacity: 0.5,
      roughness: 0.08,
      metalness: 0,
      depthWrite: false,
      side: THREE.DoubleSide,
    });
    this.mesh = new THREE.Mesh(this.geometry, this.material);
    this.mesh.name = 'Water';
    this.tintAlpha = 0.5;
  }

  update() {
    const t = time.now;
    const height = this.baseWaveHeight * this.choppiness;
    const positions = this.geometry.attributes.position;
    for (let i = 0; i < positions.count; i++) {
      const x = this.baseVertices[i * 3];
      const z = this.baseVertices[i * 3 + 2];
      const wave =
        Math.sin(t * 1.7 + x * 9) * 0.5 +
        Math.sin(t * 2.3 + z * 11) * 0.3 +
        Math.sin(t * 3.1 + (x + z) * 6) * 0.2;
      positions.setY(i, wave * height);
    }
    positions.needsUpdate = true;
    this.geometry.computeVertexNormals();
  }

  setTint(color, alpha) {
    this.material.color.copy(color);
    this.material.opacity = alpha;
    this.tintAlpha = alpha;
  }
}

// ---------------------------------------------------------------------------
// Bubbles: a light stand-in for the Unity particle system. Small points
// rise slowly from the pool floor and respawn at the bottom.

class Bubbles {
  constructor(parent, radius) {
    this.count = 60;
    this.radius = radius;
    this.positions = new Float32Array(this.count * 3);
    this.speeds = new Float32Array(this.count);
    for (let i = 0; i < this.count; i++) this.respawn(i, true);

    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position',
      new THREE.BufferAttribute(this.positions, 3));
    this.geometry = geometry;
    this.points = new THREE.Points(geometry, new THREE.PointsMaterial({
      color: 0xffffff,
      size: 0.008,
      transparent: true,
      opacity: 0.35,
      depthWrite: false,
      sizeAttenuation: true,
    }));
    this.points.name = 'Bubbles';
    parent.add(this.points);
  }

  respawn(i, randomHeight = false) {
    const p = insideUnitCircle().multiplyScalar(this.radius);
    this.positions[i * 3] = p.x;
    this.positions[i * 3 + 1] = randomHeight
      ? rand(0.03, WATER_LEVEL) : 0.03;
    this.positions[i * 3 + 2] = p.y;
    this.speeds[i] = rand(0.04, 0.09);
  }

  update() {
    for (let i = 0; i < this.count; i++) {
      this.positions[i * 3 + 1] += this.speeds[i] * time.delta;
      if (this.positions[i * 3 + 1] > WATER_LEVEL - 0.01) this.respawn(i);
    }
    this.geometry.attributes.position.needsUpdate = true;
  }
}

// ---------------------------------------------------------------------------
// Small fish school (SmallFishSchool).

const FishMode = { Hidden: 0, Roaming: 1, Nibbling: 2, Leaving: 3 };

const FISH_COLORS = [
  [0.85, 0.45, 0.2],   // warm orange
  [0.7, 0.72, 0.75],   // silver
  [0.55, 0.42, 0.3],   // sandy brown
  [0.9, 0.6, 0.35],    // light orange
];

class SmallFishSchool {
  constructor(parent, poolRadius, leftFoot, rightFoot) {
    this.fishCount = 9;
    this.poolRadius = poolRadius;
    this.swimHeightMin = 0.08;
    this.swimHeightMax = 0.24;
    this.swimSpeed = 0.35;
    this.nibbleDistance = 0.05;
    this.onNibbled = null;

    this.root = new THREE.Group();
    this.root.name = 'SmallFishSchool';
    parent.add(this.root);
    this.leftFoot = leftFoot;
    this.rightFoot = rightFoot;
    this.nibblingEnabled = false;

    this.fish = [];
    for (let i = 0; i < this.fishCount; i++) {
      const c = FISH_COLORS[i % FISH_COLORS.length];
      const length = rand(0.055, 0.085);
      const fishRoot = buildFish(`SmallFish${i}`, this.root, length,
        col(c[0], c[1], c[2]));
      fishRoot.visible = false;
      this.fish.push({
        root: fishRoot,
        tailPivot: fishRoot.userData.tailPivot,
        mode: FishMode.Hidden,
        target: new THREE.Vector3(),
        speed: this.swimSpeed * rand(0.8, 1.25),
        phase: Math.random() * 10,
        nibbleUntil: 0,
        retargetAt: 0,
      });
    }

    this._tmp = new THREE.Vector3();
    this._quat = new THREE.Quaternion();
  }

  enter() {
    this.nibblingEnabled = false;
    for (const f of this.fish) {
      const angle = Math.random() * Math.PI * 2;
      f.root.position.set(
        Math.cos(angle) * this.poolRadius,
        rand(this.swimHeightMin, this.swimHeightMax),
        Math.sin(angle) * this.poolRadius);
      f.root.visible = true;
      f.mode = FishMode.Roaming;
      f.target.copy(this.randomPoint(false));
      f.retargetAt = time.now + rand(1, 3);
    }
  }

  startNibbling() {
    this.nibblingEnabled = true;
  }

  leave() {
    this.nibblingEnabled = false;
    for (const f of this.fish) {
      if (f.mode === FishMode.Hidden) continue;
      const direction = f.root.position.clone();
      direction.y = 0;
      if (direction.lengthSq() < 0.001) direction.set(0, 0, 1);
      f.target.copy(direction.normalize()
        .multiplyScalar(this.poolRadius * 1.05));
      f.target.y = this.swimHeightMin;
      f.mode = FishMode.Leaving;
    }
  }

  hideImmediate() {
    this.nibblingEnabled = false;
    for (const f of this.fish) {
      f.root.visible = false;
      f.mode = FishMode.Hidden;
    }
  }

  randomPoint(nearFeet) {
    if (nearFeet && this.leftFoot && this.rightFoot) {
      const foot = Math.random() < 0.5 ? this.leftFoot : this.rightFoot;
      const local = this.root.worldToLocal(
        foot.getWorldPosition(new THREE.Vector3()));
      local.add(insideUnitSphere().multiplyScalar(0.07));
      local.y = THREE.MathUtils.clamp(local.y,
        this.swimHeightMin, this.swimHeightMax);
      return local;
    }
    const point = insideUnitCircle().multiplyScalar(this.poolRadius * 0.85);
    return new THREE.Vector3(point.x,
      rand(this.swimHeightMin, this.swimHeightMax), point.y);
  }

  isNearFoot(localPosition) {
    if (!this.leftFoot || !this.rightFoot) return null;
    const world = this.root.localToWorld(this._tmp.copy(localPosition));
    const toLeft = world.distanceTo(
      this.leftFoot.getWorldPosition(new THREE.Vector3()));
    const toRight = world.distanceTo(
      this.rightFoot.getWorldPosition(new THREE.Vector3()));
    if (Math.min(toLeft, toRight) >= 0.16) return null;
    return toLeft <= toRight ? Leg.Left : Leg.Right;
  }

  update() {
    const now = time.now;
    for (const f of this.fish) {
      if (f.mode === FishMode.Hidden) continue;

      // Tail wiggle keeps the primitives feeling alive.
      f.tailPivot.rotation.set(0, Math.sin(now * 9 + f.phase) * 28
        * THREE.MathUtils.DEG2RAD, 0);

      const position = f.root.position;
      const toTarget = this._tmp.copy(f.target).sub(position);
      const arrived = toTarget.length() < this.nibbleDistance;

      if (f.mode === FishMode.Leaving) {
        if (arrived) {
          f.root.visible = false;
          f.mode = FishMode.Hidden;
          continue;
        }
      } else if (f.mode === FishMode.Nibbling) {
        // Quick trembling darts right at the skin.
        position.x += Math.sin(now * 30 + f.phase) * 0.0015;
        position.y += Math.sin(now * 26 + f.phase * 2) * 0.0015;
        position.z += Math.cos(now * 28 + f.phase) * 0.0015;
        if (now >= f.nibbleUntil) {
          f.mode = FishMode.Roaming;
          f.target.copy(this.randomPoint(false));
          f.retargetAt = now + rand(0.5, 1.5);
        }
        continue;
      } else if (arrived || now >= f.retargetAt) {
        const leg = this.nibblingEnabled && arrived
          ? this.isNearFoot(position) : null;
        if (leg && Math.random() < 0.8) {
          f.mode = FishMode.Nibbling;
          f.nibbleUntil = now + rand(0.6, 1.4);
          this.onNibbled?.(leg);
          continue;
        }
        f.target.copy(this.randomPoint(
          this.nibblingEnabled && Math.random() < 0.75));
        f.retargetAt = now + rand(1.5, 4);
      }

      moveTowards(position, f.target, f.speed * time.delta);
      if (toTarget.lengthSq() > 0.0001) {
        lookRotation(toTarget.normalize(), this._quat);
        f.root.quaternion.slerp(this._quat, time.delta * 4);
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Big fish (BigFishController): circles the feet, lunges in for a strong
// bite, shakes, then retreats to the edge of the pool.

class BigFishController {
  constructor(parent, poolRadius, leftFoot, rightFoot) {
    this.bodyLength = 0.3;
    this.poolRadius = poolRadius;
    this.swimHeight = 0.16;
    this.circleSeconds = 7;
    this.lungeSeconds = 0.45;
    this.onBit = null;

    this.parent = parent;
    this.leftFoot = leftFoot;
    this.rightFoot = rightFoot;

    this.fishRoot = buildFish('BigFish', parent, this.bodyLength,
      col(0.42, 0.5, 0.58));
    this.fishRoot.visible = false;
    this.tailPivot = this.fishRoot.userData.tailPivot;
    this.choreography = null;

    this._quat = new THREE.Quaternion();
  }

  beginEncounter() {
    runner.stop(this.choreography);
    this.choreography = runner.start(this.encounter());
  }

  retreat() {
    if (this.choreography) {
      runner.stop(this.choreography);
      this.choreography = null;
    }
    if (this.fishRoot.visible) runner.start(this.swimOut());
  }

  hideImmediate() {
    runner.stop(this.choreography);
    this.choreography = null;
    this.fishRoot.visible = false;
  }

  // Gentle endless laps around the feet, never lining up a bite. Used by
  // the calm stage; retreat() or hideImmediate() ends it.
  swimCalm() {
    runner.stop(this.choreography);
    this.choreography = runner.start(this.calmSwim());
  }

  *calmSwim() {
    if (!this.fishRoot.visible) {
      this.fishRoot.visible = true;
      this.fishRoot.position.copy(
        this.angleToPosition(-Math.PI * 0.5, this.poolRadius));
    }
    const position = this.fishRoot.position;
    let angle = Math.atan2(position.z, position.x);
    const circleRadius = this.poolRadius * 0.6;
    while (true) {
      angle += (time.delta * Math.PI * 2) / 16;
      this.moveAlong(this.angleToPosition(angle, circleRadius), 4);
      yield;
    }
  }

  *encounter() {
    const firstLeg = Math.random() < 0.5 ? Leg.Left : Leg.Right;
    this.fishRoot.visible = true;

    // Slide in from the far edge of the pool.
    const startAngle = -Math.PI * 0.5;
    this.fishRoot.position.copy(
      this.angleToPosition(startAngle, this.poolRadius));

    // One slow curious circle around the feet.
    const circleRadius = this.poolRadius * 0.62;
    let elapsed = 0;
    while (elapsed < this.circleSeconds) {
      elapsed += time.delta;
      const angle = startAngle
        + (elapsed / this.circleSeconds) * Math.PI * 2.2;
      this.moveAlong(this.angleToPosition(angle, circleRadius), 6);
      yield;
    }

    yield* this.lunge(firstLeg);
    yield* waitSeconds(1.6);

    // A second, quicker pass at the other foot.
    yield* this.lunge(firstLeg === Leg.Left ? Leg.Right : Leg.Left);
    yield* waitSeconds(1);

    yield* this.swimOut();
    this.choreography = null;
  }

  *lunge(leg) {
    const foot = leg === Leg.Left ? this.leftFoot : this.rightFoot;
    if (!foot) return;
    const target = this.parent.worldToLocal(
      foot.getWorldPosition(new THREE.Vector3()));
    target.y = Math.max(0.06, target.y - 0.02);

    // Line up a bite length away, pause, then strike.
    const lineUp = target.clone().add(
      this.fishRoot.position.clone().sub(target).normalize()
        .multiplyScalar(this.bodyLength * 1.2));
    let settle = 0;
    while (settle < 1.2) {
      settle += time.delta;
      this.moveAlong(lineUp, 5);
      yield;
    }

    const start = this.fishRoot.position.clone();
    let strike = 0;
    while (strike < this.lungeSeconds) {
      strike += time.delta;
      const k = smoothStep(strike / this.lungeSeconds);
      this.fishRoot.position.lerpVectors(start, target, k);
      this.faceTowards(target, 20);
      yield;
    }

    this.onBit?.(leg);

    // Head shake while it holds the bite.
    let shake = 0;
    const baseRotation = this.fishRoot.quaternion.clone();
    const shakeEuler = new THREE.Euler();
    while (shake < 0.7) {
      shake += time.delta;
      shakeEuler.set(0,
        Math.sin(shake * 40) * 14 * THREE.MathUtils.DEG2RAD,
        Math.sin(shake * 33) * 8 * THREE.MathUtils.DEG2RAD);
      this.fishRoot.quaternion.copy(baseRotation)
        .multiply(this._quat.setFromEuler(shakeEuler));
      yield;
    }
    this.fishRoot.quaternion.copy(baseRotation);

    // Let go and back off a little.
    const backOff = this.fishRoot.position.clone().add(
      this.fishRoot.position.clone().sub(target).normalize()
        .multiplyScalar(0.12));
    backOff.y += 0.02;
    let release = 0;
    while (release < 0.8) {
      release += time.delta;
      this.moveAlong(backOff, 4);
      yield;
    }
  }

  *swimOut() {
    const direction = this.fishRoot.position.clone();
    direction.y = 0;
    if (direction.lengthSq() < 0.001) direction.set(0, 0, -1);
    const exit = direction.normalize()
      .multiplyScalar(this.poolRadius * 1.08);
    exit.y = this.swimHeight * 0.6;
    while (this.fishRoot.position.distanceTo(exit) > 0.03) {
      this.moveAlong(exit, 4);
      yield;
    }
    this.fishRoot.visible = false;
  }

  angleToPosition(angle, radius) {
    return new THREE.Vector3(Math.cos(angle) * radius, this.swimHeight,
      Math.sin(angle) * radius);
  }

  moveAlong(target, turnSpeed) {
    moveTowards(this.fishRoot.position, target, time.delta * 0.4);
    this.faceTowards(target, turnSpeed);
  }

  faceTowards(target, turnSpeed) {
    const direction = target.clone().sub(this.fishRoot.position);
    if (direction.lengthSq() < 0.00001) return;
    lookRotation(direction.normalize(), this._quat);
    this.fishRoot.quaternion.slerp(this._quat, time.delta * turnSpeed);
  }

  update() {
    if (this.fishRoot.visible) {
      this.tailPivot.rotation.set(0,
        Math.sin(time.now * 6) * 22 * THREE.MathUtils.DEG2RAD, 0);
    }
  }
}

// ---------------------------------------------------------------------------
// Jellyfish swarm (JellyfishSwarm): translucent jellyfish pulse and drift
// past the legs; brushing a foot flashes the bell and raises a sting.

class JellyfishSwarm {
  constructor(parent, poolRadius, leftFoot, rightFoot) {
    this.jellyCount = 3;
    this.poolRadius = poolRadius;
    this.driftSpeed = 0.09;
    this.stingDistance = 0.11;
    this.stingCooldown = 5;
    this.onStung = null;

    this.root = new THREE.Group();
    this.root.name = 'JellyfishSwarm';
    parent.add(this.root);
    this.leftFoot = leftFoot;
    this.rightFoot = rightFoot;
    this.entered = false;
    this.stingingEnabled = true;

    const tints = [
      col(0.85, 0.5, 0.9),
      col(0.55, 0.6, 0.95),
      col(0.95, 0.55, 0.7),
    ];
    const glows = [
      col(0.45, 0.12, 0.55),
      col(0.15, 0.2, 0.6),
      col(0.55, 0.15, 0.3),
    ];

    this.jellies = [];
    for (let i = 0; i < this.jellyCount; i++) {
      const radius = rand(0.05, 0.075);
      const jellyRoot = buildJellyfish(`Jellyfish${i}`, this.root, radius,
        tints[i % tints.length], glows[i % glows.length], 5);
      jellyRoot.visible = false;
      this.jellies.push({
        root: jellyRoot,
        bell: jellyRoot.userData.bell,
        baseBellScale: jellyRoot.userData.bell.scale.clone(),
        tentacles: jellyRoot.userData.tentacles,
        target: new THREE.Vector3(),
        phase: Math.random() * 10,
        nextStingAllowed: 0,
        flashUntil: 0,
        active: false,
      });
    }
  }

  enter() {
    this.entered = true;
    this.stingingEnabled = true;
    let index = 0;
    for (const jelly of this.jellies) {
      const angle = (index * Math.PI * 2) / this.jellies.length
        + Math.random();
      jelly.root.position.set(
        Math.cos(angle) * this.poolRadius,
        rand(0.12, 0.24),
        Math.sin(angle) * this.poolRadius);
      jelly.root.visible = true;
      jelly.active = true;
      jelly.target.copy(this.nextDriftTarget());
      jelly.nextStingAllowed = time.now + rand(2, 5);
      index++;
    }
  }

  // Keep drifting and pulsing around the legs but never sting.
  calmDrift() {
    if (!this.entered) this.enter();
    this.stingingEnabled = false;
  }

  leave() {
    this.entered = false;
    for (const jelly of this.jellies) {
      if (!jelly.active) continue;
      const direction = jelly.root.position.clone();
      direction.y = 0;
      if (direction.lengthSq() < 0.001) direction.set(0, 0, 1);
      jelly.target.copy(direction.normalize()
        .multiplyScalar(this.poolRadius * 1.05));
      jelly.target.y = 0.18;
    }
  }

  hideImmediate() {
    this.entered = false;
    for (const jelly of this.jellies) {
      jelly.root.visible = false;
      jelly.active = false;
    }
  }

  nextDriftTarget() {
    // Drift paths pass close to the legs more often than not.
    if (Math.random() < 0.6 && this.leftFoot && this.rightFoot) {
      const foot = Math.random() < 0.5 ? this.leftFoot : this.rightFoot;
      const local = this.root.worldToLocal(
        foot.getWorldPosition(new THREE.Vector3()));
      local.add(insideUnitSphere().multiplyScalar(0.08));
      local.y = THREE.MathUtils.clamp(local.y + 0.05, 0.1, 0.24);
      return local;
    }
    const point = insideUnitCircle().multiplyScalar(this.poolRadius * 0.8);
    return new THREE.Vector3(point.x, rand(0.12, 0.24), point.y);
  }

  update() {
    const now = time.now;
    for (const jelly of this.jellies) {
      if (!jelly.active) continue;

      // Bell pulse: slow contraction with a small upward push.
      const pulse = Math.sin(now * 2.2 + jelly.phase);
      jelly.bell.scale.set(
        jelly.baseBellScale.x * (1 - pulse * 0.08),
        jelly.baseBellScale.y * (1 + pulse * 0.16),
        jelly.baseBellScale.z * (1 - pulse * 0.08));

      this.drift(jelly, pulse);
      this.updateTentacles(jelly, now);
      this.updateFlash(jelly, now);
      this.trySting(jelly, now);
    }
  }

  drift(jelly, pulse) {
    const speed = this.driftSpeed * (0.7 + Math.max(0, pulse) * 0.8);
    const before = jelly.root.position.distanceTo(jelly.target);
    moveTowards(jelly.root.position, jelly.target, speed * time.delta);
    if (before < 0.03) {
      if (!this.entered) {
        jelly.root.visible = false;
        jelly.active = false;
        return;
      }
      jelly.target.copy(this.nextDriftTarget());
    }
  }

  updateTentacles(jelly, now) {
    for (const line of jelly.tentacles) {
      const positions = line.geometry.attributes.position;
      const count = positions.count;
      for (let p = 0; p < count; p++) {
        const t = p / (count - 1);
        positions.setXYZ(p,
          Math.sin(now * 1.6 + jelly.phase + t * 3) * 0.02 * t,
          -t * 0.12,
          Math.cos(now * 1.3 + jelly.phase + t * 2) * 0.02 * t);
      }
      positions.needsUpdate = true;
    }
  }

  updateFlash(jelly, now) {
    jelly.bell.material.emissiveIntensity = now < jelly.flashUntil
      ? THREE.MathUtils.lerp(4, 1, 1 - (jelly.flashUntil - now) / 0.4)
      : 1;
  }

  trySting(jelly, now) {
    if (!this.entered || !this.stingingEnabled) return;
    if (now < jelly.nextStingAllowed) return;
    if (!this.leftFoot || !this.rightFoot) return;
    const world = jelly.root.getWorldPosition(new THREE.Vector3());
    const toLeft = world.distanceTo(
      this.leftFoot.getWorldPosition(new THREE.Vector3()));
    const toRight = world.distanceTo(
      this.rightFoot.getWorldPosition(new THREE.Vector3()));
    if (Math.min(toLeft, toRight) > this.stingDistance) return;
    jelly.nextStingAllowed = now + this.stingCooldown + rand(0, 3);
    jelly.flashUntil = now + 0.4;
    this.onStung?.(toLeft <= toRight ? Leg.Left : Leg.Right);
  }
}

// ---------------------------------------------------------------------------
// Audio (AudioController): looping music plus positional one-shots.

class AudioController {
  constructor(listener, effectsWorldPosition, scene) {
    this.musicVolume = 0.55;
    this.effectsVolume = 0.9;
    this.listener = listener;
    this.lastNibbleTime = -10;
    this.buffers = {};
    this.fadeRoutine = null;

    this.music = new THREE.Audio(listener);
    this.music.setLoop(true);
    this.music.setVolume(0);

    // Effects sit at the pool so they pan with head movement while the
    // music stays non-spatial.
    this.effectsAnchor = new THREE.Object3D();
    this.effectsAnchor.position.copy(effectsWorldPosition);
    scene.add(this.effectsAnchor);
  }

  async load() {
    const loader = new THREE.AudioLoader();
    const files = {
      bgm: 'audio/drfish.mp3',
      nibble1: 'audio/drfish_fish_se1.mp3',
      nibble2: 'audio/drfish_fish_se2.mp3',
      bigFishBite: 'audio/drfish_big_se1.mp3',
      jellyfishSting: 'audio/drfish_jelly_se1.mp3',
      waterPop: 'audio/drfish_pop_se1.mp3',
    };
    await Promise.all(Object.entries(files).map(async ([key, url]) => {
      try {
        this.buffers[key] = await loader.loadAsync(url);
      } catch (error) {
        console.warn(`VR Doctor Fish: could not load ${url}`, error);
      }
    }));
    if (this.buffers.bgm) this.music.setBuffer(this.buffers.bgm);
  }

  startMusic(fadeSeconds = 2) {
    if (!this.buffers.bgm) return;
    if (!this.music.isPlaying) this.music.play();
    this.fadeMusic(this.musicVolume, fadeSeconds);
  }

  fadeOutMusic(fadeSeconds = 5) {
    this.fadeMusic(0, fadeSeconds, true);
  }

  stopMusic() {
    runner.stop(this.fadeRoutine);
    if (this.music.isPlaying) this.music.stop();
    this.music.setVolume(0);
  }

  fadeMusic(target, seconds, stopAtEnd = false) {
    runner.stop(this.fadeRoutine);
    const self = this;
    this.fadeRoutine = runner.start(function* () {
      const start = self.music.getVolume();
      let elapsed = 0;
      while (elapsed < seconds) {
        elapsed += time.delta;
        self.music.setVolume(THREE.MathUtils.lerp(start, target,
          Math.min(1, elapsed / seconds)));
        yield;
      }
      self.music.setVolume(target);
      if (stopAtEnd && self.music.isPlaying) self.music.stop();
    }());
  }

  playNibble() {
    // Small fish arrive in bursts; keep the soundscape from clipping.
    if (time.now - this.lastNibbleTime < 0.25) return;
    this.lastNibbleTime = time.now;
    const buffer = Math.random() < 0.5
      ? this.buffers.nibble1 : this.buffers.nibble2;
    this.playEffect(buffer, rand(0.92, 1.08), 0.8);
  }

  playBigFishBite() {
    this.playEffect(this.buffers.bigFishBite, 1, 1);
  }

  playJellyfishSting() {
    this.playEffect(this.buffers.jellyfishSting, rand(0.95, 1.05), 1);
  }

  playWaterPop() {
    this.playEffect(this.buffers.waterPop, 1, 0.7);
  }

  playEffect(buffer, pitch, volumeScale) {
    if (!buffer) return;
    const sound = new THREE.PositionalAudio(this.listener);
    sound.setBuffer(buffer);
    sound.setRefDistance(1);
    sound.setPlaybackRate(pitch);
    sound.setVolume(volumeScale * this.effectsVolume);
    this.effectsAnchor.add(sound);
    const anchor = this.effectsAnchor;
    sound.onEnded = function () {
      THREE.Audio.prototype.onEnded.call(this);
      anchor.remove(this);
    };
    sound.play();
  }
}

// ---------------------------------------------------------------------------
// Visual controller (VisualController): per-stage lighting, fog and water
// moods, blended over a couple of seconds.

const STAGE_LOOKS = {
  [Stage.Welcome]: {
    lightColor: [1, 0.96, 0.88], lightIntensity: 1,
    ambient: [0.5, 0.62, 0.68], fog: [0.16, 0.27, 0.32],
    water: [0.3, 0.66, 0.72], waterAlpha: 0.5, waterChoppiness: 1,
  },
  [Stage.SmallFish]: {
    lightColor: [1, 0.97, 0.9], lightIntensity: 1.05,
    ambient: [0.52, 0.64, 0.68], fog: [0.16, 0.28, 0.32],
    water: [0.32, 0.68, 0.7], waterAlpha: 0.48, waterChoppiness: 1.3,
  },
  [Stage.BigFish]: {
    lightColor: [0.9, 0.92, 1], lightIntensity: 0.9,
    ambient: [0.4, 0.5, 0.6], fog: [0.1, 0.2, 0.28],
    water: [0.24, 0.5, 0.62], waterAlpha: 0.55, waterChoppiness: 2,
  },
  [Stage.Jellyfish]: {
    lightColor: [0.75, 0.7, 1], lightIntensity: 0.65,
    ambient: [0.32, 0.28, 0.5], fog: [0.12, 0.08, 0.24],
    water: [0.4, 0.3, 0.62], waterAlpha: 0.55, waterChoppiness: 1.4,
  },
  [Stage.Calm]: {
    lightColor: [1, 0.9, 0.78], lightIntensity: 0.85,
    ambient: [0.55, 0.55, 0.55], fog: [0.2, 0.26, 0.3],
    water: [0.35, 0.7, 0.74], waterAlpha: 0.42, waterChoppiness: 0.45,
  },
};
STAGE_LOOKS[Stage.Finished] = STAGE_LOOKS[Stage.Calm];

class VisualController {
  constructor({ sun, ambient, fog, water, smallFish, bigFish, jellyfish }) {
    this.sun = sun;
    this.ambient = ambient;
    this.fog = fog;
    this.water = water;
    this.smallFish = smallFish;
    this.bigFish = bigFish;
    this.jellyfish = jellyfish;
    this.transitionSeconds = 2.5;
    this.transition = null;
  }

  applyStage(stage) {
    runner.stop(this.transition);
    this.transition = runner.start(this.blendTo(STAGE_LOOKS[stage]));

    switch (stage) {
      case Stage.SmallFish:
        this.smallFish.enter();
        this.smallFish.startNibbling();
        break;
      case Stage.BigFish:
        this.smallFish.leave();
        this.bigFish.beginEncounter();
        break;
      case Stage.Jellyfish:
        this.bigFish.retreat();
        this.jellyfish.enter();
        break;
      case Stage.Calm:
        // Every creature stays in the water, swimming gently with no
        // nibbles, bites or stings.
        this.smallFish.enter();
        this.bigFish.swimCalm();
        this.jellyfish.calmDrift();
        break;
      case Stage.Finished:
        // The creatures keep swimming until the session is restarted.
        break;
    }
  }

  applyLookImmediate(stage) {
    const look = STAGE_LOOKS[stage];
    this.sun.color = col(...look.lightColor);
    this.sun.intensity = look.lightIntensity;
    this.ambient.color = col(...look.ambient);
    this.fog.color.copy(col(...look.fog));
    this.water.setTint(col(...look.water), look.waterAlpha);
    this.water.choppiness = look.waterChoppiness;
  }

  *blendTo(look) {
    const startLight = this.sun.color.clone();
    const startIntensity = this.sun.intensity;
    const startAmbient = this.ambient.color.clone();
    const startFog = this.fog.color.clone();
    const startWater = this.water.material.color.clone();
    const startAlpha = this.water.tintAlpha;
    const startChop = this.water.choppiness;

    const endLight = col(...look.lightColor);
    const endAmbient = col(...look.ambient);
    const endFog = col(...look.fog);
    const endWater = col(...look.water);

    let elapsed = 0;
    while (elapsed < this.transitionSeconds) {
      elapsed += time.delta;
      const k = smoothStep(elapsed / this.transitionSeconds);
      this.sun.color.lerpColors(startLight, endLight, k);
      this.sun.intensity = THREE.MathUtils.lerp(startIntensity,
        look.lightIntensity, k);
      this.ambient.color.lerpColors(startAmbient, endAmbient, k);
      this.fog.color.lerpColors(startFog, endFog, k);
      this.water.setTint(
        new THREE.Color().lerpColors(startWater, endWater, k),
        THREE.MathUtils.lerp(startAlpha, look.waterAlpha, k));
      this.water.choppiness = THREE.MathUtils.lerp(startChop,
        look.waterChoppiness, k);
      yield;
    }
    this.transition = null;
  }
}

// ---------------------------------------------------------------------------
// Experience state manager (ExperienceStateManager).

const STAGE_LABELS = {
  [Stage.Welcome]: 'Stage 1/5 – Welcome: feet in the water',
  [Stage.SmallFish]: 'Stage 2/5 – Small fish nibbling',
  [Stage.BigFish]: 'Stage 3/5 – A big fish approaches…',
  [Stage.Jellyfish]: 'Stage 4/5 – Jellyfish drift past',
  [Stage.Calm]: 'Stage 5/5 – The water calms, the creatures swim on',
  [Stage.Finished]: 'Session complete – press R to run it again',
};

class ExperienceStateManager {
  constructor(visual, audio, onStageChanged) {
    this.welcomeSeconds = 12;
    this.smallFishSeconds = 45;
    this.bigFishSeconds = 22;
    this.jellyfishSeconds = 22;
    this.calmSeconds = 20;

    this.visual = visual;
    this.audio = audio;
    this.onStageChanged = onStageChanged;
    this.currentStage = Stage.Welcome;
    this.skipRequested = false;
    this.session = null;

    window.addEventListener('keydown', (event) => {
      if (!this.session) return;
      if (event.code === 'KeyN') this.skipRequested = true;
      if (event.code === 'KeyR') this.restart();
    });
  }

  start() {
    this.session = runner.start(this.runExperience());
  }

  restart() {
    runner.stop(this.session);
    this.skipRequested = false;
    this.audio.stopMusic();
    this.visual.smallFish.hideImmediate();
    this.visual.bigFish.hideImmediate();
    this.visual.jellyfish.hideImmediate();
    this.visual.applyLookImmediate(Stage.Welcome);
    this.start();
  }

  enterStage(stage) {
    this.currentStage = stage;
    this.visual.applyStage(stage);
    this.onStageChanged?.(stage);
    console.log(`VR Doctor Fish: stage -> ${stage}`);
  }

  *stageTimer(seconds) {
    let elapsed = 0;
    while (elapsed < seconds && !this.skipRequested) {
      elapsed += time.delta;
      yield;
    }
    this.skipRequested = false;
  }

  *runExperience() {
    yield;

    // 1. Welcome: feet enter the water, music begins.
    this.enterStage(Stage.Welcome);
    this.audio.startMusic(2);
    this.audio.playWaterPop();
    yield* this.stageTimer(this.welcomeSeconds);

    // 2. Small fish: ticklish nibbling at random points.
    this.enterStage(Stage.SmallFish);
    yield* this.stageTimer(this.smallFishSeconds);

    // 3. Big fish: strong one-shot bites driven by the choreography.
    this.enterStage(Stage.BigFish);
    yield* this.stageTimer(this.bigFishSeconds);

    // 4. Jellyfish: sharp electric stings driven by contact events.
    this.enterStage(Stage.Jellyfish);
    yield* this.stageTimer(this.jellyfishSeconds);

    // 5. Calm: the water settles, every creature swims gently without
    // touching the feet, and the music fades out.
    this.enterStage(Stage.Calm);
    yield* this.stageTimer(this.calmSeconds * 0.5);
    this.audio.fadeOutMusic(this.calmSeconds * 0.4);
    yield* this.stageTimer(this.calmSeconds * 0.5);

    this.enterStage(Stage.Finished);
    console.log('VR Doctor Fish: session complete. '
      + 'Press R to run the experience again.');
  }
}

// ---------------------------------------------------------------------------
// In-world status text: a canvas texture on a plane above the pool, so it
// is visible both on the desktop and inside VR.

class StatusText {
  constructor(scene) {
    this.canvas = document.createElement('canvas');
    this.canvas.width = 1024;
    this.canvas.height = 128;
    this.texture = new THREE.CanvasTexture(this.canvas);
    this.texture.colorSpace = THREE.SRGBColorSpace;
    const material = new THREE.MeshBasicMaterial({
      map: this.texture,
      transparent: true,
      depthWrite: false,
      fog: false,
    });
    this.mesh = new THREE.Mesh(new THREE.PlaneGeometry(1.15, 0.144), material);
    this.mesh.position.copy(POOL_CENTRE)
      .add(new THREE.Vector3(0, 0.5, -0.6));
    // Tilt up towards the seated viewer so the steep look-down desktop
    // view still reads it comfortably.
    this.mesh.lookAt(DESKTOP_EYE);
    scene.add(this.mesh);
    this.set('VR Doctor Fish');
  }

  set(text) {
    const ctx = this.canvas.getContext('2d');
    ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    if (text) {
      ctx.font = '600 72px "Segoe UI", system-ui, sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillStyle = 'rgba(235, 247, 255, 0.9)';
      ctx.fillText(text, this.canvas.width / 2, this.canvas.height / 2);
    }
    this.texture.needsUpdate = true;
  }
}

// ---------------------------------------------------------------------------
// Scene construction (DoctorFishBootstrap).

const scene = new THREE.Scene();
scene.background = col(0.06, 0.14, 0.18);
scene.fog = new THREE.FogExp2(col(0.16, 0.27, 0.32), 0.055);

const camera = new THREE.PerspectiveCamera(60,
  window.innerWidth / window.innerHeight, 0.05, 60);
const rig = new THREE.Group();
rig.name = 'XR Rig';
rig.add(camera);
scene.add(rig);
// Look down at the feet from a slight forward lean; in VR the headset
// pose replaces this.
camera.position.copy(DESKTOP_EYE);
camera.rotation.set(DESKTOP_PITCH_DEG * THREE.MathUtils.DEG2RAD, 0, 0);

const listener = new THREE.AudioListener();
camera.add(listener);

// Lighting.
const sun = new THREE.DirectionalLight(col(1, 0.96, 0.88), 1);
sun.position.set(1.2, 4.1, 2.6);
scene.add(sun);
scene.add(sun.target);
const ambient = new THREE.AmbientLight(col(0.5, 0.62, 0.68), 1);
scene.add(ambient);

// Environment: dark floor plus a soft round mat under the tub.
const floor = new THREE.Mesh(new THREE.PlaneGeometry(40, 40),
  creatureMaterial(col(0.14, 0.17, 0.21), 0.15));
floor.rotation.x = -Math.PI / 2;
scene.add(floor);

const mat = new THREE.Mesh(GEO.cylinder,
  creatureMaterial(col(0.24, 0.28, 0.3), 0.1));
mat.position.copy(POOL_CENTRE).add(new THREE.Vector3(0, 0.005, 0));
mat.scale.set(TUB_RADIUS * 2.8, 0.005, TUB_RADIUS * 2.8);
scene.add(mat);

// The wooden foot-spa tub.
const poolRoot = new THREE.Group();
poolRoot.name = 'Pool';
poolRoot.position.copy(POOL_CENTRE);
scene.add(poolRoot);

const wood = creatureMaterial(col(0.45, 0.31, 0.19), 0.35);
const darkWood = creatureMaterial(col(0.3, 0.2, 0.12), 0.3);

const tubBase = new THREE.Mesh(GEO.cylinder, darkWood);
tubBase.position.set(0, 0.02, 0);
tubBase.scale.set(TUB_RADIUS * 2, 0.02, TUB_RADIUS * 2);
poolRoot.add(tubBase);

const STAVES = 16;
const chord = (2 * Math.PI * TUB_RADIUS) / STAVES;
for (let i = 0; i < STAVES; i++) {
  const angle = (i * Math.PI * 2) / STAVES;
  const direction = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
  const stave = new THREE.Mesh(GEO.box, wood);
  stave.position.copy(direction).multiplyScalar(TUB_RADIUS)
    .add(new THREE.Vector3(0, TUB_WALL_HEIGHT * 0.5, 0));
  stave.scale.set(chord * 1.12, TUB_WALL_HEIGHT, 0.03);
  lookRotation(direction, stave.quaternion);
  poolRoot.add(stave);
}

// Animated water surface.
const water = new WaterSurface(TUB_RADIUS * 0.94);
water.mesh.position.set(0, WATER_LEVEL, 0);
poolRoot.add(water.mesh);

// Virtual legs: shin from the knee near the seat down into the water,
// foot resting on the tub floor with the toes forward.
const skin = creatureMaterial(col(0.87, 0.67, 0.53), 0.35);
function buildLeg(x) {
  const shin = new THREE.Mesh(GEO.capsule, skin);
  shin.position.set(x, 0.28, POOL_CENTRE.z + 0.14);
  shin.scale.set(0.09, 0.24, 0.09);
  shin.rotation.set(-24 * THREE.MathUtils.DEG2RAD, 0, 0);
  scene.add(shin);

  const foot = new THREE.Mesh(GEO.capsule, skin);
  foot.position.set(x, 0.06, POOL_CENTRE.z - 0.02);
  foot.scale.set(0.085, 0.1, 0.075);
  foot.rotation.set(-90 * THREE.MathUtils.DEG2RAD, 0, 0);
  scene.add(foot);
  return foot;
}
const leftFoot = buildLeg(-0.09);
const rightFoot = buildLeg(0.09);

const bubbles = new Bubbles(poolRoot, TUB_RADIUS * 0.75);

// Creatures.
const creatures = new THREE.Group();
creatures.name = 'Creatures';
poolRoot.add(creatures);

const smallFish = new SmallFishSchool(creatures, TUB_RADIUS * 0.85,
  leftFoot, rightFoot);
const bigFish = new BigFishController(creatures, TUB_RADIUS * 0.85,
  leftFoot, rightFoot);
const jellyfish = new JellyfishSwarm(creatures, TUB_RADIUS * 0.85,
  leftFoot, rightFoot);

const statusText = new StatusText(scene);

// Renderer.
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.xr.enabled = true;
renderer.xr.setReferenceSpaceType('local-floor');
document.body.appendChild(renderer.domElement);

window.addEventListener('resize', () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});

// ---------------------------------------------------------------------------
// Desktop look controls (DesktopCameraController): drag to look around
// from the seat. Mirrors the Unity right-mouse drag but accepts any
// button and touch so it works on phones too.

let yawDeg = 0;
let pitchDeg = DESKTOP_PITCH_DEG;
let dragging = false;
let lastPointer = { x: 0, y: 0 };
const LOOK_SENSITIVITY = 0.15;

renderer.domElement.addEventListener('contextmenu', (e) => e.preventDefault());
renderer.domElement.addEventListener('pointerdown', (event) => {
  dragging = true;
  lastPointer = { x: event.clientX, y: event.clientY };
  renderer.domElement.setPointerCapture(event.pointerId);
});
renderer.domElement.addEventListener('pointerup', () => { dragging = false; });
renderer.domElement.addEventListener('pointermove', (event) => {
  if (!dragging || renderer.xr.isPresenting) return;
  const dx = event.clientX - lastPointer.x;
  const dy = event.clientY - lastPointer.y;
  lastPointer = { x: event.clientX, y: event.clientY };
  yawDeg -= dx * LOOK_SENSITIVITY;
  pitchDeg = THREE.MathUtils.clamp(
    pitchDeg - dy * LOOK_SENSITIVITY, -85, 60);
  camera.rotation.set(pitchDeg * THREE.MathUtils.DEG2RAD,
    yawDeg * THREE.MathUtils.DEG2RAD, 0, 'YXZ');
});

// ---------------------------------------------------------------------------
// Wire up the controllers and the UI.

const audio = new AudioController(listener,
  POOL_CENTRE.clone().add(new THREE.Vector3(0, WATER_LEVEL, 0)), scene);

const visual = new VisualController({
  sun, ambient, fog: scene.fog, water, smallFish, bigFish, jellyfish,
});

smallFish.onNibbled = () => audio.playNibble();
bigFish.onBit = () => audio.playBigFishBite();
jellyfish.onStung = () => audio.playJellyfishSting();

const stageLabel = document.getElementById('stage-label');
const manager = new ExperienceStateManager(visual, audio, (stage) => {
  stageLabel.textContent = STAGE_LABELS[stage];
  if (stage === Stage.Welcome) statusText.set('VR Doctor Fish');
  else if (stage === Stage.Finished) statusText.set('Session complete');
  else statusText.set('');
});

const startOverlay = document.getElementById('start-overlay');
const startButton = document.getElementById('start-button');
const hud = document.getElementById('hud');
const vrButton = document.getElementById('vr-button');

startButton.addEventListener('click', async () => {
  startButton.disabled = true;
  startButton.textContent = 'Loading…';
  // Browsers require a user gesture before audio can play.
  if (listener.context.state === 'suspended') await listener.context.resume();
  await audio.load();
  startOverlay.style.display = 'none';
  hud.style.display = 'flex';
  manager.start();
});

// WebXR: show an Enter VR button when immersive VR is available (e.g. in
// the Meta Quest browser).
if (navigator.xr?.isSessionSupported) {
  navigator.xr.isSessionSupported('immersive-vr').then((supported) => {
    if (!supported) return;
    vrButton.style.display = 'block';
    vrButton.addEventListener('click', async () => {
      if (renderer.xr.isPresenting) {
        await renderer.xr.getSession().end();
        return;
      }
      try {
        const session = await navigator.xr.requestSession('immersive-vr', {
          optionalFeatures: ['local-floor'],
        });
        session.addEventListener('end', () => {
          vrButton.textContent = 'Enter VR';
        });
        await renderer.xr.setSession(session);
        vrButton.textContent = 'Exit VR';
        if (listener.context.state === 'suspended') {
          await listener.context.resume();
        }
      } catch (error) {
        console.warn('VR Doctor Fish: could not start VR session', error);
      }
    });
  }).catch(() => {});
}

// ---------------------------------------------------------------------------
// Main loop. setAnimationLoop is required for WebXR.

let previousTime = 0;
renderer.setAnimationLoop((timestamp) => {
  const seconds = timestamp / 1000;
  time.delta = Math.min(previousTime === 0 ? 0.016 : seconds - previousTime,
    0.1);
  time.now = seconds;
  previousTime = seconds;

  runner.update();
  water.update();
  bubbles.update();
  smallFish.update();
  bigFish.update();
  jellyfish.update();

  renderer.render(scene, camera);
});
