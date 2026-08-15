import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { CSS2DRenderer, CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import {
  positionRelative,
  sampleOrbit,
  sampleTrail,
  daysSinceJ2000,
  AU_TO_SCENE,
} from './ephemeris.js';

const AU = AU_TO_SCENE;

// Maps the ecliptic frame (X, Y, Z with Z out-of-plane) into the scene frame
// (ecliptic roughly horizontal, out-of-plane up): x = X, y = Z, z = -Y.
function toScene(v, out) {
  out.set(v[0], v[2], -v[1]);
  return out;
}

function isSmallKind(kind) {
  return kind === 'asteroid' || kind === 'comet' || kind === 'dwarf';
}

export function createScene({ container, onFrame, onSelected, onFocus }) {
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x020208);

  const camera = new THREE.PerspectiveCamera(60, 1, 0.005, 200000);
  camera.position.set(0, 150, 300);

  const renderer = new THREE.WebGLRenderer({
    antialias: true,
    logarithmicDepthBuffer: true,
  });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(container.clientWidth || window.innerWidth, container.clientHeight || window.innerHeight);
  container.appendChild(renderer.domElement);

  const css2d = new CSS2DRenderer();
  css2d.setSize(container.clientWidth || window.innerWidth, container.clientHeight || window.innerHeight);
  css2d.domElement.style.position = 'absolute';
  css2d.domElement.style.top = '0';
  css2d.domElement.style.left = '0';
  css2d.domElement.style.pointerEvents = 'none';
  container.appendChild(css2d.domElement);

  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;
  controls.minDistance = 0.02;
  controls.maxDistance = 40000;
  controls.zoomSpeed = 1.1;

  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---------------------------------------------------------------- lights
  scene.add(new THREE.AmbientLight(0x15151f, 0.6));
  const sunLight = new THREE.PointLight(0xfff2d8, 2.4, 0, 0);
  sunLight.position.set(0, 0, 0);
  scene.add(sunLight);

  const starField = buildStarfield();
  scene.add(starField);

  const belt = buildAsteroidBelt();
  scene.add(belt);

  // ---------------------------------------------------------------- state
  const records = new Map(); // id -> record
  const ZERO = new THREE.Vector3(0, 0, 0);
  let timeMs = Date.now();
  let speedSeconds = 86400;
  let playing = true;
  let followId = null;
  let flight = null;
  let toggles = {
    planets: true, moons: true, orbits: true, trails: true,
    labels: true, stars: true, belt: true, smallBodies: true,
  };

  // ---------------------------------------------------------------- builders
  function loadTexture(name) {
    if (!name) return null;
    const tex = new THREE.TextureLoader().load(`textures/${name}`);
    tex.colorSpace = THREE.SRGBColorSpace;
    return tex;
  }

  function hexColor(hex, fallback) {
    return new THREE.Color(hex || fallback);
  }

  function buildStarfield() {
    const n = 12000;
    const geo = new THREE.BufferGeometry();
    const pos = new Float32Array(n * 3);
    const col = new Float32Array(n * 3);
    const R = 50000;
    for (let i = 0; i < n; i++) {
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(2 * Math.random() - 1);
      const r = R * (0.8 + Math.random() * 0.2);
      pos[i * 3] = r * Math.sin(phi) * Math.cos(theta);
      pos[i * 3 + 1] = r * Math.cos(phi);
      pos[i * 3 + 2] = r * Math.sin(phi) * Math.sin(theta);
      const b = 0.5 + Math.random() * 0.5;
      const tint = Math.random() < 0.15 ? 0.9 : 1;
      col[i * 3] = b * tint;
      col[i * 3 + 1] = b;
      col[i * 3 + 2] = b * (Math.random() < 0.1 ? 0.8 : 1);
    }
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
    const mat = new THREE.PointsMaterial({
      size: 1.6,
      sizeAttenuation: false,
      vertexColors: true,
      transparent: true,
      opacity: 0.95,
      depthWrite: false,
    });
    return new THREE.Points(geo, mat);
  }

  function buildAsteroidBelt() {
    const n = 3800;
    const geo = new THREE.BufferGeometry();
    const pos = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) {
      const a = 2.28 + Math.random() * 1.05;
      const theta = Math.random() * Math.PI * 2;
      const jitter = (Math.random() - 0.5) * 0.14;
      pos[i * 3] = a * Math.cos(theta) * AU + (Math.random() - 0.5) * 0.6;
      pos[i * 3 + 1] = a * jitter * AU;
      pos[i * 3 + 2] = a * Math.sin(theta) * AU + (Math.random() - 0.5) * 0.6;
    }
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const mat = new THREE.PointsMaterial({
      color: 0x8a7a66,
      size: 0.5,
      sizeAttenuation: true,
      transparent: true,
      opacity: 0.65,
      depthWrite: false,
    });
    return new THREE.Points(geo, mat);
  }

  function makeLabel(text) {
    const div = document.createElement('div');
    div.className = 'label';
    div.textContent = text;
    if (reduceMotion) div.style.animation = 'none';
    return new CSS2DObject(div);
  }

  function buildSun(body) {
    const group = new THREE.Group();
    const R = body.visualRadius || 13;
    const tex = loadTexture(body.texture);
    const mat = tex
      ? new THREE.MeshBasicMaterial({ map: tex })
      : new THREE.MeshBasicMaterial({ color: 0xffcf5e });
    const mesh = new THREE.Mesh(new THREE.SphereGeometry(R, 48, 48), mat);
    group.add(mesh);

    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 256;
    const ctx = canvas.getContext('2d');
    const g = ctx.createRadialGradient(128, 128, 0, 128, 128, 128);
    g.addColorStop(0, 'rgba(255,220,140,0.9)');
    g.addColorStop(0.18, 'rgba(255,180,90,0.28)');
    g.addColorStop(1, 'rgba(255,150,60,0)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, 256, 256);
    const glowTex = new THREE.CanvasTexture(canvas);
    const glowMat = new THREE.SpriteMaterial({
      map: glowTex,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    const glow = new THREE.Sprite(glowMat);
    glow.scale.set(R * 4.6, R * 4.6, 1);
    group.add(glow);

    return { group, mesh };
  }

  function buildAtmosphere(body) {
    const R = Math.max(body.visualRadius || 1, 0.02);
    const a = body.atmosphere;
    const geo = new THREE.SphereGeometry(R * 1.035, 48, 48);
    const mat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      uniforms: {
        c: { value: new THREE.Color(a?.color || '#7ab7ff') },
        power: { value: a?.power ?? 3.5 },
        intensity: { value: a?.intensity ?? 0.9 },
      },
      vertexShader: `
        varying vec3 vNormal;
        void main() {
          vNormal = normalize(normalMatrix * normal);
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }`,
      fragmentShader: `
        uniform vec3 c; uniform float power; uniform float intensity;
        varying vec3 vNormal;
        void main() {
          float f = pow(0.72 - dot(vNormal, vec3(0.0, 0.0, 1.0)), power);
          gl_FragColor = vec4(c, 1.0) * f * intensity;
        }`,
    });
    return new THREE.Mesh(geo, mat);
  }

  function buildPlanetMesh(body) {
    const R = Math.max(body.visualRadius || 1, 0.02);
    const tex = loadTexture(body.texture);

    if (body.nightTexture) {
      const night = loadTexture(body.nightTexture);
      const mat = new THREE.ShaderMaterial({
        uniforms: {
          dayMap: { value: tex },
          nightMap: { value: night },
          sunDir: { value: new THREE.Vector3(1, 0, 0) },
          specColor: { value: new THREE.Color(body.specColor || '#ffffff') },
          specIntensity: { value: body.specular ? (body.specIntensity || 0.3) : 0 },
        },
        vertexShader: `
          varying vec2 vUv;
          varying vec3 vNormal;
          varying vec3 vWorldPos;
          void main() {
            vUv = uv;
            vec4 wp = modelMatrix * vec4(position, 1.0);
            vWorldPos = wp.xyz;
            vNormal = normalize(mat3(modelMatrix) * normal);
            gl_Position = projectionMatrix * viewMatrix * wp;
          }`,
        fragmentShader: `
          uniform sampler2D dayMap; uniform sampler2D nightMap;
          uniform vec3 sunDir; uniform vec3 specColor; uniform float specIntensity;
          varying vec2 vUv; varying vec3 vNormal; varying vec3 vWorldPos;
          void main() {
            vec3 n = normalize(vNormal);
            float sunAmt = max(dot(n, sunDir), 0.0);
            vec3 day = texture2D(dayMap, vUv).rgb;
            vec3 night = texture2D(nightMap, vUv).rgb * 1.6;
            vec3 color = mix(night, day, smoothstep(0.04, 0.42, sunAmt));
            vec3 viewDir = normalize(cameraPosition - vWorldPos);
            vec3 halfVec = normalize(viewDir + sunDir);
            float spec = specIntensity * pow(max(dot(n, halfVec), 0.0), 90.0);
            color += specColor * spec;
            gl_FragColor = vec4(color, 1.0);
          }`,
      });
      return new THREE.Mesh(new THREE.SphereGeometry(R, 48, 48), mat);
    }

    if (tex) {
      const mat = new THREE.MeshStandardMaterial({
        map: tex,
        roughness: 1,
        metalness: 0,
        emissive: 0x0a0a10,
      });
      return new THREE.Mesh(new THREE.SphereGeometry(R, 48, 48), mat);
    }

    const mat = new THREE.MeshStandardMaterial({
      color: hexColor(body.color, '#b9b4ab'),
      roughness: 0.95,
      metalness: 0,
    });
    return new THREE.Mesh(new THREE.SphereGeometry(R, 32, 32), mat);
  }

  function buildRing(body) {
    const R = Math.max(body.visualRadius || 1, 0.02);
    const inner = R * (body.ringInner || 1.3);
    const outer = R * (body.ringOuter || 2.2);
    const geo = new THREE.RingGeometry(inner, outer, 128, 1);
    const pos = geo.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i);
      const y = pos.getY(i);
      const r = Math.sqrt(x * x + y * y);
      pos.setXY(i, (r - inner) / (outer - inner), 0.5);
    }
    geo.attributes.uv.needsUpdate = true;
    const tex = loadTexture(body.ringTexture);
    const mat = new THREE.MeshBasicMaterial({
      map: tex,
      side: THREE.DoubleSide,
      transparent: true,
      alphaTest: 0.04,
      depthWrite: false,
    });
    return new THREE.Mesh(geo, mat);
  }

  function buildClouds(body) {
    const R = Math.max(body.visualRadius || 1, 0.02);
    const tex = loadTexture(body.cloudsTexture);
    if (!tex) return null;
    const mat = new THREE.MeshStandardMaterial({
      map: tex,
      transparent: true,
      opacity: 0.85,
      depthWrite: false,
      roughness: 1,
    });
    return new THREE.Mesh(new THREE.SphereGeometry(R * 1.012, 48, 48), mat);
  }

  // ---------------------------------------------------------------- add / update
  function addBody(body) {
    if (records.has(body.id)) return null;
    const isSun = body.kind === 'star';

    const mover = new THREE.Group();
    scene.add(mover);

    let mesh;
    if (isSun) {
      const built = buildSun(body);
      mover.add(built.group);
      mesh = built.mesh;
    } else {
      const tiltGroup = new THREE.Group();
      mesh = buildPlanetMesh(body);
      tiltGroup.add(mesh);
      if (body.ringTexture) {
        const ring = buildRing(body);
        ring.rotation.x = Math.PI / 2;
        tiltGroup.add(ring);
      }
      const clouds = buildClouds(body);
      if (clouds) tiltGroup.add(clouds);
      if (body.atmosphere) tiltGroup.add(buildAtmosphere(body));
      if (body.axialTiltDeg) tiltGroup.rotation.z = -body.axialTiltDeg * Math.PI / 180;
      mover.add(tiltGroup);
      mover.userData.spinMesh = mesh;
    }

    const label = makeLabel(body.name);
    label.position.y = (body.visualRadius || 1) + 1.0;
    mover.add(label);
    mesh.userData.bodyId = body.id;

    const rel = [0, 0, 0];
    let orbit = null;
    let trail = null;
    let trailArray = null;

    if (body.elements && body.elements.a) {
      const parentObj = body.parent === 'sun' ? scene : records.get(body.parent)?.mover;

      if (parentObj) {
        const pts = new Float32Array(256 * 3);
        sampleOrbit(body.elements, 256, pts);
        for (let i = 0; i < 256; i++) toScene(pts.subarray(i * 3, i * 3 + 3), tmpVec).multiplyScalar(AU).toArray(pts, i * 3);
        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.BufferAttribute(pts, 3));
        const mat = new THREE.LineBasicMaterial({
          color: 0x4a5a7a,
          transparent: true,
          opacity: isSmallKind(body.kind) ? 0.45 : 0.75,
        });
        orbit = new THREE.LineLoop(geo, mat);
        parentObj.add(orbit);
      }

      if (body.trailDays > 0) {
        const count = 220;
        trailArray = new Float32Array(count * 3);
        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.BufferAttribute(trailArray, 3));
        const mat = new THREE.LineBasicMaterial({
          color: body.kind === 'comet' ? 0x9fd0ff : 0x6a7a9a,
          transparent: true,
          opacity: 0.7,
          blending: THREE.AdditiveBlending,
          depthWrite: false,
        });
        trail = new THREE.Line(geo, mat);
        trail.frustumCulled = false;
        if (parentObj) parentObj.add(trail);
        else scene.add(trail);
      }
    }

    const rec = {
      info: body, mover, mesh, label, orbit, trail, trailArray, rel, isSmall: isSmallKind(body.kind),
    };
    records.set(body.id, rec);
    return rec;
  }

  const tmpVec = new THREE.Vector3();

  function updatePosition(rec, nowDays, out) {
    const b = rec.info;
    if (!b.elements) { out.set(0, 0, 0); return out; }
    positionRelative(b.elements, nowDays, rec.rel);
    if (b.parent === 'sun') return toScene(rec.rel, out);
    const parent = records.get(b.parent);
    if (parent) {
      updatePosition(parent, nowDays, out);
      return out.add(toScene(rec.rel, tmpVec));
    }
    return toScene(rec.rel, out);
  }

  function updateTrail(rec, nowDays) {
    if (!rec.trail || !rec.trailArray || !rec.info.elements) return;
    sampleTrail(rec.info.elements, nowDays, rec.info.trailDays, 220, rec.trailArray);
    const arr = rec.trailArray;
    for (let i = 0; i < 220; i++) {
      const X = arr[i * 3], Y = arr[i * 3 + 1], Z = arr[i * 3 + 2];
      arr[i * 3] = X * AU;
      arr[i * 3 + 1] = Z * AU;
      arr[i * 3 + 2] = -Y * AU;
    }
    rec.trail.geometry.attributes.position.needsUpdate = true;
  }

  // ---------------------------------------------------------------- visibility
  function categoryVisible(kind) {
    if (kind === 'star') return true;
    if (kind === 'planet') return toggles.planets;
    if (kind === 'moon') return toggles.moons;
    return toggles.smallBodies;
  }

  function applyVisibility() {
    for (const rec of records.values()) {
      const show = categoryVisible(rec.info.kind);
      rec.mover.visible = show;
      if (rec.orbit) rec.orbit.visible = show && toggles.orbits;
      if (rec.trail) rec.trail.visible = show && toggles.trails;
      rec.label.visible = show && toggles.labels;
    }
    starField.visible = toggles.stars;
    belt.visible = toggles.belt;
  }

  // ---------------------------------------------------------------- picking
  const raycaster = new THREE.Raycaster();
  const pointer = new THREE.Vector2();

  function pickMesh(event) {
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
    raycaster.setFromCamera(pointer, camera);
    const meshes = [];
    for (const rec of records.values()) {
      if (rec.mover.visible && rec.mesh) meshes.push(rec.mesh);
    }
    const hits = raycaster.intersectObjects(meshes, false);
    if (!hits.length) return null;
    return records.get(hits[0].object.userData.bodyId) || null;
  }

  let hovered = null;
  renderer.domElement.addEventListener('pointermove', (e) => {
    const rec = pickMesh(e);
    if (rec !== hovered) {
      if (hovered) hovered.label.element.classList.remove('hover');
      hovered = rec;
      if (hovered) hovered.label.element.classList.add('hover');
    }
    renderer.domElement.style.cursor = rec ? 'pointer' : 'default';
  });

  renderer.domElement.addEventListener('pointerleave', () => {
    if (hovered) hovered.label.element.classList.remove('hover');
    hovered = null;
    renderer.domElement.style.cursor = 'default';
  });

  // ---------------------------------------------------------------- fly-to
  function easeInOutCubic(x) {
    return x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2;
  }

  function distFor(rec) {
    const radius = Math.max(rec.info.visualRadius || 0.5, 0.02);
    const fovRad = camera.fov * Math.PI / 180;
    return Math.max(radius / Math.tan(fovRad / 2) / 0.42, radius * 2.2);
  }

  function flyTo(id, dur = 1.0) {
    const rec = records.get(id);
    if (!rec || !rec.mesh || !rec.helio) return;
    const bodyPos = rec.helio;
    const dist = distFor(rec);
    const dir = new THREE.Vector3().subVectors(bodyPos, camera.position);
    if (dir.lengthSq() < 1e-8) dir.set(0, 0, -1);
    dir.normalize();
    flight = {
      t: 0,
      dur,
      fromPos: camera.position.clone(),
      fromTarget: controls.target.clone(),
      toPos: bodyPos.clone().addScaledVector(dir, dist),
      toTarget: bodyPos.clone(),
    };
    followId = id;
    controls.enabled = false;
  }

  function updateFlight(dtSec) {
    if (!flight) return;
    flight.t += dtSec / flight.dur;
    const k = Math.min(flight.t, 1);
    const e = easeInOutCubic(k);
    camera.position.lerpVectors(flight.fromPos, flight.toPos, e);
    controls.target.lerpVectors(flight.fromTarget, flight.toTarget, e);
    if (k >= 1) {
      flight = null;
      controls.enabled = true;
    }
  }

  renderer.domElement.addEventListener('click', (e) => {
    const rec = pickMesh(e);
    if (rec) flyTo(rec.info.id);
    onSelected?.(rec ? rec.info.id : null);
  });

  renderer.domElement.addEventListener('dblclick', (e) => {
    const rec = pickMesh(e);
    if (rec) {
      flyTo(rec.info.id);
      onFocus?.(rec.info.id);
    }
  });

  renderer.domElement.addEventListener('wheel', (e) => {
    if (e.deltaY > 0) return;
    const rec = pickMesh(e);
    if (!rec) return;
    const dist = camera.position.distanceTo(rec.helio || ZERO);
    if (followId === rec.info.id && dist < distFor(rec) * 1.5) return;
    flyTo(rec.info.id);
  }, { passive: true });

  renderer.domElement.addEventListener('contextmenu', () => {
    followId = null;
  });

  // ---------------------------------------------------------------- api
  function setBodies(bodies) {
    for (const b of bodies) addBody(b);
    applyVisibility();
  }

  function setToggles(next) {
    Object.assign(toggles, next);
    applyVisibility();
  }

  function setTime(iso) {
    const t = new Date(iso);
    if (!isNaN(t.getTime())) timeMs = t.getTime();
  }

  function setSpeed(s) { speedSeconds = s; }
  function setPlaying(p) { playing = p; }
  function focus(id) {
    if (!records.has(id)) return;
    followId = id;
    if (flight) return;
    flyTo(id);
  }

  function resize() {
    const w = container.clientWidth || window.innerWidth;
    const h = container.clientHeight || window.innerHeight;
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    renderer.setSize(w, h);
    css2d.setSize(w, h);
  }

  // ---------------------------------------------------------------- loop
  let lastFrameMs = performance.now();
  let frameCount = 0;
  let frameTimeAcc = 0;

  function frame() {
    const nowMs = performance.now();
    const dtSec = Math.min((nowMs - lastFrameMs) / 1000, 0.25);
    lastFrameMs = nowMs;
    if (playing) timeMs += dtSec * 1000 * speedSeconds;
    const nowDate = new Date(timeMs);
    const nowDays = daysSinceJ2000(nowDate);

    for (const rec of records.values()) {
      const b = rec.info;
      if (!rec.helio) rec.helio = new THREE.Vector3();
      if (b.elements) updatePosition(rec, nowDays, rec.helio);
      else rec.helio.set(0, 0, 0);
      rec.mover.position.copy(rec.helio);
      updateTrail(rec, nowDays);

      if (b.rotationHours && Math.abs(b.rotationHours) > 0.001 && rec.mover.userData.spinMesh) {
        const spin = rec.mover.userData.spinMesh;
        const rotPerDay = (2 * Math.PI) / Math.abs(b.rotationHours);
        spin.rotation.y += rotPerDay * (dtSec * speedSeconds / 86400) * Math.sign(b.rotationHours);
      }
    }

    // Sun direction for the day/night shader, and follow target.
    for (const rec of records.values()) {
      const m = rec.mesh;
      if (m?.material?.uniforms?.sunDir) {
        m.material.uniforms.sunDir.value.copy(rec.helio).multiplyScalar(-1).normalize();
      }
    }

    if (flight) {
      updateFlight(dtSec);
    } else if (followId && records.has(followId)) {
      controls.target.lerp(records.get(followId).helio, 0.08);
    } else if (!controls.target.equals(ZERO)) {
      controls.target.lerp(ZERO, 0.06);
    }

    controls.update();
    renderer.render(scene, camera);
    css2d.render(scene, camera);

    frameCount++;
    frameTimeAcc += dtSec;
    if (frameTimeAcc >= 0.5) {
      const fps = frameCount / frameTimeAcc;
      frameCount = 0;
      frameTimeAcc = 0;
      onFrame?.({ fps, timeDate: nowDate });
    }
  }

  function loop() {
    requestAnimationFrame(loop);
    frame();
  }

  window.addEventListener('resize', resize);
  resize();

  return {
    start: loop,
    setBodies,
    setToggles,
    setTime,
    setSpeed,
    setPlaying,
    focus,
    resize,
  };
}
