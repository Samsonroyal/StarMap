// Keplerian propagation in AU / degrees. Mirrors Data/Ephemeris.cs.
// Angles are degrees; time is days since J2000 (JD 2451545.0).

const DEG = Math.PI / 180;
const J2000JD = 2451545.0;
const AU = 149597870.7;

export function daysSinceJ2000(date) {
  return date.getTime() / 86400000 + 2440587.5 - J2000JD;
}

export function julianFromDate(date) {
  return date.getTime() / 86400000 + 2440587.5;
}

export function solveKepler(mRad, e) {
  let m = mRad;
  while (m > Math.PI) m -= 2 * Math.PI;
  while (m < -Math.PI) m += 2 * Math.PI;
  let E = e < 0.8 ? m : Math.PI;
  for (let i = 0; i < 40; i++) {
    const f = E - e * Math.sin(E) - m;
    const fp = 1 - e * Math.cos(E);
    const d = f / fp;
    E -= d;
    if (Math.abs(d) < 1e-10) break;
  }
  return E;
}

// Position (AU) relative to the body's parent. Returns [X, Y, Z] in the same
// ecliptic frame as the C# side (out-of-plane component along Z).
export function positionRelative(el, tDays, out) {
  const dt = tDays - el.epochDays;
  const a = el.a + el.adot * dt;
  const e = Math.min(0.999999, el.e + el.edot * dt);
  const i = (el.i + el.idot * dt) * DEG;
  const raan = (el.raan + el.raandot * dt) * DEG;
  const wp = (el.wp + el.wpdot * dt) * DEG;
  const m = (el.m0 + el.mdot * dt) * DEG;

  const eAnom = solveKepler(m, e);
  const nu = 2 * Math.atan2(
    Math.sqrt(1 + e) * Math.sin(eAnom / 2),
    Math.sqrt(1 - e) * Math.cos(eAnom / 2));
  const r = a * (1 - e * Math.cos(eAnom));

  const x = r * Math.cos(nu);
  const y = r * Math.sin(nu);

  const cr = Math.cos(raan); const sr = Math.sin(raan);
  const cw = Math.cos(wp); const sw = Math.sin(wp);
  const ci = Math.cos(i); const si = Math.sin(i);

  const X = (cr * cw - sr * sw * ci) * x + (-cr * sw - sr * cw * ci) * y;
  const Y = (sr * cw + cr * sw * ci) * x + (-sr * sw + cr * cw * ci) * y;
  const Z = (sw * si) * x + (cw * si) * y;

  out[0] = X; out[1] = Y; out[2] = Z;
  return out;
}

// Sample the orbit curve (parent-relative, AU) into the destination Float32Array.
export function sampleOrbit(el, points, dest) {
  const t = 0; // fixed to epoch for the drawn ellipse
  const dt = t - el.epochDays;
  const a = el.a + el.adot * dt;
  const e = Math.min(0.999999, el.e + el.edot * dt);
  const i = (el.i + el.idot * dt) * DEG;
  const raan = (el.raan + el.raandot * dt) * DEG;
  const wp = (el.wp + el.wpdot * dt) * DEG;

  const cr = Math.cos(raan); const sr = Math.sin(raan);
  const cw = Math.cos(wp); const sw = Math.sin(wp);
  const ci = Math.cos(i); const si = Math.sin(i);

  for (let s = 0; s < points; s++) {
    const M = (s / points) * 2 * Math.PI;
    const E = solveKepler(M, e);
    const nu = 2 * Math.atan2(
      Math.sqrt(1 + e) * Math.sin(E / 2),
      Math.sqrt(1 - e) * Math.cos(E / 2));
    const r = a * (1 - e * Math.cos(E));
    const x = r * Math.cos(nu);
    const y = r * Math.sin(nu);

    const X = (cr * cw - sr * sw * ci) * x + (-cr * sw - sr * cw * ci) * y;
    const Y = (sr * cw + cr * sw * ci) * x + (-sr * sw + cr * cw * ci) * y;
    const Z = (sw * si) * x + (cw * si) * y;

    dest[s * 3] = X;
    dest[s * 3 + 1] = Y;
    dest[s * 3 + 2] = Z;
  }
}

// Trails sample past positions for [now - trailDays, now].
export function sampleTrail(el, nowDays, trailDays, points, dest) {
  const position = [0, 0, 0];
  for (let s = 0; s < points; s++) {
    const t = nowDays - trailDays * (1 - s / (points - 1));
    positionRelative(el, t, position);
    dest[s * 3] = position[0];
    dest[s * 3 + 1] = position[1];
    dest[s * 3 + 2] = position[2];
  }
}

export function sceneUnitsToAU(units) { return units / 60; }
export const AU_TO_SCENE = 60;
