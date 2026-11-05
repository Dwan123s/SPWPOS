import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { icon } from "./ui.jsx";

function values(form) {
  return Array.from(new FormData(form).entries());
}

export function SettingsForm({ name, saveLabel, onRestore, children }) {
  const formRef = useRef(null);
  const original = useRef(null);
  const [dirty, setDirty] = useState(false);
  useLayoutEffect(() => {
    original.current = values(formRef.current);
  }, []);
  useEffect(() => {
    if (!dirty) return;
    const warn = (event) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);
  function changed() {
    setDirty(
      JSON.stringify(values(formRef.current)) !==
        JSON.stringify(original.current),
    );
  }
  function restore() {
    const form = formRef.current;
    for (const control of form.elements) {
      if (!control.name || control.disabled) continue;
      if (control.type === "checkbox")
        control.checked = original.current.some(
          ([key]) => key === control.name,
        );
      else
        control.value =
          original.current.find(([key]) => key === control.name)?.[1] ?? "";
    }
    onRestore?.();
    const error = form.querySelector(".form-error");
    if (error) error.textContent = "";
    setDirty(false);
  }
  return (
    <form
      ref={formRef}
      data-form={name}
      onInputCapture={changed}
      onChangeCapture={changed}
      onSubmit={(event) => {
        if (!dirty) {
          event.preventDefault();
          event.stopPropagation();
        }
      }}
    >
      {children}
      <div className="form-error" role="alert" />
      <div className={`settings-save-bar${dirty ? " has-changes" : ""}`}>
        <span role="status" className="settings-save-status">
          {icon(dirty ? "edit" : "check")}{" "}
          {dirty ? "Bạn có thay đổi chưa lưu" : "Chưa có thay đổi"}
        </span>
        <div className="settings-save-actions">
          <button
            type="button"
            className="btn ghost"
            disabled={!dirty}
            onClick={restore}
          >
            Hoàn tác
          </button>
          <button type="submit" className="btn primary" disabled={!dirty}>
            {icon("check")} {saveLabel}
          </button>
        </div>
      </div>
    </form>
  );
}
