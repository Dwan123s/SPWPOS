import {
  useLayoutEffect,
  useRef,
  useState,
  useEffect,
} from "react";
import { Slot } from "./react-ui.jsx";
import { setSlot } from "./store.js";
import { state, modal, closeModal, toast, icon } from "./ui.jsx";
const avatarImage = (url, p) =>
  url ? (
    <img src={url} alt="Avatar" className="avatar-img" id="hero-avatar-img" />
  ) : (
    <span className="avatar-initials">
      {(p.displayName || p.firstName || "U")[0].toUpperCase()}
    </span>
  );
const removeButton = (url) =>
  url ? (
    <button
      type="button"
      className="btn ghost small danger"
      id="avatar-remove-btn"
      aria-label="Xóa ảnh"
      title="Gỡ ảnh đại diện"
      data-action="remove-avatar"
    >
      {icon("trash")} <span className="avatar-remove-label">Xóa ảnh</span>
    </button>
  ) : null;
export const avatarPreview = (p) => (
  <Slot id="profile-avatar" as="span" className="contents">
    {avatarImage(p.avatarUrl, p)}
  </Slot>
);
export const avatarRemove = (p) => (
  <Slot id="profile-remove" as="span" className="contents">
    {removeButton(p.avatarUrl)}
  </Slot>
);
export function setAvatar(url) {
  const input = document.getElementById("avatarUrl-hidden");
  if (input) input.value = url;
  setSlot("profile-avatar", avatarImage(url, state.workspace.profile));
  setSlot("profile-remove", removeButton(url));
  input?.dispatchEvent(new Event("input", { bubbles: true }));
}
export function openAvatarCropper(src) {
  modal(
    "Căn chỉnh ảnh đại diện",
    "Kéo và chọn khung ảnh phù hợp nhất cho bạn.",
    <AvatarCropper src={src} />,
  );
}
function constrain(image, transform) {
  const rotated = transform.rotation % 180 !== 0;
  const width = rotated ? image.height : image.width,
    height = rotated ? image.width : image.height;
  const minimum = Math.max(240 / width, 240 / height),
    scale = Math.max(minimum, transform.scale);
  const maxX = Math.max(0, (width * scale - 240) / 2),
    maxY = Math.max(0, (height * scale - 240) / 2);
  return {
    ...transform,
    scale,
    x: Math.max(-maxX, Math.min(maxX, transform.x)),
    y: Math.max(-maxY, Math.min(maxY, transform.y)),
    minimum,
  };
}
function draw(ctx, image, transform, size) {
  const ratio = size / 240;
  ctx.clearRect(0, 0, size, size);
  ctx.fillStyle = "#fff";
  ctx.fillRect(0, 0, size, size);
  ctx.save();
  ctx.translate(size / 2 + transform.x * ratio, size / 2 + transform.y * ratio);
  ctx.rotate((transform.rotation * Math.PI) / 180);
  ctx.scale(transform.scale * ratio, transform.scale * ratio);
  ctx.drawImage(image, -image.width / 2, -image.height / 2);
  ctx.restore();
}
export function AvatarCropper({ src }) {
  const canvas = useRef(null),
    preview = useRef(null),
    drag = useRef(null);
  const [image, setImage] = useState(null),
    [transform, setTransform] = useState({
      scale: 1,
      x: 0,
      y: 0,
      rotation: 0,
    });
  useEffect(() => {
    const img = new Image();
    let disposed = false;
    img.onload = () => {
      if (!disposed) {
        setImage(img);
        setTransform({
          scale: Math.max(240 / img.width, 240 / img.height),
          x: 0,
          y: 0,
          rotation: 0,
        });
      }
    };
    img.onerror = () => {
      if (!disposed)
        toast("Không đọc được ảnh. Hãy chọn PNG, JPEG hoặc WebP.", "error");
    };
    img.src = src;
    return () => {
      disposed = true;
      img.onload = null;
      img.onerror = null;
    };
  }, [src]);
  const current = image ? constrain(image, transform) : transform;
  useLayoutEffect(() => {
    if (!image) return;
    const ctx = canvas.current.getContext("2d");
    ctx.clearRect(0, 0, 320, 320);
    ctx.save();
    ctx.translate(160 + current.x, 160 + current.y);
    ctx.rotate((current.rotation * Math.PI) / 180);
    ctx.scale(current.scale, current.scale);
    ctx.drawImage(image, -image.width / 2, -image.height / 2);
    ctx.restore();
    ctx.fillStyle = "rgba(15,23,42,.72)";
    ctx.beginPath();
    ctx.rect(0, 0, 320, 320);
    ctx.arc(160, 160, 120, 0, Math.PI * 2, true);
    ctx.fill();
    ctx.strokeStyle = "#0bb9dc";
    ctx.lineWidth = 2.5;
    ctx.beginPath();
    ctx.arc(160, 160, 120, 0, Math.PI * 2);
    ctx.stroke();
    draw(preview.current.getContext("2d"), image, current, 60);
  }, [image, transform]);
  function apply() {
    if (!image) return;
    const output = document.createElement("canvas");
    output.width = output.height = 300;
    draw(output.getContext("2d"), image, current, 300);
    setAvatar(output.toDataURL("image/jpeg", 0.92));
    closeModal();
    toast("Đã căn chỉnh ảnh. Bấm Lưu thông tin hồ sơ để hoàn tất.", "success");
  }
  return (
    <div className="avatar-cropper-wrap">
      <p className="avatar-cropper-hint">
        Kéo ảnh để căn vị trí · Dùng thanh trượt để phóng to/thu nhỏ
      </p>
      <div
        className="avatar-cropper-canvas-box"
        id="cropper-box"
        onPointerDown={(event) => {
          drag.current = {
            x: event.clientX,
            y: event.clientY,
          };
          event.currentTarget.setPointerCapture(event.pointerId);
        }}
        onPointerMove={(event) => {
          if (!drag.current || !image) return;
          const bounds = canvas.current.getBoundingClientRect();
          const x =
              ((event.clientX - drag.current.x) * 320) /
              Math.max(1, bounds.width),
            y =
              ((event.clientY - drag.current.y) * 320) /
              Math.max(1, bounds.height);
          drag.current = {
            x: event.clientX,
            y: event.clientY,
          };
          setTransform((value) =>
            constrain(image, {
              ...value,
              x: value.x + x,
              y: value.y + y,
            }),
          );
        }}
        onPointerUp={() => {
          drag.current = null;
        }}
        onPointerCancel={() => {
          drag.current = null;
        }}
      >
        <canvas ref={canvas} id="cropper-canvas" width="320" height="320" />
      </div>
      <div className="avatar-cropper-controls">
        <label htmlFor="cropper-zoom">Thu phóng</label>
        <input
          type="range"
          id="cropper-zoom"
          className="avatar-cropper-zoom-slider"
          min={current.minimum || 1}
          max={(current.minimum || 1) * 4}
          step="0.005"
          value={current.scale}
          onChange={(event) =>
            setTransform((value) => ({
              ...value,
              scale: Number(event.target.value),
            }))
          }
        />
      </div>
      <div className="avatar-cropper-tools-row">
        <div className="avatar-crop-preview-wrap">
          <canvas
            ref={preview}
            className="avatar-crop-preview-circle"
            width="60"
            height="60"
          />
          <small>Xem trước</small>
        </div>
        <div className="actions">
          <button
            type="button"
            className="btn ghost small"
            onClick={() =>
              setTransform((value) => ({
                ...value,
                rotation: (value.rotation + 90) % 360,
              }))
            }
          >
            {icon("refresh")} Xoay 90°
          </button>
          <button
            type="button"
            className="btn ghost small"
            onClick={() =>
              image &&
              setTransform({
                scale: Math.max(240 / image.width, 240 / image.height),
                x: 0,
                y: 0,
                rotation: 0,
              })
            }
          >
            Đặt lại
          </button>
        </div>
      </div>
      <div className="form-actions right-align mt-3 w-full">
        <button type="button" className="btn ghost" data-action="close-modal">
          Hủy
        </button>
        <button
          type="button"
          className="btn primary"
          disabled={!image}
          onClick={apply}
        >
          {icon("check")} Áp dụng khung ảnh
        </button>
      </div>
    </div>
  );
}
