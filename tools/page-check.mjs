// Drives yt-tweaks.js in a headless Edge (throwaway profile, YtPlayer's narrow width) through the
// flows the host relies on, with a stub in place of the WebView2 host bridge.
// Usage: node tools/page-check.mjs [yt-tweaks.js] [profile dir]   (Windows: needs Edge; Node 22+)
// Defaults: the script in src/, and a fresh profile in the temp folder. Exits 1 on any FAIL.
import { spawn } from 'node:child_process';
import { mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const tweaksPath = process.argv[2] ?? join(here, '..', 'src', 'YtPlayer', 'Scripts', 'yt-tweaks.js');
const profileDir = process.argv[3] ?? mkdtempSync(join(tmpdir(), 'ytplayer-page-check-'));
const port = 9333;
const edge = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const proc = spawn(edge, [
  '--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`,
  '--window-size=556,460', '--autoplay-policy=no-user-gesture-required', '--mute-audio', '--no-first-run',
  '--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0',
  'about:blank'], { stdio: 'ignore' });

const sleep = ms => new Promise(r => setTimeout(r, ms));
let target;
for (let i = 0; i < 50 && !target; i++) {
  try { target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page'); }
  catch { await sleep(200); }
}
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise(r => ws.addEventListener('open', r, { once: true }));
let nextId = 1; const pending = new Map(); const waiters = [];
ws.addEventListener('message', ev => {
  const msg = JSON.parse(ev.data);
  if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); }
  else if (msg.method) for (const w of [...waiters]) if (w.method === msg.method) { waiters.splice(waiters.indexOf(w), 1); w.resolve(msg); }
});
const send = (method, params = {}) => new Promise(r => { const id = nextId++; pending.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
const waitFor = (method, ms = 20000) => Promise.race([new Promise(resolve => waiters.push({ method, resolve })), sleep(ms)]);
const evaluate = async expr => (await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true })).result?.result?.value;
const key = async (type, k, code, vk, modifiers = 0, text) =>
  send('Input.dispatchKeyEvent', { type, key: k, code, windowsVirtualKeyCode: vk, modifiers, ...(text ? { text } : {}) });
const press = async (k, code, vk, modifiers = 0, text) => { await key(text ? 'keyDown' : 'rawKeyDown', k, code, vk, modifiers, text); await key('keyUp', k, code, vk, modifiers); };

