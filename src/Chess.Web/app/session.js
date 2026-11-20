export const PROTOCOL_VERSION = 2;
export const SESSION_KEY = 'chess.online.v2';
const roomPattern = /^[A-HJ-NP-Z2-9]{8}$/;

export function normalizeCode(code) {
  const value = String(code ?? '').trim().toUpperCase();
  if (!roomPattern.test(value)) throw new Error('Enter an eight-character room code.');
  return value;
}

export function invitation(location, code) {
  const url = new URL(location);
  url.hash = new URLSearchParams({ room: normalizeCode(code) }).toString();
  url.search = '';
  return url.href;
}

export function parseFen(fen) {
  const ranks = fen.split(' ')[0].split('/');
  if (ranks.length !== 8) throw new Error('The server sent an invalid board.');
  const board = new Map();
  ranks.forEach((rank, row) => {
    let column = 0;
    for (const piece of rank) {
      if (/^[1-8]$/.test(piece)) column += Number(piece);
      else {
        if (!/^[prnbqkPRNBQK]$/.test(piece) || column > 7) throw new Error('The server sent an invalid board.');
        board.set(`${String.fromCharCode(97 + column)}${8 - row}`, piece);
        column++;
      }
    }
    if (column !== 8) throw new Error('The server sent an invalid board.');
  });
  return board;
}

export function squares(seat) {
  return Array.from({ length: 64 }, (_, i) => {
    const index = seat === 'black' ? 63 - i : i;
    return `${String.fromCharCode(97 + index % 8)}${8 - Math.floor(index / 8)}`;
  });
}

export function validTicket(ticket) {
  return ticket && roomPattern.test(ticket.code) && ['white', 'black'].includes(ticket.seat) && /^[A-F0-9]{64}$/.test(ticket.credential);
}

export function outcome(state) {
  if (state.status === 'closed') return state.closedReason === 'expired' ? 'Room expired' : 'Room closed';
  if (!state.result) return '';
  const reasons = { Checkmate: 'Checkmate', Stalemate: 'Stalemate', DrawAgreement: 'Draw agreed',
    ThreefoldRepetition: 'Threefold repetition', FiftyMoveRule: 'Fifty-move rule',
    FivefoldRepetition: 'Fivefold repetition', SeventyFiveMoveRule: 'Seventy-five-move rule',
    InsufficientMaterial: 'Insufficient material', Resignation: `${state.result.resignedBy === 'white' ? 'White' : 'Black'} resigned` };
  const winner = state.result.winner ? `${state.result.winner === 'white' ? 'White' : 'Black'} wins` : 'Draw';
  return `${winner} · ${reasons[state.result.reason] ?? 'Game finished'}`;
}

export function errorCode(error) {
  return /\b(already_seated|stale_revision|not_seated|not_connected|room_expired|room_unavailable|room_full|invalid_credential|command_conflict|capacity|rate_limit|wrong_turn|invalid_move|game_finished|opponent_unavailable|invalid_action|game_not_started|invalid_command)\b/.exec(String(error?.message ?? error))?.[1];
}

export class GameSession {
  constructor(hub, storage, changed = () => {}) {
    this.hub = hub;
    this.storage = storage;
    this.changed = changed;
    this.ticket = null;
    this.state = null;
    this.ready = false;
    this.busy = false;
    this.pending = null;
    this.storageAvailable = true;
    this.resumeRequested = false;
    try {
      const saved = JSON.parse(storage.getItem(SESSION_KEY));
      if (validTicket(saved?.ticket)) { this.ticket = saved.ticket; this.pending = saved.pending && /^[0-9a-f-]{36}$/i.test(saved.pending.id) && Number.isSafeInteger(saved.pending.expectedRevision) && ['move','offerDraw','acceptDraw','declineDraw','claimDraw','resign'].includes(saved.pending.action) ? saved.pending : null; }
    } catch { this.storageAvailable = false; }
    hub.on('RoomChanged', state => this.accept(state));
    hub.onreconnecting(() => { this.ready = false; this.changed('Reconnecting…'); });
    hub.onreconnected(() => { this.resumeRequested = true; this.continueResume(); });
    hub.onclose(() => { this.ready = false; this.changed('Connection closed. Reconnect to resume.'); });
  }

  continueResume() {
    if (!this.busy && this.resumeRequested) {
      this.resumeRequested = false;
      void this.resume().catch(() => {});
    }
  }

