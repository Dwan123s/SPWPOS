import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createSessionRefresh} from '../../frontend/src/session-refresh.js';

function sharedLocks(){let queue=Promise.resolve();return {request(name,operation){assert.equal(name,'spwpos-session-refresh');const result=queue.then(operation);queue=result.catch(()=>{});return result;}};}
const unauthorized=()=>Object.assign(new Error('expired'),{status:401});
test('two tabs with expired cookies refresh once and both recover',async()=>{
 let valid=false,rotations=0;const locks=sharedLocks();
 const probe=async()=>{if(!valid)throw unauthorized();return {user:'same user'};};
 const renew=async()=>{await Promise.resolve();assert.equal(rotations++,0,'old refresh token must never be submitted twice');valid=true;};
 const a=createSessionRefresh({locks,probe,renew}),b=createSessionRefresh({locks,probe,renew});
 await Promise.all([a(),b(),a()]);assert.equal(rotations,1);
});
test('a stale 401 response uses the session already refreshed by another tab',async()=>{
 let rotations=0;const refresh=createSessionRefresh({locks:sharedLocks(),probe:async()=>({user:'active'}),renew:async()=>{rotations++;}});
 await refresh();assert.equal(rotations,0);
});
test('temporary refresh failure releases the lock and permits a later retry',async()=>{
 let tries=0;const refresh=createSessionRefresh({locks:sharedLocks(),probe:async()=>{throw unauthorized();},renew:async()=>{if(++tries===1)throw Object.assign(new Error('unavailable'),{status:503});}});
 await assert.rejects(refresh(),error=>error.status===503);await refresh();assert.equal(tries,2);
});
test('unsupported browsers recheck the session but never race unsafe refresh calls',async()=>{
 let rotations=0;const refresh=createSessionRefresh({locks:undefined,probe:async()=>{throw unauthorized();},renew:async()=>{rotations++;}});
 await assert.rejects(refresh(),error=>error.status===401);assert.equal(rotations,0);
});
