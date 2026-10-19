# NORA sandbox results

Mode: **Live** · provider: VertexAI · model: gemini-3.5-flash-lite

Commit: `bbb93ef810f855b5aa5d90ee9e8225d4c7113757` · dirty: True · interpreter attempts: 9/9

Automatic action checks and human response review are separate. PASS does not certify live response semantics.

- FAIL: 3
- ERROR: 1
- PASS: 5

| Case | Repeat | Automatic checks | Response review | Steps |
|---|---:|---|---|---:|
| D14 | 1 | FAIL | REVIEW_REQUIRED | 1 |
| D14 | 2 | FAIL | REVIEW_REQUIRED | 1 |
| D14 | 3 | FAIL | REVIEW_REQUIRED | 1 |
| E08 | 1 | ERROR | NOT_EVALUATED | 1 |
| E08 | 2 | PASS | REVIEW_REQUIRED | 1 |
| E08 | 3 | PASS | REVIEW_REQUIRED | 1 |
| G02 | 1 | PASS | REVIEW_REQUIRED | 1 |
| G02 | 2 | PASS | REVIEW_REQUIRED | 1 |
| G02 | 3 | PASS | REVIEW_REQUIRED | 1 |

## D14 — Chia nhiều ngày với budget và giờ nghỉ (repeat 1)

Fixture: DAYS · result: **FAIL**

Stopped after step 1; dependent steps were not executed.

### Step 1: Send

Prompt:

> Mình cần 180 phút ôn thi, được chia phiên, hạn chót 22:00 ngày 27/10/2026. Giữ giới hạn phút mỗi ngày, độ dài phiên và giờ nghỉ.

Status: **NEEDS_INPUT** · error code: none · 2208 ms · persisted changes: 0

Reply:

> Yêu cầu ôn thi 180 phút với hạn chót 22:00 ngày 27/10/2026 vượt quá giới hạn dung lượng khả dụng trong thời gian yêu cầu (mỗi ngày tối đa 120 phút). Bạn có muốn chia nhỏ thời gian ôn thi ra nhiều ngày hơn hoặc điều chỉnh giới hạn ngày không?

Question: Tổng thời gian ôn thi (180 phút) vượt quá giới hạn 120 phút mỗi ngày trong khoảng thời gian từ nay đến hạn chót. Bạn muốn phân bổ thời gian ôn thi này như thế nào?

Checks:

- FAIL `expected-outcome`: Expected APPLIED; actual NEEDS_INPUT.
- PASS [invariant] `zero-persisted-changes`: Observed 0 persisted changes.
- PASS [invariant] `clarification-question`: Clarification must have a question, no proposed changes/plan, and no undo.
- FAIL `task-identity:ôn thi`: Expected one task matching ôn thi; actual 0.
- PASS [invariant] `stored-clarification-is-write-free`: Check sanitized persisted command, not the untrusted model intent.

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|

Saved events (account local time):

| Event | Start | End |
|---|---|---|

## D14 — Chia nhiều ngày với budget và giờ nghỉ (repeat 2)

Fixture: DAYS · result: **FAIL**

Stopped after step 1; dependent steps were not executed.

### Step 1: Send

Prompt:

> Mình cần 180 phút ôn thi, được chia phiên, hạn chót 22:00 ngày 27/10/2026. Giữ giới hạn phút mỗi ngày, độ dài phiên và giờ nghỉ.

Status: **NEEDS_INPUT** · error code: none · 2024 ms · persisted changes: 0

Reply:

> Tuy nhiên, tổng dung lượng khả dụng trong khoảng thời gian này vượt quá giới hạn 120 phút mỗi ngày của bạn. Bạn có muốn điều chỉnh thời hạn hoặc chia nhỏ khối lượng công việc này ra không?

Question: Tổng thời gian cần ôn thi (180 phút) vượt quá giới hạn khả dụng trong các ngày tới (120 phút/ngày). Bạn muốn xử lý thế nào?

Checks:

- FAIL `expected-outcome`: Expected APPLIED; actual NEEDS_INPUT.
- PASS [invariant] `zero-persisted-changes`: Observed 0 persisted changes.
- PASS [invariant] `clarification-question`: Clarification must have a question, no proposed changes/plan, and no undo.
- FAIL `task-identity:ôn thi`: Expected one task matching ôn thi; actual 0.
- PASS [invariant] `stored-clarification-is-write-free`: Check sanitized persisted command, not the untrusted model intent.

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|

