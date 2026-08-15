import { createScene } from './scene.js';

const host = window.chrome?.webview;

function post(msg) {
  if (host) {
    host.postMessage(msg);
  }
}

function log(level, msg) {
  post({ type: 'log', level, msg });
}

const container = document.getElementById('container');
const splash = document.getElementById('splash');
let splashHidden = false;

function hideSplash() {
  if (splashHidden) return;
  splashHidden = true;
  splash.classList.add('hidden');
  setTimeout(() => { splash.remove(); }, 600);
}

const scene = createScene({
  container,
  onFrame: ({ fps, timeDate }) => {
    hideSplash();
    post({ type: 'frame', timeIso: timeDate.toISOString(), fps });
  },
  onSelected: (id) => {
    post({ type: 'selected', id });
  },
  onFocus: (id) => {
    post({ type: 'focus', id });
  },
});

window.addEventListener('error', (e) => {
  log('error', `${e.message} @ ${e.filename}:${e.lineno} ${e.error?.stack || ''}`);
});

window.addEventListener('unhandledrejection', (e) => {
  log('error', String(e.reason));
});

function handle(msg) {
  try {
    switch (msg.type) {
      case 'init':
        if (msg.timeIso) scene.setTime(msg.timeIso);
        if (msg.speedSeconds) scene.setSpeed(msg.speedSeconds);
        if (msg.playing !== undefined) scene.setPlaying(msg.playing);
        if (Array.isArray(msg.bodies)) scene.setBodies(msg.bodies);
        break;
      case 'addBodies':
        if (Array.isArray(msg.bodies)) scene.setBodies(msg.bodies);
        break;
      case 'toggles':
        if (msg.data) scene.setToggles(msg.data);
        break;
      case 'focus':
        if (msg.id) scene.focus(msg.id);
        break;
      case 'setTime':
        if (msg.iso) scene.setTime(msg.iso);
        break;
      case 'setSpeed':
        if (msg.speedSeconds) scene.setSpeed(msg.speedSeconds);
        break;
      case 'play':
        if (msg.playing !== undefined) scene.setPlaying(msg.playing);
        break;
    }
  } catch (e) {
    log('error', `handler: ${e.message}`);
  }
}

if (host) {
  host.addEventListener('message', (ev) => {
    let msg = ev.data;
    if (typeof msg === 'string') {
      try { msg = JSON.parse(msg); } catch { return; }
    }
    if (msg && typeof msg === 'object') handle(msg);
  });
}

scene.start();
post({ type: 'ready' });
log('info', 'web renderer initialized');
