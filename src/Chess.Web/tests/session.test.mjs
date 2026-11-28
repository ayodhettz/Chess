import {test} from 'node:test';
import assert from 'node:assert/strict';
import {GameSession,invitation,parseFen,squares,SESSION_KEY} from '../app/session.js';
const state = (revision=1) => ({code:'ABCDEFGH',revision,protocolVersion:2,fen:'8/8/8/8/8/8/8/K6k w - - 0 1'});
const ticket = {code:'ABCDEFGH',seat:'white',credential:'A'.repeat(64)};
function setup(saved=null) {
  const values = new Map(saved ? [[SESSION_KEY,JSON.stringify(saved)]]:[]);
  const calls=[];
  const hub={state:'Connected',on(){},onreconnecting(){},onreconnected(fn){this.reconnected=fn;},onclose(){},async start(){this.state='Connected';},async stop(){this.state='Disconnected';},async invoke(method,...args){calls.push([method,...structuredClone(args)]);return method==='GetState'?state():method==='SubmitCommand'?{state:state(2)}:{...ticket,state:state()};}};
  const session=new GameSession(hub,{getItem:k=>values.get(k),setItem:(k,v)=>values.set(k,v),removeItem:k=>values.delete(k)});
  return {session,hub,calls};
}
test('invitation contains only a room code',()=>{
 const url=new URL(invitation('https://example.org/?secret=private#old','abcdefgh'));
 assert.equal(url.search,'');assert.equal(url.hash,'#room=ABCDEFGH');
});
test('FEN and orientation retain logical square names',()=>{
 assert.equal(parseFen(state().fen).get('a1'),'K');assert.equal(squares('black')[0],'h1');
 assert.throws(()=>parseFen('8/8'));assert.throws(()=>parseFen('9/8/8/8/8/8/8/8'));
});
test('snapshots ignore another room and older revisions',()=>{
 const {session}=setup({ticket});session.accept(state(3));session.accept(state(2));session.accept({...state(4),code:'ZZZZZZZZ'});assert.equal(session.state.revision,3);
});
test('resume on an existing seat uses GetState without rebinding',async()=>{
 const {session,calls}=setup({ticket});await session.resume();assert.equal(session.ready,true);assert.ok(!calls.some(c=>c[0]==='ResumeRoom'));
});
test('a replacement connection resumes with the stored private credential',async()=>{
 const {session,hub,calls}=setup({ticket});const invoke=hub.invoke.bind(hub);let unseated=true;
 hub.invoke=async(method,...args)=>{if(method==='GetState'&&unseated){unseated=false;throw Error('not_seated');}return invoke(method,...args);};
 await session.resume();assert.deepEqual(calls.find(c=>c[0]==='ResumeRoom'),['ResumeRoom',ticket.code,ticket.credential]);
});
test('ambiguous delivery survives reload and retries the identical command',async()=>{
 const {session,hub}=setup({ticket});session.accept(state());session.ready=true;
 const invoke=hub.invoke.bind(hub);hub.invoke=async method=>{if(method==='SubmitCommand')throw Error('network');return invoke(method);};
 await assert.rejects(session.send('move',{from:'a1',to:'a2'}));const pending=structuredClone(session.pending);
 const restored=setup({ticket,pending});await restored.session.resume();
 assert.deepEqual(restored.calls.find(c=>c[0]==='SubmitCommand')[1],pending);assert.equal(restored.session.pending,null);
});
test('stale rejected actions are cleared and authoritative state refreshed',async()=>{
 const {session,hub,calls}=setup({ticket});session.accept(state());session.ready=true;const invoke=hub.invoke.bind(hub);
 hub.invoke=async(method,...args)=>{if(method==='SubmitCommand')throw Error('stale_revision');return invoke(method,...args);};
 await assert.rejects(session.send('resign'));assert.equal(session.pending,null);assert.ok(calls.some(c=>c[0]==='GetState'));
});
test('unsupported protocol never enables play',async()=>{
 const {session,hub}=setup({ticket});hub.invoke=async()=>({...state(),protocolVersion:99});await assert.rejects(session.resume());assert.equal(session.ready,false);
});
test('malformed pending storage is discarded',()=>{
 assert.equal(setup({ticket,pending:{id:'broken'}}).session.pending,null);
});

test('difficulty is sent to the server',async()=>{
 const {session,calls}=setup();await session.computer('black','hard');
 assert.deepEqual(calls.find(c=>c[0]==='CreateComputerGame'),['CreateComputerGame','black','hard']);
});
test('matchmaking stores the assigned seat and refreshes it',async()=>{
 const {session,calls}=setup();await session.findMatch();
 assert.equal(session.ready,true);assert.equal(session.searching,false);
 assert.ok(calls.some(c=>c[0]==='FindMatch'));assert.ok(calls.some(c=>c[0]==='GetState'));
});
test('cancel returns a pairing that already won the race',async()=>{
 const {session,hub}=setup();const invoke=hub.invoke.bind(hub);
 hub.invoke=async(method,...args)=>{if(method==='FindMatch'){session.cancelSearch();return null;}return invoke(method,...args);};
 await session.findMatch();assert.equal(session.ready,true);assert.equal(session.ticket.code,ticket.code);
});
test('cancelled waiting search leaves no new seat',async()=>{
 const {session,hub}=setup();
 hub.invoke=async method=>{if(method==='FindMatch')session.cancelSearch();return null;};
 await session.findMatch();assert.equal(session.ticket,null);assert.equal(session.ready,false);assert.equal(session.busy,false);
});