Saved events (account local time):

| Event | Start | End |
|---|---|---|

## D14 — Chia nhiều ngày với budget và giờ nghỉ (repeat 3)

Fixture: DAYS · result: **FAIL**

Stopped after step 1; dependent steps were not executed.

### Step 1: Send

Prompt:

> Mình cần 180 phút ôn thi, được chia phiên, hạn chót 22:00 ngày 27/10/2026. Giữ giới hạn phút mỗi ngày, độ dài phiên và giờ nghỉ.

Status: **NEEDS_INPUT** · error code: none · 2348 ms · persisted changes: 0

Reply:

> Tôi đã tiếp nhận yêu cầu 180 phút ôn thi có hạn chót vào 22:00 ngày 27/10/2026. Tuy nhiên, giới hạn mỗi ngày hiện tại của bạn là 120 phút và tổng dung lượng khả dụng trong 2 ngày (26 và 27 tháng 10) là vừa đủ 240 phút (mỗi ngày 120 phút), nhưng hệ thống cần thời gian nghỉ giữa các phiên nên toàn bộ 180 phút không thể hoàn thành trong giới hạn ngày. Bạn có muốn điều chỉnh giới hạn mỗi ngày lên hoặc chia nhỏ lịch trình dài hơn không?

Question: Giới hạn thời gian tối đa mỗi ngày của bạn hiện là 120 phút, không đủ để hoàn thành 180 phút ôn thi kèm thời gian nghỉ trong khoảng thời gian đến 27/10/2026. Bạn muốn tăng giới hạn mỗi ngày hay kéo dài thời gian hoàn thành?

Checks:

- FAIL `expected-outcome`: Expected APPLIED; actual NEEDS_INPUT.
- PASS [invariant] `zero-persisted-changes`: Observed 0 persisted changes.
- PASS [invariant] `clarification-question`: Clarification must have a question, no proposed changes/plan, and no undo.
- FAIL `task-identity:ôn thi`: Expected one task matching ôn thi; actual 0.
- PASS [invariant] `stored-clarification-is-write-free`: Check sanitized persisted command, not the untrusted model intent.

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|

Saved events (account local time):

| Event | Start | End |
|---|---|---|

## E08 — Giữ khung thực hiện riêng vào đúng tối thứ Năm (repeat 1)

Fixture: B · result: **ERROR**

Stopped after step 1; dependent steps were not executed.

### Step 1: Send

Prompt:

> Mình muốn ôn tiếng Anh đúng tối thứ Năm 29/10/2026 trong 45 phút, hạn chót 21:00 hôm đó. Không học vào ngày khác.

Status: **REJECTED / ERROR** · error code: AGENT_AI_INVALID_RESPONSE · 2040 ms · persisted changes: 0

Retained stored turn status: FAILED (not a new successful operation).

Reply:

> AI chưa trả về yêu cầu hợp lệ. Hãy thử lại hoặc diễn đạt rõ ngày giờ; lịch chưa thay đổi.

Checks:

- FAIL `expected-outcome`: Expected APPLIED; actual AGENT_AI_INVALID_RESPONSE.
- PASS [invariant] `zero-persisted-changes`: Observed 0 persisted changes.
- FAIL `task-count`: Expected 1; actual 0.
- FAIL `task-identity:tiếng Anh`: Expected one task matching tiếng Anh; actual 0.

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|

Saved events (account local time):

| Event | Start | End |
|---|---|---|

## E08 — Giữ khung thực hiện riêng vào đúng tối thứ Năm (repeat 2)

Fixture: B · result: **PASS**

### Step 1: Send

Prompt:

> Mình muốn ôn tiếng Anh đúng tối thứ Năm 29/10/2026 trong 45 phút, hạn chót 21:00 hôm đó. Không học vào ngày khác.

Status: **APPLIED** · error code: none · 2945 ms · persisted changes: 4

Reply:

> Đã lưu các thay đổi:
> Ôn tiếng Anh: 45 phút · hạn 29/10/2026 21:00 (cứng) · ưu tiên 2 · liên tục · thực hiện 29/10/2026 18:00 → 30/10/2026 00:00
> Phiên làm việc:
> 29/10/2026 19:00 → 29/10/2026 19:45

