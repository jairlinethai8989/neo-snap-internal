const host = window.chrome?.webview;
const post = (action, extra = {}) => host?.postMessage({ action, ...extra });
const delay = document.querySelector('#delay');
const open = document.querySelector('#open');
const state = document.querySelector('#state');
const recordingBar = document.querySelector('#recordingBar');
const videoMenu = document.querySelector('#videoMenu');
document.querySelectorAll('[data-action]').forEach((button) => {
  button.addEventListener('click', () => {
    if (button.dataset.action === 'video') { videoMenu.hidden = !videoMenu.hidden; return; }
    videoMenu.hidden = true;
    post(button.dataset.action, { delayMs: Number(delay.value) });
  });
});
document.querySelectorAll('[data-video]').forEach((button) => button.addEventListener('click', () => { videoMenu.hidden = true; post(button.dataset.video); }));
delay.addEventListener('change', () => post('settings', { delayMs: Number(delay.value), openMode: open.value }));
open.addEventListener('change', () => post('settings', { delayMs: Number(delay.value), openMode: open.value }));
document.querySelector('#stop').addEventListener('click', () => post('stopVideo'));
document.querySelector('#cancel').addEventListener('click', () => post('cancelVideo'));
host?.addEventListener('message', (event) => {
  const message = event.data;
  if (message.type === 'settings') { delay.value = String(message.delayMs); open.value = message.openMode; }
  if (message.type === 'status') { state.textContent = message.text; state.classList.toggle('error', !!message.error); }
  if (message.type === 'busy') document.querySelectorAll('[data-action]').forEach((button) => { button.disabled = message.value; });
  if (message.type === 'recording') { recordingBar.hidden = !message.value; document.querySelectorAll('[data-action]').forEach((button) => { button.disabled = message.value; }); }
  if (message.type === 'elapsed') document.querySelector('#elapsed').textContent = message.text;
});
post('ready');
lucide.createIcons();
