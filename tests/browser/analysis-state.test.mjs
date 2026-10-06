import {test} from 'node:test';
import assert from 'node:assert/strict';
import {state,loadLabel} from '../../src/SPWPOS.Api/wwwroot/assets/ui.js';
import {todayPage,insightsPage} from '../../src/SPWPOS.Api/wwwroot/assets/pages.js';
test('an unavailable analysis is rendered as unknown without a fabricated low risk',()=>{
 state.workspace={tasks:[],sessions:[],availability:[],preference:{timeZoneId:'UTC'}};state.insights=null;
 const today=todayPage();assert.match(today,/Chưa xác định/);assert.doesNotMatch(today,/0% thời gian rảnh/);
 assert.match(insightsPage(),/Chưa có kết quả phân tích/);
});
test('workload label identifies demand due inside the horizon rather than all backlog',()=>{
 assert.match(loadLabel({riskLevel:'Low',effectiveCapacityMinutes:1000,loadRatio:0.3,remainingMinutes:1500,horizonDemandMinutes:300}),/300 phút đến hạn/);
});