Checks:

- PASS `expected-outcome`: Expected APPLIED; actual APPLIED.
- PASS `task-count`: Expected 1; actual 1.
- PASS `task-identity:tiếng Anh`: Expected one task matching tiếng Anh; actual 1.
- PASS `task-duration:tiếng Anh`: Expected 45; actual 45.
- PASS `task-deadline:tiếng Anh`: Actual 2026-10-29T14:00:00.0000000+00:00.
- PASS [invariant] `task-window-start:tiếng Anh`: Actual 2026-10-29T11:00:00.0000000+00:00.
- PASS [invariant] `task-window-end:tiếng Anh`: Actual 2026-10-29T17:00:00.0000000+00:00.
- PASS [invariant] `scheduled-minutes:tiếng Anh`: Expected 45; actual 45.
- PASS [invariant] `session-length:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: Actual 45 minutes.
- PASS [invariant] `session-deadline:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: Hard deadline enforced.
- PASS [invariant] `session-work-window:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: Allowed execution window enforced independently.
- PASS [invariant] `session-future:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: New work must start in the future.
- PASS [invariant] `session-active-task:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: No completed/cancelled/deleted work.
- PASS [invariant] `session-event-overlap:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: No fixed-event overlap.
- PASS [invariant] `session-overlap:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: No work overlap.
- PASS [invariant] `session-free-time:3a9e21c9-58e9-4b45-908a-e184217f8a7a`: Every scheduled minute fits effective availability.
- PASS [invariant] `unsplit-task:7f1c352f-d4e2-4b1c-b564-08ff13606efb`: Continuous work must be a single session.
- PASS [invariant] `no-excess-work:7f1c352f-d4e2-4b1c-b564-08ff13606efb`: Scheduled minutes cannot exceed the raw remaining estimate with bias disabled.
- PASS [invariant] `daily-budget:10/29/2026`: Completed and reserved work count toward the daily limit.
- PASS [invariant] `active-category:7f1c352f-d4e2-4b1c-b564-08ff13606efb`: Persisted task category is active.

Actual database actions:

| Entity | ID | Operation | Changed values |
|---|---|---|---|
| task | `7f1c352f-d4e2-4b1c-b564-08ff13606efb` | ADD | name: ∅ → Ôn tiếng Anh; estimatedMinutes: ∅ → 45; deadline: ∅ → 2026-10-29T14:00:00+00:00; priority: ∅ → 2; taskType: ∅ → STUDY; splittable: ∅ → False; status: ∅ → 0; isDeleted: ∅ → False |
| session | `3a9e21c9-58e9-4b45-908a-e184217f8a7a` | ADD | status: ∅ → 0; start: ∅ → 2026-10-29T12:00:00+00:00; end: ∅ → 2026-10-29T12:45:00+00:00 |
| preference | `account` | UPDATE | revision: ac666910-5c0a-485e-93c4-f29ef42a36ef → 0f9c22da-26e1-4bdc-bd1b-ad15b6ebd60e |
| proposal | `0110a302-b4a6-4c3c-822e-408cbb4b5a16` | ADD | status: ∅ → APPLIED; revision: ∅ → ac666910-5c0a-485e-93c4-f29ef42a36ef; isApplied: ∅ → True |

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|
| Ôn tiếng Anh | 2026-10-29 19:00 +07:00 | 2026-10-29 19:45 +07:00 | 45 | Scheduled | False |

Saved events (account local time):

| Event | Start | End |
|---|---|---|

## E08 — Giữ khung thực hiện riêng vào đúng tối thứ Năm (repeat 3)

Fixture: B · result: **PASS**

### Step 1: Send

Prompt:

> Mình muốn ôn tiếng Anh đúng tối thứ Năm 29/10/2026 trong 45 phút, hạn chót 21:00 hôm đó. Không học vào ngày khác.

Status: **APPLIED** · error code: none · 1863 ms · persisted changes: 4

Reply:

> Đã lưu các thay đổi:
> Ôn tiếng Anh: 45 phút · hạn 29/10/2026 21:00 (cứng) · ưu tiên 2 · liên tục · thực hiện 29/10/2026 18:00 → 30/10/2026 00:00
> Phiên làm việc:
> 29/10/2026 19:00 → 29/10/2026 19:45

Checks:

- PASS `expected-outcome`: Expected APPLIED; actual APPLIED.
- PASS `task-count`: Expected 1; actual 1.
- PASS `task-identity:tiếng Anh`: Expected one task matching tiếng Anh; actual 1.
- PASS `task-duration:tiếng Anh`: Expected 45; actual 45.
- PASS `task-deadline:tiếng Anh`: Actual 2026-10-29T14:00:00.0000000+00:00.
- PASS [invariant] `task-window-start:tiếng Anh`: Actual 2026-10-29T11:00:00.0000000+00:00.
- PASS [invariant] `task-window-end:tiếng Anh`: Actual 2026-10-29T17:00:00.0000000+00:00.
- PASS [invariant] `scheduled-minutes:tiếng Anh`: Expected 45; actual 45.
- PASS [invariant] `session-length:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: Actual 45 minutes.
- PASS [invariant] `session-deadline:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: Hard deadline enforced.
- PASS [invariant] `session-work-window:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: Allowed execution window enforced independently.
- PASS [invariant] `session-future:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: New work must start in the future.
- PASS [invariant] `session-active-task:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: No completed/cancelled/deleted work.
- PASS [invariant] `session-event-overlap:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: No fixed-event overlap.
- PASS [invariant] `session-overlap:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: No work overlap.
- PASS [invariant] `session-free-time:1b52f867-af7b-4380-9707-da7f4b9bbc5b`: Every scheduled minute fits effective availability.
- PASS [invariant] `unsplit-task:6461eedb-1b7a-40b8-94c9-ea87f52d9425`: Continuous work must be a single session.
- PASS [invariant] `no-excess-work:6461eedb-1b7a-40b8-94c9-ea87f52d9425`: Scheduled minutes cannot exceed the raw remaining estimate with bias disabled.
- PASS [invariant] `daily-budget:10/29/2026`: Completed and reserved work count toward the daily limit.
- PASS [invariant] `active-category:6461eedb-1b7a-40b8-94c9-ea87f52d9425`: Persisted task category is active.