let failures = 0;
const check = (name, ok, detail) => { if (!ok) failures++; console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail !== undefined ? '  ' + JSON.stringify(detail) : ''}`); };
const state = () => evaluate(`JSON.stringify({ path: location.pathname, compact: document.documentElement.classList.contains('ytpip-compact'),
  focusedSearch: document.activeElement === document.querySelector('input[name="search_query"]'),
  posted: window.__posted, mini: (() => { const m = document.querySelector('ytd-miniplayer'); return m ? getComputedStyle(m).display : null; })(),
  miniSelectors: ['ytd-app[miniplayer-is-active]', 'ytd-miniplayer #movie_player', 'ytd-miniplayer video'].filter(s => document.querySelector(s)),
  video: (() => { const v = document.querySelector('ytd-miniplayer video') || document.querySelector('#movie_player video'); return v ? { paused: v.paused, t: Math.round(v.currentTime) } : null; })() })`).then(JSON.parse);

await send('Page.enable');
await send('Runtime.enable');
const stub = `window.__posted = []; window.__hostListeners = [];
  window.chrome = window.chrome || {};
  window.chrome.webview = { postMessage: m => window.__posted.push(m), addEventListener: (t, f) => window.__hostListeners.push(f) };
  window.__host = d => window.__hostListeners.forEach(f => f({ data: d }));`;
await send('Page.addScriptToEvaluateOnNewDocument', { source: stub });
await send('Page.addScriptToEvaluateOnNewDocument', { source: readFileSync(tweaksPath, 'utf8') });

// 1. Home with compact on: the page shows as usual.
send('Page.navigate', { url: 'https://www.youtube.com/' });
await waitFor('Page.loadEventFired'); await sleep(3000);
await evaluate(`window.__host({ compact: true })`); await sleep(300);
let s = await state();
check('home: compact on but not applied', s.path === '/' && !s.compact, { path: s.path, compact: s.compact });
check('home: answers the compact state with showing:false', s.posted.some(m => m.showing === false), s.posted);

// 2. Ctrl+F opens YouTube's search box (narrow width: through its search button).
await press('f', 'KeyF', 70, 2); await sleep(800);
s = await state();
check('home: Ctrl+F focuses the search box', s.focusedSearch);

// 3. A search, then a result plays compact.
await send('Input.insertText', { text: 'depeche mode ghosts again' });
await press('Enter', 'Enter', 13, 0, '\r'); await sleep(5000);
s = await state();
check('search: results page', s.path === '/results', s.path);
const clicked = await evaluate(`(() => { const a = document.querySelector('ytd-video-renderer a#thumbnail[href^="/watch"]'); if (!a) return null; a.click(); return a.getAttribute('href'); })()`);
await sleep(6000);
s = await state();
check('result: opens its watch page', s.path === '/watch', { clicked, path: s.path });
check('result: compact applies on the watch page', s.compact);
check('result: reports showing:true', s.posted.some(m => m.showing === true), s.posted);
check('result: video plays', s.video && !s.video.paused, s.video);

// 3a. Play state for the strip's button: the page reports it, and the host's toggle pauses the player.
check('result: reports playing:true', s.posted.some(m => m.playing === true), s.posted.filter(m => 'playing' in m));
await evaluate(`window.__posted.length = 0; window.__host({ togglePlay: true })`); await sleep(500);
s = await state();
check('togglePlay pauses the player and reports playing:false', s.video?.paused === true && s.posted.some(m => m.playing === false), { video: s.video, posted: s.posted });
await evaluate(`window.__posted.length = 0; window.__host({ togglePlay: true })`); await sleep(800);
s = await state();
check('togglePlay again resumes it', s.video?.paused === false && s.posted.some(m => m.playing === true), { video: s.video, posted: s.posted });

// 3b. Ctrl+C with nothing selected copies the video's link; Ctrl+Shift+C, the link at the current time.
const videoId = await evaluate(`new URLSearchParams(location.search).get('v')`);
await evaluate(`window.getSelection().removeAllRanges(); window.__posted.length = 0`);
await press('c', 'KeyC', 67, 2); await sleep(300);
s = await state();
check('Ctrl+C copies the video link', s.posted.some(m => m.copy === `https://youtu.be/${videoId}`), s.posted);
check('Ctrl+C shows the notice', await evaluate(`document.getElementById('ytpip-toast')?.classList.contains('ytpip-toast-on') === true`));
// The link drops a time of 0, so let the video get going first.
for (let i = 0; i < 20 && !(await evaluate(`(document.querySelector('#movie_player video')?.currentTime ?? 0) >= 1`)); i++) await sleep(250);
await evaluate(`window.__posted.length = 0`);
await press('C', 'KeyC', 67, 2 | 8); await sleep(300);
s = await state();
check('Ctrl+Shift+C copies the link at the current time', s.posted.some(m => typeof m.copy === 'string' && m.copy.startsWith(`https://youtu.be/${videoId}?t=`) && /[?]t=[1-9][0-9]*$/.test(m.copy)), s.posted);
await evaluate(`window.__posted.length = 0; (() => { const t = document.createElement('p'); t.id = 'check-selection'; t.textContent = 'some text to copy'; t.style.cssText = 'position:fixed;top:0;left:0;z-index:2147483647;background:#fff;color:#000'; document.body.appendChild(t); const r = document.createRange(); r.selectNodeContents(t); const sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(r); })()`);
await press('c', 'KeyC', 67, 2); await sleep(300);
s = await state();
check('Ctrl+C with a selection stays a copy', !s.posted.some(m => m.copy), s.posted);
await evaluate(`window.getSelection().removeAllRanges(); document.getElementById('check-selection')?.remove()`);

