import { GameSession, invitation, normalizeCode, parseFen, squares, outcome, errorCode } from './session.js';
const $ = id => document.getElementById(id);
const names = {p:'Pawn',r:'Rook',n:'Knight',b:'Bishop',q:'Queen',k:'King'};
const initial = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1';
const title = value => value[0].toUpperCase() + value.slice(1);
const picture = piece => {
  const img = document.createElement('img');
  img.src = `/assets/${names[piece.toLowerCase()]}${piece === piece.toUpperCase() ? 'W' : 'B'}.png`;
  img.alt = ''; img.draggable = false; return img;
};
let selected = null, orientation = null, revision = -1, inGame = false;
const observedGames = new Set(), shownResults = new Set();
let storage;
try { storage = window.sessionStorage; } catch { storage = {getItem:()=>null,setItem:()=>{throw Error();},removeItem:()=>{}}; }
const hub = new signalR.HubConnectionBuilder().withUrl('/hubs/game')
  .withAutomaticReconnect([0,2000,5000,10000]).configureLogging(signalR.LogLevel.None).build();
const session = new GameSession(hub, storage, message => { if (message && !['Connecting…','Reconnecting…'].includes(message)) notice(message); render(); });
function notice(message) { $('notice').textContent = message; $('notice').hidden = !message; }
const errors = {
  room_unavailable:'That room is unavailable. Check the code, or create a new room.', room_full:'Both seats are taken in this room.',
  room_expired:'This room has expired. Create a new game.', stale_revision:'The game changed. The board is now up to date; choose your action again.',
  opponent_unavailable:'Your opponent is offline. Wait for them to reconnect.', wrong_turn:'It is your opponent’s turn.',
  not_seated:'This connection no longer owns the seat. Reconnect to resume it.', invalid_credential:'This seat could not be recovered.',
  rate_limit:'Too many requests. Wait a few seconds and try again.', capacity:'The server is full. Try again later.',
  invalid_move:'That move is no longer available.', game_finished:'This game has finished.', invalid_action:'That action is no longer available.',
};
async function run(action) {
  notice('');
  try { await action(); }
  catch (error) { notice(errors[errorCode(error)] ?? (session.pending ? 'Connection interrupted. Recover the pending action before continuing.' : 'Could not complete the request. Check your connection and try again.')); }
  render();
}
function confirmAction(heading, text) {
  $('confirm-title').textContent = heading; $('confirm-text').textContent = text;
  const dialog = $('confirm-dialog'); dialog.returnValue = ''; dialog.showModal();
  return new Promise(resolve => {
    dialog.addEventListener('close', () => resolve(dialog.returnValue === 'yes'), {once:true});
  });
}
$('confirm-yes').onclick = () => $('confirm-dialog').close('yes');
$('confirm-no').onclick = () => $('confirm-dialog').close('no');
async function enter(action) {
  if (session.ticket && !await confirmAction('Start another game?', 'Your previous seat stays reserved, but this tab will replace its saved credential after joining the new room.')) return;
  await run(async () => { await action(); inGame = !!session.state; render(); });
}
function computerSelected() { return document.querySelector('input[name=mode]:checked').value === 'computer'; }
for (const radio of document.querySelectorAll('input[name=mode]')) radio.onchange = () => {
  $('difficulty-panel').hidden = !computerSelected();
  $('create-room').textContent = computerSelected() ? 'Play computer' : 'Create room';
};
$('create-room').onclick = () => enter(() => {
  const seat = document.querySelector('input[name=seat]:checked').value;
  return computerSelected() ? session.computer(seat, document.querySelector('input[name=difficulty]:checked').value) : session.create(seat);
});
$('find-match').onclick = () => enter(() => session.findMatch());
$('cancel-search').onclick = () => session.cancelSearch();
$('join-form').onsubmit = event => { event.preventDefault(); void enter(() => session.join($('room-code').value)); };
$('resume-game').onclick = () => run(async () => { await session.resume(); inGame = true; });
$('reconnect').onclick = () => run(() => session.resume());
$('retry-command').onclick = () => run(() => session.retry());
$('new-game').onclick = async () => {
  if (session.pending) { notice('Recover your pending action before leaving this game.'); return; }
  if (!await confirmAction('Return to setup?', 'Your seat remains reserved. You can resume it from this tab.')) return;
  inGame = false; render();
};
$('home').onclick = event => { event.preventDefault(); if (inGame) $('new-game').click(); };
$('copy-invite').onclick = () => run(async () => {
  try { await navigator.clipboard.writeText($('invite-link').value); notice('Invitation copied.'); }
  catch { $('invite-link').select(); notice('Copy the selected invitation link.'); }
});
$('offer-draw').onclick = () => run(() => session.send('offerDraw'));
$('accept-draw').onclick = () => run(() => session.send('acceptDraw'));
$('decline-draw').onclick = () => run(() => session.send('declineDraw'));
$('claim-draw').onclick = () => run(() => session.send('claimDraw'));
$('resign').onclick = async () => {
  if (await confirmAction('Resign this game?', 'Your opponent will win. This cannot be undone.')) await run(() => session.send('resign'));
};
$('claim-intended').onchange = () => { selected = null; renderBoard(); };
function moves() { return ($('claim-intended').checked ? session.state?.draw.claimMoves : session.state?.legalMoves) ?? []; }
function active() { return session.ready && !session.busy && !session.pending && session.state?.status === 'active'; }
function canSelect() {
  const state = session.state;
  return active() && state.turn === session.ticket.seat && ($('claim-intended').checked || state.whiteConnected && state.blackConnected);
}
async function choosePromotion(options) {
  const dialog = $('promotion-dialog'); dialog.returnValue = ''; $('promotion-options').replaceChildren();
  for (const option of options) {
    const button = document.createElement('button'); button.type = 'button'; button.setAttribute('aria-label', title(option.promotion));
    const letter = {queen:'q',rook:'r',bishop:'b',knight:'n'}[option.promotion];
    button.append(picture(session.ticket.seat === 'white' ? letter.toUpperCase() : letter));
    button.onclick = () => dialog.close(option.promotion); $('promotion-options').append(button);
  }
  dialog.showModal();
  return new Promise(resolve => dialog.addEventListener('close', () => resolve(options.find(o => o.promotion === dialog.returnValue)), {once:true}));
}
$('cancel-promotion').onclick = () => $('promotion-dialog').close();
async function selectSquare(square) {
  if (!canSelect()) return;
  const candidates = moves().filter(move => move.from === selected && move.to === square);
  if (candidates.length) {
    const observedRevision = session.state.revision;
    const claim = $('claim-intended').checked;
    const move = candidates.length > 1 ? await choosePromotion(candidates) : candidates[0];
    selected = null;
    if (!move) { renderBoard(); return; }
    // A dialog may outlive a presence update or an opponent action.
    if (observedRevision !== session.state.revision || !canSelect()) { notice('The game changed. Choose your move again.'); render(); return; }
    await run(() => session.send(claim ? 'claimDraw' : 'move', move));
  } else { selected = moves().some(move => move.from === square) ? square : null; renderBoard(); }
}
function renderBoard() {
  if (!session.state) return;
  const seat = session.ticket.seat, order = squares(seat), board = parseFen(session.state.fen);
  if (orientation !== seat) {
    orientation = seat; $('board').replaceChildren();
    for (let row = 0; row < 8; row++) {
      const wrapper = document.createElement('div'); wrapper.className = 'board-row'; wrapper.setAttribute('role','row');
      for (const square of order.slice(row * 8, row * 8 + 8)) {
        const button = document.createElement('button'); button.type = 'button'; button.dataset.square = square;
        button.setAttribute('role','gridcell'); button.tabIndex = square === (seat === 'white' ? 'a8' : 'h1') ? 0 : -1;
        button.onclick = () => void selectSquare(square);
        button.onfocus = () => { for (const other of $('board').querySelectorAll('button')) other.tabIndex = other === button ? 0 : -1; };
        button.onkeydown = event => {
          const index = order.indexOf(square), column = index % 8;
          const next = {ArrowUp:index-8,ArrowDown:index+8,ArrowLeft:column ? index-1 : index,ArrowRight:column<7 ? index+1:index,Home:index-column,End:index-column+7}[event.key];
          if (next !== undefined) { event.preventDefault(); $('board').querySelector(`[data-square="${order[Math.max(0,Math.min(63,next))]}"]`).focus(); }
        };
        wrapper.append(button);
      }
      $('board').append(wrapper);
    }
    $('files').replaceChildren(...order.slice(56).map(s => { const span=document.createElement('span'); span.textContent=s[0]; return span; }));
    $('ranks').replaceChildren(...Array.from({length:8},(_,i)=>{const span=document.createElement('span'); span.textContent=order[i*8][1]; return span;}));
  }
  for (const button of $('board').querySelectorAll('button')) {
    const square = button.dataset.square, piece = board.get(square);
    button.className = `square ${(square.charCodeAt(0)-97+Number(square[1]))%2 ? 'light':'dark'}`;
    button.classList.toggle('in-check',session.state.checkedSquare === square);
    button.classList.toggle('selected',selected === square);
    button.classList.toggle('legal',canSelect() && moves().some(m=>m.from===selected && m.to===square));
    button.setAttribute('aria-selected',String(selected === square)); button.setAttribute('aria-disabled',String(!canSelect()));
    button.setAttribute('aria-label',`${square}, ${piece ? `${piece===piece.toUpperCase()?'white':'black'} ${names[piece.toLowerCase()].toLowerCase()}`:'empty'}${session.state.checkedSquare === square ? ', in check' : ''}`);
    button.replaceChildren(...(piece ? [picture(piece)] : []));
  }
}
function render() {
  $('setup').hidden = inGame; $('game').hidden = !inGame; $('home').hidden = inGame;
  $('match-label').hidden = !inGame; $('new-game').hidden = !inGame; $('resume-game').hidden = !session.ticket;
  $('find-match').disabled = session.busy;
  $('cancel-search').hidden = !session.searching;
  $('search-status').hidden = !session.searching;
  $('create-room').disabled = $('join-room').disabled = $('resume-game').disabled = session.busy;
  $('connection').textContent = session.ready ? 'Connected' : session.busy ? 'Connecting…' : session.ticket ? 'Offline' : 'Private online chess';
  const state = session.state; if (!state || !session.ticket) return;
  if (state.revision !== revision) { selected = null; revision = state.revision; }
  const own = session.ticket.seat, other = own === 'white' ? 'black':'white';
  const ownTurn = state.turn === own, live = active();
  const banner = $('result-banner');
  if (!state.result) {
    observedGames.add(state.code); banner.dataset.outcome = ''; banner.classList.remove('result-arrival');
  } else {
    const key = state.code + ':' + state.result.winner + ':' + state.result.reason;
    banner.dataset.outcome = state.result.winner == null ? 'draw' : state.result.winner === own ? 'win' : 'loss';
    if (!shownResults.has(key)) {
      shownResults.add(key);
      banner.classList.toggle('result-arrival', observedGames.has(state.code));
    }
  }
  $('turn').textContent = outcome(state) || (state.status === 'waiting' ? 'Waiting for a player' : `${title(state.turn)} to move${state.checkedSquare ? ' — check' : ''}`);
  $('game-status').textContent = state.status === 'waiting' ? 'Share the invitation to begin.' : state.status !== 'active' ? 'Start a new game whenever you’re ready.' : !state[`${other}Connected`] ? 'Opponent offline · waiting for them to return.' : ownTurn ? 'Your move.' : state.computerSeat ? 'Computer is thinking…' : 'Your opponent’s move.';
  $('opponent-label').textContent = state.computerSeat ? 'Computer · custom engine' : `Opponent · ${state[`${other}Connected`] ? 'online':'offline'}`;
  $('match-label').textContent = state.computerSeat ? 'Computer game' : 'Online game';
  $('invitation-panel').hidden = !!state.computerSeat;
  $('opponent-side').textContent = title(other); $('your-side').textContent = title(own);
  $('room-label').textContent = state.code; $('invite-link').value = invitation(location.href,state.code);
  $('reconnect').hidden = session.ready || !!session.pending; $('reconnect').disabled = session.busy;
  $('retry-command').hidden = $('pending-description').hidden = !session.pending; $('retry-command').disabled = session.busy;
  $('resign').disabled = !live; $('offer-draw').disabled = !(live && ownTurn && state.whiteConnected && state.blackConnected && state.draw.canOffer);
  $('draw-response').hidden = !(state.draw.offeredBy && state.draw.offeredBy !== own && state.status === 'active');
  $('accept-draw').disabled = $('decline-draw').disabled = !live; $('draw-message').textContent = 'Your opponent offered a draw.';
  $('claim-draw').hidden = !(state.draw.canClaim && ownTurn && state.status === 'active'); $('claim-draw').disabled = !live;
  $('claim-intended-label').hidden = !(state.draw.claimMoves.length && ownTurn && state.status === 'active');
  $('claim-intended').disabled = !live;
  if ($('claim-intended-label').hidden) $('claim-intended').checked = false;
  $('offer-status').hidden = state.draw.offeredBy !== own; $('offer-status').textContent = 'Draw offered · awaiting a response.';
  const history = $('moves'); history.replaceChildren();
  for (let i=0;i<state.moves.length;i+=2) {
    const row=document.createElement('tr');
    for (const text of [`${i/2+1}.`,state.moves[i],state.moves[i+1] ?? '']) { const cell=document.createElement('td');cell.textContent=text;row.append(cell); }
    history.append(row);
  }
  if (!session.storageAvailable) notice('Browser storage is unavailable. Keep this tab open; reloading may lose your seat.');
  renderBoard();
}
const preview = parseFen(initial);
for (const square of squares('white')) {
  const cell=document.createElement('div');cell.className=`square ${(square.charCodeAt(0)-97+Number(square[1]))%2?'light':'dark'}`;
  if (preview.has(square)) cell.append(picture(preview.get(square))); $('preview-board').append(cell);
}
const invited = new URLSearchParams(location.hash.slice(1)).get('room');
if (invited) { try { $('room-code').value=normalizeCode(invited); } catch { notice('This invitation has an invalid room code.'); } }
render();
if (session.ticket && (!invited || invited.toUpperCase() === session.ticket.code)) void run(async()=>{await session.resume();inGame=true;});