Actual database actions:

| Entity | ID | Operation | Changed values |
|---|---|---|---|
| task | `6461eedb-1b7a-40b8-94c9-ea87f52d9425` | ADD | name: ∅ → Ôn tiếng Anh; estimatedMinutes: ∅ → 45; deadline: ∅ → 2026-10-29T14:00:00+00:00; priority: ∅ → 2; taskType: ∅ → STUDY; splittable: ∅ → False; status: ∅ → 0; isDeleted: ∅ → False |
| session | `1b52f867-af7b-4380-9707-da7f4b9bbc5b` | ADD | status: ∅ → 0; start: ∅ → 2026-10-29T12:00:00+00:00; end: ∅ → 2026-10-29T12:45:00+00:00 |
| preference | `account` | UPDATE | revision: a31dceb9-281c-4f87-bd9b-8388038cc015 → 11f083bb-68fb-44d5-8325-e9ff6815176d |
| proposal | `4f1c7fef-6dcb-424a-ac84-f815cac5369f` | ADD | status: ∅ → APPLIED; revision: ∅ → a31dceb9-281c-4f87-bd9b-8388038cc015; isApplied: ∅ → True |

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|
| Ôn tiếng Anh | 2026-10-29 19:00 +07:00 | 2026-10-29 19:45 +07:00 | 45 | Scheduled | False |

Saved events (account local time):

| Event | Start | End |
|---|---|---|

## G02 — Tuần sau (repeat 1)

Fixture: B · result: **PASS**

### Step 1: Send

Prompt:

> Thứ Hai tuần sau mình họp 18:00–19:00, chỉ một buổi. Thêm giúp mình.

Status: **APPLIED** · error code: none · 1713 ms · persisted changes: 2

Reply:

> Đã lưu các thay đổi:
> Họp: 02/11/2026 18:00 → 02/11/2026 19:00
> Không có phiên làm việc cần chuyển.

Checks:

- PASS `expected-outcome`: Expected APPLIED; actual APPLIED.
- PASS `event-count`: Expected 1; actual 1.
- PASS `exact-event:họp`: Expected 2026-11-02T18:00:00+07:00 -&gt; 2026-11-02T19:00:00+07:00.
- PASS [invariant] `event-conflict:f6ce2f6b-14c5-478f-85f6-b61d4ac33506`: Fixed event conflicts checked against saved events/work.

