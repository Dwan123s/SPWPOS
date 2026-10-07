import { useEffect, useRef, useState } from "react";
import { getPasswordRequirements } from "./password-policy.js";

export function PasswordChecklist({ passwordName = "password" }) {
  const root = useRef(null);
  const [password, setPassword] = useState("");

  useEffect(() => {
    const form = root.current?.closest("form");
    if (!form) return;
    const sync = () => {
      setPassword(form.elements.namedItem(passwordName)?.value || "");
    };
    sync();
    form.addEventListener("input", sync);
    form.addEventListener("change", sync);
    return () => {
      form.removeEventListener("input", sync);
      form.removeEventListener("change", sync);
    };
  }, [passwordName]);

  const checks = getPasswordRequirements(password);
  const satisfied = (id) => checks.find((check) => check.id === id)?.met;
  const requirements = [
    checks.find((check) => check.id === "length"),
    {
      id: "letters",
      label: "Có cả chữ hoa và chữ thường",
      met: satisfied("lowercase") && satisfied("uppercase"),
    },
    {
      id: "number",
      label: "Có ít nhất một số (0–9)",
      met: satisfied("number"),
    },
    {
      id: "special",
      label: "Có ký tự đặc biệt (ví dụ: !, @, #)",
      met: satisfied("special"),
    },
  ];
  const passed = requirements.filter((requirement) => requirement.met).length;
  return (
    <div ref={root} id="password-requirements" className="password-checklist">
      <span
        className="password-checklist-summary"
        role="status"
        aria-live="polite"
        aria-atomic="true"
      >
        Đã đạt {passed}/{requirements.length} yêu cầu mật khẩu.
      </span>
      <ul aria-label="Yêu cầu mật khẩu">
        {requirements.map((requirement) => {
          const met = requirement.met;
          return (
            <li
              key={requirement.id}
              data-requirement={requirement.id}
              data-met={met}
            >
              <span className="password-checklist-indicator" aria-hidden="true">
                {met ? "✓" : ""}
              </span>
              <span>
                <span className="password-checklist-summary">
                  {met ? "Đã đạt: " : "Chưa đạt: "}
                </span>
                {requirement.label}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