// 3c. An ad's Skip button: the page reports where it is, for the host to click (a trusted click; the
// page's own would be ignored). A stand-in button during a stand-in ad, as real ads come when they like.
await evaluate(`window.__posted.length = 0; (() => { document.getElementById('movie_player').classList.add('ad-showing');
  const b = document.createElement('button'); b.className = 'ytp-skip-ad-button'; b.id = 'check-skip'; b.textContent = 'Skip';
  b.style.cssText = 'position:fixed;right:20px;bottom:60px;width:90px;height:36px;z-index:2147483647';
  document.body.appendChild(b); })()`);
await sleep(1200);
s = await state();
const skipAt = s.posted.find(m => m.skipAt)?.skipAt;
check("an ad's Skip button: the page reports where it is", !!skipAt && skipAt.x > 0 && skipAt.y > 0, s.posted);
check('an ad: the page reports its start', s.posted.some(m => m.ad === true), s.posted);
await evaluate(`window.__posted.length = 0; document.getElementById('movie_player').classList.remove('ad-showing'); document.getElementById('check-skip')?.remove()`);
await sleep(800);
s = await state();
check('an ad: the page reports its end', s.posted.some(m => m.ad === false), s.posted);

// 4. Ctrl+F in compact lifts it for the search box; Escape puts it back.
await evaluate(`window.__posted.length = 0`);
await press('f', 'KeyF', 70, 2); await sleep(800);
s = await state();
check('watch: Ctrl+F lifts compact and focuses search', !s.compact && s.focusedSearch, { compact: s.compact, focused: s.focusedSearch });
check('watch: Ctrl+F reports showing:false', s.posted.some(m => m.showing === false), s.posted);
await press('Escape', 'Escape', 27); await sleep(500);
s = await state();
check('watch: Escape restores compact', s.compact);
check('watch: Escape reports showing:true', s.posted.at(-1)?.showing === true, s.posted);

// 5. Paste outside a text field goes to the host; inside the search box it does not.
const pasteJs = target => `(() => { const dt = new DataTransfer(); dt.setData('text/plain', 'https://youtu.be/dQw4w9WgXcQ');
  ${target}.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true })); })()`;
await evaluate(`document.activeElement && document.activeElement.blur(); window.__posted.length = 0`);
await evaluate(pasteJs('document.body'));
s = await state();
check('paste outside a text field is forwarded', s.posted.some(m => m.paste === 'https://youtu.be/dQw4w9WgXcQ'), s.posted);
await evaluate(`window.__posted.length = 0; document.querySelector('input[name="search_query"]').focus()`);
await evaluate(pasteJs(`document.querySelector('input[name="search_query"]')`));
s = await state();
check('paste in the search box stays a paste', !s.posted.some(m => m.paste), s.posted);
await evaluate(`document.activeElement.blur()`);

// 6. Leaving the video: YouTube's miniplayer is hidden and the video plays on.
await evaluate(`history.back()`); await sleep(4000);
s = await state();
check('left the video: back on results', s.path === '/results', s.path);
check('left the video: miniplayer hidden', s.mini === 'none', { mini: s.mini, selectors: s.miniSelectors });
check('left the video: it plays on', s.video && !s.video.paused, { video: s.video, selectors: s.miniSelectors });

// 7. Compact turned on again away from the video brings it back.
await evaluate(`window.__host({ compact: false })`); await sleep(300);
await evaluate(`window.__host({ compact: true })`); await sleep(4000);
s = await state();
check('compact on with a background video: back to it', s.path === '/watch' && s.compact, { path: s.path, compact: s.compact, video: s.video });

ws.close(); proc.kill();
console.log(failures ? `${failures} FAILED` : 'ALL PASSED');
process.exit(failures ? 1 : 0);