Actual database actions:

| Entity | ID | Operation | Changed values |
|---|---|---|---|
| event | `f6ce2f6b-14c5-478f-85f6-b61d4ac33506` | ADD | name: ∅ → Họp; start: ∅ → 2026-11-02T11:00:00+00:00; end: ∅ → 2026-11-02T12:00:00+00:00 |
| preference | `account` | UPDATE | revision: 413adf9a-6a4a-4955-920e-a2b165a2be6f → 49eeba0f-2f2d-40c8-a791-8d6b1de1b69d |

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|

Saved events (account local time):

| Event | Start | End |
|---|---|---|
| Họp | 2026-11-02 18:00 +07:00 | 2026-11-02 19:00 +07:00 |

## G02 — Tuần sau (repeat 2)

Fixture: B · result: **PASS**

### Step 1: Send

Prompt:

> Thứ Hai tuần sau mình họp 18:00–19:00, chỉ một buổi. Thêm giúp mình.

Status: **APPLIED** · error code: none · 1787 ms · persisted changes: 2

Reply:

> Đã lưu các thay đổi:
> Họp: 02/11/2026 18:00 → 02/11/2026 19:00
> Không có phiên làm việc cần chuyển.

Checks:

- PASS `expected-outcome`: Expected APPLIED; actual APPLIED.
- PASS `event-count`: Expected 1; actual 1.
- PASS `exact-event:họp`: Expected 2026-11-02T18:00:00+07:00 -&gt; 2026-11-02T19:00:00+07:00.
- PASS [invariant] `event-conflict:f0d4dedc-49f5-4ccb-9c30-374908ff109e`: Fixed event conflicts checked against saved events/work.

Actual database actions:

| Entity | ID | Operation | Changed values |
|---|---|---|---|
| event | `f0d4dedc-49f5-4ccb-9c30-374908ff109e` | ADD | name: ∅ → Họp; start: ∅ → 2026-11-02T11:00:00+00:00; end: ∅ → 2026-11-02T12:00:00+00:00 |
| preference | `account` | UPDATE | revision: e412448a-af77-4def-9fca-655e3cab8463 → 4cf5f6c2-5676-4552-a6fe-c2d4f5955641 |

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|

Saved events (account local time):

| Event | Start | End |
|---|---|---|
| Họp | 2026-11-02 18:00 +07:00 | 2026-11-02 19:00 +07:00 |

## G02 — Tuần sau (repeat 3)

Fixture: B · result: **PASS**

### Step 1: Send

Prompt:

> Thứ Hai tuần sau mình họp 18:00–19:00, chỉ một buổi. Thêm giúp mình.

Status: **APPLIED** · error code: none · 1610 ms · persisted changes: 2

Reply:

> Đã lưu các thay đổi:
> Họp: 02/11/2026 18:00 → 02/11/2026 19:00
> Không có phiên làm việc cần chuyển.

Checks:

- PASS `expected-outcome`: Expected APPLIED; actual APPLIED.
- PASS `event-count`: Expected 1; actual 1.
- PASS `exact-event:họp`: Expected 2026-11-02T18:00:00+07:00 -&gt; 2026-11-02T19:00:00+07:00.
- PASS [invariant] `event-conflict:512f684b-ade3-4325-af0f-2410bfd62bde`: Fixed event conflicts checked against saved events/work.

Actual database actions:

| Entity | ID | Operation | Changed values |
|---|---|---|---|
| event | `512f684b-ade3-4325-af0f-2410bfd62bde` | ADD | name: ∅ → Họp; start: ∅ → 2026-11-02T11:00:00+00:00; end: ∅ → 2026-11-02T12:00:00+00:00 |
| preference | `account` | UPDATE | revision: 4cd796ca-0766-4f33-abe6-4eec0f841961 → 47dcd2ed-ce51-467b-98f0-3ff9635ad1e1 |

Saved work sessions (account local time):

| Task | Start | End | Minutes | Status | Locked |
|---|---|---|---:|---|---|

Saved events (account local time):

| Event | Start | End |
|---|---|---|
| Họp | 2026-11-02 18:00 +07:00 | 2026-11-02 19:00 +07:00 |

## Coverage

