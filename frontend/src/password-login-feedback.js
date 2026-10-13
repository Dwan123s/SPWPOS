import { useEffect, useState } from "react";

export const INVALID_LOGIN_MESSAGE =
  "Tên đăng nhập, email hoặc mật khẩu chưa đúng. Vui lòng kiểm tra và thử lại.";

const rejectionEvent = "login-password-rejected";

export function notifyLoginPasswordRejected(form) {
  const input = form.elements.namedItem("password");
  if (!input) return;
  const error = form.querySelector(".form-error");
  if (error) error.textContent = "";
  input.dispatchEvent(new Event(rejectionEvent));
}

export function useLoginPasswordFeedback(input, enabled) {
  const [rejected, setRejected] = useState(false);
  useEffect(() => {
    if (!enabled) return;
    const field = input.current;
    const form = field?.closest("form");
    if (!form) return;
    let animation;
    const cancelAnimation = () => {
      animation?.cancel();
      animation = undefined;
    };
    const show = () => {
      setRejected(true);
      field.focus({ preventScroll: true });
      cancelAnimation();
      if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
      animation = field
        .closest(".input-with-action")
        ?.animate?.(
          [
            { transform: "translateX(0)" },
            { transform: "translateX(-6px)" },
            { transform: "translateX(5px)" },
            { transform: "translateX(-3px)" },
            { transform: "translateX(2px)" },
            { transform: "translateX(0)" },
          ],
          { duration: 360, easing: "ease-out" },
        );
      animation?.finished?.catch(() => {});
    };
    const clear = (event) => {
      if (
        event.type !== "submit" &&
        !["email", "password"].includes(event.target.name)
      )
        return;
      setRejected(false);
      cancelAnimation();
    };
    field.addEventListener(rejectionEvent, show);
    form.addEventListener("input", clear);
    form.addEventListener("change", clear);
    form.addEventListener("submit", clear);
    return () => {
      field.removeEventListener(rejectionEvent, show);
      form.removeEventListener("input", clear);
      form.removeEventListener("change", clear);
      form.removeEventListener("submit", clear);
      cancelAnimation();
    };
  }, [input, enabled]);
  return rejected;
}
