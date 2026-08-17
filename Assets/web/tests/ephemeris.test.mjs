import assert from 'node:assert/strict';
import { positionRelative, sampleOrbit, sampleTrail } from '../src/ephemeris.js';

const close = (actual, expected, tolerance = 1e-9) => {
  assert.ok(Math.abs(actual - expected) <= tolerance,
    `expected ${actual} to be within ${tolerance} of ${expected}`);
};

const circular = {
  epochDays: 0,
  a: 1, adot: 0,
  e: 0, edot: 0,
  i: 0, idot: 0,
  raan: 0, raandot: 0,
  wp: 0, wpdot: 0,
  m0: 0, mdot: 1,
};

const p = [0, 0, 0];
positionRelative(circular, 0, p);
close(p[0], 1);
close(p[1], 0);
close(p[2], 0);

positionRelative(circular, 90, p);
close(p[0], 0, 1e-8);
close(p[1], 1, 1e-8);

const eccentric = { ...circular, a: 2, e: 0.25, mdot: 0 };
positionRelative(eccentric, 0, p);
close(Math.hypot(...p), 1.5, 1e-9);

const orbit = new Float32Array(4 * 3);
sampleOrbit(circular, 4, orbit);
close(orbit[0], 1);
close(orbit[4], 1);
close(orbit[6], -1);
close(orbit[10], -1);

const drifting = { ...circular, adot: 0.001, edot: 0.00001, mdot: 3 };
const trail = new Float32Array(8 * 3);
sampleTrail(drifting, 20, 5, 8, trail);
positionRelative(drifting, 20, p);
close(trail.at(-3), p[0], 1e-6);
close(trail.at(-2), p[1], 1e-6);
close(trail.at(-1), p[2], 1e-6);

console.log('ephemeris tests passed');