| Scenario | Coverage |
|---|---|
| A01 | NOT_SELECTED |
| A02 | NOT_SELECTED |
| A03 | NOT_SELECTED |
| A04 | NOT_SELECTED |
| A05 | NOT_SELECTED |
| A06 | NOT_SELECTED |
| A07 | NOT_SELECTED |
| A08 | NOT_SELECTED |
| B01 | NOT_SELECTED |
| B02 | NOT_SELECTED |
| B03 | NOT_SELECTED |
| B04 | NOT_SELECTED |
| B05 | NOT_SELECTED |
| B06 | NOT_SELECTED |
| B07 | NOT_SELECTED |
| B08 | NOT_SELECTED |
| B09 | NOT_SELECTED |
| C01 | NOT_SELECTED |
| C02 | NOT_SELECTED |
| C03 | NOT_SELECTED |
| C04 | NOT_SELECTED |
| C05 | NOT_SELECTED |
| C06 | NOT_SELECTED |
| C07 | NOT_SELECTED |
| C08 | NOT_SELECTED |
| C09 | NOT_SELECTED |
| C10 | NOT_SELECTED |
| D01 | NOT_SELECTED |
| D02 | NOT_SELECTED |
| D03 | NOT_SELECTED |
| D04 | NOT_SELECTED |
| D05 | NOT_SELECTED |
| D06 | NOT_SELECTED |
| D07 | NOT_SELECTED |
| D08 | NOT_SELECTED |
| D09 | NOT_SELECTED |
| D10 | NOT_SELECTED |
| D11 | NOT_SELECTED |
| D12 | NOT_SELECTED |
| D13 | NOT_SELECTED |
| D14 | FAIL |
| E01 | NOT_SELECTED |
| E02 | NOT_SELECTED |
| E03 | NOT_SELECTED |
| E04 | NOT_SELECTED |
| E05 | NOT_SELECTED |
| E06 | NOT_SELECTED |
| E07 | NOT_SELECTED |
| E08 | PASS |
| E09 | NOT_SELECTED |
| F01 | NOT_SELECTED |
| F02 | NOT_SELECTED |
| F03 | NOT_SELECTED |
| F04 | NOT_SELECTED |
| F05 | NOT_SELECTED |
| F06 | NOT_SELECTED |
| F07 | NOT_SELECTED |
| F08 | NOT_SELECTED |
| G01 | NOT_SELECTED |
| G02 | PASS |
| G03 | NOT_SELECTED |
| G04 | NOT_SELECTED |
| G05 | NOT_SELECTED |
| G06 | NOT_SELECTED |
| S01 | NOT_SELECTED |
| S02 | NOT_SELECTED |
| S03 | NOT_SELECTED |
| S04 | NOT_SELECTED |
| S05 | NOT_SELECTED |
| S06 | NOT_SELECTED |
| H01 | NOT_SELECTED |
| H02 | NOT_SELECTED |
| H03 | NOT_SELECTED |
| H04 | NOT_SELECTED |
| H05 | NOT_SELECTED |
| H06 | NOT_SELECTED |
| H07 | NOT_SELECTED |
| H08 | NOT_SELECTED |
| H09 | NOT_SELECTED |
| H10 | NOT_SELECTED |
| H11 | NOT_SELECTED |
| H12 | NOT_SELECTED |
| V01 | NOT_SELECTED |
| V02 | NOT_SELECTED |
| V03 | NOT_SELECTED |
| V04 | NOT_SELECTED |

## Limitations

- SQLite in-memory; no production database, authenticated HTTP API, background workers or real calendar.
- Only synthetic fixture data is sent to the configured provider. No configuration values, credentials or raw provider bodies are exported.
- Offline results test scripted interpreter fixtures and real backend actions; they do not measure live prompt comprehension.
- PASS means the listed automatic checks passed. Response semantics require human review; lexical task-name matching can need adjudication.
- Service-level runs do not verify browser rendering, microphone input, transport disconnects, SQL Server locks or unimplemented scenario variants.
- Live calls consume provider quota. Call count is a cap on interpreter attempts, including scripted and injected attempts, not a currency/token spending guarantee.

`results.json` includes structured model intents, bounded context/history, sanitized stored commands, complete synthetic before/after snapshots, and actual field values for each action. CSV has one row per executed step. No private model reasoning is requested or exported.
