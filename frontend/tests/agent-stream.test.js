import { it, expect, vi } from "vitest";
import { sendAgentStream } from "../src/agent-stream.js";

const input = {
  conversationId: "conversation",
  requestId: "request",
  text: "Xếp lịch",
  autoApply: false,
};
function response(parts, signal) {
  const encoder = new TextEncoder();
  return new Response(
    new ReadableStream({
      start(controller) {
        for (const part of parts)
          controller.enqueue(
            typeof part === "string" ? encoder.encode(part) : part,
          );
        controller.close();
      },
    }),
    { headers: { "content-type": "text/event-stream" } },
  );
}
it("parses split UTF8 bytes, CRLF boundaries and multiple SSE frames without losing Vietnamese or emoji", async () => {
  const payload = `: heartbeat\r\n\r\ndata: ${JSON.stringify({ type: "phase", stage: "READING", message: "Đang đọc lịch" })}\r\n\r\ndata: ${JSON.stringify({ type: "delta", delta: "Chào bạn 🦉" })}\r\n\r\ndata: ${JSON.stringify({ type: "result", turn: { id: "turn", status: "PREVIEW" } })}\r\n\r\n`;
  const bytes = new TextEncoder().encode(payload);
  const pieces = Array.from(bytes, (byte) => new Uint8Array([byte]));
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => response(pieces)),
  );
  const events = [];
  const result = await sendAgentStream(input, (event) => events.push(event));
  expect(result.id).toBe("turn");
  expect(events.map((event) => event.type)).toEqual(["phase", "delta"]);
  expect(events[1].delta).toBe("Chào bạn 🦉");
});
it("an interrupted stream cannot be mistaken for a successfully saved schedule", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () =>
      response([
        `data: ${JSON.stringify({ type: "delta", delta: "Mình sẽ kiểm tra lịch." })}\n\n`,
      ]),
    ),
  );
  await expect(sendAgentStream(input, () => {})).rejects.toThrow(
    "Kết nối bị ngắt",
  );
});
it("a public stream error is surfaced and malformed raw content is not copied into error messages", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () =>
      response([
        `data: ${JSON.stringify({ type: "error", message: "Hết hạn mức AI", code: "AGENT_AI_QUOTA_EXCEEDED" })}\n\n`,
      ]),
    ),
  );
  await expect(sendAgentStream(input, () => {})).rejects.toMatchObject({
    code: "AGENT_AI_QUOTA_EXCEEDED",
  });
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => response(["data: private-provider-body\n\n"])),
  );
  await expect(sendAgentStream(input, () => {})).rejects.toThrow(
    "Phản hồi streaming chưa đúng định dạng",
  );
});
it("aborting uses the original fetch signal and never silently resubmits the request", async () => {
  const controller = new AbortController();
  const fetchMock = vi.fn(async (_, options) => {
    controller.abort();
    options.signal.throwIfAborted();
  });
  vi.stubGlobal("fetch", fetchMock);
  await expect(
    sendAgentStream(input, () => {}, controller.signal),
  ).rejects.toMatchObject({ name: "AbortError" });
  expect(fetchMock).toHaveBeenCalledTimes(1);
});
