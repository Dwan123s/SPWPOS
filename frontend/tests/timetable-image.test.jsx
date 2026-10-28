import { expect, it, vi } from "vitest";
import {
  act,
  fireEvent,
  render,
  renderHook,
  screen,
  waitFor,
} from "@testing-library/react";
import {
  TimetableImageButton,
  TimetableImagePreview,
  TIMETABLE_IMAGE_MAX_BYTES,
  timetableRequest,
  useTimetableImage,
} from "../src/timetable-image.jsx";

const file = (name = "thoikhoabieu.png", type = "image/png") =>
  new File([new Uint8Array([137, 80, 78, 71])], name, { type });
const reading = {
  text: "Thứ 2: Toán, 07:00–09:00, phòng A101.",
  warnings: ["Chưa có ngày bắt đầu và số tuần áp dụng."],
  needsReview: true,
};

function Picker({ readImage, enabled = true }) {
  const image = useTimetableImage({ enabled, readImage });
  return (
    <form
      onSubmit={async (event) => {
        event.preventDefault();
        try {
          await image.read();
        } catch {
          /* The hook displays the failure. */
        }
      }}
    >
      <TimetableImageButton image={image} disabled={!enabled} />
      <TimetableImagePreview
        image={image}
        disabled={!enabled}
        provider="Gemini"
      />
      <button type="submit">Gửi</button>
    </form>
  );
}

it("keeps a chosen image local until Send, then exposes editable extraction without an extra consent step", async () => {
  const readImage = vi.fn().mockResolvedValue(reading);
  render(<Picker readImage={readImage} />);
  fireEvent.change(screen.getByLabelText("Chọn ảnh thời khóa biểu"), {
    target: { files: [file()] },
  });
  await screen.findByAltText("Ảnh thời khóa biểu đã chọn");
  expect(readImage).not.toHaveBeenCalled();
  expect(screen.getByText(/Bấm Gửi để NORA đọc ảnh bằng Gemini/)).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Gửi" }));
  const field = await screen.findByRole("textbox", {
    name: "Nội dung thời khóa biểu",
  });
  expect(field.value).toBe(reading.text);
  expect(readImage).toHaveBeenCalledTimes(1);
  expect(screen.getByText(reading.warnings[0])).toBeTruthy();
  fireEvent.change(field, { target: { value: "Thứ 2: Toán, 07:30–09:00." } });
  expect(field.value).toContain("07:30");
  fireEvent.click(
    screen.getByRole("button", { name: "Gỡ ảnh thời khóa biểu" }),
  );
  expect(screen.queryByAltText("Ảnh thời khóa biểu đã chọn")).toBeNull();
});

it("rejects unsupported, empty and oversized files without reading or sending them", async () => {
  const readImage = vi.fn();
  const { result } = renderHook(() =>
    useTimetableImage({ enabled: true, readImage }),
  );
  await act(() => result.current.select(file("bad.svg", "image/svg+xml")));
  expect(result.current.error).toContain("PNG, JPG hoặc WebP");
  await act(() =>
    result.current.select(new File([], "empty.png", { type: "image/png" })),
  );
  expect(result.current.error).toContain("5 MB");
  const large = file();
  Object.defineProperty(large, "size", {
    value: TIMETABLE_IMAGE_MAX_BYTES + 1,
  });
  await act(() => result.current.select(large));
  expect(result.current.attachment).toBeNull();
  expect(readImage).not.toHaveBeenCalled();
});

it("cancels image processing on removal and ignores a late response", async () => {
  let finish, signal;
  const readImage = vi.fn((_, token) => {
    signal = token;
    return new Promise((resolve) => {
      finish = resolve;
    });
  });
  const { result } = renderHook(() =>
    useTimetableImage({ enabled: true, readImage }),
  );
  await act(() => result.current.select(file()));
  let pending;
  act(() => {
    pending = result.current.read();
  });
  expect(result.current.phase).toBe("reading");
  act(() => result.current.remove());
  expect(signal.aborted).toBe(true);
  await act(async () => {
    finish(reading);
    await pending;
  });
  expect(result.current.attachment).toBeNull();
  expect(result.current.ready).toBe(false);
});

it("keeps the image available to retry after a reading error", async () => {
  const readImage = vi
    .fn()
    .mockRejectedValueOnce(new Error("Chưa đọc rõ ảnh"))
    .mockResolvedValueOnce(reading);
  render(<Picker readImage={readImage} />);
  fireEvent.change(screen.getByLabelText("Chọn ảnh thời khóa biểu"), {
    target: { files: [file()] },
  });
  fireEvent.click(
    await screen
      .findByAltText("Ảnh thời khóa biểu đã chọn")
      .then(() => screen.getByRole("button", { name: "Gửi" })),
  );
  await waitFor(() =>
    expect(screen.getByRole("alert").textContent).toContain("Chưa đọc rõ ảnh"),
  );
  expect(screen.getByAltText("Ảnh thời khóa biểu đã chọn")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Gửi" }));
  expect(
    (await screen.findByRole("textbox", { name: "Nội dung thời khóa biểu" }))
      .value,
  ).toBe(reading.text);
});

it("carries the corrected timetable without forcing preview mode or inventing dates or period lengths", () => {
  const text = timetableRequest(
    "Áp dụng từ 02/11/2026 trong 2 tuần",
    "Thứ 2: Toán, tiết 1–3",
  );
  expect(text).toContain("Áp dụng từ 02/11/2026 trong 2 tuần");
  expect(text).toContain("Toán, tiết 1–3");
  expect(text).not.toContain("Chỉ xem trước");
  expect(text).toContain("không tự đoán");
  expect(text).not.toContain("07:00");
  expect(timetableRequest(" Chào NORA ", "")).toBe("Chào NORA");
});
