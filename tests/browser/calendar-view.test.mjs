import {test} from 'node:test';
import assert from 'node:assert/strict';
import {state} from '../../src/SPWPOS.Api/wwwroot/assets/ui.js';
import {weekStart,calendarPage} from '../../src/SPWPOS.Api/wwwroot/assets/schedule.js';
test('calendar weeks start Monday across month and year boundaries',()=>{
 assert.equal(weekStart('2026-10-07'),'2026-10-05');
 assert.equal(weekStart('2026-10-04'),'2026-09-28');
 assert.equal(weekStart('2027-01-01'),'2026-12-28');
});
test('a fixed event ending at midnight is not repeated on the following day',()=>{
 state.workspace={preference:{timeZoneId:'UTC'},sessions:[],events:[{id:'event',name:'Midnight boundary',start:'2026-10-05T22:00:00Z',end:'2026-10-06T00:00:00Z'}]};
 state.calendarStart='2026-10-07';state.calendarView='week';state.conflicts=[];
 const html=calendarPage();assert.equal(state.calendarStart,'2026-10-05');
 assert.equal((html.match(/Midnight boundary/g)||[]).length,1);
 assert.match(html,/22:00 — 24:00/);
});
test('calendar date headings keep the selected local date in extreme time zones',()=>{
 state.workspace={preference:{timeZoneId:'Pacific/Kiritimati'},sessions:[],events:[]};
 state.calendarStart='2026-10-05';state.calendarView='week';state.conflicts=[];
 assert.match(calendarPage(),/<h2>05-10 — 11\/10\/2026<\/h2>/);
});