  requireState(state) {
    if (state.protocolVersion !== PROTOCOL_VERSION || state.code !== this.ticket?.code) throw new Error('Unsupported server response.');
    this.accept(state);
  }

  persist() {
    try {
      if (this.ticket) this.storage.setItem(SESSION_KEY, JSON.stringify({ ticket: this.ticket, pending: this.pending }));
      else this.storage.removeItem(SESSION_KEY);
    } catch { this.storageAvailable = false; }
  }

  accept(state) {
    if (!this.ticket || state.code !== this.ticket.code) return false;
    if (state.protocolVersion !== PROTOCOL_VERSION) {
      this.ready = false;
      this.changed('This server uses an unsupported protocol. Update this client before playing.');
      return false;
    }
    if (this.state && state.revision < this.state.revision) return false;
    parseFen(state.fen);
    this.state = state;
    this.changed();
    return true;
  }

  async connect() {
    if (this.hub.state === 'Disconnected') await this.hub.start();
    if (this.hub.state !== 'Connected') throw new Error('Connection is not ready.');
  }

  async enter(method, ...args) {
    if (this.busy) return;
    this.busy = true; this.ready = false; this.changed('Connecting…');
    const previous = this.ticket;
    try {
      if (this.hub.state !== 'Disconnected') await this.hub.stop();
      this.ticket = null; this.state = null;
      await this.connect();
      const ticket = await this.hub.invoke(method, ...args);
      if (!validTicket(ticket) || ticket.state.protocolVersion !== PROTOCOL_VERSION) throw new Error('Unsupported server response.');
      this.ticket = { code: ticket.code, seat: ticket.seat, credential: ticket.credential };
      this.pending = null; this.persist();
      this.accept(ticket.state);
      this.requireState(await this.hub.invoke('GetState'));
      this.ready = true;
    } catch (error) {
      if (!this.ticket) this.ticket = previous;
      throw error;
    } finally { this.busy = false; this.changed(); this.continueResume(); }
  }

  computer(seat) { return this.enter('CreateComputerRoom', seat); }

  create(seat) { return this.enter('CreateRoom', seat); }
  join(code) { return this.enter('JoinRoom', normalizeCode(code)); }

  async resume() {
    if (!this.ticket || this.busy) return;
    this.busy = true; this.ready = false; this.changed('Reconnecting…');
    try {
      await this.connect();
      try {
        this.requireState(await this.hub.invoke('GetState'));
      } catch (error) {
        if (errorCode(error) !== 'not_seated') throw error;
        const ticket = await this.hub.invoke('ResumeRoom', this.ticket.code, this.ticket.credential);
        this.requireState(ticket.state);
      }
      this.requireState(await this.hub.invoke('GetState'));
      if (this.pending) await this.deliver();
      this.ready = true;
    } catch (error) {
      this.changed(errorCode(error) === 'room_expired' || errorCode(error) === 'room_unavailable'
        ? 'This room has expired or the server restarted. Start a new game.' : 'Could not resume this seat. Reconnect to try again.');
      throw error;
    } finally { this.busy = false; this.changed(); this.continueResume(); }
  }

  async send(action, move = null) {
    if (!this.ready || this.busy || this.pending || !this.state) return;
    this.busy = true;
    this.pending = { id: crypto.randomUUID(), expectedRevision: this.state.revision, action, move };
    this.persist(); this.changed();
    try { await this.deliver(); }
    finally { this.busy = false; this.changed(); this.continueResume(); }
  }

  async deliver() {
    try {
      const result = await this.hub.invoke('SubmitCommand', this.pending);
      this.accept(result.state);
      this.pending = null; this.persist();
    } catch (error) {
      const code = errorCode(error);
      if (code) {
        this.pending = null; this.persist();
        if (['not_seated', 'not_connected', 'room_expired', 'room_unavailable'].includes(code)) this.ready = false;
        else this.requireState(await this.hub.invoke('GetState'));
      } else this.ready = false;
      throw error;
    }
  }

  async retry() {
    if (this.busy) return;
    if (!this.pending) return this.resume();
    this.ready = false;
    return this.resume();
  }

  async forget() {
    this.ready = false;
    await this.hub.stop();
    this.ticket = null; this.state = null; this.pending = null;
    this.persist(); this.changed();
  }
}
